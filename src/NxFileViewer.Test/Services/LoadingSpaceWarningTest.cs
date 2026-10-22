using System;
using System.IO;
using Emignatik.NxFileViewer.Services.FileOpening;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services;

public sealed class LoadingSpaceWarningTest
{
    [Theory]
    [InlineData(unchecked((int)0x80070070), true)]
    [InlineData(unchecked((int)0x80070027), true)]
    [InlineData(unchecked((int)0x80070005), false)]
    [InlineData(unchecked((int)0x80070020), false)]
    public void RecognizesDiskFullWithoutMistakingOtherIoErrors(int code, bool expected)
    {
        Assert.Equal(expected, LoadingSpaceWarning.IsDiskFull(new IOException("test", code)));
    }
    [Fact]
    public void RecognizesWrappedErrors()
    {
        var error = new IOException("test", unchecked((int)0x80070070));
        Assert.True(LoadingSpaceWarning.IsDiskFull(new InvalidOperationException("wrapped", error)));
        Assert.True(LoadingSpaceWarning.IsDiskFull(new AggregateException(new Exception("other"), error)));
        Assert.False(LoadingSpaceWarning.IsDiskFull(new OutOfMemoryException()));
    }
}
