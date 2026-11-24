using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Emignatik.NxFileViewer.Services.Updates;

namespace NxFileViewer.Avalonia.Updates;

internal sealed record AvaloniaRelease(string Version, string AssetName, string DownloadUrl, string? Digest, bool RequiresManifest, bool HasReleaseManifest = false);
internal sealed record UpdateFile(string Name, string Sha256);
internal sealed record UpdateJob(int ParentId, long ParentStarted, string Executable, string PreviousExecutable, UpdateFile[] Files, string? PreviousSha256 = null);
internal sealed record AvaloniaPackage(int SchemaVersion, string Framework, string Version, string RuntimeIdentifier, bool SelfContained, string Executable);

internal static class AvaloniaAppUpdater
{
    internal static ViewerVersion? ParseVersion(string value) => ViewerVersion.TryParse(Regex.Replace(value, @"-beta(?=\d+(?:\+|$))", "-beta."), out var parsed) ? parsed : null;
    internal static bool IsNewer(string installed, string latest) => ParseVersion(installed) is { } before && ParseVersion(latest) is { } after && after.CompareTo(before) > 0;

    private const long MaximumBytes = 512L * 1024 * 1024;
    internal const string ManifestName = "nxfileviewer-avalonia.json";
    private const string ReleaseManifestName = "nxfileviewer-update.json";

    internal static AvaloniaRelease? SelectRelease(JsonElement release, string repository, string rid, bool selfContained)
    {
        var tag = release.GetProperty("tag_name").GetString()!;
        if (!ViewerVersion.TryParse(tag, out _)) return null;
        var suffix = rid.StartsWith("win-", StringComparison.Ordinal) ? rid[4..] : rid;
        var runtime = selfContained ? "_self-contained" : "";
        var assets = release.GetProperty("assets").EnumerateArray().ToArray();
        var hasReleaseManifest = assets.Any(a => a.GetProperty("name").GetString() == ReleaseManifestName);
        var variant = selfContained ? "with-net" : "without-net";
        var platformName = $"v{tag.TrimStart('v', 'V')}_{rid}-{variant}.zip";
        foreach (var currentName in new[] { "NxFileViewerFork_" + platformName, platformName })
        {
            var currentAsset = assets.FirstOrDefault(a => a.GetProperty("name").GetString() == currentName);
            if (currentAsset.ValueKind == JsonValueKind.Undefined) continue;
            var url = $"https://github.com/{repository}/releases/download/{Uri.EscapeDataString(tag)}/{Uri.EscapeDataString(currentName)}";
            var digest = currentAsset.TryGetProperty("digest", out var value) ? value.GetString() : null;
            return new(tag, currentName, url, digest, true, hasReleaseManifest);
        }
        foreach (var prefix in new[] { "NxFileViewerFork_Avalonia", "NxFileViewerFork" })
        {
            var name = $"{prefix}_v{tag.TrimStart('v', 'V')}{runtime}_{suffix}.zip";
            var generic = prefix == "NxFileViewerFork";
            // Generic Windows release names may still belong to WPF. Require an explicit marker.
            if (generic && !assets.Any(a => a.GetProperty("name").GetString() == name + ".avalonia.json" || a.GetProperty("name").GetString() == "nxfileviewer-packages.json")) continue;
            var asset = assets.FirstOrDefault(a => a.GetProperty("name").GetString() == name);
            if (asset.ValueKind == JsonValueKind.Undefined) continue;
            var url = $"https://github.com/{repository}/releases/download/{Uri.EscapeDataString(tag)}/{Uri.EscapeDataString(name)}";
            var digest = asset.TryGetProperty("digest", out var value) ? value.GetString() : null;
            return new(tag, name, url, digest, generic, hasReleaseManifest);
        }
        return null;
    }

