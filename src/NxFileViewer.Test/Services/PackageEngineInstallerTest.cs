using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Emignatik.NxFileViewer.Services.BackgroundTask;
using Emignatik.NxFileViewer.Services.PackageOperations;
using Emignatik.NxFileViewer.Settings;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services;

public sealed class PackageEngineInstallerTest : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "PackageInstallerTest-" + Guid.NewGuid());
    private readonly Handler _handler = new();
    private readonly HttpClient _client;
    private bool _startupFails;
    private int _starts;
    public PackageEngineInstallerTest() => _client = new HttpClient(_handler);
    private Task<string> Install(CancellationToken token) => new PackageEngineInstaller(_client, _root, (exe, args, cwd, ct) =>
    {
        ct.ThrowIfCancellationRequested();
        Assert.True(File.Exists(exe));
        Assert.Equal("--help", Assert.Single(args));
        _starts++;
        if (_startupFails) throw new IOException("Startup failed");
    }).InstallAsync(PackageEngine.Rust, new Reporter(), token);

    [Fact]
    public async Task InstallsVerifiedAssetBesideExistingInstallation()
    {
        var token = TestContext.Current.CancellationToken;
        var first = await Install(token);
        var second = await Install(token);
        Assert.NotEqual(first, second);
        Assert.Equal(Handler.Payload, await File.ReadAllBytesAsync(first, token));
        Assert.True(File.Exists(second));
        Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(second)!, "installation.json")));
        Assert.Equal(2, _starts);
    }

    [Theory]
    [InlineData("digest")]
    [InlineData("url")]
    [InlineData("missing")]
    [InlineData("prerelease")]
    [InlineData("startup")]
    [InlineData("download")]
    public async Task FailedInstallationIsCleanedAndExistingInstallationSurvives(string failure)
    {
        var token = TestContext.Current.CancellationToken;
        var existing = await Install(token);
        _handler.Failure = failure;
        _startupFails = failure == "startup";
        await Assert.ThrowsAnyAsync<Exception>(() => Install(token));
        Assert.True(File.Exists(existing));
        Assert.Single(Directory.GetDirectories(_root));
    }

    [Fact]
    public async Task CancellationDoesNotStartPluginAndCleansNewDirectory()
    {
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Install(cancel.Token));
        Assert.Equal(0, _starts);
        Assert.Empty(Directory.GetDirectories(_root));
    }

    [Theory]
    [InlineData("traversal")]
    [InlineData("layout")]
    [InlineData("python-digest")]
    public async Task KronosRejectsUnsafeOrIncompleteDownloads(string failure)
    {
        _handler.Failure = failure;
        var installer = new PackageEngineInstaller(_client, _root, (_, _, _, _) => _starts++);
        await Assert.ThrowsAsync<InvalidDataException>(() => installer.InstallAsync(PackageEngine.Kronos,
            new Reporter(), TestContext.Current.CancellationToken));
        Assert.Empty(Directory.GetDirectories(_root));
        Assert.False(File.Exists(Path.Combine(_root, "escape.txt")));
        Assert.Equal(0, _starts);
    }

    [Fact]
    public async Task DesktopInstallsVerifiedPortableAssetWithoutLaunchingGui()
    {
        var installer = new PackageEngineInstaller(_client, _root, (_, _, _, _) => _starts++);
        var path = await installer.InstallDesktopAsync(new Reporter(), TestContext.Current.CancellationToken);
        Assert.Equal(Handler.Payload, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
        Assert.Equal("nscb-desktop.exe", Path.GetFileName(path));
        Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(path)!, "installation.json")));
        Assert.Equal(0, _starts);
    }

    [Theory]
    [InlineData("digest")]
    [InlineData("url")]
    [InlineData("missing")]
    [InlineData("prerelease")]
    [InlineData("download")]
    public async Task DesktopRejectsInvalidDownloadsAndPreservesPreviousInstallation(string failure)
    {
        var installer = new PackageEngineInstaller(_client, _root);
        var previous = await installer.InstallDesktopAsync(new Reporter(), TestContext.Current.CancellationToken);
        _handler.Failure = failure;
        await Assert.ThrowsAnyAsync<Exception>(() => installer.InstallDesktopAsync(new Reporter(), TestContext.Current.CancellationToken));
        Assert.True(File.Exists(previous));
        Assert.Single(Directory.GetDirectories(_root));
    }
    public void Dispose()
    {
        _client.Dispose();
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
    private sealed class Reporter : IProgressReporter
    {
        public void SetMode(bool value) { }
        public void SetText(string value) { }
        public void SetPercentage(double value) { }
    }
    private sealed class Handler : HttpMessageHandler
    {
        public static readonly byte[] Payload = Encoding.UTF8.GetBytes("fixture executable");
        public string Failure { get; set; } = "";
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (request.RequestUri!.AbsolutePath.EndsWith("/commits/master"))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    { Content = new StringContent(JsonSerializer.Serialize(new { sha = new string('a', 40) })) });
            if (request.RequestUri.Host == "codeload.github.com")
            {
                using var bytes = new MemoryStream();
                using (var zip = new ZipArchive(bytes, ZipArchiveMode.Create, true))
                {
                    var entry = zip.CreateEntry(Failure == "traversal" ? "repo/py/../escape.txt" :
                        Failure == "layout" ? "repo/py/other.txt" : "repo/py/ztools/squirrel.py");
                    using var writer = new StreamWriter(entry.Open());
                    writer.Write("fixture");
                }
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes.ToArray()) });
            }
            if (request.RequestUri.Host == "api.github.com")
            {
                var desktop = request.RequestUri.AbsolutePath.Contains("nscb-desktop");
                var asset = new { name = desktop ? "nscb-desktop-windows-amd64.exe" : "nscb_rust.exe", digest = "sha256:" + (Failure == "digest" ? new string('0', 64) : Convert.ToHexString(SHA256.HashData(Payload))),
                    browser_download_url = Failure == "url" ? "https://example.com/nscb_rust.exe" : desktop ? "https://github.com/cxfcxf/nscb-desktop/releases/download/v1/nscb-desktop-windows-amd64.exe" : "https://github.com/cxfcxf/nscb_rust/releases/download/v1/nscb_rust.exe" };
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new
                { tag_name = "v1", draft = false, prerelease = Failure == "prerelease", assets = Failure == "missing" ? Array.Empty<object>() : new object[] { asset } })) });
            }
            return Task.FromResult(new HttpResponseMessage(Failure == "download" ? HttpStatusCode.BadGateway : HttpStatusCode.OK)
                { Content = new ByteArrayContent(Payload) });
        }
    }
}
