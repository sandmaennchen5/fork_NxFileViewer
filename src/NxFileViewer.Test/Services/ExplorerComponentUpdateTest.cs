using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Emignatik.NxFileViewer.Services.FileAssociations;
using Emignatik.NxFileViewer.Services.Nand;
using Emignatik.NxFileViewer.Services.Nsz;
using Emignatik.NxFileViewer.Services.Updates;
using Emignatik.NxFileViewer.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services;

public sealed class ExplorerComponentUpdateTest
{
    private sealed class Handler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new StringContent("{\"sha\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"}") });
    }
    [Theory]
    [InlineData(null, "1.0.4.0", false, false)]
    [InlineData("1.0.3.0", "1.0.4.0", true, true)]
    [InlineData("1.0.4.0", "1.0.4.0", true, false)]
    [InlineData("1.0.5.0", "1.0.4.0", true, false)]
    public async Task ChecksOnlyInstalledPackagesAndShowsNewerVersionsOnStart(
        string? installed, string latest, bool included, bool available)
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        using var client = new HttpClient(new Handler());
        var settings = new AppSettings { EnablePlugins = false, EnablePackageOperations = false,
            EnableFirmwareCheck = false, TitleDbRegion = "US.en", IncludeViewerPrereleases = true };
        settings.InitializeFeatures();
        var releaseChecks = 0;
        var checker = new ComponentUpdateChecker(
            new NszPluginManager(settings, NullLogger<NszPluginManager>.Instance, Path.Combine(root, "nsz")),
            new NandPluginManager(settings, Path.Combine(root, "nand")), settings, client, root,
            _ => Task.FromResult(installed == null ? null : Version.Parse(installed)),
            (architecture, previews, token) => {
                releaseChecks++;
                Assert.True(previews);
                Assert.Equal(ExplorerIntegration.Architecture, architecture);
                return Task.FromResult<ExplorerPackageRelease?>(new(Version.Parse(latest), architecture,
                    "fixture_setup.zip", new Uri("https://example.org/fixture_setup.zip"), null));
            });
        var results = await checker.CheckResultsAsync(TestContext.Current.CancellationToken);
        var explorer = Array.Find(results, r => r.Name == "Explorer MSIX");
        Assert.Equal(included, explorer != null);
        Assert.Equal(installed == null ? 0 : 1, releaseChecks);
        if (explorer != null) Assert.Equal(available, explorer.UpdateAvailable);
        var summary = UpdateAvailabilitySummary.Format(null, results);
        Assert.Equal(available, summary.Contains("Explorer MSIX"));
        if (available) Assert.Contains(latest, summary);
        Assert.False(Directory.Exists(root));
    }
    [Fact]
    public async Task ExplorerFailureDoesNotHideOtherComponentResults()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        using var client = new HttpClient(new Handler());
        var settings = new AppSettings { TitleDbRegion = "US.en", EnableFirmwareCheck = false };
        settings.InitializeFeatures();
        var checker = new ComponentUpdateChecker(
            new NszPluginManager(settings, NullLogger<NszPluginManager>.Instance, Path.Combine(root, "nsz")),
            new NandPluginManager(settings, Path.Combine(root, "nand")), settings, client, root,
            _ => Task.FromResult<Version?>(new Version(1, 0, 3, 0)),
            (_, _, _) => throw new HttpRequestException("offline"));
        var results = await checker.CheckResultsAsync(TestContext.Current.CancellationToken);
        Assert.Contains(results, r => r.Name == "Title DB US.en" && r.UpdateAvailable);
        Assert.Contains(results, r => r.Name == "Explorer MSIX" && !r.UpdateAvailable);
    }
}