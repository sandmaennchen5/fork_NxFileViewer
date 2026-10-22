using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Emignatik.NxFileViewer.Services.FileAssociations;
using Emignatik.NxFileViewer.Services.Integrity;
using Emignatik.NxFileViewer.Services.Updates;

namespace Emignatik.NxFileViewer.Services;

public sealed record FirmwareReferenceFile(string Name, string Sha, Uri Url);

/// <summary>Forge-native component reads. A firmware snapshot always comes from one complete source.</summary>
public sealed class RepositoryMirrors(HttpClient? client = null, GitHubUpdateRequests? requests = null, bool automatic = false)
{
    private static readonly HttpClient DefaultClient = new() { Timeout = TimeSpan.FromMinutes(5) };
    private static readonly ConcurrentDictionary<string, DateTimeOffset> RetryAt = new(StringComparer.OrdinalIgnoreCase);
    public string LastNotice { get; private set; } = "";
    private readonly bool _automatic = automatic;
    private readonly HttpClient _client = client ?? DefaultClient;
    private readonly GitHubUpdateRequests _requests = requests ?? GitHubUpdateRequests.For(client);
    private static string? Text(JsonElement e, string name) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
    private static bool Flag(JsonElement e, string name) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.True;
    private static bool Failure(Exception ex, CancellationToken token) => ex is HttpRequestException or IOException or InvalidDataException or JsonException || ex is OperationCanceledException && !token.IsCancellationRequested;

