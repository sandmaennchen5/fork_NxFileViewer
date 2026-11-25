using System;

namespace Emignatik.NxFileViewer.Services.Integrity;

internal static class NcaHashPrefix
{
    internal static bool Matches(ReadOnlySpan<byte> hash, ReadOnlySpan<byte> prefix) => hash.StartsWith(prefix);
}