    internal static async Task<string> DownloadAsync(HttpClient client, AvaloniaRelease release, string install, string rid, bool selfContained, CancellationToken cancellation)
    {
        CheckDirectory(install);
        var updates = Path.Combine(install, "Updates");
        Directory.CreateDirectory(updates); CheckDirectory(updates);
        var stage = Path.Combine(updates, "avalonia-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        var zipPath = Path.Combine(stage, "package.zip");
        try
        {
            var expected = release.Digest;
            if (string.IsNullOrWhiteSpace(expected) && release.HasReleaseManifest)
            {
                var url = release.DownloadUrl[..(release.DownloadUrl.LastIndexOf('/') + 1)] + ReleaseManifestName;
                using var response = await client.GetAsync(url, cancellation);
                if (response.StatusCode != System.Net.HttpStatusCode.NotFound)
                {
                    response.EnsureSuccessStatusCode();
                    using var json = JsonDocument.Parse(await ReadLimitedAsync(await response.Content.ReadAsStreamAsync(cancellation), 65536, cancellation));
                    if (json.RootElement.TryGetProperty("AvaloniaPackages", out var packages))
                    {
                        if (json.RootElement.GetProperty("SchemaVersion").GetInt32() != 1 ||
                            json.RootElement.GetProperty("ApplicationId").GetString() != "NxFileViewerFork")
                            throw new InvalidDataException("Invalid release manifest.");
                        var matches = packages.EnumerateArray().Where(p => p.GetProperty("AssetName").GetString() == release.AssetName).ToArray();
                        if (matches.Length != 1) throw new InvalidDataException("Package missing or duplicated in release manifest.");
                        expected = ReadChecksum(matches[0], release.AssetName);
                    }
                }
            }
            if (string.IsNullOrWhiteSpace(expected))
            {
                var indexUrl = release.DownloadUrl[..(release.DownloadUrl.LastIndexOf('/') + 1)] + "nxfileviewer-packages.json";
                using var indexResponse = await client.GetAsync(indexUrl, cancellation);
                if (indexResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    // Compatibility with earlier releases that published one marker per ZIP.
                    using var metadata = await client.GetAsync(release.DownloadUrl + ".avalonia.json", cancellation);
                    metadata.EnsureSuccessStatusCode();
                    using var json = JsonDocument.Parse(await ReadLimitedAsync(await metadata.Content.ReadAsStreamAsync(cancellation), 16384, cancellation));
                    expected = ReadChecksum(json.RootElement, release.AssetName);
                }
                else
                {
                    indexResponse.EnsureSuccessStatusCode();
                    using var json = JsonDocument.Parse(await ReadLimitedAsync(await indexResponse.Content.ReadAsStreamAsync(cancellation), 262144, cancellation));
                    if (json.RootElement.GetProperty("SchemaVersion").GetInt32() != 1) throw new InvalidDataException("Unsupported package index.");
                    var matches = json.RootElement.GetProperty("Packages").EnumerateArray().Where(p => p.GetProperty("AssetName").GetString() == release.AssetName).ToArray();
                    if (matches.Length != 1) throw new InvalidDataException("Package missing or duplicated in release index.");
                    expected = ReadChecksum(matches[0], release.AssetName);
                }
            }
            if (!Regex.IsMatch(expected ?? "", @"\Asha256:[a-fA-F0-9]{64}\z")) throw new InvalidDataException("The release has no SHA-256 checksum. Update manually.");
            using (var response = await client.GetAsync(release.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellation))
            {
                response.EnsureSuccessStatusCode();
                await using var source = await response.Content.ReadAsStreamAsync(cancellation);
                await using var output = File.Create(zipPath);
                var buffer = new byte[81920]; long total = 0; int count;
                while ((count = await source.ReadAsync(buffer, cancellation)) > 0)
                {
                    total += count; if (total > MaximumBytes) throw new InvalidDataException("Update archive is too large.");
                    await output.WriteAsync(buffer.AsMemory(0, count), cancellation);
                }
            }
            if (!Hash(zipPath).Equals(expected![7..], StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Update checksum mismatch.");
            using var archive = File.OpenRead(zipPath);
            await Task.Run(() => ExtractCore(archive, Path.Combine(stage, "payload"), release, rid, selfContained, cancellation), cancellation);
            return stage;
        }
        catch { Directory.Delete(stage, true); throw; }
    }

    private static string ReadChecksum(JsonElement package, string name)
    {
        if (package.GetProperty("Framework").GetString() != "Avalonia" || package.GetProperty("AssetName").GetString() != name)
            throw new InvalidDataException("Invalid Avalonia update metadata.");
        return "sha256:" + package.GetProperty("Sha256").GetString();
    }

    private static async Task<byte[]> ReadLimitedAsync(Stream input, int limit, CancellationToken cancellation)
    {
        using var output = new MemoryStream(); var buffer = new byte[4096]; int count;
        while ((count = await input.ReadAsync(buffer, cancellation)) > 0)
        {
            if (output.Length + count > limit) throw new InvalidDataException("Update metadata too large.");
            output.Write(buffer, 0, count);
        }
        return output.ToArray();
    }

    internal static AvaloniaPackage Extract(Stream source, string destination, AvaloniaRelease release, string rid, bool selfContained) =>
        ExtractCore(source,destination,release,rid,selfContained,CancellationToken.None);

    private static AvaloniaPackage ExtractCore(Stream source, string destination, AvaloniaRelease release, string rid, bool selfContained, CancellationToken cancellation)
    {
        using var zip = new ZipArchive(source, ZipArchiveMode.Read, true);
        if (zip.Entries.Count > 16) throw new InvalidDataException("Unexpected update archive contents.");
        var windows = rid.StartsWith("win-", StringComparison.Ordinal);
        var defaultExe = "NxFileViewerFork.Avalonia" + (windows ? ".exe" : "");
        var entry = zip.GetEntry(ManifestName);
        AvaloniaPackage package;
        if (entry is not null)
        {
            if (entry.Length > 16384) throw new InvalidDataException("Package metadata too large.");
            using var stream = entry.Open();
            package = JsonSerializer.Deserialize<AvaloniaPackage>(stream) ?? throw new InvalidDataException("Missing package metadata.");
            if (package.SchemaVersion != 1 || package.Framework != "Avalonia" || package.Version != release.Version.TrimStart('v', 'V') ||
                package.RuntimeIdentifier != rid || package.SelfContained != selfContained ||
                package.Executable != defaultExe && package.Executable != "NxFileViewerFork" + (windows ? ".exe" : "") &&
                package.Executable != "NxFileViewer" + (windows ? ".exe" : ""))
                throw new InvalidDataException("Update package does not match this Avalonia installation.");
        }
        else
        {
            if (release.RequiresManifest) throw new InvalidDataException("A generic package must explicitly identify itself as Avalonia.");
            package = new(1, "Avalonia", release.Version.TrimStart('v', 'V'), rid, selfContained, defaultExe);
        }
        var allowed = new HashSet<string>(StringComparer.Ordinal) { package.Executable, ManifestName, "LICENSE.txt", "start.sh", "install-shortcut.sh" };
        if (windows) allowed.Add("nxfileviewer-package.json");
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase); long total = 0;
        Directory.CreateDirectory(destination);
        foreach (var file in zip.Entries)
        {
            cancellation.ThrowIfCancellationRequested();
            var debugSymbols = Regex.IsMatch(file.FullName, @"\A[A-Za-z0-9_-][A-Za-z0-9._-]*\.pdb\z", RegexOptions.IgnoreCase);
            if ((!allowed.Contains(file.FullName) && !debugSymbols) || !seen.Add(file.FullName) || ((file.ExternalAttributes >> 16) & 0xF000) == 0xA000)
                throw new InvalidDataException("Unsafe or unexpected update path: " + file.FullName);
            total += file.Length; if (total > MaximumBytes || file.Length < 0) throw new InvalidDataException("Unpacked update too large.");
            if (debugSymbols) continue;
            var path = Path.Combine(destination, file.FullName);
            using var input = file.Open(); using var output = File.Create(path);
            var buffer = new byte[81920]; long written = 0; int count;
            while ((count = input.Read(buffer)) > 0)
            {
                cancellation.ThrowIfCancellationRequested();
                written += count; if (written > file.Length) throw new InvalidDataException("Invalid archive entry length.");
                output.Write(buffer, 0, count);
            }
            if (written != file.Length) throw new InvalidDataException("Incomplete archive entry.");
        }
        var exe = Path.Combine(destination, package.Executable);
        if (!File.Exists(exe)) throw new InvalidDataException("Update executable missing.");
        ValidateNativeExecutable(exe, rid);
        return package;
    }

    internal static void ValidateNativeExecutable(string path, string rid)
    {
        using var input = File.OpenRead(path); using var reader = new BinaryReader(input);
        if (rid.StartsWith("win-", StringComparison.Ordinal))
        {
            using var pe = new System.Reflection.PortableExecutable.PEReader(input);
            var machine = (int)pe.PEHeaders.CoffHeader.Machine;
            var expected = rid == "win-x64" ? 0x8664 : rid == "win-arm64" ? 0xaa64 : 0x14c;
            if (machine != expected || pe.PEHeaders.CorHeader is not null) throw new InvalidDataException("Wrong update executable architecture.");
        }
        else
        {
            var header = reader.ReadBytes(32);
            if (header.Length < 32) throw new InvalidDataException("Invalid native executable.");
            if (rid.StartsWith("linux-", StringComparison.Ordinal))
            {
                var machine = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(18));
                if (header[0] != 0x7f || header[1] != 'E' || header[2] != 'L' || header[3] != 'F' || header[4] != 2 || header[5] != 1 || machine != (rid == "linux-arm64" ? 183 : 62))
                    throw new InvalidDataException("Wrong Linux update architecture.");
            }
            else
            {
                var magic = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(header);
                var cpu = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(4));
                if (magic != 0xfeedfacf || cpu != (rid == "osx-arm64" ? 0x100000cU : 0x1000007U)) throw new InvalidDataException("Wrong macOS update architecture.");
            }
        }
    }

