using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Emignatik.NxFileViewer.Services.Updates;

namespace NxFileViewer.Transition;

public static class TransitionRelease
{
    public static bool IncludePrereleases(string root)
    {
        // Match migration precedence: an existing fork configuration wins.
        var path = Path.Combine(root, "NxFileViewerFork.settings.json");
        if (!File.Exists(path)) path = Path.Combine(root, "NxFileViewer.settings.json");
        if (!File.Exists(path)) return false;
        ViewerPackageLayout.NoLinks(path);
        using var settings = JsonDocument.Parse(File.ReadAllText(path));
        return settings.RootElement.ValueKind == JsonValueKind.Object &&
            settings.RootElement.TryGetProperty("IncludeViewerPrereleases", out var value) &&
            value.ValueKind == JsonValueKind.True;
    }

    public static async Task<ViewerRelease> ResolveAsync(HttpClient client, string architecture, bool bundled,
        CancellationToken token, bool includePrereleases = false)
    {
        var release = await new ViewerUpdateService(client).CheckAsync(new Version(0, 0, 0), architecture,
            token, includePrereleases: includePrereleases, selfContained: bundled)
            ?? throw new InvalidDataException("Transition package unavailable.");
        _ = ViewerUpdateService.ExpectedHash(release.Digest);
        return release;
    }
}