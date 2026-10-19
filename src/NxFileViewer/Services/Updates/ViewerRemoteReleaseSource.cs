using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Emignatik.NxFileViewer.Services.Updates;

/// <summary>Release discovery outside GitHub; installation remains shared and provider-independent.</summary>
public sealed class ViewerRemoteReleaseSource(HttpClient client)
{
    private sealed record Asset(string Name, string Url, string? Digest);
    private sealed record Metadata(byte[] Bytes, bool? HasNext);
    private readonly ConcurrentDictionary<string, DateTimeOffset> _retryAt = new(StringComparer.OrdinalIgnoreCase);
    private const int MaximumCatalogSize = 2 * 1024 * 1024;

    public async Task<ViewerRelease?> CheckAsync(ViewerUpdateSource source, Version current, string architecture,
        bool includePrereleases, string? currentTag, bool selfContained, CancellationToken token)
    {
        var numeric = new Version(current.Major, current.Minor, Math.Max(0, current.Build));
        var installed = ViewerVersion.TryParse(currentTag, out var parsed) && parsed.Numeric == numeric ? parsed : ViewerVersion.FromNumeric(current);
        try
        {
            if (source.Kind == ViewerUpdateSourceKind.Manifest)
                return await CheckManifestAsync(source, installed, architecture, selfContained, includePrereleases, token).ConfigureAwait(false);
            var candidates = new List<ViewerRelease>();
            var pageSize = source.Kind == ViewerUpdateSourceKind.GitLab ? 100 : 50;
            for (var page = 1; page <= 100; page++)
            {
                var query = source.Kind == ViewerUpdateSourceKind.GitLab ? $"?per_page={pageSize}&page={page}" : $"?limit={pageSize}&page={page}";
                var response = await FetchAsync(new Uri(source.Endpoint.AbsoluteUri.TrimEnd('/') + "/releases" + query), MaximumCatalogSize, token).ConfigureAwait(false);
                using var json = JsonDocument.Parse(response.Bytes);
                if (json.RootElement.ValueKind != JsonValueKind.Array) throw new InvalidDataException("Expected a release list.");
                foreach (var item in json.RootElement.EnumerateArray())
                {
                    var candidate = await SelectNativeAsync(source, item, installed, architecture, selfContained, includePrereleases, token).ConfigureAwait(false);
                    if (candidate != null) candidates.Add(candidate);
                }
                if (response.HasNext == false || (response.HasNext == null && json.RootElement.GetArrayLength() < pageSize)) break;
            }
            return Newest(candidates);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound) { return null; }
    }

    private static ViewerRelease? Newest(IEnumerable<ViewerRelease> candidates) => candidates
        .OrderByDescending(r => ViewerVersion.TryParse(r.Tag, out var version) ? version : ViewerVersion.FromNumeric(r.Version))
        .ThenBy(r => r.IsPrerelease).FirstOrDefault();
    private static string? Text(JsonElement item, string name) => item.ValueKind == JsonValueKind.Object && item.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String ? property.GetString() : null;
    private static bool Flag(JsonElement item, string name) => item.ValueKind == JsonValueKind.Object && item.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.True;
    private static bool Eligible(string? tag, bool draft, bool prerelease, ViewerVersion installed, bool includePrereleases)
    {
        if (draft || tag == null || tag.Contains('/') || tag.Contains('\\') || !ViewerVersion.TryParse(tag, out var version)) return false;
        return version.CompareTo(installed) > 0 && (includePrereleases || (!prerelease && !tag.Split('+')[0].Contains('-')));
    }

