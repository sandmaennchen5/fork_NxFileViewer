using System;
using System.IO;
using System.Linq;
using System.Text;
using Emignatik.NxFileViewer.FileLoading;
using Emignatik.NxFileViewer.Models.Overview;
using Emignatik.NxFileViewer.Models.TreeItems.Impl;
using LibHac.Common;
using LibHac.Common.Keys;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.Tools.Fs;
using LibHac.Tools.FsSystem;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Emignatik.NxFileViewer.Services.KeysManagement;
using Emignatik.NxFileViewer.Services.BackgroundTask.RunnableImpl;
using Emignatik.NxFileViewer.Services.BackgroundTask;
using Emignatik.NxFileViewer.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Path = System.IO.Path;

namespace Emignatik.NxFileViewer.Test.Services;

public sealed class SwitchSourcesTest : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "NxSourcesTest_" + Guid.NewGuid().ToString("N"));
    public SwitchSourcesTest() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);

    [Fact]
    public void NroMetadataAndHeaderDetection()
    {
        var bytes = Nro();
        var path = Path.Combine(_root, "homebrew.nro"); File.WriteAllBytes(path, bytes);
        var item = new NroFileItem(path);
        Assert.Equal("Test homebrew", item.Titles.Single().AppName);
        Assert.Equal("Test author", item.Titles.Single().Publisher);
        Assert.Equal("1.2.3", item.DisplayVersion);
        Assert.Equal(NxFileType.NRO, new FileOverview(item).FileType);
        Assert.Equal(PackageType.NRO, new PackageTypeAnalyzer(NullLoggerFactory.Instance).GetType(path));
    }
    [Theory]
    [InlineData(0x98, ulong.MaxValue)]
    [InlineData(0xa0, ulong.MaxValue)]
    public void NroRejectsOutOfBoundsAssets(int position, ulong value)
    {
        var bytes = Nro(); BitConverter.GetBytes(value).CopyTo(bytes, position);
        var path = Path.Combine(_root, "bad.nro"); File.WriteAllBytes(path, bytes);
        Assert.Throws<InvalidDataException>(() => { _ = new NroFileItem(path); });
    }
    [Fact]
    public void NroWithoutAssetsDoesNotRequireKeys()
    {
        var path = Path.Combine(_root, "minimal.nro"); File.WriteAllBytes(path, Nro()[..0x80]);
        Assert.Empty(new NroFileItem(path).Titles);
    }
    [Fact]
    public void SdSeedIsReadFromProdKeys()
    {
        var path = Path.Combine(_root, "prod.keys");
        File.WriteAllText(path, "sd_seed = 00112233445566778899aabbccddeeff\n");
        var keys = KeySet.CreateDefaultKeySet(); ExternalKeyReader.ReadKeyFile(keys, filename: path);
        Assert.Equal("00112233445566778899aabbccddeeff", Convert.ToHexString(keys.SdCardEncryptionSeed.Data).ToLowerInvariant());
    }
    [Fact]
    public void NroBatchVerificationWorksWithoutKeysOrNcaVerifier()
    {
        var path = Path.Combine(_root, "homebrew.nro"); File.WriteAllBytes(path, Nro());
        var settings = new AppSettings();
        var provider = new KeySetProviderService(settings, NullLoggerFactory.Instance, _root, _root);
        var loader = (IFileLoader)Activator.CreateInstance(typeof(IFileLoader).Assembly.GetType("Emignatik.NxFileViewer.FileLoading.FileLoader")!,
            NullLoggerFactory.Instance, new PackageTypeAnalyzer(NullLoggerFactory.Instance),
            new FileItemLoader(provider, NullLoggerFactory.Instance, settings), new FileOverviewLoader(NullLoggerFactory.Instance, settings), null)!;
        using var services = new ServiceCollection().BuildServiceProvider();
        var batch = new VerifyDirectoryIntegrityRunnable(loader, services, settings, NullLogger<VerifyDirectoryIntegrityRunnable>.Instance);
        var result = Assert.Single(batch.Setup(_root, false, verifyIntegrity: true).Run(new Progress(), TestContext.Current.CancellationToken));
        Assert.Equal("NRO", result.FileType); Assert.Equal(NcasIntegrity.NoNca, result.Integrity); Assert.Null(result.Error);
        Assert.Equal("Test homebrew", result.Title); Assert.Equal("Homebrew", result.Distribution);
    }
    [Fact]
    public void SdRequiresSeedInProdKeys()
    {
        Directory.CreateDirectory(Path.Combine(_root, "registered"));
        Assert.Throws<InvalidDataException>(() => { using var sd = SdCardItem.Open(_root, KeySet.CreateDefaultKeySet()); });
    }
    [Fact]
    public void SdDiscoversOfwAndEverySupportedEmuMmcSlot()
    {
        var ofw = Path.Combine(_root, "Nintendo", "Contents");
        Directory.CreateDirectory(Path.Combine(ofw, "registered"));
        foreach (var slot in Enumerable.Range(1, 3).Select(i => "RAW" + i).Concat(Enumerable.Range(0, 100).Select(i => "SD" + i.ToString("D2"))))
            Directory.CreateDirectory(Path.Combine(_root, "emuMMC", slot, "Nintendo", "Contents", "registered"));
        foreach (var invalid in new[] { "RAW0", "RAW4", "RAW10", "SD0", "SD100", "Other" })
            Directory.CreateDirectory(Path.Combine(_root, "emuMMC", invalid, "Nintendo", "Contents", "registered"));
        var sources = SdCardSource.FindAllContents(_root);
        Assert.Equal(104, sources.Count);
        Assert.Equal(ofw, sources[0].ContentsPath);
        Assert.Equal(103, SdCardSource.FindAllContents(Path.Combine(_root, "emuMMC")).Count);
        Assert.Equal(104, SdCardSource.FindRelatedContents(sources.Last().ContentsPath).Count);
        Assert.Single(SdCardSource.FindAllContents(Path.Combine(_root, "emuMMC", "RAW2")));
        Assert.Single(SdCardSource.FindAllContents(Path.Combine(_root, "emuMMC", "SD99", "Nintendo")));
    }
    [Fact]
    public void EmuMmcOnlyCardAndOriginalPathsAreRecognized()
    {
        var contents = Path.Combine(_root, "emuMMC", "SD00", "Nintendo", "Contents");
        var registered = Directory.CreateDirectory(Path.Combine(contents, "registered", "00000001")).FullName;
        var path = Path.Combine(registered, "0123456789abcdef0123456789abcdef.nca");
        File.WriteAllBytes(path, new byte[128]);
        Assert.Equal(contents, SdCardSource.FindContents(_root));
        Assert.Equal(contents, SdCardSource.ContentsForFile(path));
        Assert.Single(SdCardSource.FindAllContents(contents));
    }
    [Fact]
    public void BatchScansOfwAndEmuMmcSeparatelyWithoutTreatingThemAsFirmware()
    {
        foreach (var path in new[] { Path.Combine(_root, "Nintendo"), Path.Combine(_root, "emuMMC", "RAW1", "Nintendo"), Path.Combine(_root, "emuMMC", "SD99", "Nintendo") })
            Directory.CreateDirectory(Path.Combine(path, "Contents", "registered"));
        var settings = new AppSettings();
        var provider = new KeySetProviderService(settings, NullLoggerFactory.Instance, _root, _root);
        var loader = (IFileLoader)Activator.CreateInstance(typeof(IFileLoader).Assembly.GetType("Emignatik.NxFileViewer.FileLoading.FileLoader")!,
            NullLoggerFactory.Instance, new PackageTypeAnalyzer(NullLoggerFactory.Instance), new FileItemLoader(provider, NullLoggerFactory.Instance, settings),
            new FileOverviewLoader(NullLoggerFactory.Instance, settings), null)!;
        using var services = new ServiceCollection().BuildServiceProvider();
        var batch = new VerifyDirectoryIntegrityRunnable(loader, services, settings, NullLogger<VerifyDirectoryIntegrityRunnable>.Instance);
        var results = batch.Setup(_root, false, verifyIntegrity: false).Run(new Progress(), TestContext.Current.CancellationToken);
        Assert.Equal(3, results.Count);
        Assert.Equal(3, results.Select(r => r.FilePath).Distinct().Count());
        Assert.All(results, result => { Assert.Equal("NAX0", result.FileType); Assert.False(result.IsFirmware); Assert.Contains("sd_seed", result.Error); });
    }
    private sealed class Progress : IProgressReporter
    {
        public void SetText(string text) { }
        public void SetMode(bool isIndeterminate) { }
        public void SetPercentage(double value) { }
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SdDecryptionPreservesPathAndSupportsSplitContent(bool split)
    {
        var contents = Path.Combine(_root, "Nintendo", "Contents");
        Directory.CreateDirectory(Path.Combine(contents, "registered", "00000001"));
        const string relative = "/registered/00000001/0123456789abcdef0123456789abcdef.nca";
        var keys = KeySet.CreateDefaultKeySet();
        keys.SetSdSeed(Enumerable.Range(1, 16).Select(i => (byte)i).ToArray());
        // Synthetic key material, no console data involved.
        keys.MasterKeys[0].Data.Fill(0x43);
        keys.DeriveSdCardKeys();
        var physical = Path.Combine(contents, relative.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        var expected = Encoding.UTF8.GetBytes("synthetic decrypted NCA content");
        using (var encrypted = new AesXtsFileSystem(new LocalFileSystem(contents), keys.SdCardEncryptionKeys[1].Data.ToArray(), 0x4000))
        {
            using var fsPath = new LibHac.Fs.Path();
            fsPath.Initialize(Encoding.UTF8.GetBytes(relative + "\0")).ThrowIfFailure();
            encrypted.CreateFile(in fsPath, expected.Length, CreateFileOptions.None).ThrowIfFailure();
            using var file = new UniqueRef<IFile>();
            encrypted.OpenFile(ref file.Ref, relative.ToU8Span(), OpenMode.Write).ThrowIfFailure();
            file.Get.Write(0, expected, WriteOption.Flush).ThrowIfFailure();
        }
        if (split)
        {
            var data = File.ReadAllBytes(physical); File.Delete(physical); Directory.CreateDirectory(physical);
            File.WriteAllBytes(Path.Combine(physical, "00"), data[..0x4000]);
            File.WriteAllBytes(Path.Combine(physical, "01"), data[0x4000..]);
        }
        Assert.Equal(contents, SdCardSource.FindContents(_root));
        Assert.True(SdCardSource.IsNax0(physical));
        using var sd = SdCardItem.Open(contents, keys);
        using var denied = new UniqueRef<IFile>();
        Assert.True(sd.PartitionFileSystem.OpenFile(ref denied.Ref, relative.ToU8Span(), OpenMode.Write).IsFailure());
        using var opened = new UniqueRef<IFile>();
        sd.PartitionFileSystem.OpenFile(ref opened.Ref, relative.ToU8Span(), OpenMode.Read).ThrowIfFailure();
        var actual = new byte[expected.Length]; opened.Get.Read(out var read, 0, actual).ThrowIfFailure();
        Assert.Equal(expected.Length, read); Assert.Equal(expected, actual);
        var entry = Assert.Single(sd.EnumerateContent(TestContext.Current.CancellationToken));
        Assert.Equal(relative, entry.FullPath);
    }
    private static byte[] Nro()
    {
        var bytes = new byte[0x80 + 0x38 + 0x4000];
        "NRO0"u8.CopyTo(bytes.AsSpan(0x10)); BitConverter.GetBytes(0x80u).CopyTo(bytes, 0x18);
        "ASET"u8.CopyTo(bytes.AsSpan(0x80));
        BitConverter.GetBytes(0x38ul).CopyTo(bytes, 0x98); BitConverter.GetBytes(0x4000ul).CopyTo(bytes, 0xa0);
        Encoding.UTF8.GetBytes("Test homebrew").CopyTo(bytes, 0xb8);
        Encoding.UTF8.GetBytes("Test author").CopyTo(bytes, 0xb8 + 0x200);
        Encoding.UTF8.GetBytes("1.2.3").CopyTo(bytes, 0xb8 + 0x3060);
        return bytes;
    }
}
