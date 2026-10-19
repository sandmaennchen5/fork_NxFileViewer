using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Reflection;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Emignatik.NxFileViewer.Services.Updates;

public sealed record ViewerRelease(Version Version, string AssetName, Uri DownloadUrl, string? Digest)
{
    public string Tag { get; init; } = "";
    public bool IsPrerelease { get; init; }
    public string SourceRepository { get; init; } = ViewerUpdateService.Repository;
    public string? Architecture { get; init; }
    public string? SourceSpec { get; init; }
    public string DisplayVersion => Tag.Length > 0 ? Tag : Version.ToString();
}
public sealed record PreparedViewerUpdate(string Directory, string Executable)
{
    public string EntryPoint { get; init; } = "NxFileViewer.exe";
    public ViewerPackageFile[] Files { get; init; } = Array.Empty<ViewerPackageFile>();
    public string[] Directories { get; init; } = Array.Empty<string>();
    public string[] RemoveDirectories { get; init; } = Array.Empty<string>();
}

public sealed class ViewerUpdateService
{
    public const string Repository = "sandmaennchen5/fork_NxFileViewer";
    private static readonly HttpClient Client = CreateClient();
    private readonly HttpClient _client;
    private readonly GitHubUpdateRequests _requests;
    private readonly string _applicationDirectory;
    private readonly ViewerRemoteReleaseSource _remoteSources;
    public string LastCheckNotice { get; private set; } = "";
    public ViewerUpdateService(HttpClient? client = null, string? applicationDirectory = null) { _client = client ?? Client; _requests = GitHubUpdateRequests.For(client); _applicationDirectory = applicationDirectory ?? AppContext.BaseDirectory; _remoteSources = new(_client); }
    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("NxFileViewerFork-Updater/1.0");
        return client;
    }
    public async Task<ViewerRelease?> CheckAsync(Version current, string architecture, CancellationToken token, bool includePrereleases = false, string? currentTag = null, bool selfContained = false, bool automatic = false)
    {
        if (architecture is not ("x64" or "x86")) throw new NotSupportedException("Unsupported update architecture.");
        LastCheckNotice = "";
        var sources = ViewerUpdateMetadata.ReadState(_applicationDirectory).UpdateSources.Select(ViewerUpdateSource.Parse).DistinctBy(source => source.Key, StringComparer.Ordinal);
        var candidates = new List<ViewerRelease>();
        Exception? failure = null;
        bool reachedSource = false;
        foreach (var source in sources)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var candidate = source.Kind == ViewerUpdateSourceKind.GitHub
                    ? await CheckRepositoryAsync(source.Repository!, current, architecture, token, includePrereleases, currentTag, selfContained, automatic).ConfigureAwait(false)
                    : await _remoteSources.CheckAsync(source, current, architecture, includePrereleases, currentTag, selfContained, token).ConfigureAwait(false);
                reachedSource = true;
                if (candidate != null) candidates.Add(candidate);
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException || ex is OperationCanceledException && !token.IsCancellationRequested)
            {
                failure = ex;
                LastCheckNotice = "Update source " + source.DisplayName + ": " + ex.Message;
            }
        }
        if (!reachedSource && failure != null) throw failure;
        return candidates.OrderByDescending(ReleaseVersion).ThenBy(r => r.IsPrerelease).FirstOrDefault();
    }

    private static bool Eligible(JsonElement release, Version current, string? currentTag, bool includePrereleases)
    {
        if (release.GetProperty("draft").GetBoolean()) return false;
        var tag = release.GetProperty("tag_name").GetString()!;
        if (tag.Contains('/') || tag.Contains('\\') || !ViewerVersion.TryParse(tag, out var candidate)) return false;
        if (!includePrereleases && (release.GetProperty("prerelease").GetBoolean() || tag.Split('+')[0].Contains('-'))) return false;
        var installed = ViewerVersion.TryParse(currentTag, out var parsed) && parsed.Numeric == Normalize(current) ? parsed : ViewerVersion.FromNumeric(current);
        return candidate.CompareTo(installed) > 0;
    }

    private async Task<ViewerRelease?> CheckRepositoryAsync(string repository, Version current, string architecture, CancellationToken token,
        bool includePrereleases, string? currentTag, bool selfContained, bool automatic)
    {
        var endpoint = "https://api.github.com/repos/" + repository + "/releases";
        if (!includePrereleases)
        {
            GitHubUpdateResponse response;
            try { response = await _requests.GetAsync(endpoint + "/latest", automatic, token).ConfigureAwait(false); }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound) { return null; }
            if (response.Notice.Length > 0) LastCheckNotice = response.Notice;
            using var latest = JsonDocument.Parse(response.Body);
            if (!Eligible(latest.RootElement, current, currentTag, false)) return null;
            var candidate = await SelectRemoteReleaseAsync(latest.RootElement, repository, current, architecture, false, currentTag, selfContained, token).ConfigureAwait(false);
            if (candidate != null) return candidate;
            // A missing distribution in latest must not hide a compatible intermediate release.
        }
        var candidates = new List<ViewerRelease>();
        for (var page = 1; page <= 100; page++)
        {
            GitHubUpdateResponse listing;
            try { listing = await _requests.GetAsync(endpoint + $"?per_page=100&page={page}", automatic, token).ConfigureAwait(false); }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound) { break; }
            if (listing.Notice.Length > 0) LastCheckNotice = listing.Notice;
            using var releases = JsonDocument.Parse(listing.Body);
            foreach (var item in releases.RootElement.EnumerateArray())
            {
                var candidate = await SelectRemoteReleaseAsync(item, repository, current, architecture, includePrereleases, currentTag, selfContained, token).ConfigureAwait(false);
                if (candidate != null) candidates.Add(candidate);
            }
            if (releases.RootElement.GetArrayLength() < 100) break;
        }
        return candidates.OrderByDescending(ReleaseVersion).ThenBy(r => r.IsPrerelease).FirstOrDefault();
    }

    private async Task<ViewerRelease?> SelectRemoteReleaseAsync(JsonElement release, string repository, Version current, string architecture,
        bool includePrereleases, string? currentTag, bool selfContained, CancellationToken token)
    {
        if (!Eligible(release, current, currentTag, includePrereleases)) return null;
        var descriptor = release.GetProperty("assets").EnumerateArray().FirstOrDefault(a => a.GetProperty("name").GetString() == ViewerUpdateMetadata.ReleaseManifestName);
        string? assetName = null;
        bool manifestPrerelease = false;
        if (descriptor.ValueKind != JsonValueKind.Undefined)
        {
            var url = new Uri(descriptor.GetProperty("browser_download_url").GetString()!);
            ValidateDownloadUrl(url, repository);
            var hash = ExpectedHash(descriptor.TryGetProperty("digest", out var digest) ? digest.GetString() : null);
            using var response = await _client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using var input = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
            using var bytes = new MemoryStream();
            var buffer = new byte[8192];
            int count;
            while ((count = await input.ReadAsync(buffer, token).ConfigureAwait(false)) > 0)
            {
                if (bytes.Length + count > ViewerUpdateMetadata.MaximumManifestSize) throw new InvalidDataException("Release manifest is too large.");
                bytes.Write(buffer, 0, count);
            }
            var data = bytes.ToArray();
            if (!Convert.ToHexString(SHA256.HashData(data)).Equals(hash, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Release manifest SHA-256 mismatch.");
            var manifest = ViewerUpdateMetadata.ReadRelease(data, release.GetProperty("tag_name").GetString()!);
            if (manifest.Draft || (!includePrereleases && manifest.Prerelease)) return null;
            manifestPrerelease = manifest.Prerelease;
            var package = manifest.Packages.SingleOrDefault(p => p.Architecture == architecture && p.SelfContained == selfContained);
            assetName = package?.AssetName;
            if (assetName == null) return null;
            if (package!.Sha256 != null)
            {
                var archiveAsset = release.GetProperty("assets").EnumerateArray().FirstOrDefault(a => a.GetProperty("name").GetString() == assetName);
                if (archiveAsset.ValueKind == JsonValueKind.Undefined) return null;
                var archiveDigest = archiveAsset.TryGetProperty("digest", out var archiveHash) ? archiveHash.GetString() : null;
                if (!ExpectedHash(archiveDigest).Equals(package.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Release ZIP digest does not match manifest.");
            }
        }
        var selected = SelectRelease(release, current, architecture, includePrereleases, true, currentTag, selfContained, repository, assetName);
        return selected == null ? null : selected with { IsPrerelease = selected.IsPrerelease || manifestPrerelease };
    }
    public static ViewerRelease? SelectRelease(JsonElement release, Version current, string architecture, bool includePrereleases = false, bool skipMissingPackage = false, string? currentTag = null, bool selfContained = false, string repository = Repository, string? assetName = null)
    {
        if (architecture is not ("x64" or "x86")) throw new NotSupportedException("Unsupported update architecture.");
        if (release.GetProperty("draft").GetBoolean()) return null;
        var tag = release.GetProperty("tag_name").GetString()!;
        if (tag.Contains('/') || tag.Contains('\\')) return null;
        var prerelease = release.GetProperty("prerelease").GetBoolean() || tag.Split('+')[0].Contains('-');
        if (prerelease && !includePrereleases) return null;
        if (!ViewerVersion.TryParse(tag, out var candidateVersion)) return null;
        var version = candidateVersion.Numeric;
        var installedVersion = ViewerVersion.TryParse(currentTag, out var parsedCurrent) && parsedCurrent.Numeric == Normalize(current)
            ? parsedCurrent : ViewerVersion.FromNumeric(current);
        if (candidateVersion.CompareTo(installedVersion) <= 0) return null;
        var variant = selfContained ? "_self-contained" : "";
        var name = assetName == null ? $"NxFileViewerFork_v{version.Major}.{version.Minor}.{version.Build}{variant}_{architecture}.zip" : ViewerUpdateMetadata.Asset(assetName);
        var assets = release.GetProperty("assets").EnumerateArray().ToArray();
        var taggedName = $"NxFileViewerFork_v{tag.TrimStart('v', 'V')}{variant}_{architecture}.zip";
        var asset = assets.FirstOrDefault(a => a.GetProperty("name").GetString() == name);
        if (asset.ValueKind == JsonValueKind.Undefined && assetName == null) asset = assets.FirstOrDefault(a => a.GetProperty("name").GetString() == taggedName);
        if (asset.ValueKind == JsonValueKind.Undefined)
        {
            if (skipMissingPackage) return null;
            throw new InvalidDataException("Missing release package: " + name);
        }
        name = asset.GetProperty("name").GetString()!;
        var url = new Uri(asset.GetProperty("browser_download_url").GetString()!);
        ValidateDownloadUrl(url, repository);
        var digest = asset.TryGetProperty("digest", out var hash) ? hash.GetString() : null;
        return new(version, name, url, digest) { Tag = tag, IsPrerelease = prerelease, SourceRepository = repository, Architecture = architecture };
    }
    private static ViewerVersion ReleaseVersion(ViewerRelease release) =>
        ViewerVersion.TryParse(release.Tag, out var parsed) ? parsed : ViewerVersion.FromNumeric(release.Version);
    private static Version Normalize(Version version) => new(version.Major, version.Minor, Math.Max(0, version.Build));
    private static void ValidateDownloadUrl(Uri url, string repository)
    {
        if (url.Scheme != "https" || url.Host != "github.com" || !url.IsDefaultPort || url.UserInfo.Length != 0 || !url.AbsolutePath.StartsWith("/" + ViewerUpdateMetadata.Repository(repository) + "/releases/download/", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Unexpected update download URL.");
    }
    public static string ExpectedHash(string? digest)
    {
        if (digest == null || !digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) || digest.Length != 71 ||
            !digest[7..].All(Uri.IsHexDigit)) throw new InvalidDataException("Release has no valid SHA-256 digest. Re-upload its application ZIP on GitHub.");
        return digest[7..];
    }
    public async Task<PreparedViewerUpdate> PrepareAsync(ViewerRelease release, string applicationDirectory, CancellationToken token, string? installedVersion = null)
    {
        if (release.SourceSpec == null) ValidateDownloadUrl(release.DownloadUrl, release.SourceRepository);
        else ViewerUpdateSource.Parse(release.SourceSpec).ValidateDownload(release.DownloadUrl);
        var expectedHash = ExpectedHash(release.Digest);
        var updates = Path.Combine(Path.GetFullPath(applicationDirectory), "Updates");
        Directory.CreateDirectory(updates);
        if ((File.GetAttributes(updates) & FileAttributes.ReparsePoint) != 0) throw new IOException("Updates must be a local application subdirectory.");
        var stage = Path.Combine(updates, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        var archive = Path.Combine(stage, "release.zip");
        try
        {
            using var response = await _client.GetAsync(release.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using (var input = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false))
            await using (var output = File.Create(archive))
            {
                var buffer = new byte[81920];
                long total = 0;
                int read;
                while ((read = await input.ReadAsync(buffer, token).ConfigureAwait(false)) > 0)
                {
                    total += read;
                    if (total > ViewerPackageLayout.MaximumSize) throw new InvalidDataException("Update archive exceeds size limit.");
                    await output.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false);
                }
            }
            await using (var input = File.OpenRead(archive))
            {
                var actual = Convert.ToHexString(await SHA256.HashDataAsync(input, token).ConfigureAwait(false));
                if (!actual.Equals(expectedHash, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Update SHA-256 mismatch.");
            }
            var prepared = ViewerPackageLayout.Extract(archive, stage, release.AssetName, token);
            var exe = prepared.Executable;
            using (var executable = File.OpenRead(exe))
            using (var pe = new PEReader(executable))
            {
                var expectedMachine = (release.Architecture ?? (release.AssetName.EndsWith("_x64.zip", StringComparison.Ordinal) ? "x64" : "x86")) == "x64" ? Machine.Amd64 : Machine.I386;
                if (pe.PEHeaders.CoffHeader.Machine != expectedMachine ||
                    (pe.PEHeaders.CoffHeader.Characteristics & Characteristics.Dll) != 0 || pe.PEHeaders.CorHeader != null) throw new InvalidDataException("Update executable architecture mismatch.");
            }
            var info = System.Diagnostics.FileVersionInfo.GetVersionInfo(exe);
            if (!string.IsNullOrEmpty(release.Tag) &&
                (!ViewerVersion.TryParse(release.Tag, out var expectedVersion) ||
                 !ViewerVersion.TryParse(info.ProductVersion, out var actualVersion) ||
                 expectedVersion.CompareTo(actualVersion) != 0))
                throw new InvalidDataException("Update executable preview version does not match release tag.");
            if (new Version(info.FileMajorPart, info.FileMinorPart, info.FileBuildPart) != Normalize(release.Version))
                throw new InvalidDataException("Update executable version does not match release.");
            prepared = ViewerUpdateMetadata.Prepare(prepared, applicationDirectory,
                installedVersion ?? Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? Assembly.GetExecutingAssembly().GetName().Version!.ToString(),
                release.Tag.Length > 0 ? release.Tag : release.Version.ToString(), token);
            File.Delete(archive);
            return prepared;
        }
        catch
        {
            ViewerPackageLayout.DeleteOwnedStage(stage);
            throw;
        }
    }
    public static string ExtractExecutable(string archive, string stage, string assetName, CancellationToken token)
    {
        using var zip = ZipFile.OpenRead(archive);
        var folder = Path.GetFileNameWithoutExtension(assetName);
        // Compress-Archive on Windows writes backslashes; ZIP tools also use forward slashes.
        var files = zip.Entries.Where(e => !e.FullName.Replace('\\', '/').EndsWith('/')).ToArray();
        var executablePath = files.Length == 1 ? files[0].FullName.Replace('\\', '/') : null;
        if (files.Length != 1 || (executablePath != folder + "/NxFileViewer.exe" && executablePath != "NxFileViewer.exe") ||
            files[0].Length == 0 || files[0].Length > 256L * 1024 * 1024)
            throw new InvalidDataException("Expected a single-file NxFileViewer release ZIP.");
        token.ThrowIfCancellationRequested();
        // Use a fixed filename rather than any archive-supplied path.
        var destination = Path.Combine(stage, "new.exe");
        using var input = files[0].Open();
        using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write);
        var buffer = new byte[81920];
        int read;
        long total = 0;
        while ((read = input.Read(buffer)) > 0)
        {
            token.ThrowIfCancellationRequested();
            total += read;
            if (total > files[0].Length || total > ViewerPackageLayout.MaximumSize) throw new InvalidDataException("Update executable exceeds declared size.");
            output.Write(buffer, 0, read);
        }
        if (total != files[0].Length) throw new InvalidDataException("Incomplete update executable.");
        return destination;
    }
}
