using System;
using System.Collections.Generic;
using Emignatik.NxFileViewer.Utils;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Utils;

public class IdHelperTest
{
    [Fact]
    public void SpanAndArrayProduceTheSameUppercaseIdWithoutReversingBytes()
    {
        byte[] bytes = { 0x00, 0x01, 0xab, 0xff, 0x10 };
        Assert.Equal("0001ABFF10", bytes.AsSpan().ToStrId());
        Assert.Equal("0001ABFF10", bytes.ToStrId());
        Assert.Equal("0001ABFF10", ((IEnumerable<byte>)bytes).ToStrId());
        Assert.Equal("01AB", bytes.AsSpan(1, 2).ToStrId());
        Assert.Equal("", Span<byte>.Empty.ToStrId());
    }

    [Fact]
    public void NumericIdsKeepTheirExistingBigEndianFormatting()
    {
        Assert.Equal("0100F43008C44000", 0x0100F43008C44000UL.ToStrId());
        Assert.Equal("00000001", 1.ToStrId());
        Assert.Equal("FFFFFFFF", (-1).ToStrId());
    }
}