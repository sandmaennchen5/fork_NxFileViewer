using System;
using Emignatik.NxFileViewer.Services;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Emignatik.NxFileViewer.Services.Nand;
using Emignatik.NxFileViewer.Services.Nsz;
using Emignatik.NxFileViewer.Services.Updates;
using Emignatik.NxFileViewer.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services;

public sealed class ComponentUpdateCheckerTest
{
    [Theory]
    [InlineData("v23.0.1", "23.0.2", true)]
    [InlineData("23.0.2", "v23.0.2", false)]
    [InlineData("23.0.3", "23.0.2", false)]
    public void VersionsCompareNumerically(string installed, string latest, bool expected) =>
        Assert.Equal(expected, ComponentUpdateChecker.IsNewer(installed, latest));

    [Fact]
    public async Task ChecksInstalledPluginsAndDataWithoutChangingFilesAndContinuesAfterFailure()
    {
        var root = Path.Combine(Path.GetTempPath(), "component-update-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var plugin = Path.Combine(root, "nsz");
            Directory.CreateDirectory(plugin);
            File.WriteAllText(Path.Combine(plugin, "nsz.exe"), "fixture");
            File.WriteAllText(Path.Combine(plugin, "active.json"), "{\"Executable\":\"nsz.exe\",\"Version\":\"1.0\"}");
            var titleRoot = Path.Combine(root, "Plugins", "TitleDB");
            Directory.CreateDirectory(titleRoot);
            var titlePath = Path.Combine(titleRoot, "US.en.json");
            File.WriteAllText(titlePath, "{}");
            var sha = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes("blob 2\0{}")));
            using var client = new HttpClient(new Handler(sha));
            var settings = new AppSettings { TitleDbRegion = "US.en" };
            var checker = new ComponentUpdateChecker(new NszPluginManager(settings, NullLogger<NszPluginManager>.Instance, plugin),
                new NandPluginManager(settings, Path.Combine(root, "nand")), settings, client, root, _ => Task.FromResult<Version?>(null));
            var status = await checker.CheckAsync(CancellationToken.None);
            Assert.Contains("NSZ:", status);
            Assert.Contains("2.0", status);
            Assert.Contains("NxNandManager:", status);
            Assert.Contains("Title DB US.en:", status);
            Assert.Equal("{}", File.ReadAllText(titlePath));
            Assert.Equal("fixture", File.ReadAllText(Path.Combine(plugin, "nsz.exe")));
            Assert.True(ComponentUpdateChecker.MatchesGitBlob(titlePath, sha));
            File.WriteAllText(titlePath, "changed");
            Assert.False(ComponentUpdateChecker.MatchesGitBlob(titlePath, sha));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task PackagePluginsCompareReleaseAndCommitAndKeepCustomVersionsLocal()
    {
        var root = Path.Combine(Path.GetTempPath(), "package-check-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var rust = Path.Combine(root, "rust");
            var kronos = Path.Combine(root, "kronos");
            var desktop = Path.Combine(root, "desktop");
            Directory.CreateDirectory(desktop);
            File.WriteAllText(Path.Combine(desktop, "nscb-desktop.exe"), "fixture");
            File.WriteAllText(Path.Combine(desktop, "installation.json"), System.Text.Json.JsonSerializer.Serialize(new { Repository = ExternalSources.Current.RustDesktopRepository, Version = "1.0" }));
            Directory.CreateDirectory(rust);
            Directory.CreateDirectory(Path.Combine(kronos, "runtime"));
            Directory.CreateDirectory(Path.Combine(kronos, "ztools"));
            File.WriteAllText(Path.Combine(rust, "nscb_rust.exe"), "fixture");
            File.WriteAllText(Path.Combine(kronos, "runtime", "python.exe"), "fixture");
            File.WriteAllText(Path.Combine(kronos, "ztools", "squirrel.py"), "fixture");
            File.WriteAllText(Path.Combine(rust, "installation.json"),
                System.Text.Json.JsonSerializer.Serialize(new { Repository = ExternalSources.Current.RustPackageRepository, Version = "1.0" }));
            File.WriteAllText(Path.Combine(kronos, "installation.json"),
                System.Text.Json.JsonSerializer.Serialize(new { Repository = ExternalSources.Current.KronosPackageRepository, Commit = new string('a', 40) }));
            using var client = new HttpClient(new Handler(new string('b', 40)));
            var settings = new AppSettings { RustPackageExecutablePath = Path.Combine(rust, "nscb_rust.exe"),
                KronosPackageDirectory = kronos, RustDesktopExecutablePath = Path.Combine(desktop, "nscb-desktop.exe"), EnablePackageOperations = true, TitleDbRegion = "US.en" };
            var checker = new ComponentUpdateChecker(new NszPluginManager(settings, NullLogger<NszPluginManager>.Instance, Path.Combine(root, "nsz")),
                new NandPluginManager(settings, Path.Combine(root, "nand")), settings, client, root, _ => Task.FromResult<Version?>(null));
            var results = await checker.CheckResultsAsync(CancellationToken.None);
            Assert.Contains(results, r => r.Name == "nscb_rust" && r.UpdateAvailable);
            Assert.Contains(results, r => r.Name == "NSCB Desktop" && r.UpdateAvailable);
            Assert.Contains(results, r => r.Name == "Kronos / NSC_BUILDER" && r.UpdateAvailable);
            File.WriteAllText(Path.Combine(kronos, "installation.json"),
                System.Text.Json.JsonSerializer.Serialize(new { Repository = ExternalSources.Current.KronosPackageRepository, Commit = new string('b', 40) }));
            File.Delete(Path.Combine(rust, "installation.json"));
            results = await checker.CheckResultsAsync(CancellationToken.None);
            Assert.Contains(results, r => r.Name == "nscb_rust" && !r.UpdateAvailable);
            Assert.Contains(results, r => r.Name == "Kronos / NSC_BUILDER" && !r.UpdateAvailable);
            Assert.Equal("fixture", File.ReadAllText(settings.RustPackageExecutablePath));
        }
        finally { Directory.Delete(root, true); }
    }
    private sealed class Handler(string sha) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            var url = request.RequestUri!.AbsoluteUri;
            return Task.FromResult(url.Contains("fw/hashes") ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) :
                new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(url.Contains("releases/latest") ?
                    "{\"tag_name\":\"2.0\"}" : "{\"sha\":\"" + sha + "\"}") });
        }
    }
}
