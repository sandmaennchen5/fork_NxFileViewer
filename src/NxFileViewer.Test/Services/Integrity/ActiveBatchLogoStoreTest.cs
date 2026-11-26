using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Emignatik.NxFileViewer.Services.Integrity;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services.Integrity;

public sealed class ActiveBatchLogoStoreTest : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ActiveBatchLogos-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void ActiveLogosRemainReadableAndAreRemovedOnDispose()
    {
        Directory.CreateDirectory(_root);
        var id = Guid.NewGuid();
        string archive;
        BatchIconReference icon;
        using (var active = new ActiveBatchLogoStore(_root))
        {
            archive = active.ArchivePath;
            icon = active.StageIcon(id, new byte[] { 1, 2, 3 })!;
            Assert.Equal(icon, active.StageIcon(id, new byte[] { 1, 2, 3 }));
            Assert.Equal(new byte[] { 1, 2, 3 }, active.ReadIcon(icon));
            active.Flush();
            Assert.Equal(new byte[] { 1, 2, 3 }, active.ReadIcon(icon));
            Assert.Equal(_root, Path.GetDirectoryName(archive));
            using (var zip = ZipFile.OpenRead(archive))
            {
                Assert.Single(zip.Entries);
                Assert.StartsWith("icons/", zip.Entries[0].FullName);
            }
            active.StageIcon(id, new byte[] { 4, 5 });
            active.Flush();
            using (var zip = ZipFile.OpenRead(archive)) Assert.Equal(2, zip.Entries.Count);
        }
        Assert.False(File.Exists(archive));
        Assert.Empty(Directory.GetFiles(_root, "*.zip"));
    }

    [Fact]
    public void TwoRunningInstancesCleanUpOnlyTheirOwnArchives()
    {
        Directory.CreateDirectory(_root);
        using var first = new ActiveBatchLogoStore(_root);
        using var second = new ActiveBatchLogoStore(_root);
        var id = Guid.NewGuid();
        var icon = first.StageIcon(id, new byte[] { 1 })!;
        var other = second.StageIcon(id, new byte[] { 2 })!;
        first.Flush();
        second.Flush();
        first.Dispose();
        Assert.False(File.Exists(first.ArchivePath));
        Assert.True(File.Exists(second.ArchivePath));
        Assert.Equal(new byte[] { 2 }, second.ReadIcon(other));
        Assert.Null(first.ReadIcon(icon));
    }

    [Fact]
    public void PendingImagesAreReleasedOnCleanupAndCannotLeakIntoAnotherScan()
    {
        Directory.CreateDirectory(_root);
        var active = new ActiveBatchLogoStore(_root);
        var id = Guid.NewGuid();
        var icon = active.StageIcon(id, new byte[] { 1 })!;
        Assert.Throws<InvalidOperationException>(() => active.StageIcon(Guid.NewGuid(), new byte[] { 2 }));
        active.Dispose();
        Assert.Null(active.ReadIcon(icon));
        Assert.False(File.Exists(active.ArchivePath));
        Assert.Throws<ObjectDisposedException>(() => active.Flush());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CorruptOrTruncatedTemporaryZipIsRebuiltWithoutBreakingTheScan(bool truncated)
    {
        Directory.CreateDirectory(_root);
        using var active = new ActiveBatchLogoStore(_root);
        var id = Guid.NewGuid();
        var old = active.StageIcon(id, new byte[] { 1 })!;
        Assert.False(active.Flush());
        File.WriteAllBytes(active.ArchivePath, truncated ? Array.Empty<byte>() : new byte[] { 80, 75, 3, 4, 0, 0 });
        var next = active.StageIcon(id, new byte[] { 2 })!;
        Assert.True(active.Flush());
        Assert.Equal(new byte[] { 2 }, active.ReadIcon(next));
        Assert.Null(active.ReadIcon(old));
        Assert.False(active.Contains(old));
        Assert.False(active.Flush());
        using var zip = ZipFile.OpenRead(active.ArchivePath);
        Assert.Single(zip.Entries);
        Assert.Empty(Directory.GetFiles(_root, "*.tmp"));
    }
    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}