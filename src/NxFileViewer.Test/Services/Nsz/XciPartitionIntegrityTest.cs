using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Emignatik.NxFileViewer.Services.Nsz;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services.Nsz;

public sealed class XciPartitionIntegrityTest : IDisposable
{
    private readonly string _root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "xcz-layout-" + Guid.NewGuid())).FullName;
    private sealed record Content(string Name, byte[] Data, long Reserved = 0);
    private static byte[] Nca(int seed, int size = 0x4200)
    {
        var data = new byte[size]; new Random(seed).NextBytes(data); return data;
    }
    private static string Name(byte[] data) => Convert.ToHexString(SHA256.HashData(data))[..32].ToLowerInvariant() + ".nca";
    private static byte[] NczMetadata(byte[] nca)
    {
        using var memory = new MemoryStream(); using var writer = new BinaryWriter(memory);
        writer.Write(nca[..0x4000]); writer.Write("NCZSECTN"u8); writer.Write(1L);
        writer.Write(0xc00L); writer.Write(nca.Length - 0xc00L); writer.Write(new byte[48]);
        return memory.ToArray(); // Layout correction reads metadata, not the compressed payload.
    }
    private static byte[] Hfs(params Content[] files)
    {
        var names = files.SelectMany(f => Encoding.UTF8.GetBytes(f.Name + "\0")).ToArray();
        var padding = (0x200 - (16 + files.Length * 64 + names.Length) % 0x200) % 0x200;
        var headerSize = 16 + files.Length * 64 + names.Length + padding;
        var size = headerSize + files.Sum(f => f.Reserved == 0 ? f.Data.Length : f.Reserved);
        using var memory = new MemoryStream(new byte[checked((int)size)], true); using var writer = new BinaryWriter(memory);
        writer.Write("HFS0"u8); writer.Write(files.Length); writer.Write(names.Length + padding); writer.Write(0);
        long offset = 0; var nameOffset = 0;
        foreach (var file in files)
        {
            writer.Write(offset); writer.Write(file.Reserved == 0 ? file.Data.LongLength : file.Reserved); writer.Write(nameOffset);
            writer.Write(0); writer.Write(0L); writer.Write(new byte[32]);
            offset += file.Reserved == 0 ? file.Data.Length : file.Reserved; nameOffset += Encoding.UTF8.GetByteCount(file.Name) + 1;
        }
        writer.Write(names); memory.Position = headerSize;
        foreach (var file in files) writer.Write(file.Data); // Reproduce compact payload + oversized directory slots.
        return memory.ToArray();
    }
    private string Xci(string name, byte[] update, byte[] secure)
    {
        var hfs = Hfs(new Content("update", update), new Content("logo", Hfs(new Content("logo.dat", "nxfv-logo-payload-unique"u8.ToArray()))), new Content("normal", Hfs()), new Content("secure", secure));
        var bytes = new byte[0x200 + hfs.Length]; "HEAD"u8.CopyTo(bytes.AsSpan(0x100));
        BitConverter.GetBytes(0x200L).CopyTo(bytes, 0x130); BitConverter.GetBytes(0x200L).CopyTo(bytes, 0x138);
        hfs.CopyTo(bytes, 0x200); SHA256.HashData(hfs.AsSpan(0,0x200)).CopyTo(bytes,0x140);
        var path = Path.Combine(_root, name); File.WriteAllBytes(path, bytes); return path;
    }
    private (string Source, string Output) Fixture(bool broken)
    {
        var first = Nca(1); var second = Nca(2); var plain = Nca(3, 0xe00); var last = Nca(5, 0xe00); var game = Nca(4);
        var source = Xci("source.xcz", Hfs(new Content(Name(plain), plain), new Content(Name(first)[..^1] + "z", NczMetadata(first)), new Content(Name(second)[..^1] + "z", NczMetadata(second)), new Content(Name(last), last)), Hfs(new Content(Name(game), game)));
        return (source, Xci("output.xci", Hfs(new Content(Name(plain), plain), new Content(Name(first), first, first.Length + (broken ? 0x3400 : 0)), new Content(Name(second), second, second.Length + (broken ? 0x3400 : 0)), new Content(Name(last), last)), Hfs(new Content(Name(game), game))));
    }
    [Fact]
    public void ChangedExtraFilesInAnotherPartitionAreRejectedWithoutPatching()
    {
        var fixture = Fixture(false); var bytes = File.ReadAllBytes(fixture.Output);
        var offset = bytes.AsSpan().IndexOf("nxfv-logo-payload-unique"u8);
        Assert.True(offset > 0); bytes[offset] ^= 1; File.WriteAllBytes(fixture.Output, bytes);
        Assert.Throws<InvalidDataException>(() => XciPartitionIntegrity.CorrectStagedLayout(fixture.Source, fixture.Output, TestContext.Current.CancellationToken));
        Assert.Equal(bytes, File.ReadAllBytes(fixture.Output));
    }

    [Fact]
    public void CorrectsKnownSizeOvercountAndIsIdempotentWithoutChangingSource()
    {
        var fixture = Fixture(true); var source = File.ReadAllBytes(fixture.Source);
        Assert.Throws<InvalidDataException>(() => XciPartitionIntegrity.Verify(fixture.Output, TestContext.Current.CancellationToken));
        Assert.True(XciPartitionIntegrity.CorrectStagedLayout(fixture.Source, fixture.Output, TestContext.Current.CancellationToken) > 0);
        XciPartitionIntegrity.Verify(fixture.Output, TestContext.Current.CancellationToken);
        var corrected = File.ReadAllBytes(fixture.Output);
        Assert.Equal(0, XciPartitionIntegrity.CorrectStagedLayout(fixture.Source, fixture.Output, TestContext.Current.CancellationToken));
        Assert.Equal(corrected, File.ReadAllBytes(fixture.Output)); Assert.Equal(source, File.ReadAllBytes(fixture.Source));
        Assert.Equal(SHA256.HashData(corrected.AsSpan(0x200,0x200)), corrected.AsSpan(0x140,32).ToArray());
    }
    [Fact]
    public void CorrectOutputRemainsByteIdentical()
    {
        var fixture = Fixture(false); var original = File.ReadAllBytes(fixture.Output);
        Assert.Equal(0, XciPartitionIntegrity.CorrectStagedLayout(fixture.Source, fixture.Output, TestContext.Current.CancellationToken));
        XciPartitionIntegrity.Verify(fixture.Output, TestContext.Current.CancellationToken);
        Assert.Equal(original, File.ReadAllBytes(fixture.Output));
    }
    [Fact]
    public void UnknownCorruptionAndCancellationNeverPatchTheOutput()
    {
        var fixture = Fixture(true); var bytes = File.ReadAllBytes(fixture.Output);
        // First inner NCA starts after the XCI, root and update headers.
        bytes[0x600 + 100] ^= 0xff; File.WriteAllBytes(fixture.Output, bytes);
        Assert.Throws<InvalidDataException>(() => XciPartitionIntegrity.CorrectStagedLayout(fixture.Source, fixture.Output, TestContext.Current.CancellationToken));
        Assert.Equal(bytes, File.ReadAllBytes(fixture.Output));
        Assert.ThrowsAny<OperationCanceledException>(() => XciPartitionIntegrity.CorrectStagedLayout(fixture.Source, fixture.Output, new CancellationToken(true)));
        Assert.Equal(bytes, File.ReadAllBytes(fixture.Output));
    }
    [Fact]
    public void RebuildsAlignedGamecardHeaderWithoutChangingNcasAndIsIdempotent()
    {
        var fixture = Fixture(false);
        var bytes = File.ReadAllBytes(fixture.Output);
        // Newer flags at 0x11c must survive updating the four-byte ValidDataEndAddress.
        BitConverter.GetBytes(0x89abcdefU).CopyTo(bytes, 0x11c);
        File.WriteAllBytes(fixture.Output, bytes);
        Assert.Throws<InvalidDataException>(() => XciPartitionIntegrity.VerifyGamecardHeader(fixture.Output, TestContext.Current.CancellationToken));
        Assert.True(XciPartitionIntegrity.NormalizeStagedGamecardHeader(fixture.Output, TestContext.Current.CancellationToken));
        XciPartitionIntegrity.VerifyGamecardHeader(fixture.Output, TestContext.Current.CancellationToken);
        XciPartitionIntegrity.Verify(fixture.Output, TestContext.Current.CancellationToken);
        var rebuilt = File.ReadAllBytes(fixture.Output);
        Assert.Equal(0x89abcdefU, BitConverter.ToUInt32(rebuilt, 0x11c));
        Assert.False(XciPartitionIntegrity.NormalizeStagedGamecardHeader(fixture.Output, TestContext.Current.CancellationToken));
        Assert.Equal(rebuilt, File.ReadAllBytes(fixture.Output));
        // A stale pointer and a bad root hash must both be detected.
        rebuilt[0x104] ^= 1; File.WriteAllBytes(fixture.Output, rebuilt);
        Assert.Throws<InvalidDataException>(() => XciPartitionIntegrity.VerifyGamecardHeader(fixture.Output, TestContext.Current.CancellationToken));
        rebuilt[0x104] ^= 1; rebuilt[0x140] ^= 1; File.WriteAllBytes(fixture.Output, rebuilt);
        Assert.Throws<InvalidDataException>(() => XciPartitionIntegrity.VerifyGamecardHeader(fixture.Output, TestContext.Current.CancellationToken));
    }
    [Fact]
    public void HeaderRebuildRejectsCorruptionAndCancellationWithoutReplacingTheStagedFile()
    {
        var fixture = Fixture(false); var bytes = File.ReadAllBytes(fixture.Output);
        bytes[0x600 + 100] ^= 1; File.WriteAllBytes(fixture.Output, bytes);
        Assert.Throws<InvalidDataException>(() => XciPartitionIntegrity.NormalizeStagedGamecardHeader(fixture.Output, TestContext.Current.CancellationToken));
        Assert.Equal(bytes, File.ReadAllBytes(fixture.Output));
        Assert.ThrowsAny<OperationCanceledException>(() => XciPartitionIntegrity.NormalizeStagedGamecardHeader(fixture.Output, new CancellationToken(true)));
        Assert.Empty(Directory.GetFiles(_root, "*.tmp"));
        Assert.Equal(bytes, File.ReadAllBytes(fixture.Output));
    }
    [Fact]
    public void PreservesCertificateAndExtendedPrefixBytesFromAvailableSource()
    {
        var fixture = Fixture(false);
        byte[] Relocate(string path)
        {
            var old = File.ReadAllBytes(path); var bytes = new byte[old.Length + 0xee00];
            old.AsSpan(0, 0x200).CopyTo(bytes); old.AsSpan(0x200).CopyTo(bytes.AsSpan(0xf000));
            BitConverter.GetBytes(0xf000L).CopyTo(bytes, 0x130); return bytes;
        }
        var source = Relocate(fixture.Source); var output = Relocate(fixture.Output);
        new Random(43).NextBytes(source.AsSpan(0x200, 0xee00));
        File.WriteAllBytes(fixture.Source, source); File.WriteAllBytes(fixture.Output, output);
        XciPartitionIntegrity.PreserveStagedPrefix(fixture.Source, fixture.Output, TestContext.Current.CancellationToken);
        var preserved = File.ReadAllBytes(fixture.Output);
        Assert.Equal(source.AsSpan(0x200, 0xee00).ToArray(), preserved.AsSpan(0x200, 0xee00).ToArray());
        Assert.Equal(output.AsSpan(0,0x200).ToArray(), preserved.AsSpan(0,0x200).ToArray());
        Assert.Equal(output.AsSpan(0xf000).ToArray(), preserved.AsSpan(0xf000).ToArray());
        Assert.Equal(source, File.ReadAllBytes(fixture.Source));
    }
    private sealed class RepairProgress : Emignatik.NxFileViewer.Services.BackgroundTask.IProgressReporter
    {
        public Action<double>? PercentageChanged;
        public void SetMode(bool value) { }
        public void SetText(string value) { }
        public void SetPercentage(double value) => PercentageChanged?.Invoke(value);
    }
    private sealed class RepairVerifier : IConversionVerifier
    {
        public int Calls;
        public bool Reject;
        public Emignatik.NxFileViewer.Services.Integrity.BatchIntegrityResult Verify(string path,
            Emignatik.NxFileViewer.Services.BackgroundTask.IProgressReporter progress, CancellationToken token)
        {
            Calls++;
            XciPartitionIntegrity.Verify(path, token);
            XciPartitionIntegrity.VerifyGamecardHeader(path, token);
            return new(path, "XCI", "Application", "XCI", "None",
                Reject ? Emignatik.NxFileViewer.Models.Overview.NcasIntegrity.Error : Emignatik.NxFileViewer.Models.Overview.NcasIntegrity.Original, null);
        }
    }

    [Fact]
    public void OpeningInspectionOffersOnlyARepairableHeaderAndDoesNotModifyTheFile()
    {
        var fixture = Fixture(false);
        var original = File.ReadAllBytes(fixture.Output);
        var verifier = new RepairVerifier();
        var service = new XciRepairService(verifier);
        var candidate = Assert.IsType<XciRepairCandidate>(service.Inspect(fixture.Output, new RepairProgress(), TestContext.Current.CancellationToken));
        Assert.True(candidate.HeaderRepair);
        Assert.Equal(0, candidate.LayoutChanges);
        Assert.Null(candidate.SourceXcz);
        Assert.Equal(original, File.ReadAllBytes(fixture.Output));
        Assert.Equal(0, verifier.Calls);
        var repaired = service.RepairCopy(candidate, Path.Combine(_root, "header-repaired.xci"), new RepairProgress(), TestContext.Current.CancellationToken);
        Assert.Equal(original, File.ReadAllBytes(fixture.Output));
        Assert.Equal(1, verifier.Calls);
        XciPartitionIntegrity.VerifyGamecardHeader(repaired, TestContext.Current.CancellationToken);
        Assert.Null(service.Inspect(repaired, new RepairProgress(), TestContext.Current.CancellationToken));
        Assert.Empty(Directory.GetDirectories(_root, ".nxfv-repair-*"));
    }

    [Fact]
    public void MatchingCompanionEnablesKnownDecompressionLayoutRepairOnACopy()
    {
        var fixture = Fixture(true);
        var companion = Path.ChangeExtension(fixture.Output, ".xcz");
        File.Copy(fixture.Source, companion);
        var original = File.ReadAllBytes(fixture.Output);
        var compressed = File.ReadAllBytes(companion);
        var service = new XciRepairService(new RepairVerifier());
        var candidate = Assert.IsType<XciRepairCandidate>(service.Inspect(fixture.Output, new RepairProgress(), TestContext.Current.CancellationToken));
        Assert.True(candidate.LayoutChanges > 0);
        Assert.Equal(companion, candidate.SourceXcz);
        Assert.Equal(original, File.ReadAllBytes(fixture.Output));
        Assert.Equal(compressed, File.ReadAllBytes(companion));
        var repaired = service.RepairCopy(candidate, Path.Combine(_root, "layout-repaired.xci"), new RepairProgress(), TestContext.Current.CancellationToken);
        XciPartitionIntegrity.Verify(repaired, TestContext.Current.CancellationToken);
        XciPartitionIntegrity.VerifyGamecardHeader(repaired, TestContext.Current.CancellationToken);
        Assert.Equal(original, File.ReadAllBytes(fixture.Output));
        Assert.Equal(compressed, File.ReadAllBytes(companion));
    }

    [Fact]
    public void MissingCompanionOrUnknownPayloadCorruptionDoesNotOfferRepair()
    {
        var broken = Fixture(true);
        var service = new XciRepairService(new RepairVerifier());
        Assert.Null(service.Inspect(broken.Output, new RepairProgress(), TestContext.Current.CancellationToken));
        var fixture = Fixture(false);
        var bytes = File.ReadAllBytes(fixture.Output);
        bytes[0x600 + 100] ^= 1;
        File.WriteAllBytes(fixture.Output, bytes);
        Assert.Null(service.Inspect(fixture.Output, new RepairProgress(), TestContext.Current.CancellationToken));
        Assert.Equal(bytes, File.ReadAllBytes(fixture.Output));
    }

    [Fact]
    public void UnrelatedCompanionDoesNotQualifyBrokenLayoutForRepair()
    {
        var fixture = Fixture(true);
        var companion = Path.ChangeExtension(fixture.Output, ".xcz");
        var source = File.ReadAllBytes(fixture.Source);
        var logo = source.AsSpan().IndexOf("nxfv-logo-payload-unique"u8);
        source[logo] ^= 1;
        File.WriteAllBytes(companion, source);
        var service = new XciRepairService(new RepairVerifier());
        Assert.Null(service.Inspect(fixture.Output, new RepairProgress(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public void RepairNeverOverwritesInputOrAnExistingDestination()
    {
        var fixture = Fixture(false);
        var original = File.ReadAllBytes(fixture.Output);
        var service = new XciRepairService(new RepairVerifier());
        var candidate = service.Inspect(fixture.Output, new RepairProgress(), TestContext.Current.CancellationToken)!;
        Assert.Throws<IOException>(() => service.RepairCopy(candidate, fixture.Output, new RepairProgress(), TestContext.Current.CancellationToken));
        var destination = Path.Combine(_root, "existing.xci");
        File.WriteAllText(destination, "keep existing file");
        Assert.Throws<IOException>(() => service.RepairCopy(candidate, destination, new RepairProgress(), TestContext.Current.CancellationToken));
        Assert.Equal("keep existing file", File.ReadAllText(destination));
        Assert.Equal(original, File.ReadAllBytes(fixture.Output));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void VerificationFailureAndCancellationNeverPublishAPartialRepair(bool cancel)
    {
        var fixture = Fixture(false);
        var original = File.ReadAllBytes(fixture.Output);
        using var cancellation = new CancellationTokenSource();
        var verifier = new RepairVerifier { Reject = !cancel };
        var service = new XciRepairService(verifier);
        var candidate = service.Inspect(fixture.Output, new RepairProgress(), TestContext.Current.CancellationToken)!;
        var destination = Path.Combine(_root, "must-not-exist.xci");
        var progress = new RepairProgress { PercentageChanged = value => { if (cancel && value > 0) cancellation.Cancel(); } };
        if (cancel)
            Assert.ThrowsAny<OperationCanceledException>(() => service.RepairCopy(candidate, destination, progress, cancellation.Token));
        else
            Assert.Throws<InvalidDataException>(() => service.RepairCopy(candidate, destination, progress, TestContext.Current.CancellationToken));
        Assert.False(File.Exists(destination));
        Assert.Empty(Directory.GetDirectories(_root, ".nxfv-repair-*"));
        Assert.Equal(original, File.ReadAllBytes(fixture.Output));
    }

    [Fact]
    public void SourceChangedAfterConfirmationRequiresANewInspection()
    {
        var fixture = Fixture(false);
        var service = new XciRepairService(new RepairVerifier());
        var candidate = service.Inspect(fixture.Output, new RepairProgress(), TestContext.Current.CancellationToken)!;
        File.SetLastWriteTimeUtc(fixture.Output, File.GetLastWriteTimeUtc(fixture.Output).AddSeconds(2));
        var destination = Path.Combine(_root, "changed.xci");
        Assert.Throws<InvalidOperationException>(() => service.RepairCopy(candidate, destination, new RepairProgress(), TestContext.Current.CancellationToken));
        Assert.False(File.Exists(destination));
    }

    [Fact]
    public void UnknownTrailingDataIsNeverSilentlyDiscarded()
    {
        var fixture = Fixture(false);
        using (var append = new FileStream(fixture.Output, FileMode.Append)) append.Write(new byte[512]);
        var service = new XciRepairService(new RepairVerifier());
        Assert.Null(service.Inspect(fixture.Output, new RepairProgress(), TestContext.Current.CancellationToken));
    }

    public void Dispose() => Directory.Delete(_root, true);
}
