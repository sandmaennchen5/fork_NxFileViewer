using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using LibHac.NSZ;
using NxFileViewer.Avalonia;
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StandaloneNczHashingReleasesFileOnCompletionAndCancellation(bool cancel)
    {
        var directory = Directory.CreateTempSubdirectory("nx-ncz-");
        try
        {
            var path = Path.Combine(directory.FullName, new string('a', 32) + ".ncz");
            using (var input = SolidNcz())
            using (var output = File.Create(path))
                input.CopyTo(output);

            using var token = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            var hashes = new List<NcaHashResult>();
            if (cancel)
                Assert.ThrowsAny<OperationCanceledException>(() =>
                    PackageFile.Read(path, null, hashes, token.Token, hashProgress: (_, _, _, _) => token.Cancel()));
            else
            {
                Assert.Equal("NCZ", PackageFile.Read(path, null, hashes, token.Token).Type);
                Assert.False(Assert.Single(hashes).Matches);
            }
            using var exclusive = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        }
        finally { directory.Delete(recursive: true); }
    }
}
