using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Emignatik.NxFileViewer.Models;
using Emignatik.NxFileViewer.Models.Overview;
using Emignatik.NxFileViewer.Models.TreeItems.Impl;
using Emignatik.NxFileViewer.Services.BackgroundTask;
using Emignatik.NxFileViewer.Services.Integrity;
using LibHac.Common.Keys;
using LibHac.NSZ;
using LibHac.Tools.Fs;
using LibHac.Tools.FsSystem.NcaUtils;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using ZstdSharp;

namespace Emignatik.NxFileViewer.Test.Services.Integrity;

public sealed class NczResourceLifetimeTest
{
    private static MemoryStream SolidNcz()
    {
        var header = new byte[NczHeader.INCOMPRESSIBLE_HEADER_SIZE];
        Encoding.ASCII.GetBytes("NCA3").CopyTo(header, 0x200);
        BitConverter.GetBytes((long)header.Length + 1024 * 1024).CopyTo(header, 0x208);
        using var compressor = new Compressor();
        var memory = new MemoryStream();
        using (var writer = new BinaryWriter(memory, Encoding.ASCII, true))
        {
            writer.Write(header); writer.Write(Encoding.ASCII.GetBytes("NCZSECTN")); writer.Write(1L);
            writer.Write((long)header.Length); writer.Write(1024L * 1024); writer.Write(1L); writer.Write(0L);
            writer.Write(new byte[32]); writer.Write(compressor.Wrap(new byte[1024 * 1024]).ToArray());
        }
        memory.Position = 0;
        return memory;
    }
    private static string FillCache(Ncz ncz)
    {
        ncz.NcaStream.Position = ncz.NcaStream.Length - 1;
        Assert.Equal(0, ncz.NcaStream.ReadByte());
        object decoder = typeof(LibHac.NSZ.Streams.NczToNcaStream).GetField("_decompressionStream", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(ncz.NcaStream)!;
        if (decoder is LibHac.NSZ.Streams.NcaEncryptionStream)
            decoder = decoder.GetType().GetField("_nczDecompressionStream", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(decoder)!;
        return ((FileStream)decoder.GetType().GetField("_cache", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(decoder)!).Name;
    }
    private sealed class HashReader(string outcome) : INcaHashService
    {
        public Ncz? Reader;
        public string? CachePath;
        public Task<byte[]> ComputeSha256Async(Nca nca, CancellationToken? cancellationToken = null, IProgressReporter? progressReporter = null, int? bufferSize = null)
        {
            Reader = Assert.IsType<Ncz>(nca);
            CachePath = FillCache(Reader);
            Assert.True(File.Exists(CachePath));
            if (outcome == "cancel") throw new OperationCanceledException();
            if (outcome == "disk") throw new IOException("disk full", unchecked((int)0x80070070));
            return Task.FromResult(new byte[32]);
        }
    }
    [Fact]
    public async Task SequentialPackagesReleaseCachesEvenWhileCompletedModelsAreRetained()
    {
        var path = Path.Combine(Path.GetTempPath(), "ncz-batch-" + Guid.NewGuid() + ".nsz");
        var header = new byte[16]; Encoding.ASCII.GetBytes("PFS0").CopyTo(header, 0); File.WriteAllBytes(path, header);
        var retainedModels = new System.Collections.Generic.List<NxFile>();
        try
        {
            for (var i = 0; i < 12; i++)
            {
                using var source = SolidNcz();
                var keys = KeySet.CreateDefaultKeySet();
                var root = NspItem.FromFile(path, keys);
                using var file = new NxFile(path, root, new FileOverview(root));
                retainedModels.Add(file);
                var ncz = new Ncz(keys, source, NczReadMode.Fast);
                var item = new NczItem(ncz, new DirectoryEntryEx(new string('a',32) + ".ncz", "/program.ncz", LibHac.Fs.DirectoryEntryType.File, source.Length), root);
                var cache = FillCache(ncz);
                var hashes = new HashReader("success");
                await new NcaItemIntegrityService(hashes, NullLogger<NcaItemIntegrityService>.Instance)
                    .SafeCheckAsync(item, new byte[32], TestContext.Current.CancellationToken);
                Assert.False(hashes.Reader!.NcaStream.CanRead);
                Assert.True(SpinWait.SpinUntil(() => !File.Exists(hashes.CachePath), TimeSpan.FromSeconds(2)));
                file.Dispose();
                Assert.True(SpinWait.SpinUntil(() => !File.Exists(cache), TimeSpan.FromSeconds(2)));
            }
            Assert.Equal(12, retainedModels.Count);
            GC.KeepAlive(retainedModels);
        }
        finally { File.Delete(path); }
    }
    [Theory]
    [InlineData("success")]
    [InlineData("cancel")]
    [InlineData("disk")]
    public async Task TemporaryHashReaderIsClosedImmediatelyAndPackageReaderOnClose(string outcome)
    {
        var path = Path.Combine(Path.GetTempPath(), "ncz-owner-" + Guid.NewGuid() + ".nsz");
        File.WriteAllBytes(path, Encoding.ASCII.GetBytes("PFS0"));
        // Empty PFS0 container for the reader's actual package parent.
        using (var output = new FileStream(path, FileMode.Open)) output.SetLength(16);
        var keys = KeySet.CreateDefaultKeySet();
        using var source = SolidNcz();
        try
        {
            var root = NspItem.FromFile(path, keys);
            using var file = new NxFile(path, root, new FileOverview(root));
            var ncz = new Ncz(keys, source, NczReadMode.Fast);
            var item = new NczItem(ncz, new DirectoryEntryEx(new string('a',32) + ".ncz", "/program.ncz", LibHac.Fs.DirectoryEntryType.File, source.Length), root);
            var liveCache = FillCache(ncz);
            var hashes = new HashReader(outcome);
            var service = new NcaItemIntegrityService(hashes, NullLogger<NcaItemIntegrityService>.Instance);
            if (outcome == "success") await service.SafeCheckAsync(item, new byte[32], TestContext.Current.CancellationToken);
            else if (outcome == "cancel") await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.SafeCheckAsync(item, null, TestContext.Current.CancellationToken));
            else await Assert.ThrowsAsync<IOException>(() => service.SafeCheckAsync(item, null, TestContext.Current.CancellationToken));
            Assert.NotNull(hashes.Reader);
            Assert.False(hashes.Reader.NcaStream.CanRead);
            Assert.False(File.Exists(hashes.CachePath));
            Assert.True(ncz.NcaStream.CanRead);
            ncz.NcaStream.Position = NczHeader.INCOMPRESSIBLE_HEADER_SIZE;
            Assert.Equal(0, ncz.NcaStream.ReadByte());
            Assert.True(File.Exists(liveCache));
            file.Dispose();
            Assert.False(ncz.NcaStream.CanRead);
            Assert.False(File.Exists(liveCache));
        }
        finally { File.Delete(path); }
    }
}
