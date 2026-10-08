using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace Emignatik.NxFileViewer.Services.Nand;

public sealed record NandExplorerPartition(string Name, long Offset, long Size);
public sealed record NandExplorerEntry(string Name, string Path, bool IsDirectory, uint Cluster, long Size)
{
    public string Title { get; init; } = "";
    public string TitleId { get; init; } = "";
    public string UserId { get; init; } = "";
    public bool IsSave { get; init; }
    public string Type => IsDirectory ? Localization.LocalizationManager.Instance.Current.Keys.Nand_ExplorerFolder : System.IO.Path.GetExtension(Name).TrimStart('.').ToUpperInvariant();
}

// This reader deliberately exposes no writes to the NAND source.
public sealed class NandExplorer : IDisposable
{
    private readonly List<(FileStream Stream, long Start, long Length)> _parts = new();
    private long _length;
    private NandExplorerPartition? _partition;
    private byte[]? _key;
    private readonly Dictionary<long, byte[]> _decryptedBlocks = new();
    private int _sectorSize, _clusterSize;
    private long _fat, _data;
    private uint _root, _clusterCount;
    private readonly CancellationToken _token;
    public static bool IsDevice(string path) => path.StartsWith(@"\\.\", StringComparison.Ordinal);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(SafeFileHandle handle, uint code, IntPtr input, uint inputSize,
        out long output, uint outputSize, out uint returned, IntPtr overlapped);

    public NandExplorer(string path, CancellationToken token = default)
    {
        _token = token;
        try
        {
            foreach (var part in IsDevice(path) ? new[] { path } : NandDetection.SplitFiles(path))
            {
                var stream = new FileStream(part, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096);
                long size;
                try
                {
                    if (IsDevice(part))
                    {
                        if (!DeviceIoControl(stream.SafeFileHandle, 0x7405c, IntPtr.Zero, 0, out size, 8, out _, IntPtr.Zero))
                            throw new IOException("Cannot read drive size.");
                    }
                    else size = stream.Length;
                }
                catch { stream.Dispose(); throw; }
                _parts.Add((stream, _length, size));
                _length = checked(_length + size);
            }
        }
        catch { Dispose(); throw; }
    }

    private byte[] ReadRaw(long offset, int count)
    {
        _token.ThrowIfCancellationRequested();
        if (offset < 0 || count < 0 || offset > _length - count) throw new InvalidDataException("NAND read exceeds source bounds.");
        var bytes = new byte[count];
        var written = 0;
        while (written < count)
        {
            var part = _parts.First(p => offset >= p.Start && offset < p.Start + p.Length);
            var take = (int)Math.Min(count - written, part.Length - (offset - part.Start));
            // Raw devices can require sector-aligned offsets and reads.
            var position = offset - part.Start;
            var aligned = position / 512 * 512;
            var skip = (int)(position - aligned);
            var length = (skip + take + 511) / 512 * 512;
            length = (int)Math.Min(length, part.Length - aligned);
            var buffer = new byte[length];
            part.Stream.Position = aligned;
            part.Stream.ReadExactly(buffer);
            buffer.AsSpan(skip, take).CopyTo(bytes.AsSpan(written));
            offset += take; written += take;
        }
        return bytes;
    }