    internal static string CreateJob(string stage, string currentExecutable)
    {
        var payload = Path.Combine(stage, "payload");
        var manifestPath = Path.Combine(payload, ManifestName);
        var package = File.Exists(manifestPath) ? JsonSerializer.Deserialize<AvaloniaPackage>(File.ReadAllText(manifestPath)) : null;
        var executable = package?.Executable ?? Path.GetFileName(currentExecutable);
        var process = Process.GetCurrentProcess();
        var files = Directory.GetFiles(payload).Select(p => new UpdateFile(Path.GetFileName(p), Hash(p))).ToArray();
        var job = new UpdateJob(process.Id, process.StartTime.ToUniversalTime().Ticks, executable, Path.GetFileName(currentExecutable), files, Hash(currentExecutable));
        var worker = Path.Combine(stage, "worker"); Directory.CreateDirectory(worker);
        var workerExe = Path.Combine(worker, Path.GetFileName(currentExecutable));
        File.Copy(currentExecutable, workerExe);
        MakeExecutable(workerExe);
        var path = Path.Combine(stage, "job.json"); File.WriteAllText(path, JsonSerializer.Serialize(job));
        return path;
    }

    internal static void StartWorker(string jobPath)
    {
        var stage = Path.GetDirectoryName(jobPath)!;
        var worker = Directory.GetFiles(Path.Combine(stage, "worker")).Single();
        var start = new ProcessStartInfo(worker) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = stage };
        start.ArgumentList.Add("--apply-avalonia-update"); start.ArgumentList.Add(jobPath);
        using var process = Process.Start(start) ?? throw new IOException("Could not start the update worker.");
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern uint SetErrorMode(uint mode);

