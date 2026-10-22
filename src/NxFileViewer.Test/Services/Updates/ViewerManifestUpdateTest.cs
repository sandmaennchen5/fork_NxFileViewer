using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Emignatik.NxFileViewer.Services.Updates;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services.Updates;

public sealed class ViewerManifestUpdateTest : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ViewerManifest-" + Guid.NewGuid().ToString("N"));
    public ViewerManifestUpdateTest() => Directory.CreateDirectory(_root);
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => Task.FromResult(respond(request));
    }
    private static HttpResponseMessage Json(object value) => new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(value)) };
    private static object Asset(string repository, string tag, string name, string? hash = null) => new {
        name, browser_download_url = $"https://github.com/{repository}/releases/download/{tag}/{name}", digest = "sha256:" + (hash ?? new string('1', 64))
    };
    private static object Release(string repository, string tag, params object[] assets) => new { tag_name = tag, draft = false, prerelease = false, assets };

    [Fact]
    public async Task ReleaseManifestSelectsRenamedZipWithoutChangingLocalFiles()
    {
        var repository = ViewerUpdateService.Repository;
        var descriptor = JsonSerializer.SerializeToUtf8Bytes(new ViewerReleaseManifest(1, ViewerPackageLayout.ApplicationId, "4.2.0",
            new[] { new ViewerReleasePackage("x64", false, "RenamedProduct-portable.zip") }));
        var hash = Convert.ToHexString(SHA256.HashData(descriptor));
        var requests = 0;
        using var client = new HttpClient(new Handler(request => {
            requests++;
            if (request.RequestUri!.Host == "github.com") return new(HttpStatusCode.OK) { Content = new ByteArrayContent(descriptor) };
            return Json(Release(repository, "v4.2.0", Asset(repository, "v4.2.0", ViewerUpdateMetadata.ReleaseManifestName, hash), Asset(repository, "v4.2.0", "RenamedProduct-portable.zip")));
        }));
        var result = await new ViewerUpdateService(client, _root).CheckAsync(new Version(4, 1, 0), "x64", TestContext.Current.CancellationToken);
        Assert.Equal("RenamedProduct-portable.zip", result!.AssetName);
        Assert.Equal("x64", result.Architecture);
        Assert.Equal(2, requests);
        Assert.Empty(Directory.GetFileSystemEntries(_root));
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task UsesRememberedRepositoriesAndFallsBackWhenOneIsUnavailable(bool newRepositoryAvailable)
    {
        var newer = "another-owner/renamed-repository";
        var state = JsonSerializer.Serialize(new ViewerUpdateState(1, ViewerPackageLayout.ApplicationId, new[] { newer, ViewerUpdateService.Repository }, Array.Empty<string>()));
        File.WriteAllText(Path.Combine(_root, ViewerUpdateMetadata.StateName), state);
        using var client = new HttpClient(new Handler(request => {
            bool isNew = request.RequestUri!.AbsolutePath.Contains(newer);
            if (isNew != newRepositoryAvailable) return new(HttpStatusCode.ServiceUnavailable);
            var repository = isNew ? newer : ViewerUpdateService.Repository;
            return Json(Release(repository, "v4.2.0", Asset(repository, "v4.2.0", "NxFileViewerFork_v4.2.0_x64.zip")));
        }));
        var result = await new ViewerUpdateService(client, _root).CheckAsync(new Version(4, 1, 0), "x64", TestContext.Current.CancellationToken);
        Assert.Equal(newRepositoryAvailable ? newer : ViewerUpdateService.Repository, result!.SourceRepository);
        Assert.Equal(state, File.ReadAllText(Path.Combine(_root, ViewerUpdateMetadata.StateName)));
    }

    [Fact]
    public async Task StableLatestWithoutCompatiblePackageFallsBackToIntermediateRelease()
    {
        var repository = ViewerUpdateService.Repository;
        using var client = new HttpClient(new Handler(request => request.RequestUri!.AbsolutePath.EndsWith("/latest")
            ? Json(Release(repository, "v4.3.0"))
            : Json(new[] { Release(repository, "v4.3.0"), Release(repository, "v4.2.0", Asset(repository, "v4.2.0", "NxFileViewerFork_v4.2.0_x64.zip")) })));
        var result = await new ViewerUpdateService(client, _root).CheckAsync(new Version(4, 1, 0), "x64", TestContext.Current.CancellationToken);
        Assert.Equal(new Version(4, 2, 0), result!.Version);
    }

    [Fact]
    public async Task RejectsTamperedReleaseManifestWithoutChangingLocalState()
    {
        var repository = ViewerUpdateService.Repository;
        using var client = new HttpClient(new Handler(request => request.RequestUri!.Host == "github.com"
            ? new(HttpStatusCode.OK) { Content = new StringContent("{}") }
            : Json(Release(repository, "v4.2.0", Asset(repository, "v4.2.0", ViewerUpdateMetadata.ReleaseManifestName)))));
        await Assert.ThrowsAsync<InvalidDataException>(() => new ViewerUpdateService(client, _root).CheckAsync(new Version(4, 1, 0), "x64", TestContext.Current.CancellationToken));
        Assert.Empty(Directory.GetFileSystemEntries(_root));
    }

    [Theory]
    [InlineData("../outside")] [InlineData("Updates/a")] [InlineData("prod.keys")]
    [InlineData("NxFileViewerFork.settings.json")] [InlineData("NxFileViewerFork.update-state.json")]
    public void RejectsUnsafeMigrationPaths(string path)
    {
        var manifest = new ViewerPackageManifest(1, ViewerPackageLayout.ApplicationId, "New.exe") {
            Version = "4.2.0", Migrations = new[] { new ViewerMigration("move", "4.0.1", "4.2.0", new[] { new ViewerMigrationAction("MoveDirectory", path, "Data/New") }) }
        };
        Assert.Throws<InvalidDataException>(() => ViewerPackageLayout.ReadManifest(JsonSerializer.SerializeToUtf8Bytes(manifest)));
    }

    [Theory]
    [InlineData("https://evil.example/a")] [InlineData("owner/repo/extra")] [InlineData("owner/..")]
    public void RejectsInvalidRepositorySources(string repository) => Assert.Throws<InvalidDataException>(() => ViewerUpdateMetadata.Repository(repository));

    [Fact]
    public async Task ExplicitSourcesCanRetireTheOriginalRepository()
    {
        var repository = "new-owner/retired-old-location";
        File.WriteAllText(Path.Combine(_root, ViewerUpdateMetadata.StateName), JsonSerializer.Serialize(
            new ViewerUpdateState(1, ViewerPackageLayout.ApplicationId, new[] { repository }, Array.Empty<string>())));
        using var client = new HttpClient(new Handler(request => {
            Assert.Contains("/repos/" + repository + "/", request.RequestUri!.AbsolutePath);
            return Json(Release(repository, "v4.2.0", Asset(repository, "v4.2.0", "NxFileViewerFork_v4.2.0_x64.zip")));
        }));
        var result = await new ViewerUpdateService(client, _root).CheckAsync(new Version(4, 1, 0), "x64", TestContext.Current.CancellationToken);
        Assert.Equal(repository, result!.SourceRepository);
    }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
