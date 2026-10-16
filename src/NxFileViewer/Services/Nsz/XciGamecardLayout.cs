using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Threading;

namespace Emignatik.NxFileViewer.Services.Nsz;

public static partial class XciPartitionIntegrity
{
    private static long SectorAlign(long value) => checked((value + 511) / 512 * 512);
    private static uint Read32(Stream input, long offset)
    {
        Span<byte> bytes = stackalloc byte[4]; input.Position = offset; input.ReadExactly(bytes);
        return BinaryPrimitives.ReadUInt32LittleEndian(bytes);
    }
    private static void Write32(Stream output, long offset, long value)
    {
        Span<byte> bytes = stackalloc byte[4]; BinaryPrimitives.WriteUInt32LittleEndian(bytes, checked((uint)value));
        output.Position = offset; output.Write(bytes);
    }
    private static void CopyRange(Stream input, Stream output, long offset, long size, CancellationToken token)
    {
        Range(input, offset, size, input.Length);
        var buffer = new byte[1024 * 1024]; input.Position = offset;
        while (size > 0)
        {
            token.ThrowIfCancellationRequested();
            var count = input.Read(buffer, 0, (int)Math.Min(size, buffer.Length));
            if (count == 0) throw new EndOfStreamException();
            output.Write(buffer, 0, count); size -= count;
        }
    }
    /// <summary>NSZ copies only the first 0x200 bytes. Retain available certificate/extended header bytes, too.</summary>
    public static void PreserveStagedPrefix(string sourcePath, string stagedPath, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        using var source = File.OpenRead(sourcePath);
        using var output = new FileStream(stagedPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var sourceRoot = Root(source, compressed: true).Offset;
        var outputRoot = Root(output, compressed: true).Offset;
        if (sourceRoot < 0x200 || sourceRoot != outputRoot)
            throw new InvalidDataException("Cannot preserve XCI prefix with changed root header location.");
        output.Position = 0x200;
        CopyRange(source, output, 0x200, sourceRoot - 0x200, token);
        output.Flush(true);
    }
    /// <summary>Checks layout metadata, not the RSA signature of a rebuilt Gamecard header.</summary>
    public static void VerifyGamecardHeader(string path, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        using var input = File.OpenRead(path); var root = Root(input);
        var secure = root.Entries.SingleOrDefault(e => e.Name == "secure")
            ?? throw new InvalidDataException("XCI secure partition is missing.");
        if (root.Offset < 0x200 || root.Offset % 512 != 0 || input.Length % 512 != 0 ||
            root.Entries.Any(e => e.Offset % 512 != 0 || e.Size % 512 != 0) ||
            Read32(input, 0x104) != secure.Offset / 512 || Read32(input, 0x18c) != secure.Offset / 512 ||
            Read32(input, 0x118) != (input.Length - 1) / 512)
            throw new InvalidDataException("XCI Gamecard header does not match the partition layout.");
        var end = checked(root.Offset + SectorAlign(root.HeaderSize));
        foreach (var entry in root.Entries.OrderBy(e => e.Offset))
        {
            token.ThrowIfCancellationRequested();
            if (entry.Offset < end) throw new InvalidDataException("Overlapping XCI partitions.");
            end = checked(entry.Offset + entry.Size);
        }
        if (secure.Offset + secure.Size != end || end != input.Length)
            throw new InvalidDataException("Unexpected data after the XCI secure partition.");
        var hashLength = Read64(input, 0x138);
        if (hashLength != SectorAlign(root.HeaderSize)) throw new InvalidDataException("Incorrect XCI root header hash size.");
        var hash = Hash(input, root.Offset, hashLength, token);
        var stored = new byte[32]; input.Position = 0x140; input.ReadExactly(stored);
        if (!hash.AsSpan().SequenceEqual(stored)) throw new InvalidDataException("XCI root header hash mismatch.");
    }
    /// <summary>Rebuilds only a private staged XCI. Publication still requires the complete conversion verifier.</summary>
    public static bool NormalizeStagedGamecardHeader(string stagedPath, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        try { VerifyGamecardHeader(stagedPath, token); return false; }
        catch (InvalidDataException) { }
        // Do not turn arbitrary corrupt files into apparently valid rebuilt containers.
        Verify(stagedPath, token);
        var temporary = stagedPath + ".header-" + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var input = File.OpenRead(stagedPath))
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            {
                var root = Root(input);
                if (root.Offset < 0x200 || root.Offset % 512 != 0 || !root.Entries.Any(e => e.Name == "secure"))
                    throw new InvalidDataException("Unsupported XCI root layout.");
                // Bounds are checked by Root; also reject overlapping original extents before copying.
                var end = checked(root.Offset + root.HeaderSize);
                foreach (var entry in root.Entries.OrderBy(e => e.Offset))
                {
                    if (entry.Offset < end) throw new InvalidDataException("Overlapping staged XCI partitions.");
                    end = checked(entry.Offset + entry.Size);
                }
                CopyRange(input, output, 0, root.Offset + root.HeaderSize, token);
                var headerSize = SectorAlign(root.HeaderSize);
                long position = checked(root.Offset + headerSize), secureOffset = 0;
                foreach (var entry in root.Entries.OrderBy(e => e.Name == "secure" ? 1 : 0))
                {
                    output.Position = position;
                    CopyRange(input, output, entry.Offset, entry.Size, token);
                    var size = SectorAlign(entry.Size);
                    output.SetLength(checked(position + size));
                    Write64(output, entry.Record, position - root.Offset - root.HeaderSize);
                    Write64(output, entry.Record + 8, size);
                    RefreshHash(output, entry with { Offset = position, Size = size }, token);
                    if (entry.Name == "secure") secureOffset = position;
                    position = checked(position + size);
                }
                Write32(output, 0x104, secureOffset / 512);
                Write32(output, 0x18c, secureOffset / 512);
                Write32(output, 0x118, (position - 1) / 512);
                // 0x11c is reserved/extended flags on newer cards: never overwrite it as an Int64.
                Write64(output, 0x138, headerSize);
                var hash = Hash(output, root.Offset, headerSize, token); output.Position = 0x140; output.Write(hash);
                output.Flush(true);
            }
            VerifyGamecardHeader(temporary, token);
            token.ThrowIfCancellationRequested();
            File.Move(temporary, stagedPath, overwrite: true);
            return true;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