    internal static int RunWorker(string jobPath)
    {
        // A background updater must not wait for native loader/WER error dialogs.
        if (OperatingSystem.IsWindows()) SetErrorMode(0x8003);

        string? stage = null;
        string? install = null;
        UpdateJob? job = null;
        try
        {
            stage = Path.GetDirectoryName(Path.GetFullPath(jobPath))!;
            if (!Regex.IsMatch(Path.GetFileName(stage), @"\Aavalonia-[a-f0-9]{32}\z") ||
                !Path.GetFullPath(AppContext.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar).Equals(Path.Combine(stage,"worker"), OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal))
                throw new InvalidDataException("Invalid update job location.");
            install = Path.GetDirectoryName(Path.GetDirectoryName(stage))!;
            if (Path.GetFileName(Path.GetDirectoryName(stage)) != "Updates") throw new InvalidDataException("Invalid update stage.");
            CheckDirectory(install); CheckDirectory(Path.GetDirectoryName(stage)!); CheckDirectory(stage);
            job = JsonSerializer.Deserialize<UpdateJob>(File.ReadAllText(jobPath)) ?? throw new InvalidDataException("Missing update job.");
            using var updateLock = new FileStream(Path.Combine(Path.GetDirectoryName(stage)!, "avalonia-update.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            try
            {
                using var parent = Process.GetProcessById(job.ParentId);
                if (!parent.HasExited && parent.StartTime.ToUniversalTime().Ticks == job.ParentStarted && !parent.WaitForExit(120000)) throw new IOException("The app did not close in time.");
            }
            catch (ArgumentException) { }
            catch (InvalidOperationException) { }
            var ready = Path.Combine(stage,"ready");
            if (File.Exists(ready)) File.Delete(ready);
            Apply(stage, install, job, start =>
            {
                start.ArgumentList.Add("--avalonia-update-ready"); start.ArgumentList.Add(ready);
                using var app = Process.Start(start) ?? throw new IOException("Restart failed.");
                File.WriteAllText(Path.Combine(stage,"started-process.json"),JsonSerializer.Serialize(new { app.Id, Started=app.StartTime.ToUniversalTime().Ticks }));
                for(var i=0;i<1200;i++)
                {
                    if(File.Exists(ready)) return;
                    if(app.HasExited) throw new IOException("The updated app exited before opening its window.");
                    Thread.Sleep(100);
                }
                app.Kill(true);app.WaitForExit();
                throw new IOException("The updated app did not open its window. The previous version was restored.");
            });
            return 0;
        }
        catch (Exception error)
        {
            if (stage is not null)
            {
                File.WriteAllText(Path.Combine(stage, "update-error.txt"), error.ToString());
                // Restart only an intact previous executable after rollback.
                if (install is not null && job?.PreviousSha256 is { } previousHash &&
                    File.Exists(Path.Combine(install,job.PreviousExecutable)) && Hash(Path.Combine(install,job.PreviousExecutable))==previousHash)
                {
                    try
                    {
                        var restart=new ProcessStartInfo(Path.Combine(install,job.PreviousExecutable)){UseShellExecute=false,WorkingDirectory=install};
                        restart.ArgumentList.Add("--avalonia-update-ready"); restart.ArgumentList.Add(Path.Combine(stage,"ready"));
                        using var previous=Process.Start(restart);
                        if(previous is not null) File.WriteAllText(Path.Combine(stage,"started-process.json"),JsonSerializer.Serialize(new { previous.Id, Started=previous.StartTime.ToUniversalTime().Ticks }));
                    }
                    catch (Exception restartError) when (restartError is IOException or System.ComponentModel.Win32Exception) { }
                }
            }
            return 1;
        }
    }

    internal static void Apply(string stage, string install, UpdateJob job, Action<ProcessStartInfo> launch)
    {
        var validExecutables = new[] { "NxFileViewer", "NxFileViewer.exe", "NxFileViewerFork.Avalonia", "NxFileViewerFork.Avalonia.exe", "NxFileViewerFork", "NxFileViewerFork.exe" };
        if (!validExecutables.Contains(job.Executable) || !validExecutables.Contains(job.PreviousExecutable) || job.Files.Length is < 1 or > 5 ||
            !job.Files.Any(f => f.Name == job.Executable) || job.Files.Select(f=>f.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=job.Files.Length)
            throw new InvalidDataException("Invalid update file list.");
        if (job.Executable != job.PreviousExecutable && File.Exists(Path.Combine(install, job.Executable))) throw new IOException("The new executable name is already occupied by another installation.");
        var allowed = new[] { job.Executable, ManifestName, "LICENSE.txt", "start.sh", "install-shortcut.sh" };
        CheckDirectory(install); CheckDirectory(stage); CheckDirectory(Path.Combine(stage,"payload"));
        foreach (var file in job.Files)
        {
            if (!allowed.Contains(file.Name) || Path.GetFileName(file.Name)!=file.Name || !Regex.IsMatch(file.Sha256,@"\A[A-Fa-f0-9]{64}\z")) throw new InvalidDataException("Unsafe update file.");
            var source = Path.Combine(stage,"payload",file.Name); CheckFile(source);
            if (Hash(source) != file.Sha256) throw new InvalidDataException("Staged update changed.");
            var target = Path.Combine(install,file.Name); if(File.Exists(target)) CheckFile(target);
        }
        var backup = Path.Combine(stage,"backup"); Directory.CreateDirectory(backup);
        var changed = new List<(string Name,bool Existed)>();
        try
        {
            foreach (var file in job.Files)
            {
                var target = Path.Combine(install,file.Name); var existed = File.Exists(target);
                if(existed) File.Copy(target,Path.Combine(backup,file.Name));
                changed.Add((file.Name,existed));
                File.Copy(Path.Combine(stage,"payload",file.Name),target,true);
                if(file.Name==job.Executable||file.Name.EndsWith(".sh",StringComparison.Ordinal))MakeExecutable(target);
            }
            launch(new ProcessStartInfo(Path.Combine(install,job.Executable)){UseShellExecute=false,WorkingDirectory=install});
        }
        catch
        {
            foreach(var file in changed.AsEnumerable().Reverse())
            {
                var target=Path.Combine(install,file.Name);
                if(file.Existed) File.Copy(Path.Combine(backup,file.Name),target,true); else File.Delete(target);
            }
            throw;
        }
    }

    internal static string Hash(string path) { using var stream=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(stream)); }
    private static void CheckFile(string path) { if ((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0) throw new IOException("Symbolic links are not allowed in update paths."); }
    private static void CheckDirectory(string path) { for(var directory=new DirectoryInfo(Path.GetFullPath(path));directory is not null;directory=directory.Parent) CheckFile(directory.FullName); }
    private static void MakeExecutable(string path) { if(!OperatingSystem.IsWindows()) File.SetUnixFileMode(path,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute|UnixFileMode.GroupRead|UnixFileMode.GroupExecute|UnixFileMode.OtherRead|UnixFileMode.OtherExecute); }
}
