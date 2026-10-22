using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Text;
using Emignatik.NxFileViewer.Services.Nand;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services.Nand;

public sealed class NandExplorerTest
{
    private static byte[] FatImage()
    {
        var image = new byte[0x10000];
        BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(11), 512);
        image[13] = 1; image[14] = 1; image[16] = 1;
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(32), 128);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(36), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(44), 2);
        image[510] = 0x55; image[511] = 0xaa;
        foreach (var cluster in new[] { 2, 4, 5 }) BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(512 + cluster * 4), 0x0fffffff);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(512 + 3 * 4), 5);
        Entry(1536, "SAVE       ", 0x10, 4, 0);
        Entry(2048, "HELLO   TXT", 0, 3, 600);
        image.AsSpan(2048, 512).Fill(0); // file cluster 3
        image.AsSpan(3072, 88).Fill(0x42); // fragmented file cluster 5
        Entry(2560, "HELLO   TXT", 0, 3, 600); // folder cluster 4
        return image;
        void Entry(int offset, string name, byte attr, ushort cluster, uint size)
        {
            Encoding.ASCII.GetBytes(name).CopyTo(image, offset);
            image[offset + 11] = attr;
            BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(offset + 26), cluster);
            BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(offset + 28), size);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BrowsesFoldersAndExportsFragmentedFileWithoutChangingSource(bool encrypted)
    {
        var image = FatImage();
        var key = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
        if (encrypted)
        {
            var plain = image.ToArray();
            for (var offset = 0; offset < image.Length; offset += 0x4000)
            {
                var iv = new byte[16]; BinaryPrimitives.WriteInt64BigEndian(iv.AsSpan(8), offset / 0x4000);
                LibHac.Crypto.Aes.EncryptXts128(plain.AsSpan(offset, 0x4000), image.AsSpan(offset, 0x4000), key.AsSpan(0, 16), key.AsSpan(16, 16), iv);
            }
        }
        WithImage(image, path =>
        {
            using var reader = new NandExplorer(path, TestContext.Current.CancellationToken);
            var partition = Assert.Single(reader.GetPartitions("USER.bin"));
            reader.OpenPartition(partition, encrypted ? key : null);
            Assert.True(Assert.Single(reader.List("/")).IsDirectory);
            var file = Assert.Single(reader.List("/SAVE"));
            var prefix = reader.ReadPrefix(file, 520);
            Assert.Equal(520, prefix.Length);
            Assert.All(prefix[512..], value => Assert.Equal(0x42, value));
            Assert.Empty(reader.ReadPrefix(file, 0));
            Assert.Throws<ArgumentException>(() => reader.ReadPrefix(file, -1));
            using (var stream = reader.OpenFile(file))
            {
                Assert.False(stream.CanWrite);
                Assert.Equal(600, stream.Length);
                stream.Seek(510, SeekOrigin.Begin);
                var acrossClusters = new byte[12];
                Assert.Equal(12, stream.Read(acrossClusters));
                Assert.Equal(new byte[] { 0, 0 }, acrossClusters[..2]);
                Assert.All(acrossClusters[2..], value => Assert.Equal(0x42, value));
                stream.Seek(-1, SeekOrigin.End);
                Assert.Equal(0x42, stream.ReadByte());
                Assert.Equal(-1, stream.ReadByte());
                stream.Position = 0;
                Assert.Equal(0, stream.ReadByte());
                Assert.Throws<NotSupportedException>(() => stream.WriteByte(1));
                Assert.Throws<IOException>(() => stream.Seek(-1, SeekOrigin.Begin));
            }
            using var output = new MemoryStream(); reader.Export(file, output);
            Assert.Equal(600, output.Length);
            Assert.All(output.ToArray()[512..], value => Assert.Equal(0x42, value));
            Assert.Equal(image, File.ReadAllBytes(path));
        });
    }

    [Fact]
    public void ReadsFragmentedFileAcrossSplitDumpBoundary()
    {
        var image = FatImage();
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(512 + 3 * 4), 65);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(512 + 65 * 4), 0x0fffffff);
        image.AsSpan(1536 + (65 - 2) * 512, 88).Fill(0x43);
        var first = Path.Combine(Path.GetTempPath(), "NandSplit-" + Guid.NewGuid() + ".00");
        var second = first[..^2] + "01";
        try
        {
            File.WriteAllBytes(first, image[..0x8000]); File.WriteAllBytes(second, image[0x8000..]);
            using var reader = new NandExplorer(first, TestContext.Current.CancellationToken);
            reader.OpenPartition(Assert.Single(reader.GetPartitions("USER.bin")), null);
            using var output = new MemoryStream(); reader.Export(Assert.Single(reader.List("/SAVE")), output);
            Assert.Equal(600, output.Length);
            Assert.All(output.ToArray()[512..], value => Assert.Equal(0x43, value));
        }
        finally { File.Delete(first); File.Delete(second); }
    }

    [Fact]
    public void RejectsCyclicClusterChains()
    {
        var image = FatImage(); BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(512 + 3 * 4), 3);
        WithImage(image, path =>
        {
            using var reader = new NandExplorer(path, TestContext.Current.CancellationToken);
            reader.OpenPartition(Assert.Single(reader.GetPartitions("USER.bin")), null);
            using var output = new MemoryStream();
            Assert.Throws<InvalidDataException>(() => reader.Export(Assert.Single(reader.List("/SAVE")), output));
        });
    }
    [Fact]
    public void RejectsNonNandSource()
    {
        WithImage(new byte[4096], path =>
        {
            using var reader = new NandExplorer(path, TestContext.Current.CancellationToken);
            Assert.Throws<InvalidDataException>(() => reader.GetPartitions("other.bin"));
        });
    }
    private static void WithImage(byte[] image, Action<string> check)
    {
        var path = Path.Combine(Path.GetTempPath(), "NandExplorer-" + Guid.NewGuid() + ".bin");
        try { File.WriteAllBytes(path, image); check(path); }
        finally { File.Delete(path); }
    }
}
