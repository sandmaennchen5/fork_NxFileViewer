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
    public static async Task<ViewerRelease> ResolveAsync(HttpClient client, string architecture, bool bundled, CancellationToken token)
    {
        using var response = await client.GetAsync($"https://api.github.com/repos/{ViewerUpdateService.Repository}/releases/latest", token);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        var release = ViewerUpdateService.SelectRelease(json.RootElement, new Version(0, 0, 0), architecture, includePrereleases: false, selfContained: bundled)
            ?? throw new InvalidDataException("Transition package unavailable.");
        _ = ViewerUpdateService.ExpectedHash(release.Digest);
        return release;
    }
}