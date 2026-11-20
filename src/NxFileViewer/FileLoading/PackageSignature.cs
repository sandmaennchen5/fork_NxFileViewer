using System;
using System.IO;

namespace Emignatik.NxFileViewer.FileLoading;

internal static class PackageSignature
{
    internal static PackageType Read(Stream stream)
    {
        Span<byte> header = stackalloc byte[0x104];
        var size = stream.ReadAtLeast(header, header.Length, false);
        if (size >= 0x14 && header.Slice(0x10, 4).SequenceEqual("NRO0"u8))
            return PackageType.NRO;
        if (size >= 4 && header[..4].SequenceEqual("PFS0"u8))
            return PackageType.NSP;
        if (size >= 0x104 && header.Slice(0x100, 4).SequenceEqual("HEAD"u8))
            return PackageType.XCI;
        return PackageType.UNKNOWN;
    }
}