    public IReadOnlyList<NandExplorerPartition> GetPartitions(string sourceName)
    {
        var bases = new List<long> { 0, 0x800000, 0x1800000 };
        var mbr = ReadRaw(0, 512);
        if (mbr[510] == 0x55 && mbr[511] == 0xaa)
            for (var i = 0; i < 4; i++)
            {
                var start = (long)BinaryPrimitives.ReadUInt32LittleEndian(mbr.AsSpan(446 + i * 16 + 8)) * 512;
                if (start > 0) bases.AddRange(new[] { start, start + 0x800000, start + 0x1800000 });
            }
        foreach (var start in bases.Distinct())
        {
            if (start > _length - 1024) continue;
            var header = ReadRaw(start + 512, 512);
            if (!header.AsSpan(0, 8).SequenceEqual("EFI PART"u8)) continue;
            var entries = BinaryPrimitives.ReadUInt64LittleEndian(header.AsSpan(72));
            var count = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(80));
            var stride = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(84));
            if (count is 0 or > 128 || stride is < 128 or > 1024 || entries > (ulong)(_length / 512)) continue;
            var partitions = new List<NandExplorerPartition>();
            for (var i = 0; i < count; i++)
            {
                var entry = ReadRaw(checked(start + (long)entries * 512 + i * stride), (int)stride);
                var name = Encoding.Unicode.GetString(entry, 56, 72).TrimEnd('\0');
                if (!NandCliPlugin.PartitionNames.Contains(name)) continue;
                var first = BinaryPrimitives.ReadUInt64LittleEndian(entry.AsSpan(32));
                var last = BinaryPrimitives.ReadUInt64LittleEndian(entry.AsSpan(40));
                if (last < first || last >= (ulong)((_length - start) / 512)) throw new InvalidDataException("Invalid NAND partition bounds.");
                partitions.Add(new(name, checked(start + (long)first * 512), checked((long)(last - first + 1) * 512)));
            }
            if (partitions.Any(p => p.Name == "SYSTEM") && partitions.Any(p => p.Name == "PRODINFO")) return partitions;
        }
        var single = Path.GetFileNameWithoutExtension(sourceName);
        if (single is "USER" or "SYSTEM" or "SAFE" or "PRODINFOF") return new[] { new NandExplorerPartition(single, 0, _length) };
        throw new InvalidDataException("No supported Switch NAND partition table found.");
    }

    public void OpenPartition(NandExplorerPartition partition, byte[]? key)
    {
        _partition = partition; _key = null; _decryptedBlocks.Clear();
        var boot = ReadPartition(0, 512);
        if (!LooksLikeFat(boot))
        {
            if (key?.Length != 32) throw new InvalidDataException("The partition is encrypted. Its BIS key is required.");
            _key = key;
            boot = ReadPartition(0, 512);
        }
        if (!LooksLikeFat(boot)) throw new InvalidDataException("No readable FAT32 filesystem. Check the BIS keys and partition.");
        _sectorSize = BinaryPrimitives.ReadUInt16LittleEndian(boot.AsSpan(11));
        _clusterSize = checked(_sectorSize * boot[13]);
        var reserved = BinaryPrimitives.ReadUInt16LittleEndian(boot.AsSpan(14));
        var fatSectors = BinaryPrimitives.ReadUInt32LittleEndian(boot.AsSpan(36));
        var sectors = BinaryPrimitives.ReadUInt32LittleEndian(boot.AsSpan(32));
        var dataSectors = (long)sectors - reserved - (long)boot[16] * fatSectors;
        if (reserved == 0 || fatSectors == 0 || dataSectors <= 0 || (long)sectors * _sectorSize > partition.Size)
            throw new InvalidDataException("Invalid FAT32 geometry.");
        _clusterCount = checked((uint)(dataSectors / boot[13]));
        _root = BinaryPrimitives.ReadUInt32LittleEndian(boot.AsSpan(44));
        var flags = BinaryPrimitives.ReadUInt16LittleEndian(boot.AsSpan(40));
        var activeFat = (flags & 0x80) == 0 ? 0 : flags & 0xf;
        if (activeFat >= boot[16] || (long)(_clusterCount + 2) * 4 > (long)fatSectors * _sectorSize)
            throw new InvalidDataException("Invalid FAT32 allocation table.");
        _fat = ((long)reserved + (long)activeFat * fatSectors) * _sectorSize;
        _data = ((long)reserved + (long)boot[16] * fatSectors) * _sectorSize;
    }

    private static bool LooksLikeFat(byte[] boot) => boot[510] == 0x55 && boot[511] == 0xaa &&
        BinaryPrimitives.ReadUInt16LittleEndian(boot.AsSpan(11)) is 512 or 1024 or 2048 or 4096 &&
        boot[13] is > 0 and <= 128 && (boot[13] & (boot[13] - 1)) == 0 && boot[16] is 1 or 2 &&
        BinaryPrimitives.ReadUInt16LittleEndian(boot.AsSpan(17)) == 0;

    private byte[] ReadPartition(long offset, int count)
    {
        var partition = _partition ?? throw new InvalidOperationException();
        if (offset < 0 || offset > partition.Size - count) throw new InvalidDataException("Read exceeds partition bounds.");
        if (_key == null) return ReadRaw(partition.Offset + offset, count);
        var result = new byte[count];
        for (var done = 0; done < count;)
        {
            var position = offset + done;
            var block = position / 0x4000;
            var within = (int)(position % 0x4000);
            if (!_decryptedBlocks.TryGetValue(block, out var decrypted))
            {
                var encrypted = ReadRaw(partition.Offset + block * 0x4000, 0x4000);
                decrypted = new byte[0x4000];
                var iv = new byte[16];
                // Switch BIS uses a big-endian 128-bit data-unit number.
                BinaryPrimitives.WriteInt64BigEndian(iv.AsSpan(8), block);
                LibHac.Crypto.Aes.DecryptXts128(encrypted, decrypted, _key.AsSpan(0, 16), _key.AsSpan(16, 16), iv);
                if (_decryptedBlocks.Count >= 8) _decryptedBlocks.Remove(_decryptedBlocks.Keys.First());
                _decryptedBlocks.Add(block, decrypted);
            }
            var take = Math.Min(count - done, 0x4000 - within);
            decrypted.AsSpan(within, take).CopyTo(result.AsSpan(done));
            done += take;
        }
        return result;
    }

    private IEnumerable<uint> Chain(uint first)
    {
        var visited = new HashSet<uint>();
        for (var cluster = first; cluster < 0x0ffffff8;)
        {
            _token.ThrowIfCancellationRequested();
            if (cluster < 2 || cluster >= (ulong)_clusterCount + 2 || !visited.Add(cluster))
                throw new InvalidDataException("Invalid or cyclic FAT cluster chain.");
            yield return cluster;
            cluster = BinaryPrimitives.ReadUInt32LittleEndian(ReadPartition(_fat + cluster * 4L, 4)) & 0x0fffffff;
        }
    }
    private long ClusterOffset(uint cluster) => checked(_data + (cluster - 2L) * _clusterSize);

    public IReadOnlyList<NandExplorerEntry> List(string path)
    {
        var cluster = _root;
        var current = "/";
        foreach (var segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            var entry = ReadDirectory(cluster, current).FirstOrDefault(e => e.IsDirectory && e.Name.Equals(segment, StringComparison.OrdinalIgnoreCase))
                ?? throw new DirectoryNotFoundException(path);
            cluster = entry.Cluster; current = entry.Path;
        }
        return ReadDirectory(cluster, current).OrderByDescending(e => e.IsDirectory).ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private IReadOnlyList<NandExplorerEntry> ReadDirectory(uint first, string path)
    {
        var entries = new List<NandExplorerEntry>();
        var longName = new SortedDictionary<int, string>();
        foreach (var cluster in Chain(first))
        {
            if (entries.Count > 100000) throw new InvalidDataException("Directory entry limit exceeded.");
            var bytes = ReadPartition(ClusterOffset(cluster), _clusterSize);
            for (var offset = 0; offset < bytes.Length; offset += 32)
            {
                var entry = bytes.AsSpan(offset, 32);
                if (entry[0] == 0) return entries;
                if (entry[0] == 0xe5) { longName.Clear(); continue; }
                if (entry[11] == 0x0f)
                {
                    if ((entry[0] & 0x40) != 0) longName.Clear();
                    var number = entry[0] & 0x1f;
                    if (number is < 1 or > 20) { longName.Clear(); continue; }
                    longName[number] = Encoding.Unicode.GetString(entry.Slice(1, 10)) + Encoding.Unicode.GetString(entry.Slice(14, 12)) + Encoding.Unicode.GetString(entry.Slice(28, 4));
                    continue;
                }
                var shortName = Encoding.ASCII.GetString(entry[..8]).TrimEnd();
                var extension = Encoding.ASCII.GetString(entry.Slice(8, 3)).TrimEnd();
                var name = longName.Count > 0 ? string.Concat(longName.Values).Split('\0')[0].TrimEnd('\uffff') : shortName + (extension.Length > 0 ? "." + extension : "");
                longName.Clear();
                if ((entry[11] & 8) != 0 || name is "." or ".." || name.Length == 0) continue;
                if (name.IndexOfAny(new[] { '/', '\\' }) >= 0) throw new InvalidDataException("Invalid FAT filename.");
                var child = ((uint)BinaryPrimitives.ReadUInt16LittleEndian(entry.Slice(20)) << 16) | BinaryPrimitives.ReadUInt16LittleEndian(entry.Slice(26));
                entries.Add(new(name, path.TrimEnd('/') + "/" + name, (entry[11] & 0x10) != 0, child,
                    BinaryPrimitives.ReadUInt32LittleEndian(entry.Slice(28))));
            }
        }
        return entries;
    }

    public byte[] ReadPrefix(NandExplorerEntry entry, int maximum)
    {
        if (entry.IsDirectory || maximum < 0) throw new ArgumentException("Invalid file prefix request.");
        var bytes = new byte[(int)Math.Min(entry.Size, maximum)];
        var done = 0;
        if (bytes.Length == 0) return bytes;
        foreach (var cluster in Chain(entry.Cluster))
        {
            var count = Math.Min(bytes.Length - done, _clusterSize);
            ReadPartition(ClusterOffset(cluster), count).CopyTo(bytes, done);
            done += count;
            if (done == bytes.Length) return bytes;
        }
        throw new InvalidDataException("Truncated FAT file chain.");
    }

    public Stream OpenFile(NandExplorerEntry entry)
    {
        if (entry.IsDirectory) throw new ArgumentException("Select a file.", nameof(entry));
        return new FatFileStream(this, entry);
    }

    private sealed class FatFileStream : Stream
    {
        private readonly NandExplorer _owner;
        private readonly long _length;
        private readonly IEnumerator<uint> _chain;
        private readonly List<uint> _clusters = new();
        private long _position;
        private bool _disposed;
        public FatFileStream(NandExplorer owner, NandExplorerEntry entry)
        { _owner = owner; _length = entry.Size; _chain = owner.Chain(entry.Cluster).GetEnumerator(); }
        public override bool CanRead => !_disposed;
        public override bool CanSeek => !_disposed;
        public override bool CanWrite => false;
        public override long Length => _length;
        public override long Position { get => _position; set => Seek(value, SeekOrigin.Begin); }
        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
        public override int Read(Span<byte> buffer)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var count = (int)Math.Min(buffer.Length, Math.Max(0, _length - _position));
            var done = 0;
            while (done < count)
            {
                _owner._token.ThrowIfCancellationRequested();
                var index = checked((int)(_position / _owner._clusterSize));
                while (_clusters.Count <= index)
                {
                    if (!_chain.MoveNext()) throw new InvalidDataException("Truncated FAT file chain.");
                    _clusters.Add(_chain.Current);
                }
                var within = (int)(_position % _owner._clusterSize);
                var take = Math.Min(count - done, _owner._clusterSize - within);
                _owner.ReadPartition(_owner.ClusterOffset(_clusters[index]) + within, take).CopyTo(buffer[done..]);
                done += take; _position += take;
            }
            return done;
        }
        public override long Seek(long offset, SeekOrigin origin)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var position = checked((origin switch { SeekOrigin.Begin => 0, SeekOrigin.Current => _position,
                SeekOrigin.End => _length, _ => throw new ArgumentOutOfRangeException(nameof(origin)) }) + offset);
            if (position < 0) throw new IOException("Negative file position.");
            return _position = position;
        }
        public override void Flush() { }
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing)
        { if (disposing) { _disposed = true; _chain.Dispose(); } base.Dispose(disposing); }
    }

    public void Export(NandExplorerEntry entry, Stream destination)
    {
        if (entry.IsDirectory) throw new InvalidOperationException("Select a file to export.");
        var remaining = entry.Size;
        if (remaining == 0) return;
        foreach (var cluster in Chain(entry.Cluster))
        {
            var count = (int)Math.Min(remaining, _clusterSize);
            destination.Write(ReadPartition(ClusterOffset(cluster), count));
            remaining -= count;
            if (remaining == 0) return;
        }
        throw new InvalidDataException("Truncated FAT file chain.");
    }
    public void Dispose() { foreach (var part in _parts) part.Stream.Dispose(); _parts.Clear(); }
}
