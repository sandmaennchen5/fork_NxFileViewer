using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Emignatik.NxFileViewer.Services.Updates;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services.Updates;

public sealed class ViewerArchitectureUpdateTest : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ViewerArchitecture-" + Guid.NewGuid().ToString("N"));
    public ViewerArchitectureUpdateTest()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, ViewerUpdateMetadata.StateName), JsonSerializer.Serialize(
            new ViewerUpdateState(1, ViewerPackageLayout.ApplicationId, new[] { "manifest:https://updates.example/index.json" }, Array.Empty<string>())));
    }
    private sealed class Handler(Func<int, HttpResponseMessage> response) : HttpMessageHandler
    {
        public int Requests;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Assert.Equal("https://updates.example/index.json", request.RequestUri!.AbsoluteUri); // Discovery never downloads a ZIP.
            return Task.FromResult(response(++Requests));
        }
    }
    private static ViewerReleaseManifest Manifest(string version, bool bundled, params string[] architectures) =>
        new(1, ViewerPackageLayout.ApplicationId, version, architectures.Select(architecture =>
            new ViewerReleasePackage(architecture, bundled, $"Viewer-{version}-{architecture}.zip") { Sha256 = new string('a', 64) }).ToArray());
    private static HttpResponseMessage Catalog(params ViewerReleaseManifest[] releases) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(new { SchemaVersion = 1, ApplicationId = ViewerPackageLayout.ApplicationId, Releases = releases }))
    };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task X86OnX64DetectsX64OnlyReleaseWithoutChangingDistribution(bool bundled)
    {
        using var handler = new Handler(_ => Catalog(Manifest("5.0.0", bundled, "x64")));
        using var client = new HttpClient(handler);
        var result = await new ViewerUpdateService(client, _root).CheckInstalledAsync(new Version(4, 1, 0), "x86", "x64", TestContext.Current.CancellationToken, selfContained: bundled);
        Assert.True(result.RequiresX64Switch);
        Assert.Equal("x64", result.InstallableRelease!.Architecture);
        Assert.Equal(result.InstallableRelease, result.X64OnlyRelease);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task X86WindowsGetsAnInformationalReleaseButCannotInstallX64(bool bundled)
    {
        using var client = new HttpClient(new Handler(_ => Catalog(Manifest("5.0.0", bundled, "x64"))));
        var result = await new ViewerUpdateService(client, _root).CheckInstalledAsync(new Version(4, 1, 0), "x86", "x86", TestContext.Current.CancellationToken, selfContained: bundled);
        Assert.Null(result.InstallableRelease);
        Assert.NotNull(result.X64OnlyRelease);
        Assert.False(result.RequiresX64Switch);
    }

    [Theory]
    [InlineData("4.2.0", "4.2.0", "x86", false)]
    [InlineData("4.3.0", "4.2.0", "x86", false)]
    [InlineData("4.2.0", "5.0.0", "x64", true)]
    public async Task OnlySwitchesWhenX64IsNewerThanTheBestX86Update(string nativeVersion, string x64Version, string expected, bool switches)
    {
        using var client = new HttpClient(new Handler(_ => Catalog(Manifest(nativeVersion, false, "x86"), Manifest(x64Version, false, "x64"))));
        var result = await new ViewerUpdateService(client, _root).CheckInstalledAsync(new Version(4, 1, 0), "x86", "x64", TestContext.Current.CancellationToken);
        Assert.Equal(expected, result.InstallableRelease!.Architecture);
        Assert.Equal(switches, result.RequiresX64Switch);
        Assert.Equal(switches, result.X64OnlyRelease != null);
    }

    [Fact]
    public async Task X86WindowsRetainsAnOlderCompatibleUpdateAlongsideTheX64Notice()
    {
        using var client = new HttpClient(new Handler(_ => Catalog(Manifest("4.2.0", false, "x86"), Manifest("5.0.0", false, "x64"))));
        var result = await new ViewerUpdateService(client, _root).CheckInstalledAsync(new Version(4, 1, 0), "x86", "x86", TestContext.Current.CancellationToken);
        Assert.Equal("x86", result.InstallableRelease!.Architecture);
        Assert.Equal("5.0.0", result.X64OnlyRelease!.Tag);
        Assert.False(result.RequiresX64Switch);
    }

    [Theory]
    [InlineData(false, "x86")]
    [InlineData(true, "x64")]
    public async Task ArchitectureFallbackRespectsThePrereleaseSetting(bool previews, string expected)
    {
        using var client = new HttpClient(new Handler(_ => Catalog(Manifest("4.2.0", false, "x86"), Manifest("5.0.0-beta1", false, "x64"))));
        var result = await new ViewerUpdateService(client, _root).CheckInstalledAsync(new Version(4, 1, 0), "x86", "x64", TestContext.Current.CancellationToken, includePrereleases: previews);
        Assert.Equal(expected, result.InstallableRelease!.Architecture);
    }

    [Fact]
    public async Task NeverSilentlySwitchesTheDotNetDistribution()
    {
        using var client = new HttpClient(new Handler(_ => Catalog(Manifest("5.0.0", true, "x64"))));
        var result = await new ViewerUpdateService(client, _root).CheckInstalledAsync(new Version(4, 1, 0), "x86", "x64", TestContext.Current.CancellationToken, selfContained: false);
        Assert.Null(result.InstallableRelease); Assert.Null(result.X64OnlyRelease);
    }

    [Theory]
    [InlineData("x64", "x64")]
    [InlineData("arm64", "arm64")]
    [InlineData("x86", "arm64")]
    public async Task OtherArchitecturesKeepTheirExistingDiscovery(string process, string os)
    {
        using var handler = new Handler(_ => Catalog(Manifest("5.0.0", false, process)));
        using var client = new HttpClient(handler);
        var result = await new ViewerUpdateService(client, _root).CheckInstalledAsync(new Version(4, 1, 0), process, os, TestContext.Current.CancellationToken);
        Assert.Equal(process, result.InstallableRelease!.Architecture);
        Assert.Null(result.X64OnlyRelease); Assert.False(result.RequiresX64Switch); Assert.Equal(1, handler.Requests);
    }

    [Fact]
    public async Task SecondaryFailureRetainsTheCompatibleUpdateAndReportsTheFailure()
    {
        using var client = new HttpClient(new Handler(n => n == 1 ? Catalog(Manifest("4.2.0", false, "x86")) : new(HttpStatusCode.ServiceUnavailable)));
        var service = new ViewerUpdateService(client, _root);
        var result = await service.CheckInstalledAsync(new Version(4, 1, 0), "x86", "x64", TestContext.Current.CancellationToken);
        Assert.Equal("x86", result.InstallableRelease!.Architecture);
        Assert.Null(result.X64OnlyRelease); Assert.Contains("x64", service.LastCheckNotice);
    }

    [Fact]
    public async Task ExplicitX86ChecksRemainStrictForInstallersAndOtherCallers()
    {
        using var client = new HttpClient(new Handler(_ => Catalog(Manifest("5.0.0", false, "x64"))));
        Assert.Null(await new ViewerUpdateService(client, _root).CheckAsync(new Version(4, 1, 0), "x86", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SameNumericVersionStillRecognizesTheStableSuccessorOfAPreview()
    {
        using var client = new HttpClient(new Handler(_ => Catalog(Manifest("4.2.0-beta2", false, "x86"), Manifest("4.2.0", false, "x64"))));
        var result = await new ViewerUpdateService(client, _root).CheckInstalledAsync(new Version(4, 2, 0), "x86", "x64",
            TestContext.Current.CancellationToken, includePrereleases: true, currentTag: "4.2.0-beta1");
        Assert.Equal("4.2.0", result.InstallableRelease!.Tag);
        Assert.True(result.RequiresX64Switch);
    }

    [Fact]
    public async Task CancellationDuringTheAdditionalCheckIsNotReportedAsUpToDate()
    {
        using var cancellation = new CancellationTokenSource();
        using var client = new HttpClient(new Handler(n =>
        {
            if (n == 1) return Catalog(Manifest("4.2.0", false, "x86"));
            cancellation.Cancel();
            throw new OperationCanceledException(cancellation.Token);
        }));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ViewerUpdateService(client, _root)
            .CheckInstalledAsync(new Version(4, 1, 0), "x86", "x64", cancellation.Token));
    }

    public void Dispose() => Directory.Delete(_root, true);
}