    private sealed class JsonPage(JsonDocument document, bool? hasNext) : IDisposable
    {
        public JsonElement RootElement => document.RootElement;
        public bool? HasNext => hasNext;
        public void Dispose() => document.Dispose();
    }
    private async Task<byte[]> BytesAsync(Uri url, int limit, CancellationToken token, Action<HttpResponseMessage>? readHeaders = null)
    {
        _ = ViewerUpdateSource.Https(url.AbsoluteUri);
        var origin = url.GetLeftPart(UriPartial.Authority);
        if (RetryAt.TryGetValue(origin, out var retry) && retry > DateTimeOffset.UtcNow) throw new HttpRequestException("Component source is rate limited.", null, HttpStatusCode.TooManyRequests);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd("NxFileViewer-Components/1.0");
        request.Headers.CacheControl = new() { NoCache = true };
        using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.TooManyRequests || response.StatusCode == HttpStatusCode.Forbidden && response.Headers.RetryAfter != null)
            RetryAt[origin] = response.Headers.RetryAfter?.Date ?? DateTimeOffset.UtcNow.Add(response.Headers.RetryAfter?.Delta ?? TimeSpan.FromMinutes(1));
        response.EnsureSuccessStatusCode();
        readHeaders?.Invoke(response);
        if (response.Content.Headers.ContentLength > limit) throw new InvalidDataException("Component metadata exceeds size limit.");
        await using var input = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
        using var output = new MemoryStream();
        var buffer = new byte[8192]; int count;
        while ((count = await input.ReadAsync(buffer, timeout.Token).ConfigureAwait(false)) > 0)
        {
            if (output.Length + count > limit) throw new InvalidDataException("Component metadata exceeds size limit.");
            output.Write(buffer, 0, count);
        }
        return output.ToArray();
    }
    private async Task<JsonPage> JsonAsync(ViewerUpdateSource source, Uri url, CancellationToken token)
    {
        if (source.Kind == ViewerUpdateSourceKind.GitHub && url.Host == "api.github.com")
        {
            var response = await _requests.GetAsync(url.AbsoluteUri, _automatic, token).ConfigureAwait(false);
            LastNotice = response.Notice;
            return new(JsonDocument.Parse(response.Body), null);
        }
        LastNotice = "";
        bool? next = null;
        var bytes = await BytesAsync(url, 2 * 1024 * 1024, token, response => {
            if (response.Headers.TryGetValues("X-Next-Page", out var pages)) next = pages.Any(p => int.TryParse(p, out var n) && n > 0);
            else if (response.Headers.TryGetValues("Link", out var links)) next = links.Any(link => link.Contains("rel=\"next\"", StringComparison.OrdinalIgnoreCase) || link.Contains("rel=next", StringComparison.OrdinalIgnoreCase));
        }).ConfigureAwait(false);
        return new(JsonDocument.Parse(bytes), next);
    }

    public async Task<IReadOnlyList<FirmwareReferenceFile>> FirmwareListingAsync(GitHubContentLocation location, CancellationToken token)
    {
        Exception? failure = null;
        foreach (var spec in location.SourceSpecs)
        {
            token.ThrowIfCancellationRequested();
            try { return await ListAsync(location, ViewerUpdateSource.Parse(spec), token).ConfigureAwait(false); }
            catch (Exception ex) when (Failure(ex, token)) { failure = ex; }
        }
        throw new InvalidDataException("No firmware reference source is available.", failure);
    }
    private async Task<List<FirmwareReferenceFile>> ListAsync(GitHubContentLocation location, ViewerUpdateSource source, CancellationToken token)
    {
        var result = new List<FirmwareReferenceFile>();
        var prefix = source.Endpoint.AbsoluteUri.TrimEnd('/');
        var path = string.Join('/', location.Path.Split('/').Select(Uri.EscapeDataString));
        var reference = Uri.EscapeDataString(location.Ref);
        for (var page = 1; page <= 16; page++)
        {
            var address = source.Kind switch {
                ViewerUpdateSourceKind.GitHub => $"{prefix}/contents/{path}?ref={reference}",
                ViewerUpdateSourceKind.GitLab => $"{prefix}/repository/tree?path={Uri.EscapeDataString(location.Path)}&ref={reference}&per_page=100&page={page}",
                ViewerUpdateSourceKind.Forgejo => $"{prefix}/contents/{path}?ref={reference}",
                _ => throw new InvalidDataException("Firmware requires a repository source.")
            };
            using var json = await JsonAsync(source, new Uri(address), token).ConfigureAwait(false);
            if (json.RootElement.ValueKind != JsonValueKind.Array) throw new InvalidDataException("Expected firmware directory listing.");
            foreach (var entry in json.RootElement.EnumerateArray())
            {
                var name = Text(entry, "name");
                var type = Text(entry, "type");
                if (type != (source.Kind == ViewerUpdateSourceKind.GitLab ? "blob" : "file") || name == null || !name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;
                if (name != ViewerPackageLayout.Relative(name) || name.Contains('/') || name.Contains('\\')) throw new InvalidDataException("Unsafe firmware filename.");
                var sha = Text(entry, source.Kind == ViewerUpdateSourceKind.GitLab ? "id" : "sha");
                if (sha == null || sha.Length != 40 || !sha.All(Uri.IsHexDigit)) throw new InvalidDataException("Missing firmware Git blob hash.");
                var filePath = (location.Path.Length > 0 ? location.Path + "/" : "") + name;
                var url = source.Kind switch {
                    ViewerUpdateSourceKind.GitHub => new Uri(new GitHubContentLocation(source.Repository!, location.Ref, location.Path).RawUrl(name)),
                    ViewerUpdateSourceKind.GitLab => new Uri($"{prefix}/repository/files/{Uri.EscapeDataString(filePath)}/raw?ref={reference}"),
                    _ => ViewerUpdateSource.Https(Text(entry, "download_url") ?? $"{prefix}/raw/{string.Join('/', filePath.Split('/').Select(Uri.EscapeDataString))}?ref={reference}")
                };
                if (source.Kind != ViewerUpdateSourceKind.GitHub) source.ValidateDownload(url);
                if (result.Count >= 256 || result.Any(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) throw new InvalidDataException("Duplicate or excessive firmware references.");
                result.Add(new(name, sha, url));
            }
            if (source.Kind != ViewerUpdateSourceKind.GitLab || json.HasNext == false || json.HasNext == null && json.RootElement.GetArrayLength() < 100) break;
            if (page == 16) throw new InvalidDataException("Firmware listing pagination limit exceeded.");
        }
        if (result.Count == 0) throw new InvalidDataException("Firmware source has no reference JSONs.");
        return result;
    }
    public async Task<IReadOnlyDictionary<string, string>> FirmwareManifestsAsync(GitHubContentLocation location, CancellationToken token)
    {
        Exception? failure = null;
        foreach (var spec in location.SourceSpecs)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var files = await ListAsync(location, ViewerUpdateSource.Parse(spec), token).ConfigureAwait(false);
                var manifests = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                long total = 0;
                foreach (var file in files)
                {
                    var bytes = await BytesAsync(file.Url, 8 * 1024 * 1024, token).ConfigureAwait(false);
                    if ((total += bytes.Length) > 64 * 1024 * 1024) throw new InvalidDataException("Firmware references exceed size limit.");
                    using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
                    hash.AppendData(Encoding.ASCII.GetBytes("blob " + bytes.Length + "\0")); hash.AppendData(bytes);
                    if (!Convert.ToHexString(hash.GetHashAndReset()).Equals(file.Sha, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Firmware reference changed during download.");
                    manifests.Add(file.Name, Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF'));
                }
                _ = new FirmwareIntegrityVerifier(manifests.Values);
                return manifests;
            }
            catch (Exception ex) when (Failure(ex, token)) { failure = ex; }
        }
        throw new InvalidDataException("No complete verified firmware reference source is available.", failure);
    }

    public async Task<ExplorerPackageRelease?> ExplorerAsync(string[] specs, string architecture, bool previews, CancellationToken token)
    {
        if (architecture is not ("x64" or "arm64")) throw new PlatformNotSupportedException("Explorer requires x64 or ARM64.");
        var candidates = new List<ExplorerPackageRelease>(); Exception? failure = null; bool reached = false;
        foreach (var spec in specs)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var source = ViewerUpdateSource.Parse(spec);
                var size = source.Kind == ViewerUpdateSourceKind.Forgejo ? 50 : 100;
                for (var page = 1; page <= 100; page++)
                {
                    var query = source.Kind == ViewerUpdateSourceKind.Forgejo ? $"?limit={size}&page={page}" : $"?per_page={size}&page={page}";
                    using var json = await JsonAsync(source, new Uri(source.Endpoint.AbsoluteUri.TrimEnd('/') + "/releases" + query), token).ConfigureAwait(false);
                    if (json.RootElement.ValueKind != JsonValueKind.Array) throw new InvalidDataException("Expected Explorer release list.");
                    foreach (var release in json.RootElement.EnumerateArray())
                    {
                        if (Flag(release, "draft") || Flag(release, "upcoming_release") ||
                            DateTimeOffset.TryParse(Text(release, "released_at"), out var date) && date > DateTimeOffset.UtcNow) continue;
                        var tag = Text(release, "tag_name");
                        if (tag == null || !ViewerVersion.TryParse(tag, out _) || !previews && (Flag(release, "prerelease") || tag.Contains('-'))) continue;
                        ExplorerPackageRelease? candidate;
                        if (source.Kind == ViewerUpdateSourceKind.GitHub) candidate = ExplorerPackageService.SelectRelease(release, architecture, previews, spec);
                        else candidate = await NativeExplorerAsync(source, release, tag, architecture, previews, token).ConfigureAwait(false);
                        if (candidate != null) candidates.Add(candidate);
                    }
                    if (json.HasNext == false || json.HasNext == null && json.RootElement.GetArrayLength() < size) break;
                    if (page == 100) throw new InvalidDataException("Explorer release pagination limit exceeded.");
                }
                reached = true;
            }
            catch (Exception ex) when (Failure(ex, token)) { failure = ex; }
        }
        if (!reached && failure != null && candidates.Count == 0) throw new InvalidDataException("No Explorer release source is available.", failure);
        return candidates.OrderByDescending(c => c.Version).FirstOrDefault();
    }
    private async Task<ExplorerPackageRelease?> NativeExplorerAsync(ViewerUpdateSource source, JsonElement release, string tag, string arch, bool previews, CancellationToken token)
    {
        if (!release.TryGetProperty("assets", out var assets)) return null;
        if (source.Kind == ViewerUpdateSourceKind.GitLab && (assets.ValueKind != JsonValueKind.Object || !assets.TryGetProperty("links", out assets))) return null;
        if (assets.ValueKind != JsonValueKind.Array) throw new InvalidDataException("Expected release assets.");
        var entries = assets.EnumerateArray().ToArray();
        if (!entries.Any(e => (Text(e,"name") ?? "").StartsWith("NxFileViewerFork.Explorer_", StringComparison.Ordinal) && (Text(e,"name") ?? "").EndsWith("_" + arch + "_setup.zip", StringComparison.Ordinal))) return null;
        string? Url(JsonElement e) => source.Kind == ViewerUpdateSourceKind.GitLab ? Text(e,"direct_asset_url") ?? Text(e,"url") : Text(e,"browser_download_url");
        var descriptor = entries.Where(e => Text(e,"name") == ViewerUpdateMetadata.ReleaseManifestName).ToArray();
        if (descriptor.Length == 0) return null;
        if (descriptor.Length != 1) throw new InvalidDataException("Duplicate release descriptor.");
        var descriptorUrl = ViewerUpdateSource.Https(Url(descriptor[0]) ?? ""); source.ValidateDownload(descriptorUrl);
        var manifest = ViewerUpdateMetadata.ReadRelease(await BytesAsync(descriptorUrl, ViewerUpdateMetadata.MaximumManifestSize, token).ConfigureAwait(false), tag);
        if (manifest.Draft || !previews && manifest.Prerelease) return null;
        var normalized = new List<object>();
        foreach (var package in manifest.ExplorerPackages)
        {
            if (package.Architecture != arch) continue;
            var matches = entries.Where(e => Text(e,"name") == package.AssetName).ToArray();
            if (matches.Length == 0) continue;
            if (matches.Length != 1) throw new InvalidDataException("Duplicate Explorer ZIP.");
            var hash = ViewerUpdateService.ExpectedHash(package.Sha256 == null ? null : "sha256:" + package.Sha256);
            var apiDigest = Text(matches[0], "digest");
            if (apiDigest != null && !ViewerUpdateService.ExpectedHash(apiDigest).Equals(hash, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Explorer asset checksum disagrees with descriptor.");
            normalized.Add(new { name=package.AssetName, browser_download_url=Url(matches[0]), digest="sha256:" + hash });
        }
        return ExplorerPackageService.SelectRelease(JsonSerializer.SerializeToElement(new { draft=false, prerelease=Flag(release,"prerelease") || manifest.Prerelease, tag_name=tag, assets=normalized }), arch, previews, source.Value);
    }
}
