using System;
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
using Emignatik.NxFileViewer.Services.Nsz;
using Emignatik.NxFileViewer.Services.Updates;
using Emignatik.NxFileViewer.Settings;

namespace Emignatik.NxFileViewer.Services.PackageOperations;

/// <summary>Installs to a fresh directory; the caller activates only a complete, checked installation.</summary>
public sealed class PackageEngineInstaller(HttpClient? httpClient = null, string? rootDirectory = null,
    Action<string, string[], string, CancellationToken>? runProcess = null)
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromMinutes(15) };
    private const string PythonVersion = "3.12.10";
    private const string PythonDigest = "4acbed6dd1c744b0376e3b1cf57ce906f9dc9e95e68824584c8099a63025a3c3";
    private HttpClient Http => httpClient ?? Client;

    public async Task<string> InstallAsync(PackageEngine engine, IProgressReporter progress, CancellationToken token)
    {
        if (!Environment.Is64BitOperatingSystem) throw new PlatformNotSupportedException("These plugins require 64-bit Windows.");
        var root = Path.GetFullPath(rootDirectory ?? Path.Combine(AppContext.BaseDirectory, "Plugins", "PackageEngines"));
        var directory = Path.Combine(root, engine + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        progress.SetMode(true);
        progress.SetText(LocalizationManager.Instance.Current.Keys.Update_Downloading);
        try
        {
            var result = engine switch
            {
                PackageEngine.Rust => await InstallRust(directory, progress, token).ConfigureAwait(false),
                PackageEngine.Kronos => await InstallKronos(directory, progress, token).ConfigureAwait(false),
                _ => throw new ArgumentOutOfRangeException(nameof(engine))
            };
            token.ThrowIfCancellationRequested();
            return result;
        }
        catch
        {
            // Only this invocation's fresh directory is eligible for cleanup.
            try { Directory.Delete(directory, true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            throw;
        }
        finally { progress.SetMode(false); }
    }

    private async Task<string> InstallRust(string directory, IProgressReporter progress, CancellationToken token)
    {
        var repository = ExternalSources.Current.RustPackageRepository;
        var metadata = await GitHubUpdateRequests.For(httpClient).GetAsync(ExternalSources.ReleaseApi(repository), false, token).ConfigureAwait(false);
        using var json = JsonDocument.Parse(metadata.Body);
        var release = json.RootElement;
        if (release.GetProperty("draft").GetBoolean() || release.GetProperty("prerelease").GetBoolean())
            throw new InvalidDataException("Expected a stable nscb_rust release.");
        var candidates = release.GetProperty("assets").EnumerateArray()
            .Where(a => a.GetProperty("name").GetString() == "nscb_rust.exe").ToArray();
        if (candidates.Length != 1) throw new InvalidDataException("No unambiguous Windows nscb_rust release asset.");
        var asset = candidates[0];
        var url = new Uri(asset.GetProperty("browser_download_url").GetString()!);
        if (url.Scheme != "https" || url.Host != "github.com" ||
            !url.AbsolutePath.StartsWith(ExternalSources.ReleaseDownloadPrefix(repository), StringComparison.Ordinal))
            throw new InvalidDataException("Unexpected nscb_rust download URL.");
        var digest = asset.GetProperty("digest").GetString();
        if (digest == null || !digest.StartsWith("sha256:", StringComparison.Ordinal) || digest.Length != 71)
            throw new InvalidDataException("The nscb_rust release asset has no SHA-256 digest.");
        var path = Path.Combine(directory, "nscb_rust.exe");
        await Download(url.AbsoluteUri, path, digest[7..], progress, token).ConfigureAwait(false);
        Run(path, new[] { "--help" }, directory, token);
        await File.WriteAllTextAsync(Path.Combine(directory, "installation.json"),
            JsonSerializer.Serialize(new { Repository = repository, Version = release.GetProperty("tag_name").GetString(), Sha256 = digest }), token).ConfigureAwait(false);
        return path;
    }

    private async Task<string> InstallKronos(string directory, IProgressReporter progress, CancellationToken token)
    {
        var repository = ExternalSources.Current.KronosPackageRepository;
        var metadata = await GitHubUpdateRequests.For(httpClient).GetAsync(
            "https://api.github.com/repos/" + repository + "/commits/master", false, token).ConfigureAwait(false);
        using var json = JsonDocument.Parse(metadata.Body);
        var commit = json.RootElement.GetProperty("sha").GetString()!;
        if (commit.Length != 40 || commit.Any(c => !Uri.IsHexDigit(c))) throw new InvalidDataException("Invalid Kronos commit.");
        var source = Path.Combine(directory, "source.zip");
        await Download("https://codeload.github.com/" + repository + "/zip/" + commit, source, null, progress, token).ConfigureAwait(false);
        Extract(source, directory, true, token);
        File.Delete(source);
        if (!File.Exists(Path.Combine(directory, "ztools", "squirrel.py"))) throw new InvalidDataException("Kronos source layout is incompatible.");
        var archive = Path.Combine(directory, "python.zip");
        await Download($"https://www.python.org/ftp/python/{PythonVersion}/python-{PythonVersion}-embed-amd64.zip",
            archive, PythonDigest, progress, token).ConfigureAwait(false);
        var runtime = Path.Combine(directory, "runtime");
        Directory.CreateDirectory(runtime);
        Extract(archive, runtime, false, token);
        File.Delete(archive);
        // Enable only this installation's site-packages; no system Python is changed.
        await File.WriteAllTextAsync(Path.Combine(runtime, "python312._pth"), "python312.zip\n.\nLib/site-packages\nimport site\n", token).ConfigureAwait(false);
        var pipResponse = await Http.GetStringAsync("https://pypi.org/pypi/pip/24.0/json", token).ConfigureAwait(false);
        using var pip = JsonDocument.Parse(pipResponse);
        var wheel = pip.RootElement.GetProperty("urls").EnumerateArray().Single(a => a.GetProperty("filename").GetString() == "pip-24.0-py3-none-any.whl");
        var wheelUrl = new Uri(wheel.GetProperty("url").GetString()!);
        if (wheelUrl.Scheme != "https" || wheelUrl.Host != "files.pythonhosted.org") throw new InvalidDataException("Unexpected pip wheel URL.");
        var pipArchive = Path.Combine(directory, "pip.zip");
        await Download(wheelUrl.AbsoluteUri, pipArchive, wheel.GetProperty("digests").GetProperty("sha256").GetString(), progress, token).ConfigureAwait(false);
        var packages = Path.Combine(runtime, "Lib", "site-packages");
        Directory.CreateDirectory(packages);
        Extract(pipArchive, packages, false, token);
        File.Delete(pipArchive);
        progress.SetText(LocalizationManager.Instance.Current.Keys.Update_Installing + " Kronos / Python");
        var python = Path.Combine(runtime, "python.exe");
        // Embedded Python cannot use pip's temporary build environment (_pth isolates sys.path).
        Run(python, new[] { "-m", "pip", "--isolated", "install", "--disable-pip-version-check", "--no-warn-script-location",
            "--no-cache-dir", "--index-url", "https://pypi.org/simple", "--only-binary=:all:", "setuptools==75.8.0", "wheel==0.45.1" }, directory, token);
        Run(python, new[] { "-m", "pip", "--isolated", "install", "--no-build-isolation", "--disable-pip-version-check", "--no-warn-script-location",
            "--no-cache-dir", "--index-url", "https://pypi.org/simple",
            "urllib3", "unidecode", "tqdm", "beautifulsoup4", "requests", "pillow", "pywin32", "pycryptodome",
            "pykakasi", "googletrans==4.0.0rc1", "chardet", "eel", "bottle", "zstandard", "colorama",
            "google-auth-httplib2", "google-auth-oauthlib", "windows-curses", "oauth2client", "comtypes", "google-api-python-client" }, directory, token);
        Run(python, new[] { Path.Combine(directory, "ztools", "squirrel.py"), "--help" }, directory, token);
        await File.WriteAllTextAsync(Path.Combine(directory, "installation.json"),
            JsonSerializer.Serialize(new { Repository = repository, Commit = commit, Python = PythonVersion }), token).ConfigureAwait(false);
        return directory;
    }

    private void Run(string executable, string[] args, string directory, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromMinutes(15));
        if (runProcess != null) runProcess(executable, args, directory, timeout.Token);
        else
        {
            var diagnostics = new System.Collections.Generic.Queue<string>();
            var gate = new object();
            try
            {
                NszProcess.Run(executable, args, directory, timeout.Token, onDiagnostic: line =>
                {
                    lock (gate) { diagnostics.Enqueue(line); while (diagnostics.Count > 30) diagnostics.Dequeue(); }
                });
            }
            catch (IOException ex)
            {
                lock (gate) throw new IOException(ex.Message + Environment.NewLine + string.Join(Environment.NewLine, diagnostics), ex);
            }
        }
    }

    private async Task Download(string url, string path, string? digest, IProgressReporter progress, CancellationToken token)
    {
        using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using (var output = File.Create(path))
        await using (var input = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false))
        {
            var buffer = new byte[81920];
            long total = 0;
            int count;
            while ((count = await input.ReadAsync(buffer, token).ConfigureAwait(false)) > 0)
            {
                total += count;
                if (total > 1024L * 1024 * 1024) throw new InvalidDataException("Plugin download exceeds 1 GiB.");
                await output.WriteAsync(buffer.AsMemory(0, count), token).ConfigureAwait(false);
                if (response.Content.Headers.ContentLength is > 0)
                {
                    progress.SetMode(false);
                    progress.SetPercentage((double)total / response.Content.Headers.ContentLength.Value);
                }
            }
        }
        if (digest != null)
        {
            await using var stream = File.OpenRead(path);
            var actual = Convert.ToHexString(await SHA256.HashDataAsync(stream, token).ConfigureAwait(false));
            if (!actual.Equals(digest, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Plugin download SHA-256 mismatch.");
        }
        progress.SetMode(true);
    }

    private static void Extract(string archivePath, string target, bool sourceTree, CancellationToken token)
    {
        using var archive = ZipFile.OpenRead(archivePath);
        var root = Path.GetFullPath(target) + Path.DirectorySeparatorChar;
        long size = 0;
        foreach (var entry in archive.Entries)
        {
            token.ThrowIfCancellationRequested();
            var name = entry.FullName.Replace('\\', '/');
            if (sourceTree)
            {
                var slash = name.IndexOf('/');
                if (slash < 0) continue;
                name = name[(slash + 1)..];
                if (name.StartsWith("py/", StringComparison.Ordinal)) name = name[3..];
                else if (name != "LICENSE") continue;
            }
            if (name.Length == 0) continue;
            if (name.Split('/').Any(p => p is "." or ".." || p.Contains(':')) ||
                ((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000)
                throw new InvalidDataException("Unsafe plugin archive entry.");
            var path = Path.GetFullPath(Path.Combine(target, name));
            if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Plugin archive entry escapes installation.");
            size += entry.Length;
            if (size > 2L * 1024 * 1024 * 1024) throw new InvalidDataException("Plugin archive exceeds 2 GiB.");
            if (name.EndsWith('/')) { Directory.CreateDirectory(path); continue; }
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var input = entry.Open();
            using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
            var buffer = new byte[81920];
            int count;
            while ((count = input.Read(buffer)) > 0) { token.ThrowIfCancellationRequested(); output.Write(buffer, 0, count); }
        }
    }
}
