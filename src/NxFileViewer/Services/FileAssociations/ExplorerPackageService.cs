using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Emignatik.NxFileViewer.Services.Updates;

namespace Emignatik.NxFileViewer.Services.FileAssociations;

public sealed record ExplorerPackageRelease(Version Version, string Architecture, string AssetName, Uri DownloadUrl, string? Digest)
{
    public string? SourceSpec { get; init; }
}
public enum ExplorerInstallStage { Checking, Downloading, Trusting, Installing, Current, Installed }
public interface IExplorerPackageDeployment
{
    Task<Version?> InstalledVersionAsync();
    bool IsTrusted(X509Certificate2 certificate);
    Task TrustAsync(X509Certificate2 certificate);
    Task InstallAsync(string package);
    void RefreshConfiguration();
}

public sealed class PreparedExplorerPackage : IDisposable
{
    public string Directory { get; }
    public string PackagePath { get; }
    public X509Certificate2 Certificate { get; }
    internal PreparedExplorerPackage(string directory, string package, X509Certificate2 certificate)
    { Directory = directory; PackagePath = package; Certificate = certificate; }
    public void Dispose()
    {
        Certificate.Dispose();
        ExplorerPackageService.Cleanup(Directory);
    }
}

public sealed class ExplorerPackageService
{
    private const long Limit = 32L * 1024 * 1024;
    private static readonly HttpClient DefaultClient = CreateClient();
    private readonly HttpClient _client;
    private readonly GitHubUpdateRequests _requests;
    private readonly string[] _sources;
    public ExplorerPackageService(HttpClient? client = null, string[]? sources = null)
    { _client = client ?? DefaultClient; _requests = GitHubUpdateRequests.For(client); _sources = sources ?? ExternalSources.Current.ExplorerSourceSpecs; ExternalSources.ValidateRepositories(_sources); }
    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("NxFileViewer-Explorer/1.0");
        return client;
    }
    public static ExplorerPackageRelease? SelectRelease(JsonElement release, string architecture, bool includePrereleases, string? sourceSpec = null)
    {
        if (architecture is not ("x64" or "arm64")) throw new PlatformNotSupportedException("Windows 11 Explorer requires x64 or ARM64.");
        if (release.GetProperty("draft").GetBoolean() || (!includePrereleases &&
            (release.GetProperty("prerelease").GetBoolean() || (release.GetProperty("tag_name").GetString() ?? "").Contains('-')))) return null;
        var candidates = new List<ExplorerPackageRelease>();
        foreach (var asset in release.GetProperty("assets").EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString() ?? "";
            var match = Regex.Match(name, @"^NxFileViewerFork\.Explorer_(\d+\.\d+\.\d+\.\d+)_" + architecture + @"_setup\.zip$");
            if (!match.Success || !Version.TryParse(match.Groups[1].Value, out var version) || version.Major == 0 ||
                new[] { version.Major, version.Minor, version.Build, version.Revision }.Any(v => v > 65535)) continue;
            var url = new Uri(asset.GetProperty("browser_download_url").GetString()!);
            ValidateUrl(url, name, sourceSpec);
            candidates.Add(new(version, architecture, name, url, asset.TryGetProperty("digest", out var digest) ? digest.GetString() : null) { SourceSpec = sourceSpec });
        }
        return candidates.OrderByDescending(c => c.Version).FirstOrDefault();
    }
    private static void ValidateUrl(Uri url, string asset, string? sourceSpec = null)
    {
        if (sourceSpec != null)
        {
            var source = ViewerUpdateSource.Parse(sourceSpec);
            source.ValidateDownload(url);
            if (source.Kind != ViewerUpdateSourceKind.GitHub) return;
            if (url.Query.Length != 0 || Uri.UnescapeDataString(url.Segments.Last()) != asset) throw new InvalidDataException("Unexpected Explorer ZIP path.");
            return;
        }
        if (!url.IsAbsoluteUri || url.Scheme != "https" || url.Host != "github.com" || !url.IsDefaultPort || url.UserInfo.Length != 0 ||
            url.Query.Length != 0 || url.Fragment.Length != 0 ||
            !url.AbsolutePath.StartsWith("/" + ExternalSources.Current.ExplorerRepository + "/releases/download/", StringComparison.Ordinal) ||
            Uri.UnescapeDataString(url.Segments.Last()) != asset)
            throw new InvalidDataException("Unexpected Explorer release download URL.");
    }
    public Task<ExplorerPackageRelease?> CheckAsync(string architecture, bool includePrereleases, CancellationToken token) =>
        new RepositoryMirrors(_client, _requests).ExplorerAsync(_sources, architecture, includePrereleases, token);
    public async Task<ExplorerInstallStage> InstallAsync(string architecture, bool includePrereleases, string downloadRoot,
        IExplorerPackageDeployment deployment, IProgress<ExplorerInstallStage>? progress, CancellationToken token)
    {
        progress?.Report(ExplorerInstallStage.Checking);
        var installed = await deployment.InstalledVersionAsync();
        var release = await CheckAsync(architecture, includePrereleases, token);
        if (release == null) throw new InvalidOperationException(Localization.LocalizationManager.Instance.Current.Keys.Explorer_NoPackage);
        if (installed != null && installed >= release.Version)
        {
            deployment.RefreshConfiguration();
            progress?.Report(ExplorerInstallStage.Current);
            return ExplorerInstallStage.Current;
        }
        progress?.Report(ExplorerInstallStage.Downloading);
        using var prepared = await PrepareAsync(release, downloadRoot, token);
        if (!deployment.IsTrusted(prepared.Certificate))
        {
            progress?.Report(ExplorerInstallStage.Trusting);
            await deployment.TrustAsync(prepared.Certificate);
            if (!deployment.IsTrusted(prepared.Certificate)) throw new OperationCanceledException("Certificate trust was not confirmed.");
        }
        progress?.Report(ExplorerInstallStage.Installing);
        await deployment.InstallAsync(prepared.PackagePath);
        progress?.Report(ExplorerInstallStage.Installed);
        return ExplorerInstallStage.Installed;
    }
    public async Task<PreparedExplorerPackage> PrepareAsync(ExplorerPackageRelease release, string downloadRoot, CancellationToken token)
    {
        ValidateUrl(release.DownloadUrl, release.AssetName, release.SourceSpec);
        string expected;
        try { expected = ViewerUpdateService.ExpectedHash(release.Digest); }
        catch (InvalidDataException ex) { throw new InvalidDataException("Explorer setup ZIP has no valid SHA-256 digest. Re-publish the package and release metadata.", ex); }
        var root = Path.GetFullPath(downloadRoot);
        System.IO.Directory.CreateDirectory(root);
        if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0) throw new IOException("Explorer downloads require a local directory.");
        var stage = Path.Combine(root, Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(stage);
        var archive = Path.Combine(stage, "release.zip");
        try
        {
            using var response = await _client.GetAsync(release.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using (var input = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false))
            await using (var output = File.Create(archive))
            {
                var buffer = new byte[81920]; long total = 0; int read;
                while ((read = await input.ReadAsync(buffer, token).ConfigureAwait(false)) > 0)
                {
                    if ((total += read) > Limit) throw new InvalidDataException("Explorer download exceeds size limit.");
                    await output.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false);
                }
            }
            await using (var input = File.OpenRead(archive))
                if (!Convert.ToHexString(await SHA256.HashDataAsync(input, token).ConfigureAwait(false)).Equals(expected, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Explorer ZIP SHA-256 mismatch.");
            var package = ExtractArchive(archive, stage, release);
            var certificate = ValidatePackage(package, Path.Combine(stage, "NxFileViewerFork.Explorer.cer"), release);
            return new(stage, package, certificate);
        }
        catch { Cleanup(stage); throw; }
    }
    public static string ExtractArchive(string archive, string destination, ExplorerPackageRelease release)
    {
        using var zip = ZipFile.OpenRead(archive);
        var packageName = $"NxFileViewerFork.Explorer_{release.Version}_{release.Architecture}.msix";
        var allowed = new[] { packageName, "NxFileViewerFork.Explorer.cer", "Trust-ExplorerCertificate.ps1", "Trust-ExplorerCertificate.cmd", "Explorer-install.txt" };
        if (zip.Entries.Count != 5 || zip.Entries.Sum(e => e.Length) > Limit ||
            zip.Entries.Any(e => !allowed.Contains(e.FullName, StringComparer.Ordinal)) ||
            zip.Entries.Select(e => e.FullName).Distinct(StringComparer.OrdinalIgnoreCase).Count() != 5)
            throw new InvalidDataException("Expected the official five-file Explorer setup ZIP.");
        foreach (var name in new[] { packageName, "NxFileViewerFork.Explorer.cer" })
        {
            var entry = zip.GetEntry(name)!;
            var limit = name.EndsWith(".cer", StringComparison.Ordinal) ? 65536 : Limit;
            if (entry.Length == 0 || entry.Length > limit) throw new InvalidDataException("Invalid Explorer setup payload size.");
            using var input = entry.Open();
            using var output = new FileStream(Path.Combine(destination, name.EndsWith(".msix", StringComparison.Ordinal) ? "package.msix" : name), FileMode.CreateNew, FileAccess.Write);
            CopyBounded(input, output, entry.Length);
        }
        // Downloaded scripts are never executed. The app embeds its own consent helper.
        return Path.Combine(destination, "package.msix");
    }
    private static void CopyBounded(Stream input, Stream output, long length)
    {
        var buffer = new byte[81920]; long total = 0; int read;
        while ((read = input.Read(buffer)) != 0)
        {
            if ((total += read) > length) throw new InvalidDataException("ZIP payload exceeds declared size.");
            output.Write(buffer, 0, read);
        }
        if (total != length) throw new InvalidDataException("Incomplete ZIP payload.");
    }
    public static X509Certificate2 ValidatePackage(string package, string publicCertificate, ExplorerPackageRelease release)
    {
        var bytes = File.ReadAllBytes(publicCertificate);
        if (bytes.Length > 65536 || X509Certificate2.GetCertContentType(bytes) != X509ContentType.Cert) throw new InvalidDataException("Expected a public certificate.");
        var certificate = X509CertificateLoader.LoadCertificate(bytes);
        try
        {
            using var zip = ZipFile.OpenRead(package);
            byte[] Read(string name)
            {
                var entries = zip.Entries.Where(e => e.FullName == name).ToArray();
                if (entries.Length != 1 || entries[0].Length > 1024 * 1024) throw new InvalidDataException("Invalid MSIX metadata.");
                using var input = entries[0].Open(); using var output = new MemoryStream();
                CopyBounded(input, output, entries[0].Length); return output.ToArray();
            }
            using var manifestStream = new MemoryStream(Read("AppxManifest.xml"));
            using var reader = XmlReader.Create(manifestStream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            var manifest = XDocument.Load(reader);
            var identity = manifest.Root?.Element(XName.Get("Identity", "http://schemas.microsoft.com/appx/manifest/foundation/windows10"));
            if (identity == null || (string?)identity.Attribute("Name") != ExplorerIntegration.PackageName ||
                (string?)identity.Attribute("ProcessorArchitecture") != release.Architecture ||
                (string?)identity.Attribute("Version") != release.Version.ToString() ||
                (string?)identity.Attribute("Publisher") != certificate.Subject)
                throw new InvalidDataException("Explorer package identity, version, architecture or publisher mismatch.");
            var signature = Read("AppxSignature.p7x");
            if (signature.Length < 5 || !signature.AsSpan(0, 4).SequenceEqual("PKCX"u8)) throw new InvalidDataException("Missing MSIX signature.");
            var cms = new SignedCms(); cms.Decode(signature.AsSpan(4).ToArray()); cms.CheckSignature(true);
            if (cms.SignerInfos.Count != 1 || cms.SignerInfos[0].Certificate == null ||
                !cms.SignerInfos[0].Certificate!.RawData.AsSpan().SequenceEqual(certificate.RawData))
                throw new InvalidDataException("MSIX signer does not match the supplied public certificate.");
            // Windows deployment validates block-map integrity, timestamp and trust before installing.
            return certificate;
        }
        catch { certificate.Dispose(); throw; }
    }
    internal static void Cleanup(string stage)
    {
        foreach (var name in new[] { "release.zip", "package.msix", "NxFileViewerFork.Explorer.cer" })
            try { File.Delete(Path.Combine(stage, name)); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        try { System.IO.Directory.Delete(stage); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}