    private async Task<ViewerRelease?> SelectNativeAsync(ViewerUpdateSource source, JsonElement release, ViewerVersion installed,
        string architecture, bool selfContained, bool includePrereleases, CancellationToken token)
    {
        var tag = Text(release, "tag_name");
        var future = Flag(release, "upcoming_release") || (DateTimeOffset.TryParse(Text(release, "released_at"), CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal, out var releasedAt) && releasedAt > DateTimeOffset.UtcNow);
        var prerelease = Flag(release, "prerelease");
        if (!Eligible(tag, Flag(release, "draft") || future, prerelease, installed, includePrereleases)) return null;
        if (!release.TryGetProperty("assets", out var assetsElement)) return null;
        JsonElement entries;
        if (source.Kind == ViewerUpdateSourceKind.GitLab)
        {
            if (assetsElement.ValueKind != JsonValueKind.Object || !assetsElement.TryGetProperty("links", out entries)) return null;
        }
        else entries = assetsElement;
        if (entries.ValueKind != JsonValueKind.Array) throw new InvalidDataException("Invalid release asset list.");
        var assets = new List<Asset>();
        foreach (var entry in entries.EnumerateArray())
        {
            var name = Text(entry, "name");
            var address = source.Kind == ViewerUpdateSourceKind.GitLab ? Text(entry, "direct_asset_url") ?? Text(entry, "url") : Text(entry, "browser_download_url");
            if (name == null || string.IsNullOrEmpty(address)) continue;
            assets.Add(new(name, address, Text(entry, "digest")));
        }
        var descriptors = assets.Where(a => a.Name == ViewerUpdateMetadata.ReleaseManifestName).ToArray();
        if (descriptors.Length == 0) return null;
        if (descriptors.Length != 1) throw new InvalidDataException("Duplicate release manifests.");
        var descriptor = descriptors[0];
        var descriptorUrl = ViewerUpdateSource.Https(descriptor.Url);
        source.ValidateDownload(descriptorUrl);
        Metadata metadata;
        try { metadata = await FetchAsync(descriptorUrl, ViewerUpdateMetadata.MaximumManifestSize, token).ConfigureAwait(false); }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound) { return null; }
        if (descriptor.Digest?.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) == true)
            Verify(metadata.Bytes, ViewerUpdateService.ExpectedHash(descriptor.Digest));
        var manifest = ViewerUpdateMetadata.ReadRelease(metadata.Bytes, tag!);
        if (manifest.Draft || (!includePrereleases && manifest.Prerelease)) return null;
        var package = manifest.Packages.SingleOrDefault(p => p.Architecture == architecture && p.SelfContained == selfContained);
        if (package == null) return null;
        var hash = PackageHash(package);
        Uri url;
        if (package.Url != null) url = new Uri(descriptorUrl, package.Url);
        else
        {
            var matches = assets.Where(a => a.Name == package.AssetName).ToArray();
            if (matches.Length == 0) return null;
            if (matches.Length != 1) throw new InvalidDataException("Duplicate update ZIP assets.");
            url = ViewerUpdateSource.Https(matches[0].Url);
            if (matches[0].Digest?.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) == true &&
                !ViewerUpdateService.ExpectedHash(matches[0].Digest).Equals(hash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Release ZIP digest does not match manifest.");
        }
        source.ValidateDownload(url);
        ViewerVersion.TryParse(tag, out var version);
        return new(version.Numeric, package.AssetName, url, "sha256:" + hash) {
            Tag = tag!, IsPrerelease = prerelease || manifest.Prerelease || tag!.Split('+')[0].Contains('-'),
            Architecture = architecture, SourceSpec = source.Value, SourceRepository = source.DisplayName
        };
    }

    private async Task<ViewerRelease?> CheckManifestAsync(ViewerUpdateSource source, ViewerVersion installed, string architecture,
        bool selfContained, bool includePrereleases, CancellationToken token)
    {
        var response = await FetchAsync(source.Endpoint, MaximumCatalogSize, token).ConfigureAwait(false);
        using var json = JsonDocument.Parse(response.Bytes);
        if (json.RootElement.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Invalid update catalog.");
        var manifests = new List<ViewerReleaseManifest>();
        if (json.RootElement.TryGetProperty("Releases", out var releases))
        {
            if (!json.RootElement.TryGetProperty("SchemaVersion", out var schema) || schema.ValueKind != JsonValueKind.Number || !schema.TryGetInt32(out var schemaVersion) || schemaVersion != 1 ||
                Text(json.RootElement, "ApplicationId") != ViewerPackageLayout.ApplicationId || releases.ValueKind != JsonValueKind.Array || releases.GetArrayLength() > 1000)
                throw new InvalidDataException("Update catalog identity or schema mismatch.");
            foreach (var release in releases.EnumerateArray()) manifests.Add(ReadManifest(release));
        }
        else manifests.Add(ReadManifest(json.RootElement));
        var candidates = new List<ViewerRelease>();
        foreach (var manifest in manifests)
        {
            if (!Eligible(manifest.Version, manifest.Draft, manifest.Prerelease, installed, includePrereleases)) continue;
            var package = manifest.Packages.SingleOrDefault(p => p.Architecture == architecture && p.SelfContained == selfContained);
            if (package == null) continue;
            var hash = PackageHash(package);
            var url = new Uri(source.Endpoint, package.Url ?? package.AssetName);
            source.ValidateDownload(url);
            ViewerVersion.TryParse(manifest.Version, out var version);
            candidates.Add(new(version.Numeric, package.AssetName, url, "sha256:" + hash) {
                Tag = manifest.Version, IsPrerelease = manifest.Prerelease || manifest.Version.Split('+')[0].Contains('-'),
                Architecture = architecture, SourceSpec = source.Value, SourceRepository = source.DisplayName
            });
        }
        return Newest(candidates);
    }
    private static ViewerReleaseManifest ReadManifest(JsonElement value) => ViewerUpdateMetadata.ReadRelease(
        System.Text.Encoding.UTF8.GetBytes(value.GetRawText()), Text(value, "Version") ?? "");
    private static string PackageHash(ViewerReleasePackage package) => ViewerUpdateService.ExpectedHash(package.Sha256 == null ? null : "sha256:" + package.Sha256);
    private static void Verify(byte[] bytes, string hash)
    {
        if (!Convert.ToHexString(SHA256.HashData(bytes)).Equals(hash, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Release manifest SHA-256 mismatch.");
    }

    private async Task<Metadata> FetchAsync(Uri url, int maximumBytes, CancellationToken token)
    {
        _ = ViewerUpdateSource.Https(url.AbsoluteUri);
        var origin = url.GetLeftPart(UriPartial.Authority);
        if (_retryAt.TryGetValue(origin, out var retryAt) && retryAt > DateTimeOffset.UtcNow)
            throw new HttpRequestException("Update source rate limit; retry after " + retryAt.ToString("O"), null, HttpStatusCode.TooManyRequests);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd("NxFileViewer-Updates/1.0");
        request.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.TooManyRequests || response.StatusCode == HttpStatusCode.Forbidden && response.Headers.RetryAfter != null)
            _retryAt[origin] = response.Headers.RetryAfter?.Date ?? DateTimeOffset.UtcNow.Add(response.Headers.RetryAfter?.Delta ?? TimeSpan.FromMinutes(1));
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > maximumBytes) throw new InvalidDataException("Update metadata exceeds size limit.");
        await using var input = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
        using var bytes = new MemoryStream();
        var buffer = new byte[8192];
        int count;
        while ((count = await input.ReadAsync(buffer, timeout.Token).ConfigureAwait(false)) > 0)
        {
            if (bytes.Length + count > maximumBytes) throw new InvalidDataException("Update metadata exceeds size limit.");
            bytes.Write(buffer, 0, count);
        }
        bool? hasNext = null;
        if (response.Headers.TryGetValues("X-Next-Page", out var pages)) hasNext = pages.Any(p => int.TryParse(p, out var number) && number > 0);
        else if (response.Headers.TryGetValues("Link", out var links)) hasNext = links.Any(link => link.Contains("rel=\"next\"", StringComparison.OrdinalIgnoreCase) || link.Contains("rel=next", StringComparison.OrdinalIgnoreCase));
        return new(bytes.ToArray(), hasNext);
    }
}
