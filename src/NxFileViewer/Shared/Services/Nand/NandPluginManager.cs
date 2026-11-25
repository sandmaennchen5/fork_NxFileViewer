using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Emignatik.NxFileViewer.Localization;
using Emignatik.NxFileViewer.Services.BackgroundTask;
using Emignatik.NxFileViewer.Settings;

namespace Emignatik.NxFileViewer.Services.Nand;

public sealed class NandPluginManager(IAppSettings settings, string? rootDirectory = null,
    HttpClient? httpClient = null, Action<string, CancellationToken>? compatibilityCheck = null)
{
    private void EnsureEnabled()
    {
        if (!settings.NandPluginActive) throw new InvalidOperationException(LocalizationManager.Instance.Current.Keys.Features_PluginsDisabled);
    }
    private static readonly HttpClient Client = CreateClient();
    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("NxFileViewer-NAND/1.0");
        return client;
    }
    public string RootDirectory => Path.GetFullPath(rootDirectory ?? Path.Combine(AppContext.BaseDirectory, "Plugins", "NxNandManager"));
    private Updates.PluginVersionStorage VersionStorage => new(RootDirectory, StatePath);
    private string StatePath => Path.Combine(RootDirectory, "active.json");
    private sealed record State(string Executable, string Version, string Digest, string ExecutableDigest,
        string? PreviousExecutable = null, string? PreviousVersion = null);
    private string Resolve(string path)
    {
        var root = Path.GetFullPath(RootDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var resolved = Path.GetFullPath(path, root);
        if (!resolved.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Invalid managed NAND path.");
        return resolved;
    }
    private State? ReadState()
    {
        if (!settings.NandPluginActive) return null;
        if (!File.Exists(StatePath)) return null;
        using (var document = JsonDocument.Parse(File.ReadAllText(StatePath)))
        {
            if (document.RootElement.TryGetProperty("SchemaVersion", out _))
                return ReadUnifiedState();
        }
        var state = JsonSerializer.Deserialize<State>(File.ReadAllText(StatePath)) ?? throw new InvalidDataException("Invalid NAND installation state.");
        var resolved = state with { Executable = Resolve(state.Executable), PreviousExecutable = state.PreviousExecutable == null ? null : Resolve(state.PreviousExecutable) };
        if (!File.Exists(StatePath + ".legacy")) File.Copy(StatePath, StatePath + ".legacy");
        WriteState(resolved, activate: false);
        return ReadUnifiedState();
    }
    public bool UsesManagedInstallation
    {
        get
        {
            var selected = settings.NandExecutablePath;
            if (string.IsNullOrWhiteSpace(selected)) return true;
            var state = ReadState();
            if (state == null) return false;
            if (string.Equals(Path.GetFullPath(selected), state.Executable, StringComparison.OrdinalIgnoreCase)) return true;
            var relative = Path.GetRelativePath(RootDirectory, state.Executable).Replace('\\', '/');
            return relative.StartsWith("version-", StringComparison.OrdinalIgnoreCase) &&
                relative.Split('/').Length == 2 && Guid.TryParseExact(relative.Split('/')[0][8..], "N", out _) &&
                !File.Exists(selected) && selected.Replace('\\', '/').EndsWith("/" + relative, StringComparison.OrdinalIgnoreCase);
        }
    }
    public void AdoptManagedPath()
    {
        if (UsesManagedInstallation && ReadState() is { } state) settings.NandExecutablePath = state.Executable;
    }
    public string? InstalledVersion => UsesManagedInstallation ? ReadState()?.Version : null;
    public string ExecutablePath => !settings.NandPluginActive ? "" : UsesManagedInstallation ? ReadState()?.Executable ?? "" : Path.GetFullPath(settings.NandExecutablePath);
    public bool CanRollback
    {
        get { try { return UsesManagedInstallation && File.Exists(ReadState()?.PreviousExecutable); } catch { return false; } }
    }
    public string Status
    {
        get
        {
            if (!settings.NandPluginActive) return LocalizationManager.Instance.Current.Keys.Features_PluginsDisabled;
            try
            {
                var state = UsesManagedInstallation ? ReadState() : null;
                return File.Exists(ExecutablePath) ? LocalizationManager.Instance.Current.Keys.Nand_Installed + " — " +
                    (state?.Version ?? LocalizationManager.Instance.Current.Keys.Nsz_CustomExecutable) + Environment.NewLine + ExecutablePath
                    : LocalizationManager.Instance.Current.Keys.Nand_NotInstalled;
            }
            catch { return LocalizationManager.Instance.Current.Keys.Nand_NotInstalled; }
        }
    }
    private State ReadUnifiedState()
    {
        var pointers = VersionStorage.Read()!;
        var current = Updates.PluginVersionStorage.Metadata(Path.GetDirectoryName(pointers.Current)!)
            ?? throw new InvalidDataException("NAND installation metadata missing.");
        var previous = pointers.Previous == null ? null : Updates.PluginVersionStorage.Metadata(Path.GetDirectoryName(pointers.Previous)!);
        return new(pointers.Current, current.Version ?? "", current.SourceDigest ?? "", current.ExecutableDigest ?? "",
            pointers.Previous, previous?.Version);
    }
    private void WriteState(State state, bool activate = true)
    {
        VersionStorage.EnsureMetadata(state.Executable, new(ExternalSources.Current.NandRepository, state.Version,
            Sha256: state.ExecutableDigest, SourceDigest: state.Digest, ExecutableDigest: state.ExecutableDigest));
        if (state.PreviousExecutable != null)
        {
            string? digest = null;
            if (File.Exists(state.PreviousExecutable)) {
                using var file = File.OpenRead(state.PreviousExecutable);
                digest = Convert.ToHexString(SHA256.HashData(file));
            }
            VersionStorage.EnsureMetadata(state.PreviousExecutable, new(ExternalSources.Current.NandRepository,
                state.PreviousVersion, ExecutableDigest: digest));
        }
        VersionStorage.Write(state.Executable, state.PreviousExecutable);
        if (activate) settings.NandExecutablePath = state.Executable;
    }
    public System.Collections.Generic.IReadOnlyList<string> OldVersionDirectories()
    {
        EnsureEnabled();
        var state = ReadState();
        return state == null ? Array.Empty<string>() : Updates.OldVersionCleanup.PluginVersions(RootDirectory, state.Executable, settings.NandExecutablePath);
    }

    public void RemoveOldVersions(System.Collections.Generic.IReadOnlyList<string> confirmedPaths)
    {
        EnsureEnabled();
        var state = ReadState() ?? throw new InvalidDataException("No managed plugin installation state.");
        foreach (var path in confirmedPaths)
        {
            if (!OldVersionDirectories().Contains(Path.GetFullPath(path), StringComparer.OrdinalIgnoreCase))
                throw new IOException("The old plugin version is no longer eligible for removal: " + path);
            if (state.PreviousExecutable != null && Path.GetFullPath(state.PreviousExecutable).StartsWith(
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                state = state with { PreviousExecutable = null, PreviousVersion = null };
                WriteState(state);
            }
            Updates.OldVersionCleanup.DeletePluginVersion(RootDirectory, path, state.Executable, settings.NandExecutablePath);
        }
    }
    public void Rollback()
    {
        EnsureEnabled();
        var state = ReadState();
        if (!CanRollback || state == null) throw new InvalidOperationException("No previous NAND version.");
        VersionStorage.VerifyExecutable(state.PreviousExecutable!);
        using var file = File.OpenRead(state.PreviousExecutable!);
        var hash = Convert.ToHexString(SHA256.HashData(file));
        WriteState(new(state.PreviousExecutable!, state.PreviousVersion!, "", hash, state.Executable, state.Version));
    }
    public void Prepare(IProgressReporter progress, CancellationToken token)
    {
        EnsureEnabled();
        if (UsesManagedInstallation && !File.Exists(ExecutablePath))
            UpdateAsync(progress, token).GetAwaiter().GetResult();
    }
    public async Task UpdateAsync(IProgressReporter progress, CancellationToken token)
    {
        EnsureEnabled();
        if (!UsesManagedInstallation) throw new InvalidOperationException(LocalizationManager.Instance.Current.Keys.Nand_CustomUpdate);
        progress.SetMode(true);
        progress.SetText(LocalizationManager.Instance.Current.Keys.Nand_Updating);
        var client = httpClient ?? Client;
        var response = await Emignatik.NxFileViewer.Services.Updates.GitHubUpdateRequests.For(httpClient).GetAsync(ExternalSources.ReleaseApi(ExternalSources.Current.NandRepository), false, token).ConfigureAwait(false);
        using var release = JsonDocument.Parse(response.Body);
        var root = release.RootElement;
        if (root.GetProperty("draft").GetBoolean() || root.GetProperty("prerelease").GetBoolean()) throw new InvalidDataException("NAND release is not stable.");
        var tag = root.GetProperty("tag_name").GetString()!;
        var assets = root.GetProperty("assets").EnumerateArray().Where(a => a.GetProperty("name").GetString() == $"NxNandManager.{tag}.zip").ToArray();
        if (assets.Length != 1) throw new InvalidDataException("No supported NxNandManager Windows package.");
        var asset = assets[0];
        var digest = asset.GetProperty("digest").GetString();
        if (digest == null || !digest.StartsWith("sha256:", StringComparison.Ordinal) || digest.Length != 71 || !digest[7..].All(Uri.IsHexDigit))
            throw new InvalidDataException("NAND package has no SHA-256 digest.");
        var url = new Uri(asset.GetProperty("browser_download_url").GetString()!);
        if (url.Scheme != "https" || url.Host != "github.com" || !url.AbsolutePath.StartsWith(ExternalSources.ReleaseDownloadPrefix(ExternalSources.Current.NandRepository), StringComparison.Ordinal))
            throw new InvalidDataException("Unexpected NAND download URL.");
        var state = ReadState();
        if (state?.Version == tag && state.Digest == digest && File.Exists(state.Executable))
        {
            using var installed = File.OpenRead(state.Executable);
            if (Convert.ToHexString(SHA256.HashData(installed)).Equals(state.ExecutableDigest, StringComparison.OrdinalIgnoreCase))
            { (compatibilityCheck ?? CheckCompatibility)(state.Executable, token); return; }
        }
        Directory.CreateDirectory(RootDirectory);
        var staging = Path.Combine(RootDirectory, "version-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try
        {
            var zip = Path.Combine(staging, "package.zip");
            using var download = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            download.EnsureSuccessStatusCode();
            await using (var output = File.Create(zip)) await download.Content.CopyToAsync(output, token).ConfigureAwait(false);
            using (var input = File.OpenRead(zip))
                if (!Convert.ToHexString(SHA256.HashData(input)).Equals(digest[7..], StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("NAND SHA-256 mismatch.");
            var unpacked = Path.Combine(staging, "files");
            ZipFile.ExtractToDirectory(zip, unpacked);
            File.Delete(zip);
            var executables = Directory.GetFiles(unpacked, "NxNandManager.exe", SearchOption.AllDirectories);
            if (executables.Length != 1) throw new InvalidDataException("Unexpected NAND package layout.");
            var executable = executables[0];
            (compatibilityCheck ?? CheckCompatibility)(executable, token);
            token.ThrowIfCancellationRequested();
            using var activated = File.OpenRead(executable);
            var hash = Convert.ToHexString(SHA256.HashData(activated));
            WriteState(new(executable, tag, digest, hash, state?.Executable, state?.Version));
        }
        catch
        {
            try { Directory.Delete(staging, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            throw;
        }
        finally { progress.SetMode(false); }
    }
    public static void CheckCompatibility(string executable, CancellationToken token)
    {
        executable = Path.GetFullPath(executable);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true,
            RedirectStandardError = true, RedirectStandardInput = true, WorkingDirectory = Path.GetDirectoryName(executable)!
        };
        // Upstream prints usage and exits -1001 for the unknown --help argument.
        start.ArgumentList.Add("--help");
        var help = new NandProcessRunner(allowUsageExit: true).Run(start, new SilentProgress(), timeout.Token);
        if (new[] { "NxNandManager", "-i", "-o", "-part=", "--info", "-keyset" }.Any(option => !help.Contains(option, StringComparison.Ordinal)))
            throw new InvalidDataException("Incompatible NxNandManager CLI.");
    }
    private sealed class SilentProgress : IProgressReporter
    {
        public void SetMode(bool value) { }
        public void SetText(string value) { }
        public void SetPercentage(double value) { }
    }
}
