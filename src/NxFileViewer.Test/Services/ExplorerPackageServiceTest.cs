using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Emignatik.NxFileViewer.Services.FileAssociations;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services;

public sealed class ExplorerPackageServiceTest : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "NxExplorerPackageTest-" + Guid.NewGuid().ToString("N"));
    private static readonly Version PackageVersion = new(4, 0, 1, 5);
    private const string Repository = "https://github.com/sandmaennchen5/fork_NxFileViewer/releases/download/v4.0.1/";
    public ExplorerPackageServiceTest() => Directory.CreateDirectory(_root);
    private static string Asset(string arch = "x64", Version? version = null) => $"NxFileViewerFork.Explorer_{version ?? PackageVersion}_{arch}_setup.zip";
    private static JsonElement Release(string arch = "x64", bool preview = false, bool draft = false, string? url = null, Version? version = null, string? digest = null) =>
        JsonSerializer.SerializeToElement(new { tag_name = preview ? "v4.0.1-beta4" : "v4.0.1", prerelease = preview, draft,
            assets = new[] { new { name = Asset(arch, version), browser_download_url = url ?? Repository + Asset(arch, version), digest = digest ?? "sha256:" + new string('1', 64) } } });
    private static X509Certificate2 Certificate(string name = "CN=ExplorerTest")
    {
        using var rsa = RSA.Create(2048);
        return new CertificateRequest(name, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(2));
    }
    private static byte[] Package(X509Certificate2 signer, string arch = "x64", string? publisher = null, string name = ExplorerIntegration.PackageName, Version? version = null)
    {
        var cms = new SignedCms(new ContentInfo(new byte[] { 1, 2, 3, 4 }));
        cms.ComputeSignature(new CmsSigner(signer));
        using var memory = new MemoryStream();
        using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, true))
        {
            Write(zip, "AppxManifest.xml", Encoding.UTF8.GetBytes($"<Package xmlns=\"http://schemas.microsoft.com/appx/manifest/foundation/windows10\"><Identity Name=\"{name}\" Publisher=\"{publisher ?? signer.Subject}\" ProcessorArchitecture=\"{arch}\" Version=\"{version ?? PackageVersion}\" /></Package>"));
            Write(zip, "AppxSignature.p7x", Encoding.ASCII.GetBytes("PKCX").Concat(cms.Encode()).ToArray());
        }
        return memory.ToArray();
    }
    private static void Write(ZipArchive zip, string name, byte[] bytes)
    { using var output = zip.CreateEntry(name).Open(); output.Write(bytes); }
    private static byte[] Setup(byte[] package, X509Certificate2 certificate, string? extra = null)
    {
        using var memory = new MemoryStream();
        using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, true))
        {
            Write(zip, $"NxFileViewerFork.Explorer_{PackageVersion}_x64.msix", package);
            Write(zip, "NxFileViewerFork.Explorer.cer", certificate.Export(X509ContentType.Cert));
            foreach (var name in new[] { "Trust-ExplorerCertificate.ps1", "Trust-ExplorerCertificate.cmd", "Explorer-install.txt" })
                Write(zip, name, Encoding.UTF8.GetBytes("throw 'downloaded scripts must never run'"));
            if (extra != null) Write(zip, extra, new byte[] { 0 });
        }
        return memory.ToArray();
    }
    [Theory]
    [InlineData("x64")][InlineData("arm64")]
    public void SelectsExplorerAssetForOperatingSystemIndependentlyOfViewerVersion(string arch)
    {
        Assert.Null(ExplorerPackageService.SelectRelease(Release(arch, preview: true), arch, false));
        var found = ExplorerPackageService.SelectRelease(Release(arch, preview: true), arch, true)!;
        Assert.Equal(PackageVersion, found.Version);
        Assert.Equal(arch, found.Architecture);
        Assert.Null(ExplorerPackageService.SelectRelease(Release(arch, draft: true), arch, true));
        Assert.Null(ExplorerPackageService.SelectRelease(Release(arch == "x64" ? "arm64" : "x64"), arch, true));
    }
    [Theory]
    [InlineData("http://github.com/sandmaennchen5/fork_NxFileViewer/releases/download/v4.0.1/")]
    [InlineData("https://github.com/other/repo/releases/download/v4.0.1/")]
    [InlineData("https://github.com.evil.example/sandmaennchen5/fork_NxFileViewer/releases/download/v4.0.1/")]
    public void RejectsDownloadsOutsideOfficialRepository(string prefix) =>
        Assert.Throws<InvalidDataException>(() => ExplorerPackageService.SelectRelease(Release(url: prefix + Asset()), "x64", true));
    [Fact]
    public void RejectsUnsupportedExplorerArchitecture() =>
        Assert.Throws<PlatformNotSupportedException>(() => ExplorerPackageService.SelectRelease(Release(), "x86", true));
    [Theory]
    [InlineData("../escape.ps1")][InlineData("private.pfx")][InlineData("Trust-ExplorerCertificate.ps1")]
    public void RejectsUnexpectedPathsPrivateKeysAndDuplicateEntries(string extra)
    {
        using var certificate = Certificate();
        var archive = Path.Combine(_root, "bad.zip");
        File.WriteAllBytes(archive, Setup(Package(certificate), certificate, extra));
        var release = ExplorerPackageService.SelectRelease(Release(), "x64", true)!;
        Assert.Throws<InvalidDataException>(() => ExplorerPackageService.ExtractArchive(archive, _root, release));
        Assert.False(File.Exists(Path.Combine(_root, "package.msix")));
    }
    [Fact]
    public void ChecksIdentityArchitectureVersionPublisherAndSignerBeforeTrust()
    {
        using var certificate = Certificate(); using var other = Certificate("CN=Other");
        var cert = Path.Combine(_root, "public.cer"); File.WriteAllBytes(cert, certificate.Export(X509ContentType.Cert));
        var package = Path.Combine(_root, "package.msix");
        var release = ExplorerPackageService.SelectRelease(Release(), "x64", true)!;
        foreach (var bytes in new[] { Package(certificate, arch: "arm64"), Package(certificate, name: "Other.App"),
            Package(certificate, version: new Version(9, 0, 0, 0)), Package(certificate, publisher: "CN=Other"),
            Package(other, publisher: certificate.Subject) })
        {
            File.WriteAllBytes(package, bytes);
            Assert.Throws<InvalidDataException>(() => ExplorerPackageService.ValidatePackage(package, cert, release));
        }
        File.WriteAllBytes(package, Package(certificate));
        using var verified = ExplorerPackageService.ValidatePackage(package, cert, release);
        Assert.Equal(certificate.Thumbprint, verified.Thumbprint);
        Assert.False(verified.HasPrivateKey);
    }
    [Fact]
    public void RejectsModifiedCmsSignature()
    {
        using var certificate = Certificate();
        var package = Path.Combine(_root, "tampered.msix"); File.WriteAllBytes(package, Package(certificate));
        var cert = Path.Combine(_root, "public.cer"); File.WriteAllBytes(cert, certificate.Export(X509ContentType.Cert));
        using (var zip = ZipFile.Open(package, ZipArchiveMode.Update))
        {
            var signature = zip.GetEntry("AppxSignature.p7x")!;
            using var memory = new MemoryStream(); using (var input = signature.Open()) input.CopyTo(memory);
            var bytes = memory.ToArray(); bytes[^1] ^= 1; signature.Delete(); Write(zip, "AppxSignature.p7x", bytes);
        }
        Assert.ThrowsAny<CryptographicException>(() => ExplorerPackageService.ValidatePackage(package, cert, ExplorerPackageService.SelectRelease(Release(), "x64", true)!));
    }
    private sealed class Handler(byte[] archive, string? badDigest = null) : HttpMessageHandler
    {
        public int Downloads;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            if (request.RequestUri!.Host == "api.github.com")
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[" + Release(digest: badDigest ?? "sha256:" + Convert.ToHexString(SHA256.HashData(archive))).GetRawText() + "]") });
            Downloads++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(archive) });
        }
    }
    private sealed class Deployment : IExplorerPackageDeployment
    {
        public Version? Version; public bool Trusted; public bool CancelTrust;
        public readonly List<string> Calls = new();
        public Task<Version?> InstalledVersionAsync() { Calls.Add("version"); return Task.FromResult(Version); }
        public bool IsTrusted(X509Certificate2 certificate) => Trusted;
        public Task TrustAsync(X509Certificate2 certificate) { Calls.Add("trust"); if (!CancelTrust) Trusted = true; return Task.CompletedTask; }
        public Task InstallAsync(string package) { Calls.Add("install"); Assert.True(File.Exists(package)); return Task.CompletedTask; }
        public void RefreshConfiguration() => Calls.Add("refresh");
    }
    [Theory]
    [InlineData(true)][InlineData(false)]
    public async Task DownloadsVerifiesThenTrustsOnlyWhenNecessaryAndInstalls(bool alreadyTrusted)
    {
        using var certificate = Certificate();
        var handler = new Handler(Setup(Package(certificate), certificate)); using var client = new HttpClient(handler);
        var deployment = new Deployment { Version = new Version(4, 0, 1, 4), Trusted = alreadyTrusted };
        Assert.Equal(ExplorerInstallStage.Installed, await new ExplorerPackageService(client, new[] { "github:sandmaennchen5/fork_NxFileViewer" }).InstallAsync("x64", false, _root, deployment, null, CancellationToken.None));
        Assert.Equal(alreadyTrusted ? new[] { "version", "install" } : new[] { "version", "trust", "install" }, deployment.Calls);
        Assert.Equal(1, handler.Downloads);
        Assert.Empty(Directory.GetFileSystemEntries(_root));
    }
    [Theory]
    [InlineData(5)][InlineData(6)]
    public async Task CurrentOrNewerInstalledPackageOnlyRefreshesExecutablePathWithoutDownloadOrTrust(int revision)
    {
        var handler = new Handler(Array.Empty<byte>()); using var client = new HttpClient(handler);
        var deployment = new Deployment { Version = new Version(4, 0, 1, revision) };
        Assert.Equal(ExplorerInstallStage.Current, await new ExplorerPackageService(client, new[] { "github:sandmaennchen5/fork_NxFileViewer" }).InstallAsync("x64", false, _root, deployment, null, CancellationToken.None));
        Assert.Equal(new[] { "version", "refresh" }, deployment.Calls); Assert.Equal(0, handler.Downloads);
    }
    [Fact]
    public async Task DecliningTrustStopsInstallationAndCleansDownload()
    {
        using var certificate = Certificate(); var handler = new Handler(Setup(Package(certificate), certificate));
        using var client = new HttpClient(handler); var deployment = new Deployment { CancelTrust = true };
        await Assert.ThrowsAsync<OperationCanceledException>(() => new ExplorerPackageService(client, new[] { "github:sandmaennchen5/fork_NxFileViewer" }).InstallAsync("x64", false, _root, deployment, null, CancellationToken.None));
        Assert.Equal(new[] { "version", "trust" }, deployment.Calls); Assert.Empty(Directory.GetFileSystemEntries(_root));
    }
    [Theory]
    [InlineData(null)][InlineData("sha256:0000000000000000000000000000000000000000000000000000000000000000")]
    public async Task InvalidDigestOrMismatchedSignerNeverStartsTrustOrInstallation(string? digest)
    {
        using var certificate = Certificate(); using var other = Certificate("CN=Mismatch");
        var handler = new Handler(Setup(Package(certificate), other), digest); using var client = new HttpClient(handler);
        var deployment = new Deployment();
        await Assert.ThrowsAsync<InvalidDataException>(() => new ExplorerPackageService(client, new[] { "github:sandmaennchen5/fork_NxFileViewer" }).InstallAsync("x64", false, _root, deployment, null, CancellationToken.None));
        Assert.Equal(new[] { "version" }, deployment.Calls); Assert.Empty(Directory.GetFileSystemEntries(_root));
    }
    [Fact]
    public async Task MissingDigestIsRejectedBeforeAnyDownload()
    {
        var handler = new Handler(Array.Empty<byte>()); using var client = new HttpClient(handler);
        var release = ExplorerPackageService.SelectRelease(Release(), "x64", false)! with { Digest = null };
        await Assert.ThrowsAsync<InvalidDataException>(() => new ExplorerPackageService(client, new[] { "github:sandmaennchen5/fork_NxFileViewer" }).PrepareAsync(release, _root, CancellationToken.None));
        Assert.Equal(0, handler.Downloads); Assert.Empty(Directory.GetFileSystemEntries(_root));
    }
    private sealed class ListingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[" +
                Release(version: new Version(4, 0, 1, 4)).GetRawText() + "," +
                Release(version: new Version(4, 0, 1, 9)).GetRawText() + "," +
                Release(version: new Version(4, 0, 1, 6)).GetRawText() + "]") });
    }
    [Fact]
    public async Task ChoosesHighestMsixVersionRatherThanReleaseListingOrder()
    {
        using var client = new HttpClient(new ListingHandler());
        var release = await new ExplorerPackageService(client, new[] { "github:sandmaennchen5/fork_NxFileViewer" }).CheckAsync("x64", false, CancellationToken.None);
        Assert.Equal(new Version(4, 0, 1, 9), release!.Version);
    }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
