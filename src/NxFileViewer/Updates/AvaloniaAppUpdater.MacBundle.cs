using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace NxFileViewer.Avalonia.Updates;

internal static partial class AvaloniaAppUpdater
{
    internal static string? GetRunningMacBundle()
    {
        if (!OperatingSystem.IsMacOS() || Environment.ProcessPath is not { } executable) return null;
        return GetMacBundle(executable);
    }

    internal static string? GetMacBundle(string executable)
    {
        var macos = new FileInfo(executable).Directory;
        var contents = macos?.Parent;
        var bundle = contents?.Parent;
        return macos?.Name == "MacOS" && contents?.Name == "Contents" &&
            bundle != null && IsMacBundleName(bundle.Name) && File.Exists(Path.Combine(contents.FullName, "Info.plist"))
            ? bundle.FullName : null;
    }

    private static bool IsMacBundleName(string name) => name.Length > 4 &&
        name.EndsWith(".app", StringComparison.OrdinalIgnoreCase) &&
        Path.GetFileName(name) == name && !name.Contains('\\') && !name.Contains(':');

    private static bool IsMacExecutable(string name) => name is "NxFileViewerFork" or "NxFileViewerFork.Avalonia" or "NxFileViewer";

    internal static string? GetMacBundleExecutable(string install, UpdateJob job) =>
        job.MacBundle is { } bundle && IsMacBundleName(bundle) && IsMacExecutable(job.PreviousExecutable)
            ? Path.Combine(install, bundle, "Contents", "MacOS", job.PreviousExecutable) : null;

    internal static void ExtractMacBundle(Stream source, string destination, string rid, CancellationToken cancellation)
    {
        using var zip = new ZipArchive(source, ZipArchiveMode.Read, leaveOpen: true);
        if (zip.Entries.Count > 1024) throw new InvalidDataException("Too many entries in the app bundle.");
        string? root = null;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long total = 0;
        foreach (var entry in zip.Entries)
        {
            cancellation.ThrowIfCancellationRequested();
            var directory = entry.FullName.EndsWith('/');
            var parts = entry.FullName.TrimEnd('/').Split('/');
            var mode = (entry.ExternalAttributes >> 16) & 0xFFFF;
            if (parts.Any(part => part is "" or "." or ".." || part.Contains('\\') || part.Contains(':') || part.Any(char.IsControl)) ||
                (mode & 0xF000) is not (0 or 0x4000 or 0x8000))
                throw new InvalidDataException("Unsafe app bundle entry: " + entry.FullName);
            // ditto stores macOS archive metadata separately from the bundle.
            if (parts[0] == "__MACOSX") continue;
            root ??= parts[0];
            if (!IsMacBundleName(root) || parts[0] != root ||
                (parts.Length == 1 ? !directory : parts[1] != "Contents") ||
                !seen.Add(string.Join('/', parts)))
                throw new InvalidDataException("Unexpected app bundle entry: " + entry.FullName);
            var target = Path.Combine(destination, Path.Combine(parts[1..]));
            if (directory)
            {
                Directory.CreateDirectory(target);
                continue;
            }
            if (entry.Length > MaximumBytes - total) throw new InvalidDataException("Unpacked app bundle is too large.");
            total += entry.Length;
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            using (var input = entry.Open())
            using (var output = new FileStream(target, FileMode.CreateNew))
            {
                var buffer = new byte[81920];
                long written = 0;
                int count;
                while ((count = input.Read(buffer)) > 0)
                {
                    cancellation.ThrowIfCancellationRequested();
                    written += count;
                    if (written > entry.Length) throw new InvalidDataException("Invalid bundle entry length.");
                    output.Write(buffer, 0, count);
                }
                if (written != entry.Length) throw new InvalidDataException("Incomplete bundle entry.");
            }
            if ((mode & 0x49) != 0) MakeExecutable(target);
        }
        MakeExecutable(ReadMacBundleExecutable(destination, rid));
    }

