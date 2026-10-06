using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Emignatik.NxFileViewer.FileLoading;
using Emignatik.NxFileViewer.Models;
using Emignatik.NxFileViewer.Models.TreeItems.Impl;
using Emignatik.NxFileViewer.Services.BackgroundTask;
using Emignatik.NxFileViewer.Services.BackgroundTask.RunnableImpl;
using Emignatik.NxFileViewer.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using LibHac.Common.Keys;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services.Integrity;

public sealed class PackageZipTest : IDisposable
{
    private readonly string _root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "NxZip-" + Guid.NewGuid())).FullName;
    private string CreateZip(params string[] names)
    {
        var path = Path.Combine(_root, Guid.NewGuid() + ".zip");
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var name in names)
        {
            using var output = zip.CreateEntry(name).Open();
            output.Write(new byte[] { 1, 2, 3, 4 });
        }
        return path;
    }
    [Fact] public void EnumeratesPackagesAndNcaWithSeparateBatchFilter()
    {
        var path = CreateZip("a.nsp", "sub/B.NSZ", "c.xci", "d.xcz", "control.nca", "readme.txt");
        Assert.Equal(5, PackageZip.GetEntries(path).Count);
        Assert.Equal(4, PackageZip.GetEntries(path, false).Count);
    }
    [Fact] public void ExtractsOnlySelectedEntryAndDeletesItOnDispose()
    {
        var path = CreateZip("sub/game.nsp", "another.nsp");
        var original = File.ReadAllBytes(path);
        string extracted;
        using (var lease = PackageZip.Extract(path, "sub/game.nsp", TestContext.Current.CancellationToken, Path.Combine(_root, "temp")))
        {
            extracted = lease.FilePath;
            Assert.Equal("game.nsp", Path.GetFileName(extracted));
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, File.ReadAllBytes(extracted));
            Assert.Single(Directory.GetFiles(Path.GetDirectoryName(extracted)!));
        }
        Assert.False(File.Exists(extracted));
        Assert.Equal(original, File.ReadAllBytes(path));
    }
    [Theory] [InlineData("../outside.nsp")] [InlineData("/outside.nsp")] [InlineData("sub/../../outside.nca")]
    public void RejectsUnsafeNames(string entry)
    {
        var path = CreateZip(entry);
        Assert.Throws<InvalidDataException>(() => PackageZip.GetEntries(path));
        Assert.Throws<InvalidDataException>(() => PackageZip.Extract(path, entry, TestContext.Current.CancellationToken, Path.Combine(_root, "temp")));
    }
    [Fact] public void RejectsDuplicateNamesAndCancelledExtraction()
    {
        var path = CreateZip("game.nsp", "game.nsp");
        Assert.Throws<InvalidDataException>(() => PackageZip.GetEntries(path));
        Assert.Throws<InvalidDataException>(() => PackageZip.Extract(path, "game.nsp", TestContext.Current.CancellationToken, Path.Combine(_root, "temp")));
        path = CreateZip("game.nsp");
        Assert.ThrowsAny<OperationCanceledException>(() => PackageZip.Extract(path, "game.nsp", new CancellationToken(true), Path.Combine(_root, "temp")));
        Assert.False(Directory.Exists(Path.Combine(_root, "temp")));
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public void BatchChecksEveryPackageInZipWithoutFirmwareNetwork(bool includeNca)
    {
        var path = includeNca ? CreateZip("game.nsp", "sub/update.nsz", "control.nca") : CreateZip("game.nsp", "sub/update.nsz");
        var loader = new FailingLoader();
        var handler = new OfflineHandler();
        using var client = new HttpClient(handler);
        var runnable = new VerifyDirectoryIntegrityRunnable(loader, null!, new AppSettings(),
            NullLogger<VerifyDirectoryIntegrityRunnable>.Instance, Path.Combine(_root, "missing"), client);
        var results = runnable.Setup(path, false).Run(new Progress(), TestContext.Current.CancellationToken);
        Assert.Equal(2, results.Count);
        Assert.Equal(2, loader.Calls);
        Assert.All(results, result => { Assert.False(result.IsFirmware); Assert.True(PackageZip.IsMember(result.FilePath)); });
        Assert.Equal(0, handler.Requests);
    }
    [Fact] public void BrokenZipDoesNotStopBatchOrGetClassifiedAsFirmware()
    {
        File.WriteAllText(Path.Combine(_root, "broken.zip"), "bad");
        File.WriteAllText(Path.Combine(_root, "game.nsp"), "bad");
        using var client = new HttpClient(new OfflineHandler());
        var results = new VerifyDirectoryIntegrityRunnable(new FailingLoader(), null!, new AppSettings(),
            NullLogger<VerifyDirectoryIntegrityRunnable>.Instance, Path.Combine(_root, "missing"), client)
            .Setup(_root, false).Run(new Progress(), TestContext.Current.CancellationToken);
        Assert.Equal(2, results.Count);
        Assert.All(results, result => Assert.False(result.IsFirmware));
    }
    [Fact] public void OpensStandalonePlainNcaAndReleasesFileHandle()
    {
        var path = Path.Combine(_root, new string('a', 32) + ".nca");
        var bytes = new byte[0xc00];
        System.Text.Encoding.ASCII.GetBytes("NCA3").CopyTo(bytes, 0x200);
        BitConverter.GetBytes((long)bytes.Length).CopyTo(bytes, 0x208);
        File.WriteAllBytes(path, bytes);
        using (var item = new StandaloneNcaFileItem(path, KeySet.CreateDefaultKeySet()))
        {
            Assert.Equal(Path.GetFileName(path), item.NcaItem.FileName);
            Assert.Equal(Emignatik.NxFileViewer.Models.Overview.NxFileType.NCA,
                new Emignatik.NxFileViewer.Models.Overview.FileOverview(item).FileType);
            using var file = item.NcaItem.LoadFile();
        }
        File.Delete(path);
        Assert.False(File.Exists(path));
    }
    [Fact] public void FullLoaderOpensSelectedZipPackageAndCleansTemporaryFile()
    {
        var path = Path.Combine(_root, "packages.zip");
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            foreach (var name in new[] { "first.nsp", "sub/second.nsp" })
            {
                using var output = zip.CreateEntry(name).Open();
                var header = new byte[16];
                System.Text.Encoding.ASCII.GetBytes("PFS0").CopyTo(header, 0);
                output.Write(header);
            }
        }
        var loader = CreateLoader();
        string tempFile;
        using (var file = loader.Load(PackageZip.MemberPath(path, "sub/second.nsp"), TestContext.Current.CancellationToken))
        {
            Assert.Equal("sub/second.nsp", file.ArchiveEntry);
            Assert.Equal(path, file.ArchivePath);
            Assert.Equal(2, file.ArchiveEntries.Count);
            Assert.IsType<NspItem>(file.RootItem);
            tempFile = Assert.IsType<ExtractedPackage>(file.OwnedResource).FilePath;
            Assert.True(File.Exists(tempFile));
        }
        Assert.False(File.Exists(tempFile));
        using var first = loader.Load(path, TestContext.Current.CancellationToken);
        Assert.Equal("first.nsp", first.ArchiveEntry);
        Assert.False(Emignatik.NxFileViewer.Services.Nsz.PackageConversionService.Supports(first.FilePath,
            Emignatik.NxFileViewer.Services.Nsz.NszOperation.Compress));
    }
    [Fact] public void FailedZipLoadCleansTemporaryDirectory()
    {
        var path = CreateZip("broken.nsp");
        var temp = Path.Combine(AppContext.BaseDirectory, "Temp", "ZIP");
        var before = Directory.Exists(temp) ? Directory.GetDirectories(temp).Order().ToArray() : Array.Empty<string>();
        Assert.ThrowsAny<Exception>(() => CreateLoader().Load(path, TestContext.Current.CancellationToken));
        Assert.Equal(before, Directory.GetDirectories(temp).Order().ToArray());
    }
    // Synthetic solid LZMA2 archives generated by 7-Zip. No keys or game/firmware data.
    private string Create7z(bool firmware = false)
    {
        const string packages = "N3q8ryccAAQfvjAjiAAAAAAAAAAhAAAAAAAAAJlBEg/gAD8ADV0AKBGGqjnzb9jkHZYAAAAAAIEzB64Pz0tvjAfIQ39Bsfr+GZCpcX2l2/2icWIofl78q/kOAvcIwEJlkCOpKyqQ1F6ABx9AEnWFEMORSSOLnvfON7LeV4wtHqhY5hkjoBT3K0avZcVhIDUZSXgNz5tVtLYVLCCgi/joYfH9ORxep2RsFwYVAQlzAAcLAQABIwMBAQVdABAAAAyArgoBVSIAWAAA";
        const string fw = "N3q8ryccAAQLlf5+ZwAAAAAAAAAgAAAAAAAAAIKDNhsBAAcBAgMEBQYHCAAAAIEzB64PzpwGxQkqrtOqhSYoExi7AyVnHGlhhhrxB+LwNl69MHfajTHqyt7dR+Y4ejG4oQGNO4Vy/d9XpxhNjMb26NtXHJH/K+RgBCu/FZaklpT8fWBGAAAAFwYMAQlbAAcLAQABIwMBAQVdABAAAAxuCgGuWoiMAAA=";
        var path = Path.Combine(_root, Guid.NewGuid() + ".7z");
        File.WriteAllBytes(path, Convert.FromBase64String(firmware ? fw : packages));
        return path;
    }
    private static string Manifest(string version, params string[] names) => System.Text.Json.JsonSerializer.Serialize(new
    {
        name = version, file_count = names.Length,
        files = names.ToDictionary(name => name, name => new { size = 4, sha256 = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(name == "a.nca" ? new byte[] { 1, 2, 3, 4 } : new byte[] { 5, 6, 7, 8 })) })
    });
    private sealed class KnownReferences : Emignatik.NxFileViewer.Services.Integrity.IFirmwareReferenceProvider
    {
        public int Calls;
        public (Emignatik.NxFileViewer.Services.Integrity.FirmwareIntegrityVerifier?, string) Load(CancellationToken token)
        {
            Calls++;
            return (new(new[] { Manifest("1.0.0", "a.nca"), Manifest("2.0.0", "a.nca", "b.nca") }), "synthetic references");
        }
    }
    [Fact] public void Solid7zPackagesCanBeListedSelectedAndCancelledWithoutReferences()
    {
        var path = Create7z();
        Assert.Equal(new[] { "a.nsp", "b.nsz", "c.xci", "d.xcz" }, PackageZip.GetEntries(path));
        using (var file = CreateLoader().Load(PackageZip.MemberPath(path, "b.nsz"), TestContext.Current.CancellationToken))
        {
            Assert.Equal("b.nsz", file.ArchiveEntry);
            Assert.IsType<NspItem>(file.RootItem);
        }
        Assert.ThrowsAny<OperationCanceledException>(() => PackageZip.Extract(path, "d.xcz", new CancellationToken(true), Path.Combine(_root, "cancel")));
        Assert.False(Directory.Exists(Path.Combine(_root, "cancel")));
        var handler = new OfflineHandler();
        using var client = new HttpClient(handler);
        var results = new VerifyDirectoryIntegrityRunnable(new FailingLoader(), null!, new AppSettings(),
            NullLogger<VerifyDirectoryIntegrityRunnable>.Instance, null, client).Setup(path, false).Run(new Progress(), TestContext.Current.CancellationToken);
        Assert.Equal(4, results.Count);
        Assert.Contains(results, result => result.FileType == "NSP (7z)");
        Assert.Contains(results, result => result.FileType == "XCZ (7z)");
        Assert.All(results, result => Assert.False(result.IsFirmware));
        Assert.Equal(0, handler.Requests);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public void SingleModeIdentifiesCompleteFirmwareZipAndSolid7zWithoutKeys(bool sevenZip)
    {
        var path = sevenZip ? Create7z(true) : Path.Combine(_root, "firmware.zip");
        if (!sevenZip)
        {
            using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
            using (var a = zip.CreateEntry("sub/a.nca").Open()) a.Write(new byte[] { 1, 2, 3, 4 });
            using (var b = zip.CreateEntry("sub/b.nca").Open()) b.Write(new byte[] { 5, 6, 7, 8 });
        }
        var references = new KnownReferences();
        using var file = CreateLoader(references).Load(path, TestContext.Current.CancellationToken);
        Assert.IsType<FirmwareFileItem>(file.RootItem);
        Assert.Equal("2.0.0", file.FirmwareResult!.Structure);
        Assert.Equal(Emignatik.NxFileViewer.Models.Overview.NcasIntegrity.Original, file.FirmwareResult.Integrity);
        Assert.Contains("synthetic references", file.FirmwareResult.FirmwareDetails);
        Assert.Equal(1, references.Calls);
    }
    [Fact] public void SingleRenamedNcaListsAllMatchingFirmwareVersionsAndRejectsChangedContent()
    {
        var path = Path.Combine(_root, "renamed.nca");
        File.WriteAllBytes(path, new byte[] { 1, 2, 3, 4 });
        var references = new KnownReferences();
        using (var file = CreateLoader(references).Load(path, TestContext.Current.CancellationToken))
        {
            Assert.IsType<FirmwareFileItem>(file.RootItem);
            Assert.Equal("1.0.0 / 2.0.0", file.FirmwareResult!.Structure);
        }
        File.WriteAllBytes(path, new byte[] { 1, 2, 3, 5 });
        var verifier = references.Load(TestContext.Current.CancellationToken).Item1!;
        Assert.False(verifier.IdentifyNca(path, TestContext.Current.CancellationToken).IsFirmware);
        Assert.ThrowsAny<OperationCanceledException>(() => verifier.IdentifyNca(path, new CancellationToken(true)));
    }
    [Fact] public void FirmwareArchiveWithoutReferencesDisplaysNoticeAndRemainsUnchecked()
    {
        using var file = CreateLoader().Load(Create7z(true), TestContext.Current.CancellationToken);
        Assert.Equal(Emignatik.NxFileViewer.Models.Overview.NcasIntegrity.Unchecked, file.Overview.NcasIntegrity);
        Assert.Contains("no references", file.FirmwareResult!.FirmwareDetails);
    }
    [Fact] public void BatchFirmware7zUsesOfflineReferencesAndIndividualMemberIsIdentified()
    {
        var path = Create7z(true);
        var refs = Directory.CreateDirectory(Path.Combine(_root, "refs")).FullName;
        File.WriteAllText(Path.Combine(refs, "2.0.0.json"), Manifest("2.0.0", "a.nca", "b.nca"));
        using var client = new HttpClient(new OfflineHandler());
        var results = new VerifyDirectoryIntegrityRunnable(new FailingLoader(), null!, new AppSettings(),
            NullLogger<VerifyDirectoryIntegrityRunnable>.Instance, refs, client).Setup(path, false).Run(new Progress(), TestContext.Current.CancellationToken);
        var result = Assert.Single(results);
        Assert.True(result.IsFirmware);
        Assert.Equal("7Z", result.FileType);
        Assert.Equal("2.0.0", result.Structure);
        Assert.Equal(Emignatik.NxFileViewer.Models.Overview.NcasIntegrity.Original, result.Integrity);
        string extracted;
        using (var file = CreateLoader(new KnownReferences()).Load(PackageZip.MemberPath(path, "a.nca"), TestContext.Current.CancellationToken))
        {
            Assert.Equal("1.0.0 / 2.0.0", file.FirmwareResult!.Structure);
            Assert.Equal(2, file.ArchiveEntries.Count);
            extracted = Assert.IsType<ExtractedPackage>(file.OwnedResource).FilePath;
        }
        Assert.False(File.Exists(extracted));
    }
    [Fact] public void Broken7zDoesNotStopBatch()
    {
        File.WriteAllText(Path.Combine(_root, "broken.7z"), "bad");
        using var client = new HttpClient(new OfflineHandler());
        var results = new VerifyDirectoryIntegrityRunnable(new FailingLoader(), null!, new AppSettings(),
            NullLogger<VerifyDirectoryIntegrityRunnable>.Instance, null, client).Setup(_root, false).Run(new Progress(), TestContext.Current.CancellationToken);
        Assert.Single(results);
        Assert.False(results[0].IsFirmware);
    }
    private sealed class FixedReferences(Emignatik.NxFileViewer.Services.Integrity.FirmwareIntegrityVerifier verifier)
        : Emignatik.NxFileViewer.Services.Integrity.IFirmwareReferenceProvider
    {
        public (Emignatik.NxFileViewer.Services.Integrity.FirmwareIntegrityVerifier?, string) Load(CancellationToken token) => (verifier, "synthetic references");
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public void IndividualNcaKeepsDecryptedContentAlongsideFirmwareIdentification(bool known)
    {
        var path = Path.Combine(_root, "plain.nca");
        var bytes = new byte[0xc00];
        System.Text.Encoding.ASCII.GetBytes("NCA3").CopyTo(bytes, 0x200);
        BitConverter.GetBytes((long)bytes.Length).CopyTo(bytes, 0x208);
        File.WriteAllBytes(path, bytes);
        var manifest = System.Text.Json.JsonSerializer.Serialize(new {
            name = "plain-test", file_count = 1, files = new System.Collections.Generic.Dictionary<string, object> {
                ["plain.nca"] = new { size = bytes.Length, sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)) }
            }
        });
        var references = known ? new FixedReferences(new(new[] { manifest })) : (Emignatik.NxFileViewer.Services.Integrity.IFirmwareReferenceProvider)new EmptyReferences();
        using var file = CreateLoader(references).Load(path, TestContext.Current.CancellationToken);
        Assert.IsType<StandaloneNcaFileItem>(file.RootItem);
        Assert.Equal(known, file.FirmwareResult!.IsFirmware);
        if (known) Assert.Equal("plain-test", file.FirmwareResult.Structure);
    }
    private string CreatePackageArchive(bool sevenZip)
    {
        if (sevenZip) return Create7z();
        var path = Path.Combine(_root, Guid.NewGuid() + ".zip");
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var name in new[] { "a.nsp", "b.nsz" })
        {
            using var output = zip.CreateEntry(name).Open();
            var header = new byte[16];
            System.Text.Encoding.ASCII.GetBytes("PFS0").CopyTo(header, 0);
            output.Write(header);
        }
        return path;
    }
    private sealed class CountingLoader : IFileLoader
    {
        private readonly IFileLoader _inner = CreateLoader();
        public int Calls;
        public NxFile? LastLoaded;
        public NxFile Load(string path) => Load(path, TestContext.Current.CancellationToken);
        public NxFile Load(string path, CancellationToken token)
        {
            Calls++;
            return LastLoaded = _inner.Load(path, token);
        }
    }
    private sealed class ImmediateRunner : IMainBackgroundTaskRunnerService
    {
        public TaskCompletionSource<bool>? Gate;
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged { add { } remove { } }
        public string ProgressText => "";
        public double ProgressValue => 0;
        public string ProgressValueText => "";
        public bool IsRunning => false;
        public bool IsIndeterminate => false;
        public System.Windows.Input.ICommand CancelCommand => null!;
        public Task RunAsync(IRunnable runnable) { runnable.Run(new Progress(), TestContext.Current.CancellationToken); return Task.CompletedTask; }
        public async Task<T> RunAsync<T>(IRunnable<T> runnable)
        {
            var result = runnable.Run(new Progress(), TestContext.Current.CancellationToken);
            if (Gate != null) await Gate.Task;
            return result;
        }
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task IndividualArchiveSelectionKeepsExtractedPackagesAndReusesLoadedMembersUntilClose(bool sevenZip)
    {
        var archive = CreatePackageArchive(sevenZip);
        var loader = new CountingLoader();
        var service = new Emignatik.NxFileViewer.Services.FileOpening.FileOpeningService(NullLoggerFactory.Instance, new AppSettings(), loader, new ImmediateRunner());
        try
        {
            await service.SafeOpenFile(archive);
            var first = service.OpenedFile!;
            var firstTemp = Assert.IsType<ExtractedPackage>(first.OwnedResource).FilePath;
            await service.SafeOpenFile(PackageZip.MemberPath(archive, "b.nsz"));
            var second = service.OpenedFile!;
            var secondTemp = Assert.IsType<ExtractedPackage>(second.OwnedResource).FilePath;
            Assert.True(File.Exists(firstTemp));
            Assert.True(File.Exists(secondTemp));
            // Cached members stay usable even while the original archive is unavailable.
            File.Move(archive, archive + ".held");
            for (var i = 0; i < 3; i++)
            {
                await service.SafeOpenFile(PackageZip.MemberPath(archive, "a.nsp"));
                Assert.Same(first, service.OpenedFile);
                await service.SafeOpenFile(PackageZip.MemberPath(archive.ToUpperInvariant(), "b.nsz"));
                Assert.Same(second, service.OpenedFile);
            }
            Assert.Equal(2, loader.Calls);
            File.Move(archive + ".held", archive);
            await service.SafeOpenFile(PackageZip.MemberPath(archive, "missing.nsp"));
            Assert.Same(second, service.OpenedFile);
            Assert.True(File.Exists(firstTemp));
            Assert.True(File.Exists(secondTemp));
            var otherArchive = CreatePackageArchive(sevenZip);
            await service.SafeOpenFile(otherArchive);
            Assert.False(File.Exists(firstTemp));
            Assert.False(File.Exists(secondTemp));
            var otherTemp = Assert.IsType<ExtractedPackage>(service.OpenedFile!.OwnedResource).FilePath;
            service.SafeClose();
            Assert.Null(service.OpenedFile);
            Assert.False(File.Exists(otherTemp));
            await service.SafeOpenFile(archive);
            Assert.Equal(5, loader.Calls);
        }
        finally { service.SafeClose(); }
    }
    [Fact] public async Task ClosingDuringArchiveLoadDisposesPendingExtractionWithoutReopening()
    {
        var loader = new CountingLoader();
        var runner = new ImmediateRunner { Gate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var service = new Emignatik.NxFileViewer.Services.FileOpening.FileOpeningService(NullLoggerFactory.Instance, new AppSettings(), loader, runner);
        var pending = service.SafeOpenFile(CreatePackageArchive(false));
        var extracted = Assert.IsType<ExtractedPackage>(loader.LastLoaded!.OwnedResource).FilePath;
        service.SafeClose();
        runner.Gate.SetResult(true);
        await pending;
        Assert.Null(service.OpenedFile);
        Assert.False(File.Exists(extracted));
    }
    [Theory]
    [InlineData("game.nsp", "NSP")]
    [InlineData("a.zip::sub/game.nsp", "NSP (ZIP)")]
    [InlineData("a.7z::sub/game.nsz", "NSZ (7z)")]
    [InlineData("a.zip::game.xci", "XCI (ZIP)")]
    [InlineData("a.7z::game.xcz", "XCZ (7z)")]
    public void BatchFileTypeIncludesArchiveOrigin(string path, string expected) => Assert.Equal(expected, PackageZip.DisplayFileType(path));
    private static IFileLoader CreateLoader(Emignatik.NxFileViewer.Services.Integrity.IFirmwareReferenceProvider? references = null) => (IFileLoader)Activator.CreateInstance(
        typeof(IFileLoader).Assembly.GetType("Emignatik.NxFileViewer.FileLoading.FileLoader")!,
        NullLoggerFactory.Instance, new PackageTypeAnalyzer(NullLoggerFactory.Instance), new SyntheticItemLoader(),
        new FileOverviewLoader(NullLoggerFactory.Instance), references ?? new EmptyReferences())!;
    private sealed class EmptyReferences : Emignatik.NxFileViewer.Services.Integrity.IFirmwareReferenceProvider
    {
        public (Emignatik.NxFileViewer.Services.Integrity.FirmwareIntegrityVerifier?, string) Load(CancellationToken token) => (null, "no references");
    }
    private sealed class SyntheticItemLoader : IFileItemLoader
    {
        public event MissingKeyExceptionHandler? MissingKey { add { } remove { } }
        public NspItem LoadNsp(string path) => NspItem.FromFile(path, KeySet.CreateDefaultKeySet());
        public XciItem LoadXci(string path) => XciItem.FromFile(path, KeySet.CreateDefaultKeySet());
        public StandaloneNcaFileItem LoadNca(string path) => new(path, KeySet.CreateDefaultKeySet());
    }
    private sealed class FailingLoader : IFileLoader
    {
        public int Calls;
        public NxFile Load(string path) { Calls++; throw new InvalidDataException("synthetic package"); }
    }
    private sealed class OfflineHandler : HttpMessageHandler
    {
        public int Requests;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        { Requests++; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)); }
    }
    private sealed class Progress : IProgressReporter
    {
        public void SetMode(bool value) { }
        public void SetText(string value) { }
        public void SetPercentage(double value) { }
    }
    public void Dispose() => Directory.Delete(_root, recursive: true);
}
