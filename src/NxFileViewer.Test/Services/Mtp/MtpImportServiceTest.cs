using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Emignatik.NxFileViewer.FileLoading;
using Emignatik.NxFileViewer.Services.BackgroundTask;
using Emignatik.NxFileViewer.Services.Mtp;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services.Mtp;

public sealed class MtpImportServiceTest : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "MtpTest-" + Guid.NewGuid().ToString("N"));
    private readonly Capture _progress = new();
    [Fact]
    public void SdImportPreservesNaxPathsAndSplitPartsAndDoesNotCopyPhotos()
    {
        var backend = SdBackend(); var service = new MtpImportService(backend, _root);
        var result = service.Import(backend.Entries["sd"], _progress, TestContext.Current.CancellationToken).Run();
        var contents = SdCardSource.FindContents(result);
        Assert.NotNull(contents);
        var parts = Path.Combine(contents, "registered", "title.nca");
        Assert.Equal(backend.Bytes["00"], File.ReadAllBytes(Path.Combine(parts, "00")));
        Assert.Equal(backend.Bytes["01"], File.ReadAllBytes(Path.Combine(parts, "01")));
        Assert.Empty(Directory.GetFiles(result, "*.jpg", SearchOption.AllDirectories));
        Assert.Contains(1d, _progress.Percentages);
        service.Cleanup(); Assert.Empty(Directory.GetDirectories(_root));
    }
    [Fact]
    public void IndividualSplitNcaSelectionImportsSiblingsWithContentsAncestry()
    {
        var backend = SdBackend(); var service = new MtpImportService(backend, _root);
        var result = service.Import(backend.Entries["00"], _progress, TestContext.Current.CancellationToken).Run();
        Assert.True(Directory.Exists(result));
        Assert.True(File.Exists(Path.Combine(result, "01")));
        var single = SphairaMtpActions.SinglePath(result);
        Assert.True(SdCardSource.IsNax0(single));
        Assert.NotNull(SdCardSource.ContentsForFile(single));
        service.Cleanup();
    }
    [Fact]
    public void CancelledCopyRemovesPartialSession()
    {
        var backend = SdBackend(); using var cancelled = new CancellationTokenSource();
        backend.AfterRead = cancelled.Cancel;
        var service = new MtpImportService(backend, _root);
        Assert.Throws<OperationCanceledException>(() => service.Import(backend.Entries["00"], _progress, cancelled.Token).Run());
        Assert.Empty(Directory.GetDirectories(_root));
    }
    [Fact]
    public void TruncatedTransferRemovesPartialSession()
    {
        var backend = SdBackend(); backend.WrongLength = true;
        var service = new MtpImportService(backend, _root);
        Assert.Throws<InvalidDataException>(() => service.Import(backend.Entries["00"], _progress, TestContext.Current.CancellationToken).Run());
        Assert.Empty(Directory.GetDirectories(_root));
    }
    [Fact]
    public void RejectsDeviceRootAndNoSupportedFiles()
    {
        var backend = SdBackend(); var service = new MtpImportService(backend, _root);
        Assert.Throws<InvalidOperationException>(() => service.Import(new MtpEntry("Switch", "device", true, true), _progress, TestContext.Current.CancellationToken).Run());
        Assert.Throws<InvalidDataException>(() => service.Import(backend.Entries["photo"], _progress, TestContext.Current.CancellationToken).Run());
    }
    [Theory]
    [InlineData("../outside.nsp")]
    [InlineData("folder/../../outside.nsp")]
    [InlineData("NUL.bin")]
    [InlineData("file:stream")]
    public void RejectsUnsafeCachePaths(string path) => Assert.Throws<InvalidDataException>(() => MtpImportService.CachePath(_root, path));
    [Fact]
    public void DirectFolderCreatesOnlyDescriptorsAndReadsRemoteBytesWithSeeking()
    {
        var backend = new FakeBackend();
        backend.Add("folder", "games", true, null);
        backend.Add("game", "game.nsp", false, "folder", "PFS0"u8.ToArray().Concat(new byte[12]).ToArray());
        backend.Add("nca", "game.nca", false, "folder", Enumerable.Range(0, 256).Select(i => (byte)i).ToArray());
        var service = new MtpImportService(backend, _root);
        var path = service.PrepareDirect(backend.Entries["folder"], _progress, TestContext.Current.CancellationToken);
        Assert.Equal(0, backend.OpenCount);
        var file = Path.Combine(path, "game.nca");
        Assert.Equal(0, new FileInfo(file).Length);
        Assert.True(MtpDirectSource.IsDirect(SphairaMtpActions.SinglePath(path)));
        Assert.True(MtpDirectSource.IsDirect(file));
        using (var stream = MtpDirectSource.OpenRead(file)) {
            Assert.Equal(256, stream.Length);
            Assert.Equal(128, stream.Seek(128, SeekOrigin.Begin));
            Assert.Equal(128, stream.ReadByte());
            stream.Seek(0, SeekOrigin.Begin); Assert.Equal(0, stream.ReadByte());
            Assert.False(stream.CanWrite);
        }
        using (var logger = Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance) {
            Assert.Equal(PackageType.NSP, new PackageTypeAnalyzer(logger).GetType(Path.Combine(path, "game.nsp")));
        }
        var disposedBefore = backend.DisposeCount;
        using (var item = Emignatik.NxFileViewer.Models.TreeItems.Impl.NspItem.FromFile(Path.Combine(path, "game.nsp"), new LibHac.Common.Keys.KeySet())) {
            Assert.Equal("game.nsp", item.Name);
            var opens = backend.OpenCount; Assert.Equal(16, MtpDirectSource.Length(Path.Combine(path, "game.nsp"))); Assert.Equal(opens, backend.OpenCount);
        }
        Assert.True(backend.DisposeCount > disposedBefore);
        Assert.False(Emignatik.NxFileViewer.Services.Nsz.PackageConversionService.Supports(Path.Combine(path, "game.nsp"), Emignatik.NxFileViewer.Services.Nsz.NszOperation.Compress));
        var loader = new DiscoveryLoader();
        var rows = new Emignatik.NxFileViewer.Services.BackgroundTask.RunnableImpl.VerifyDirectoryIntegrityRunnable(loader, null!, new Emignatik.NxFileViewer.Settings.AppSettings(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<Emignatik.NxFileViewer.Services.BackgroundTask.RunnableImpl.VerifyDirectoryIntegrityRunnable>.Instance)
            .Setup(path, true, verifyIntegrity: false).Run(_progress, TestContext.Current.CancellationToken);
        Assert.Equal(2, rows.Count); Assert.Equal(2, loader.Calls);
        Assert.All(rows, row => Assert.False(row.IsFirmware));
        service.Cleanup(); Assert.False(MtpDirectSource.IsDirect(file)); Assert.False(File.Exists(file));
    }
    [Fact]
    public void UnsupportedDirectSelectionLeavesNoDescriptorsAndCacheRemainsAvailable()
    {
        var backend = new FakeBackend();
        backend.Add("folder", "games", true, null);
        backend.Add("game", "game.nsp", false, "folder", new byte[8]);
        backend.Add("zip", "archive.zip", false, "folder", new byte[10]);
        var service = new MtpImportService(backend, _root);
        Assert.Throws<InvalidDataException>(() => service.PrepareDirect(backend.Entries["folder"], _progress, TestContext.Current.CancellationToken));
        if (Directory.Exists(_root)) Assert.Empty(Directory.GetFiles(_root, "*", SearchOption.AllDirectories));
        var copied = service.Import(backend.Entries["zip"], _progress, TestContext.Current.CancellationToken).Run();
        Assert.Equal(backend.Bytes["zip"], File.ReadAllBytes(copied));
        service.Cleanup();
    }
    [Fact]
    public void DirectSdStructureRequiresCache()
    {
        var backend = SdBackend(); var service = new MtpImportService(backend, _root);
        Assert.Throws<InvalidDataException>(() => service.PrepareDirect(backend.Entries["contents"], _progress, TestContext.Current.CancellationToken));
        Assert.Equal(0, backend.OpenCount);
    }
    [Fact]
    public void SdBatchAreasAreFilteredBeforeMtpCopyWhileExplicitContentsStillImports()
    {
        var backend = SdBackend();
        backend.Add("switch", "switch", true, "sd");
        backend.Add("app", "app.nro", false, "switch", new byte[] { 1, 2 });
        backend.Add("hbmenu", "hbmenu.nro", false, "sd", new byte[] { 3, 4 });
        backend.Add("tools", "tools", true, "sd");
        backend.Add("tool", "tool.nro", false, "tools", new byte[] { 5, 6 });
        var service = new MtpImportService(backend, _root);
        var areas = new SdBatchAreas(true, true, false, false, true, false);
        var path = service.Import(backend.Entries["sd"], _progress, TestContext.Current.CancellationToken, areas: areas).Run();
        Assert.Equal(2, backend.OpenCount);
        Assert.Equal(2, Directory.GetFiles(path, "*", SearchOption.AllDirectories).Length);
        Assert.False(Directory.Exists(Path.Combine(path, "Nintendo")));
        Assert.False(Directory.Exists(Path.Combine(path, "tools")));
        Assert.True(File.Exists(Path.Combine(path, "switch", "app.nro")));
        Assert.True(File.Exists(Path.Combine(path, "hbmenu.nro")));
        var contents = service.Import(backend.Entries["contents"], _progress, TestContext.Current.CancellationToken, areas: areas).Run();
        Assert.True(File.Exists(Path.Combine(contents, "registered", "title.nca", "00")));
        service.Cleanup();
    }
    [Fact]
    public void CustomNestedMtpAreaCopiesOnlySelectedDescendantsAndFiles() {
        var backend = SdBackend();
        backend.Add("backup", "backup", true, "sd");
        backend.Add("saves", "saves", true, "backup");
        backend.Add("chosen", "chosen.zip", false, "saves", new byte[] { 7 });
        backend.Add("ignored", "ignored.zip", false, "backup", new byte[] { 8 });
        backend.Add("other", "other", true, "backup");
        backend.Add("sibling", "sibling.zip", false, "other", new byte[] { 9 });
        backend.Add("cfg", "config", true, "sd");
        backend.Add("file", "game.nsp", false, "cfg", new byte[] { 10 });
        backend.Add("extra", "extra.nsp", false, "cfg", new byte[] { 11 });
        var areas = new SdBatchAreas(true, false, false, false, false, false) { CustomPaths = "/backup/saves\n/config/game.nsp" };
        var service = new MtpImportService(backend, _root);
        var path = service.Import(backend.Entries["sd"], _progress, TestContext.Current.CancellationToken, areas: areas).Run();
        Assert.Equal(2, backend.OpenCount);
        Assert.Equal(2, Directory.GetFiles(path, "*", SearchOption.AllDirectories).Length);
        Assert.True(File.Exists(Path.Combine(path, "backup", "saves", "chosen.zip")));
        Assert.True(File.Exists(Path.Combine(path, "config", "game.nsp")));
        Assert.False(File.Exists(Path.Combine(path, "config", "extra.nsp")));
        service.Cleanup();
    }
    [Theory]
    [InlineData(Emignatik.NxFileViewer.Settings.SdBatchSourceMode.SwitchSd, 1)]
    [InlineData(Emignatik.NxFileViewer.Settings.SdBatchSourceMode.Folder, 2)]
    [InlineData(Emignatik.NxFileViewer.Settings.SdBatchSourceMode.Auto, 2)]
    public void MtpSourceChoiceControlsAreaFilteringWithoutSdMarkers(Emignatik.NxFileViewer.Settings.SdBatchSourceMode mode, int count) {
        var backend = new FakeBackend(); backend.Add("root", "microSD card", true, null);
        backend.Add("games", "games", true, "root"); backend.Add("other", "other", true, "root");
        backend.Add("one", "one.nsp", false, "games", new byte[] { 1 }); backend.Add("two", "two.nsp", false, "other", new byte[] { 2 });
        var areas = new SdBatchAreas(true, false, false, false, false, false) { SourceMode = mode, CustomPaths = "/games" };
        var service = new MtpImportService(backend, _root);
        var path = service.Import(backend.Entries["root"], _progress, TestContext.Current.CancellationToken, areas: areas).Run();
        Assert.Equal(count, backend.OpenCount); Assert.Equal(count, Directory.GetFiles(path, "*", SearchOption.AllDirectories).Length);
        service.Cleanup();
    }
    private static FakeBackend SdBackend()
    {
        var backend = new FakeBackend();
        backend.Add("sd", "microSD card", true, null);
        backend.Add("nintendo", "Nintendo", true, "sd");
        backend.Add("contents", "Contents", true, "nintendo");
        backend.Add("registered", "registered", true, "contents");
        backend.Add("nca", "title.nca", true, "registered");
        var bytes = new byte[64]; "NAX0"u8.CopyTo(bytes.AsSpan(0x20));
        backend.Add("00", "00", false, "nca", bytes);
        backend.Add("01", "01", false, "nca", new byte[] { 4, 5, 6 });
        backend.Add("photo", "photo.jpg", false, "sd", new byte[] { 1 }); return backend;
    }
    private sealed class DiscoveryLoader : IFileLoader {
        public int Calls;
        public Emignatik.NxFileViewer.Models.NxFile Load(string path) { Calls++; throw new InvalidDataException("Synthetic discovery probe"); }
    }
    private sealed class FakeBackend : IMtpBackend
    {
        public Dictionary<string, MtpEntry> Entries { get; } = new();
        public Dictionary<string, byte[]> Bytes { get; } = new();
        private readonly Dictionary<string, string?> _parents = new();
        public Action? AfterRead;
        public bool WrongLength;
        public int OpenCount;
        public int DisposeCount;
        public void Add(string path, string name, bool directory, string? parent, byte[]? bytes = null)
        { Entries[path] = new(name, path, directory); _parents[path] = parent; if (bytes != null) Bytes[path] = bytes; }
        public IReadOnlyList<MtpEntry> List(string? path) => Entries.Values.Where(e => _parents[e.Path] == path).ToArray();
        public MtpEntry? Parent(string path) => _parents[path] is { } parent ? Entries[parent] : null;
        public Stream OpenRead(string path) { Interlocked.Increment(ref OpenCount); return new TestStream(Bytes[path], AfterRead, WrongLength, () => Interlocked.Increment(ref DisposeCount)); }
    }
    private sealed class TestStream(byte[] bytes, Action? afterRead, bool wrongLength, Action disposed) : MemoryStream(bytes, false)
    {
        protected override void Dispose(bool disposing) { if (disposing) disposed(); base.Dispose(disposing); }
        public override long Length => base.Length + (wrongLength ? 1 : 0);
        public override int Read(byte[] buffer, int offset, int count)
        { var read = base.Read(buffer, offset, count); if (read > 0) afterRead?.Invoke(); return read; }
    }
    private sealed class Capture : IProgressReporter
    {
        public List<double> Percentages { get; } = new();
        public void SetMode(bool mode) { }
        public void SetText(string text) { }
        public void SetPercentage(double value) => Percentages.Add(value);
    }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
