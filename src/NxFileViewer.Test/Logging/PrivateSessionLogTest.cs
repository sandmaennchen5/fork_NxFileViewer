using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using Emignatik.NxFileViewer.Logging;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Logging;

public sealed class PrivateSessionLogTest
{
    private static MethodInfo CompressionMethod => typeof(SessionFileLog).Assembly
        .GetType("Emignatik.NxFileViewer.Views.Windows.FeedbackWindow", true)!
        .GetMethod("CompressPrivateLog", BindingFlags.NonPublic | BindingFlags.Static)!;

    [Fact]
    public void UploadCompressionPreservesEveryOriginalByte()
    {
        var original = Encoding.UTF8.GetBytes("\uFEFFOriginal C:\\Users\\Alice\\Secret Game.nsp\r\n");
        using var input = new MemoryStream(original);
        var payload = CompressionMethod.Invoke(null, new object[] { input })!;
        var encoded = (string)payload.GetType().GetProperty("GzipBase64")!.GetValue(payload)!;
        Assert.Equal(original.Length, payload.GetType().GetProperty("Size")!.GetValue(payload));
        using var gzip = new GZipStream(new MemoryStream(Convert.FromBase64String(encoded)), CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        Assert.Equal(original, output.ToArray());
    }

    [Fact]
    public void OversizedSessionIsRejectedBeforeUpload()
    {
        using var input = new MemoryStream(new byte[5 * 1024 * 1024 + 1]);
        var error = Assert.Throws<TargetInvocationException>(() => CompressionMethod.Invoke(null, new object[] { input }));
        Assert.IsType<InvalidDataException>(error.InnerException);
    }
}
