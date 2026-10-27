using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Emignatik.NxFileViewer.Services.Updates;
using Emignatik.NxFileViewer.Services;
using global::NxFileViewer.Transition;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services.Updates;

public sealed class TransitionReleaseTest
{
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => Task.FromResult(response(request));
    }
    private static object Asset(string prefix, string suffix, string? digest = null) => new {
        name = prefix + "_v4.1.0_" + suffix + ".zip",
        browser_download_url = $"https://github.com/{ViewerUpdateService.Repository}/releases/download/v4.1.0/{prefix}_v4.1.0_{suffix}.zip",
        digest = digest ?? "sha256:" + new string('a', 64)
    };
    private static HttpClient Client(object[] assets, string tag = "v4.1.0") => new(new Handler(request => {
        var latest = "https://api.github.com/repos/" + ViewerUpdateService.Repository + "/releases/latest";
        var release = new { tag_name = tag, draft = false, prerelease = false, assets };
        string body;
        if (request.RequestUri!.AbsoluteUri == latest) body = JsonSerializer.Serialize(release);
        else
        {
            Assert.Equal("https://api.github.com/repos/" + ViewerUpdateService.Repository + "/releases?per_page=100&page=1", request.RequestUri.AbsoluteUri);
            body = JsonSerializer.Serialize(new[] { release });
        }
        return new(HttpStatusCode.OK) { Content = new StringContent(body) };
    }));
    [Theory]
    [InlineData("x64", false)][InlineData("x86", false)][InlineData("x64", true)][InlineData("x86", true)]
    public async Task LatestStableReleaseChoosesNewNameAndPreservesArchitectureAndRuntime(string architecture, bool bundled)
    {
        var suffixes = new[] { "x64", "x86", "self-contained_x64", "self-contained_x86" };
        using var client = Client(suffixes.SelectMany(s => new[] { Asset("NxFileViewer", s), Asset("NxFileViewerFork", s) }).ToArray());
        var release = await TransitionRelease.ResolveAsync(client, architecture, bundled, TestContext.Current.CancellationToken);
        Assert.Equal("NxFileViewerFork_v4.1.0_" + (bundled ? "self-contained_" : "") + architecture + ".zip", release.AssetName);
    }
    [Fact]
    public async Task MissingNewPackageNeverSelectsTheTransitionAgain()
    {
        using var client = Client(new[] { Asset("NxFileViewer", "x64") });
        await Assert.ThrowsAsync<InvalidDataException>(() => TransitionRelease.ResolveAsync(client, "x64", false, TestContext.Current.CancellationToken));
    }
    [Fact]
    public async Task LaterReleaseIsSelectedWithoutRebuildingTheDownloadLogic()
    {
        using var client = new HttpClient(new Handler(request => {
            Assert.EndsWith("/releases/latest", request.RequestUri!.AbsoluteUri);
            var body = JsonSerializer.Serialize(new { tag_name = "v4.8.2", draft = false, prerelease = false,
                assets = new[] { new { name = "NxFileViewerFork_v4.8.2_x64.zip",
                    browser_download_url = "https://github.com/" + ViewerUpdateService.Repository + "/releases/download/v4.8.2/NxFileViewerFork_v4.8.2_x64.zip",
                    digest = "sha256:" + new string('a', 64) } } });
            return new(HttpStatusCode.OK) { Content = new StringContent(body) };
        }));
        var release = await TransitionRelease.ResolveAsync(client, "x64", false, TestContext.Current.CancellationToken);
        Assert.Equal("v4.8.2", release.Tag);
        Assert.Equal(new Version(4, 8, 2), release.Version);
    }
    [Fact]
    public async Task MissingDigestIsRejected()
    {
        using var invalid = Client(new[] { Asset("NxFileViewerFork", "x64", "invalid") });
        await Assert.ThrowsAsync<InvalidDataException>(() => TransitionRelease.ResolveAsync(invalid, "x64", false, TestContext.Current.CancellationToken));
    }
    [Theory]
    [InlineData(true, false)][InlineData(false, true)]
    public async Task DraftsAndPreviewsAreNotInstalled(bool draft, bool prerelease)
    {
        using var client = new HttpClient(new Handler(_ => new(HttpStatusCode.OK) {
            Content = new StringContent(JsonSerializer.Serialize(new { tag_name = "v4.1.0", draft, prerelease,
                assets = new[] { Asset("NxFileViewerFork", "x64") } }))
        }));
        await Assert.ThrowsAsync<InvalidDataException>(() => TransitionRelease.ResolveAsync(client, "x64", false, TestContext.Current.CancellationToken));
    }
    [Theory]
    [InlineData("x64", false)][InlineData("x86", false)]
    [InlineData("x64", true)][InlineData("x86", true)]
    public async Task EnabledPreviewsChooseHighestCompatibleVersion(string architecture, bool bundled)
    {
        object Release(string tag, string suffix, bool draft = false) => new {
            tag_name = tag, draft, prerelease = tag.Contains('-'),
            assets = new[] { new {
                name = $"NxFileViewerFork_{tag}_{suffix}.zip",
                browser_download_url = $"https://github.com/{ViewerUpdateService.Repository}/releases/download/{tag}/NxFileViewerFork_{tag}_{suffix}.zip",
                digest = "sha256:" + new string('a', 64)
            } }
        };
        var suffix = (bundled ? "self-contained_" : "") + architecture;
        using var client = new HttpClient(new Handler(request => {
            Assert.EndsWith("/releases?per_page=100&page=1", request.RequestUri!.AbsoluteUri);
            return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new[] {
                Release("v4.1.0-beta1", suffix), Release("v4.0.0", suffix),
                Release("v4.1.0-beta2", suffix),
                Release("v4.1.0-beta3", bundled ? architecture : "self-contained_" + architecture),
                Release("v4.2.0-beta1", suffix, draft: true)
            })) };
        }));
        var release = await TransitionRelease.ResolveAsync(client, architecture, bundled,
            TestContext.Current.CancellationToken, includePrereleases: true);
        Assert.Equal("v4.1.0-beta2", release.Tag);
        Assert.Equal($"NxFileViewerFork_v4.1.0-beta2_{suffix}.zip", release.AssetName);
        Assert.True(release.IsPrerelease);
    }

    [Theory]
    [InlineData(null, null, false)]
    [InlineData("""{"IncludeViewerPrereleases":true}""", null, true)]
    [InlineData("""{"IncludeViewerPrereleases":false}""", null, false)]
    [InlineData("""{"IncludeViewerPrereleases":true}""", """{}""", false)]
    [InlineData("""{"IncludeViewerPrereleases":true}""", """{"IncludeViewerPrereleases":false}""", false)]
    [InlineData("""{"IncludeViewerPrereleases":false}""", """{"IncludeViewerPrereleases":true}""", true)]
    [InlineData("""{"IncludeViewerPrereleases":"true"}""", null, false)]
    public void ReadsExplicitPreferenceBeforeMigration(string? legacy, string? fork, bool expected)
    {
        var root = Path.Combine(Path.GetTempPath(), "nxfv-transition-settings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try {
            if (legacy != null) File.WriteAllText(Path.Combine(root, "NxFileViewer.settings.json"), legacy);
            if (fork != null) File.WriteAllText(Path.Combine(root, "NxFileViewerFork.settings.json"), fork);
            Assert.Equal(expected, TransitionRelease.IncludePrereleases(root));
            Assert.Equal(legacy != null, File.Exists(Path.Combine(root, "NxFileViewer.settings.json")));
        } finally { Directory.Delete(root, true); }
    }
    [Fact]
    public void NormalApplicationUsesNewPathsWithoutMigratingLegacyFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "nxfv-paths-" + Guid.NewGuid().ToString("N"));
        try {
            Directory.CreateDirectory(Path.Combine(root, "Cache", "TitleDB"));
            File.WriteAllText(Path.Combine(root, "Cache", "TitleDB", "old.json"), "old");
            Assert.Equal(Path.Combine(root, "Plugins", "TitleDB"), ApplicationDataPaths.TitleDb(root));
            Assert.False(Directory.Exists(Path.Combine(root, "Plugins")));
            Assert.True(File.Exists(Path.Combine(root, "Cache", "TitleDB", "old.json")));
        } finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}