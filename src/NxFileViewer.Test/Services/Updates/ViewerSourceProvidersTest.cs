using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Emignatik.NxFileViewer.Services.Updates;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services.Updates;

public sealed class ViewerSourceProvidersTest : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ViewerProviders-" + Guid.NewGuid().ToString("N"));
    private static readonly string Hash = new('1', 64);
    public ViewerSourceProvidersTest() => Directory.CreateDirectory(_root);
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => Task.FromResult(respond(request));
    }
    private static HttpResponseMessage Json(object value) => new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(value)) };
    private void Sources(params string[] sources) => File.WriteAllText(Path.Combine(_root, ViewerUpdateMetadata.StateName),
        JsonSerializer.Serialize(new ViewerUpdateState(1, ViewerPackageLayout.ApplicationId, sources, Array.Empty<string>())));
    private static ViewerReleaseManifest Manifest(string version = "4.2.0", string architecture = "x64", bool bundled = false, string? hash = null, string? url = null) =>
        new(1, ViewerPackageLayout.ApplicationId, version, new[] {
            new ViewerReleasePackage(architecture, bundled, "CustomProduct.zip") { Sha256 = hash ?? Hash, Url = url }
        });
    private static object NativeRelease(bool gitlab, string origin, string tag = "v4.2.0", bool draft = false, bool upcoming = false) => new {
        tag_name = tag, draft, prerelease = tag.Contains('-'), upcoming_release = upcoming,
        assets = gitlab ? (object)new { links = new[] {
            new { name = ViewerUpdateMetadata.ReleaseManifestName, direct_asset_url = origin + "/files/" + ViewerUpdateMetadata.ReleaseManifestName, url = "https://untrusted.example/wrong.json" },
            new { name = "CustomProduct.zip", direct_asset_url = origin + "/files/CustomProduct.zip", url = "https://untrusted.example/wrong.zip" },
            new { name = "Documentation", direct_asset_url = "http://docs.example/manual", url = "http://docs.example/manual" }
        } } : new[] {
            new { name = ViewerUpdateMetadata.ReleaseManifestName, browser_download_url = origin + "/files/" + ViewerUpdateMetadata.ReleaseManifestName },
            new { name = "CustomProduct.zip", browser_download_url = origin + "/files/CustomProduct.zip" },
            new { name = "Documentation", browser_download_url = "http://docs.example/manual" }
        }
    };

    [Theory]
    [InlineData("github:owner/repo", "GitHub", "https://api.github.com/repos/owner/repo")]
    [InlineData("owner/repo", "GitHub", "https://api.github.com/repos/owner/repo")]
    [InlineData("gitlab:https://gitlab.example/team/subgroup/app", "GitLab", "https://gitlab.example/api/v4/projects/team%2Fsubgroup%2Fapp")]
    [InlineData("gitlab:https://gitlab.example/base/api/v4/projects/24", "GitLab", "https://gitlab.example/base/api/v4/projects/24")]
    [InlineData("forgejo:https://forgejo.example/base/team/app.git", "Forgejo", "https://forgejo.example/base/api/v1/repos/team/app")]
    [InlineData("forgejo:https://forgejo.example/api/v1/repos/team/app", "Forgejo", "https://forgejo.example/api/v1/repos/team/app")]
    [InlineData("manifest:https://updates.example/App/index.json", "Manifest", "https://updates.example/App/index.json")]
    [InlineData("https://updates.example/App/index.json", "Manifest", "https://updates.example/App/index.json")]
    public void ParsesHostedAndIndependentSources(string value, string kind, string endpoint)
    {
        var source = ViewerUpdateSource.Parse(value);
        Assert.Equal(kind, source.Kind.ToString()); Assert.Equal(endpoint, source.Endpoint.AbsoluteUri);
    }

    [Theory]
    [InlineData("gitlab:http://gitlab.example/team/app")]
    [InlineData("forgejo:https://user:password@forgejo.example/team/app")]
    [InlineData("manifest:file:///C:/updates.json")]
    [InlineData("manifest:https://updates.example/index.json#fragment")]
    [InlineData("gitlab:team/app")]
    [InlineData("ssh://git.example/team/app")]
    public void RejectsUnsafeOrUnsupportedSourceAddresses(string value) => Assert.Throws<InvalidDataException>(() => ViewerUpdateSource.Parse(value));

    [Theory]
    [InlineData("gitlab:https://gitlab.example/team/app", "x64", false)]
    [InlineData("gitlab:https://gitlab.example/team/app", "x64", true)]
    [InlineData("gitlab:https://gitlab.example/team/app", "x86", false)]
    [InlineData("gitlab:https://gitlab.example/team/app", "x86", true)]
    [InlineData("forgejo:https://forgejo.example/team/app", "x64", false)]
    [InlineData("forgejo:https://forgejo.example/team/app", "x64", true)]
    [InlineData("forgejo:https://forgejo.example/team/app", "x86", false)]
    [InlineData("forgejo:https://forgejo.example/team/app", "x86", true)]
    public async Task NativeProvidersUseDescriptorAndPreserveArchitectureAndRuntime(string value, string architecture, bool bundled)
    {
        Sources(value);
        var source = ViewerUpdateSource.Parse(value);
        bool gitlab = source.Kind == ViewerUpdateSourceKind.GitLab;
        var origin = source.Endpoint.GetLeftPart(UriPartial.Authority);
        using var client = new HttpClient(new Handler(request => {
            if (request.RequestUri!.AbsolutePath.EndsWith(ViewerUpdateMetadata.ReleaseManifestName)) return Json(Manifest(architecture: architecture, bundled: bundled));
            Assert.StartsWith(source.Endpoint.AbsoluteUri + "/releases?", request.RequestUri.AbsoluteUri);
            Assert.Contains(gitlab ? "per_page=100" : "limit=50", request.RequestUri.Query);
            return Json(new[] { NativeRelease(gitlab, origin) });
        }));
        var result = await new ViewerUpdateService(client, _root).CheckAsync(new Version(4, 1, 0), architecture, TestContext.Current.CancellationToken, selfContained: bundled);
        Assert.Equal("CustomProduct.zip", result!.AssetName); Assert.Equal(architecture, result.Architecture);
        Assert.Equal("sha256:" + Hash, result.Digest); Assert.Equal(value, result.SourceSpec);
        Assert.Equal(origin + "/files/CustomProduct.zip", result.DownloadUrl.AbsoluteUri);
        Assert.Single(Directory.GetFileSystemEntries(_root));
    }

    [Theory]
    [InlineData(false, "4.2.0")]
    [InlineData(true, "4.3.0-beta2")]
    public async Task IndependentCatalogChoosesChannelAndIgnoresDraftAndMissingDistribution(bool previews, string expected)
    {
        Sources("manifest:https://updates.example/app/index.json");
        var catalog = new { SchemaVersion = 1, ApplicationId = ViewerPackageLayout.ApplicationId, Releases = new[] {
            Manifest("4.2.0", url: "https://cdn.example/4.2.0.zip"), Manifest("4.3.0-beta2", url: "beta.zip"),
            Manifest("4.8.0") with { Draft = true }, Manifest("4.9.0", architecture: "x86")
        } };
        using var client = new HttpClient(new Handler(_ => Json(catalog)));
        var result = await new ViewerUpdateService(client, _root).CheckAsync(new Version(4, 1, 0), "x64", TestContext.Current.CancellationToken, previews);
        Assert.Equal(expected, result!.Tag); Assert.Equal(previews, result.IsPrerelease);
        Assert.Equal(previews ? "https://updates.example/app/beta.zip" : "https://cdn.example/4.2.0.zip", result.DownloadUrl.AbsoluteUri);
    }

    [Fact]
    public async Task IndependentSourceAcceptsTheGeneratedSingleReleaseDescriptor()
    {
        Sources("https://updates.example/app/nxfileviewer-update.json");
        using var client = new HttpClient(new Handler(_ => Json(Manifest())));
        var result = await new ViewerUpdateService(client, _root).CheckAsync(new Version(4, 1, 0), "x64", TestContext.Current.CancellationToken);
        Assert.Equal("https://updates.example/app/CustomProduct.zip", result!.DownloadUrl.AbsoluteUri);
    }

    [Theory]
    [InlineData("manifest:https://updates.example/index.json")]
    [InlineData("gitlab:https://gitlab.example/team/app")]
    [InlineData("forgejo:https://forgejo.example/team/app")]
    public async Task AllNewProvidersRequirePackageChecksum(string value)
    {
        Sources(value); var source = ViewerUpdateSource.Parse(value);
        var manifest = Manifest() with { Packages = new[] { new ViewerReleasePackage("x64", false, "CustomProduct.zip") } };
        using var client = new HttpClient(new Handler(request => {
            if (source.Kind == ViewerUpdateSourceKind.Manifest || request.RequestUri!.AbsolutePath.EndsWith(ViewerUpdateMetadata.ReleaseManifestName)) return Json(manifest);
            return Json(new[] { NativeRelease(source.Kind == ViewerUpdateSourceKind.GitLab, source.Endpoint.GetLeftPart(UriPartial.Authority)) });
        }));
        await Assert.ThrowsAsync<InvalidDataException>(() => new ViewerUpdateService(client, _root).CheckAsync(new Version(4, 1, 0), "x64", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ForgejoFollowsNextLinkEvenWhenInstanceCapsPageSize()
    {
        Sources("forgejo:https://forgejo.example/team/app"); int pages = 0;
        using var client = new HttpClient(new Handler(request => {
            if (request.RequestUri!.AbsolutePath.EndsWith(ViewerUpdateMetadata.ReleaseManifestName)) return Json(Manifest());
            pages++;
            var response = Json(new[] { NativeRelease(false, "https://forgejo.example", pages == 1 ? "v4.9.0" : "v4.2.0", draft: pages == 1) });
            if (pages == 1) response.Headers.TryAddWithoutValidation("Link", "<https://forgejo.example/api/v1/repos/team/app/releases?page=2>; rel=\"next\"");
            return response;
        }));
        var result = await new ViewerUpdateService(client, _root).CheckAsync(new Version(4, 1, 0), "x64", TestContext.Current.CancellationToken);
        Assert.Equal(2, pages); Assert.Equal("v4.2.0", result!.Tag);
    }

    [Fact]
    public async Task GitLabUpcomingReleaseIsNotDownloaded()
    {
        Sources("gitlab:https://gitlab.example/team/app");
        using var client = new HttpClient(new Handler(request => {
            if (request.RequestUri!.AbsolutePath.EndsWith(ViewerUpdateMetadata.ReleaseManifestName)) return Json(Manifest());
            return Json(new[] { NativeRelease(true, "https://gitlab.example", "v4.9.0", upcoming: true), NativeRelease(true, "https://gitlab.example") });
        }));
        var result = await new ViewerUpdateService(client, _root).CheckAsync(new Version(4, 1, 0), "x64", TestContext.Current.CancellationToken);
        Assert.Equal("v4.2.0", result!.Tag);
    }

    [Fact]
    public async Task RateLimitedForgeDoesNotBlockIndependentMirrorAndCooldownIsRespected()
    {
        Sources("gitlab:https://gitlab.example/team/app", "manifest:https://updates.example/index.json"); int forgeRequests = 0;
        using var client = new HttpClient(new Handler(request => {
            if (request.RequestUri!.Host != "gitlab.example") return Json(Manifest());
            forgeRequests++;
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromHours(1));
            return response;
        }));
        var service = new ViewerUpdateService(client, _root);
        for (int i = 0; i < 2; i++) Assert.NotNull(await service.CheckAsync(new Version(4, 1, 0), "x64", TestContext.Current.CancellationToken));
        Assert.Equal(1, forgeRequests);
    }

    [Fact]
    public async Task IndependentDownloadHashFailureCannotChangeInstalledFiles()
    {
        Sources("manifest:https://updates.example/index.json");
        File.WriteAllText(Path.Combine(_root, "Current.exe"), "old-program");
        using var client = new HttpClient(new Handler(request => request.RequestUri!.AbsolutePath.EndsWith("index.json") ? Json(Manifest()) :
            new(HttpStatusCode.OK) { Content = new StringContent("corrupt ZIP") }));
        var service = new ViewerUpdateService(client, _root);
        var release = await service.CheckAsync(new Version(4, 1, 0), "x64", TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidDataException>(() => service.PrepareAsync(release!, _root, TestContext.Current.CancellationToken));
        Assert.Equal("old-program", File.ReadAllText(Path.Combine(_root, "Current.exe")));
        Assert.Empty(Directory.GetDirectories(Path.Combine(_root, "Updates")));
    }

    [Fact]
    public void NativeOriginsAreRestrictedButIndependentManifestCanNameHttpsCdn()
    {
        Assert.Throws<InvalidDataException>(() => ViewerUpdateSource.Parse("forgejo:https://forgejo.example/team/app").ValidateDownload(new Uri("https://other.example/asset.zip")));
        Assert.Throws<InvalidDataException>(() => ViewerUpdateSource.Parse("manifest:https://updates.example/index.json").ValidateDownload(new Uri("http://cdn.example/asset.zip")));
        ViewerUpdateSource.Parse("manifest:https://updates.example/index.json").ValidateDownload(new Uri("https://cdn.example/asset.zip"));
        Assert.NotEqual(ViewerUpdateSource.Parse("https://updates.example/Upper.json").Key, ViewerUpdateSource.Parse("https://updates.example/upper.json").Key);
    }
    [Fact]
    public async Task FirstStartAfterOlderInstallerLearnsSourcesWithoutRunningLocalMigrations()
    {
        var package = new ViewerPackageManifest(1, ViewerPackageLayout.ApplicationId, "Current.exe") {
            Version = "4.1.0", UpdateSources = new[] { "manifest:https://updates.example/index.json" },
            Migrations = new[] { new ViewerMigration("future-move", "4.0.1", "4.2.0", new[] { new ViewerMigrationAction("MoveDirectory", "OldData", "NewData") }) }
        };
        File.WriteAllBytes(Path.Combine(_root, ViewerPackageLayout.ManifestName), JsonSerializer.SerializeToUtf8Bytes(package));
        Directory.CreateDirectory(Path.Combine(_root, "OldData"));
        File.WriteAllText(Path.Combine(_root, "OldData/a.txt"), "keep");
        using var client = new HttpClient(new Handler(request => {
            Assert.Equal("updates.example", request.RequestUri!.Host);
            return Json(Manifest());
        }));
        var result = await new ViewerUpdateService(client, _root).CheckAsync(new Version(4, 1, 0), "x64", TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal("keep", File.ReadAllText(Path.Combine(_root, "OldData/a.txt")));
        Assert.False(Directory.Exists(Path.Combine(_root, "NewData")));
        Assert.False(File.Exists(Path.Combine(_root, ViewerUpdateMetadata.StateName)));
    }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
