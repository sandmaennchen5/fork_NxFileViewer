using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Emignatik.NxFileViewer.Services.Updates;

namespace Emignatik.NxFileViewer.Services;

internal static class TitleDbSources
{
    private static readonly Lazy<IReadOnlyList<GitHubContentLocation>> Locations = new(() =>
    {
        using var stream = typeof(TitleDbSources).Assembly.GetManifestResourceStream("Viewer.ExternalSources.json")
            ?? throw new InvalidDataException("Embedded external source configuration missing.");
        using var config = JsonDocument.Parse(stream);
        var defaultSource = config.RootElement.GetProperty("TitleDb").Deserialize<GitHubContentLocation>()!;
        var alternatives = config.RootElement.TryGetProperty("TitleDbAlternatives", out var entries)
            ? entries.Deserialize<GitHubContentLocation[]>() ?? Array.Empty<GitHubContentLocation>()
            : Array.Empty<GitHubContentLocation>();
        return alternatives.Prepend(defaultSource).ToArray();
    });

    internal static IReadOnlyList<GitHubContentLocation> All => Locations.Value;

    internal static GitHubContentLocation Resolve(string? repository) =>
        All.FirstOrDefault(source => string.Equals(source.Repository, repository, StringComparison.OrdinalIgnoreCase)) ?? All[0];

    internal static string CatalogPath(string directory, GitHubContentLocation source, string region) =>
        string.Equals(source.Repository, "blawar/titledb", StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(directory, region + ".json")
            : Path.Combine(directory, source.Repository, region + ".json");

    private static string DateUrl(GitHubContentLocation source, string region) =>
        "https://api.github.com/repos/" + source.Repository + "/commits?per_page=1&path=" +
        Uri.EscapeDataString((source.Path.Length == 0 ? "" : source.Path + "/") + region + ".json") +
        (source.Ref == "HEAD" ? "" : "&sha=" + Uri.EscapeDataString(source.Ref));

    internal static string Label(GitHubContentLocation source, DateTimeOffset? date)
    {
        var owner = source.Repository.Split('/')[0];
        var name = All.Count(candidate => candidate.Repository.StartsWith(owner + "/", StringComparison.OrdinalIgnoreCase)) > 1
            ? source.Repository : owner;
        return date is { } value ? name + " (" + value.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ")" : name;
    }

    internal static DateTimeOffset? CachedDate(GitHubContentLocation source, string region) =>
        ReadDate(GitHubUpdateRequests.For(null).ReadCachedBody(DateUrl(source, region)));

    internal static async Task<DateTimeOffset?> UpdateDateAsync(GitHubContentLocation source, string region, CancellationToken token = default)
    {
        var requests = GitHubUpdateRequests.For(null);
        var url = DateUrl(source, region);
        try
        {
            var result = await requests.GetAsync(url, automatic: true, token);
            return ReadDate(result.Body);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException || ex is OperationCanceledException && !token.IsCancellationRequested)
        {
            return ReadDate(requests.ReadCachedBody(url));
        }
    }

    private static DateTimeOffset? ReadDate(string? json)
    {
        if (json == null) return null;
        try
        {
            using var document = JsonDocument.Parse(json);
            var commits = document.RootElement;
            return commits.GetArrayLength() > 0 &&
                commits[0].GetProperty("commit").GetProperty("committer").GetProperty("date").TryGetDateTimeOffset(out var date)
                ? date : null;
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }
}