    private static async Task ExtractMacDiskImageAsync(string image, string destination, string rid, CancellationToken cancellation)
    {
        if (!OperatingSystem.IsMacOS()) throw new PlatformNotSupportedException("DMG updates require macOS.");
        cancellation.ThrowIfCancellationRequested();
        var mount = Directory.CreateTempSubdirectory("nxfileviewer-dmg-").FullName;
        var attached = false;
        try
        {
            await RunDiskImageToolAsync("attach", "-readonly", "-nobrowse", "-noautoopen", "-mountpoint", mount, image);
            attached = true;
            await Task.Run(() =>
            {
                cancellation.ThrowIfCancellationRequested();
                var bundles = Directory.GetDirectories(mount).Where(path => IsMacBundleName(Path.GetFileName(path))).ToArray();
                if (bundles.Length != 1) throw new InvalidDataException("Expected one app bundle in the disk image.");
                long remaining = MaximumBytes;
                var entries = 0;
                CopyBundle(bundles[0], destination);
                MakeExecutable(ReadMacBundleExecutable(destination, rid));

                void CopyBundle(string source, string target)
                {
                    CheckFile(source);
                    Directory.CreateDirectory(target);
                    foreach (var path in Directory.EnumerateFileSystemEntries(source))
                    {
                        cancellation.ThrowIfCancellationRequested();
                        if (++entries > 1024) throw new InvalidDataException("Too many entries in the app bundle.");
                        CheckFile(path);
                        var output = Path.Combine(target, Path.GetFileName(path));
                        if (Directory.Exists(path))
                        {
                            CopyBundle(path, output);
                            continue;
                        }
                        remaining -= new FileInfo(path).Length;
                        if (remaining < 0) throw new InvalidDataException("Unpacked app bundle is too large.");
                        File.Copy(path, output);
                        if (OperatingSystem.IsMacOS() &&
                            (File.GetUnixFileMode(path) & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0)
                            MakeExecutable(output);
                    }
                }
            }, cancellation);
        }
        finally
        {
            if (attached || (Directory.Exists(mount) && Directory.EnumerateFileSystemEntries(mount).Any()))
            {
                try { await RunDiskImageToolAsync("detach", mount); }
                catch (IOException) { await RunDiskImageToolAsync("detach", "-force", mount); }
            }
            if (Directory.Exists(mount)) Directory.Delete(mount);
        }
    }

    private static async Task RunDiskImageToolAsync(params string[] arguments)
    {
        var start = new ProcessStartInfo("/usr/bin/hdiutil")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new IOException("Could not start hdiutil.");
        var output = process.StandardOutput.BaseStream.CopyToAsync(Stream.Null);
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        await output;
        if (process.ExitCode != 0) throw new IOException($"hdiutil {arguments[0]} failed: {(await error).Trim()}");
        await error;
    }

    private static string ReadMacBundleExecutable(string bundle, string rid)
    {
        var plist = Path.Combine(bundle, "Contents", "Info.plist");
        if (!File.Exists(plist) || new FileInfo(plist).Length > 65536)
            throw new InvalidDataException("Missing or oversized bundle Info.plist.");
        CheckFile(plist);
        using var reader = System.Xml.XmlReader.Create(plist, new System.Xml.XmlReaderSettings { DtdProcessing = System.Xml.DtdProcessing.Ignore });
        var entries = XDocument.Load(reader).Root?.Element("dict")?.Elements().ToArray() ?? [];
        string? Read(string key) => entries.FirstOrDefault(entry => entry.Name == "key" && entry.Value == key)?.ElementsAfterSelf().FirstOrDefault()?.Value;
        var executable = Read("CFBundleExecutable");
        if (Read("CFBundleIdentifier") != "io.github.nxfileviewer.nxfileviewerfork.avalonia" ||
            executable == null || !IsMacExecutable(executable))
            throw new InvalidDataException("The app bundle does not identify NxFileViewer Fork.");
        var path = Path.Combine(bundle, "Contents", "MacOS", executable);
        CheckDirectory(Path.GetDirectoryName(path)!);
        CheckFile(path);
        ValidateNativeExecutable(path, rid);
        return path;
    }

    private static UpdateFile[] MacBundleFiles(string root) => ReadMacDirectory(root, root).OrderBy(file => file.Name, StringComparer.Ordinal).ToArray();

    private static IEnumerable<UpdateFile> ReadMacDirectory(string root, string directory)
    {
        CheckFile(directory);
        foreach (var path in Directory.EnumerateFileSystemEntries(directory))
        {
            CheckFile(path);
            if (Directory.Exists(path))
            {
                foreach (var file in ReadMacDirectory(root, path)) yield return file;
            }
            else yield return new UpdateFile(Path.GetRelativePath(root, path), Hash(path));
        }
    }

    internal static string CreateMacBundleJob(string stage, string bundle, string executable, string rid)
    {
        var payload = Path.Combine(stage, "bundle");
        var next = ReadMacBundleExecutable(payload, rid);
        using var process = Process.GetCurrentProcess();
        var job = new UpdateJob(process.Id, process.StartTime.ToUniversalTime().Ticks, Path.GetFileName(next),
            Path.GetFileName(executable), MacBundleFiles(payload), Hash(executable), MacBundle: Path.GetFileName(bundle));
        var worker = Directory.CreateDirectory(Path.Combine(stage, "worker"));
        var workerExe = Path.Combine(worker.FullName, job.PreviousExecutable);
        File.Copy(executable, workerExe);
        MakeExecutable(workerExe);
        var path = Path.Combine(stage, "job.json");
        File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(job));
        return path;
    }

    internal static void ApplyMacBundle(string stage, string install, UpdateJob job, string rid, Action<ProcessStartInfo> launch)
    {
        if (GetMacBundleExecutable(install, job) is not { } previous)
            throw new InvalidDataException("Invalid macOS update target.");
        var target = Path.Combine(install, job.MacBundle!);
        var payload = Path.Combine(stage, "bundle");
        CheckDirectory(target);
        CheckDirectory(Path.GetDirectoryName(previous)!);
        CheckFile(previous);
        if (ReadMacBundleExecutable(target, rid) != previous || Hash(previous) != job.PreviousSha256 ||
            !MacBundleFiles(payload).SequenceEqual(job.Files) ||
            Path.GetFileName(ReadMacBundleExecutable(payload, rid)) != job.Executable)
            throw new InvalidDataException("The installed or staged bundle changed.");

        var previousDirectory = Path.GetDirectoryName(previous)!;
        var nextDirectory = Path.Combine(payload, "Contents", "MacOS");
        if (ViewerDataMigration.HasData(previousDirectory) &&
            (!OperatingSystem.IsMacOS() || !ViewerDataMigration.WasCopied(previousDirectory, ViewerDataDirectory.GetMacBundleDataRoot())))
        {
            var result = ViewerDataMigration.Copy(previousDirectory, nextDirectory);
            if (result.Conflicts != 0) throw new IOException("The new bundle conflicts with existing program data.");
        }
        var backup = Path.Combine(stage, "previous.app");
        Directory.Move(target, backup);
        try
        {
            Directory.Move(payload, target);
            var next = Path.Combine(target, "Contents", "MacOS", job.Executable);
            launch(new ProcessStartInfo(next) { UseShellExecute = false, WorkingDirectory = install });
        }
        catch
        {
            if (Directory.Exists(target)) Directory.Move(target, payload);
            Directory.Move(backup, target);
            throw;
        }
    }
}
