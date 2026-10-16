using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Emignatik.NxFileViewer.Services.Nsz;

/// <summary>Checks all XCI partitions and corrects NSZ's known NCZ size overcount in staged outputs only.</summary>
public static partial class XciPartitionIntegrity
{
    private sealed record Entry(string Name, long Offset, long Size, long Record, long HashOffset, int HashSize);
    private sealed record DirectoryInfo(long Offset, long HeaderSize, Entry[] Entries);
    private sealed record Change(Entry Entry, long Offset, long Size, long DataBase);
    private static long Read64(Stream input, long position)
    {
        Span<byte> bytes = stackalloc byte[8]; input.Position = position; input.ReadExactly(bytes);
        var value = BinaryPrimitives.ReadUInt64LittleEndian(bytes);
        return value <= long.MaxValue ? (long)value : throw new InvalidDataException("XCI offset exceeds supported range.");
    }
    private static void Range(Stream input, long offset, long size, long end)
    {
        if (offset < 0 || size < 0 || end > input.Length || offset > end || size > end - offset)
            throw new InvalidDataException("XCI entry is outside its container.");
    }
    private static DirectoryInfo ReadDirectory(Stream input, long offset, long end, bool allowFinalPadding = false, bool allowEntryOverflow = false)
    {
        Range(input, offset, 16, end);
        var header = new byte[16]; input.Position = offset; input.ReadExactly(header);
        if (!header.AsSpan(0, 4).SequenceEqual("HFS0"u8)) throw new InvalidDataException("Invalid XCI partition table.");
        var count = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(4));
        var stringSize = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(8));
        if (count < 0 || count > 100000 || stringSize < 0 || stringSize > 16 * 1024 * 1024)
            throw new InvalidDataException("Invalid XCI partition table size.");
        var length = checked(16L + count * 64L + stringSize); Range(input, offset, length, end);
        var table = new byte[count * 64]; var strings = new byte[stringSize]; input.ReadExactly(table); input.ReadExactly(strings);
        var entries = new List<Entry>(); var names = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < count; i++)
        {
            var row = table.AsSpan(i * 64, 64);
            var nameOffset = BinaryPrimitives.ReadInt32LittleEndian(row[16..]);
            if (nameOffset < 0 || nameOffset >= strings.Length) throw new InvalidDataException("Invalid XCI filename.");
            var tail = strings.AsSpan(nameOffset); var terminator = tail.IndexOf((byte)0);
            if (terminator < 0) throw new InvalidDataException("Unterminated XCI filename.");
            var name = Encoding.UTF8.GetString(tail[..terminator]);
            if (!names.Add(name)) throw new InvalidDataException("Duplicate XCI filename.");
            var relative = BinaryPrimitives.ReadInt64LittleEndian(row);
            var size = BinaryPrimitives.ReadInt64LittleEndian(row[8..]);
            if (relative < 0) throw new InvalidDataException("Negative HFS0 entry offset.");
            var position = checked(offset + length + relative);
            // Compressed XCZ containers may omit the final sector's padding only.
            if (allowFinalPadding && position <= end && size > end - position && size - (end - position) < 0x200)
                size = end - position;
            if (allowEntryOverflow)
            {
                if (position < 0 || size < 0) throw new InvalidDataException("Invalid staged XCI entry extent.");
                _ = checked(position + size); // Broken NSZ directory slots may extend past their partition.
            }
            else Range(input, position, size, end);
            var hashSize = BinaryPrimitives.ReadInt32LittleEndian(row[20..]);
            var hashOffset = BinaryPrimitives.ReadInt64LittleEndian(row[24..]);
            if (hashSize < 0 || hashOffset < 0 || hashOffset > size || hashSize > size - hashOffset)
                throw new InvalidDataException("Invalid HFS0 hash range.");
            entries.Add(new(name, position, size, offset + 16 + i * 64L, hashOffset, hashSize));
        }
        return new(offset, length, entries.ToArray());
    }
    private static DirectoryInfo Root(Stream input, bool compressed = false)
    {
        Range(input, 0, 0x160, input.Length);
        Span<byte> magic = stackalloc byte[4]; input.Position = 0x100; input.ReadExactly(magic);
        if (!magic.SequenceEqual("HEAD"u8)) throw new InvalidDataException("Invalid XCI header.");
        return ReadDirectory(input, Read64(input, 0x130), input.Length, compressed);
    }
    private static byte[] Hash(Stream input, long offset, long size, CancellationToken token)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[1024 * 1024]; input.Position = offset;
        while (size > 0)
        {
            token.ThrowIfCancellationRequested();
            var count = input.Read(buffer, 0, (int)Math.Min(size, buffer.Length));
            if (count == 0) throw new EndOfStreamException();
            hash.AppendData(buffer, 0, count); size -= count;
        }
        return hash.GetHashAndReset();
    }
    private static void CheckNca(Stream input, Entry entry, string partition, CancellationToken token)
    {
        if (!entry.Name.EndsWith(".nca", StringComparison.OrdinalIgnoreCase)) return;
        var id = entry.Name.Split('.')[0];
        if (id.Length != 32 || !id.All(Uri.IsHexDigit) || !Convert.ToHexString(Hash(input, entry.Offset, entry.Size, token)).StartsWith(id, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"XCI NCA hash mismatch: {partition}/{entry.Name}");
    }
    public static void Verify(string path, CancellationToken token)
    {
        using var input = File.OpenRead(path);
        foreach (var partition in Root(input).Entries)
        {
            token.ThrowIfCancellationRequested();
            foreach (var entry in ReadDirectory(input, partition.Offset, checked(partition.Offset + partition.Size)).Entries)
                CheckNca(input, entry, partition.Name, token);
        }
    }
    private static (long Correct, long Legacy) NczSize(Stream input, Entry entry)
    {
        Range(input, entry.Offset, 0x4010, checked(entry.Offset + entry.Size));
        input.Position = entry.Offset + 0x4000;
        using var reader = new BinaryReader(input, Encoding.UTF8, true);
        if (!reader.ReadBytes(8).AsSpan().SequenceEqual("NCZSECTN"u8)) throw new InvalidDataException("Invalid NCZ section header.");
        var count = reader.ReadInt64();
        if (count < 1 || count > 100000 || count > (entry.Size - 0x4010) / 64) throw new InvalidDataException("Invalid NCZ section count.");
        long correct = 0x4000, legacy = 0x4000;
        for (long i = 0; i < count; i++)
        {
            var offset = reader.ReadInt64(); var size = reader.ReadInt64();
            if (offset < 0 || size < 0) throw new InvalidDataException("Invalid NCZ section extent.");
            correct = Math.Max(correct, checked(offset + size)); legacy = checked(legacy + size);
            input.Position += 48;
        }
        return (correct, legacy);
    }
    private static void Write64(Stream output, long offset, long value)
    {
        Span<byte> bytes = stackalloc byte[8]; BinaryPrimitives.WriteInt64LittleEndian(bytes, value);
        output.Position = offset; output.Write(bytes);
    }
    private static void RefreshHash(Stream output, Entry entry, CancellationToken token)
    {
        if (entry.HashSize == 0) return;
        var hash = Hash(output, checked(entry.Offset + entry.HashOffset), entry.HashSize, token);
        output.Position = entry.Record + 32; output.Write(hash);
    }
    private static (List<Change> Changes, List<(Entry, DirectoryInfo)> Directories, DirectoryInfo Root) BuildLayoutPlan(
        Stream source, Stream output, CancellationToken token, bool verifyAllNcas = false)
    {
        var sourcePartitions = Root(source, compressed: true).Entries.ToDictionary(e => e.Name, StringComparer.Ordinal);
        var root = Root(output); var changes = new List<Change>(); var directories = new List<(Entry, DirectoryInfo)>();
        if (sourcePartitions.Count != root.Entries.Length) throw new InvalidDataException("XCI partitions changed during decompression.");
        foreach (var partition in root.Entries)
        {
            token.ThrowIfCancellationRequested();
            if (!sourcePartitions.TryGetValue(partition.Name, out var original)) throw new InvalidDataException("Unexpected output partition.");
            var originalEntries = ReadDirectory(source, original.Offset, checked(original.Offset + original.Size), allowFinalPadding: true).Entries
                .ToDictionary(e => e.Name.EndsWith(".ncz", StringComparison.OrdinalIgnoreCase) ? e.Name[..^4] + ".nca" : e.Name, StringComparer.Ordinal);
            var directory = ReadDirectory(output, partition.Offset, checked(partition.Offset + partition.Size), allowEntryOverflow: true);
            if (directory.Entries.Length != originalEntries.Count) throw new InvalidDataException("XCI partition contents changed during decompression.");
            long excess = 0; var planned = new List<Change>();
            foreach (var entry in directory.Entries)
            {
                if (!originalEntries.TryGetValue(entry.Name, out var compressed)) throw new InvalidDataException("Unexpected output file.");
                var size = compressed.Size;
                if (compressed.Name.EndsWith(".ncz", StringComparison.OrdinalIgnoreCase))
                {
                    var lengths = NczSize(source, compressed); size = lengths.Correct;
                    if (entry.Size != size && entry.Size != lengths.Legacy) throw new InvalidDataException("Unexpected decompressed NCZ size.");
                }
                else if (entry.Size != size) throw new InvalidDataException("Uncompressed file size changed.");
                if (entry.Size < size) throw new InvalidDataException("Output entry is smaller than the original NCA.");
                var offset = checked(entry.Offset - excess);
                if (offset < directory.Offset + directory.HeaderSize) throw new InvalidDataException("Invalid corrected HFS0 entry.");
                Range(output, offset, size, checked(partition.Offset + partition.Size));
                planned.Add(new(entry, offset, size, directory.Offset + directory.HeaderSize)); excess = checked(excess + entry.Size - size);
            }
            // Extra files (tickets, logo files, etc.) must be byte-identical in every partition.
            foreach (var change in planned.Where(c => !c.Entry.Name.EndsWith(".nca", StringComparison.OrdinalIgnoreCase)))
            {
                var originalEntry = originalEntries[change.Entry.Name];
                if (!Hash(source, originalEntry.Offset, originalEntry.Size, token).AsSpan().SequenceEqual(Hash(output, change.Offset, change.Size, token)))
                    throw new InvalidDataException("Non-NCA data changed during decompression.");
            }
            var changed = planned.Any(c => c.Offset != c.Entry.Offset || c.Size != c.Entry.Size);
            // Read-only inspection can validate unchanged partitions as well before offering a repair.
            if (changed || verifyAllNcas)
                foreach (var change in planned)
                    CheckNca(output, change.Entry with { Offset = change.Offset, Size = change.Size }, partition.Name, token);
            if (!changed) continue;
            changes.AddRange(planned); directories.Add((partition, directory));
        }
        return (changes, directories, root);
    }

    public static int CorrectStagedLayout(string sourceXcz, string stagedXci, CancellationToken token)
    {
        using var source = File.OpenRead(sourceXcz);
        using var output = new FileStream(stagedXci, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var (changes, directories, root) = BuildLayoutPlan(source, output, token);
        foreach (var change in changes)
        {
            token.ThrowIfCancellationRequested();
            Write64(output, change.Entry.Record, change.Offset - change.DataBase);
            Write64(output, change.Entry.Record + 8, change.Size);
            RefreshHash(output, change.Entry with { Offset = change.Offset, Size = change.Size }, token);
        }
        if (changes.Count > 0)
        {
            foreach (var (partition, _) in directories) RefreshHash(output, partition, token);
            var rootHashSize = Read64(output, 0x138); Range(output, root.Offset, rootHashSize, output.Length);
            var hash = Hash(output, root.Offset, rootHashSize, token); output.Position = 0x140; output.Write(hash);
            output.Flush(true);
        }
        return changes.Count(c => c.Offset != c.Entry.Offset || c.Size != c.Entry.Size);
    }
}
