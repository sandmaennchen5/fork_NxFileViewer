using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Emignatik.NxFileViewer.Models;
using Emignatik.NxFileViewer.Models.Overview;
using Emignatik.NxFileViewer.Models.TreeItems.Impl;
using ZstdSharp;

namespace Emignatik.NxFileViewer.FileLoading;

internal static class NupReader
{
    public static bool IsNup(string path) =>
        string.Equals(Path.GetExtension(path), ".nup", StringComparison.OrdinalIgnoreCase);

    public static NxFile Load(string path, CancellationToken token) => Read(path, token, null);

    public static void Verify(string path, CancellationToken token, Action<double> progress)
    {
        using var file = Read(path, token, progress);
    }

    private static NxFile Read(string path, CancellationToken token, Action<double>? progress)
    {
        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream);
        Require(reader.ReadUInt32() == 0x0150554e);
        var headerSize = reader.ReadUInt16();
        var version = reader.ReadUInt16();
        var tocOffset = reader.ReadUInt64();
        var tocSize = reader.ReadUInt64();
        var hash = reader.ReadBytes(32);
        var blobOffset = reader.ReadUInt64();
        var fileSize = reader.ReadUInt64();
        var packageCount = reader.ReadUInt32();
        Require(reader.ReadUInt32() == 0 && version == 1 && headerSize == 128 &&
            tocOffset == headerSize && fileSize == (ulong)stream.Length &&
            blobOffset <= fileSize && blobOffset % 16 == 0 &&
            tocOffset <= blobOffset && tocSize >= 48 && tocSize <= blobOffset - tocOffset &&
            packageCount > 0 && packageCount <= (tocSize - 48) / 64);

        stream.Position = (long)tocOffset;
        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[65536];
        for (var remaining = tocSize; remaining > 0;)
        {
            token.ThrowIfCancellationRequested();
            var count = (int)Math.Min(remaining, (ulong)buffer.Length);
            stream.ReadExactly(buffer.AsSpan(0, count));
            hasher.AppendData(buffer, 0, count);
            remaining -= (ulong)count;
        }
        Require(hasher.GetHashAndReset().AsSpan().SequenceEqual(hash));

        stream.Position = (long)tocOffset;
        Require(reader.ReadUInt32() == 0x01434f54 && reader.ReadUInt32() == packageCount);
        var directoryCount = reader.ReadUInt64();
        var fileCount = reader.ReadUInt64();
        var poolOffset = reader.ReadUInt64();
        var poolSize = reader.ReadUInt64();
        var tables = 48UL + packageCount * 64UL;
        Require(poolOffset >= tables && poolOffset <= tocSize && poolSize <= tocSize - poolOffset &&
            directoryCount <= (poolOffset - tables) / 24);
        var directories = tables;
        tables += directoryCount * 24;
        Require(fileCount <= (poolOffset - tables) / 80);

        var root = new NupFileItem(path);
        for (var index = 0u; index < packageCount; index++)
        {
            token.ThrowIfCancellationRequested();
            stream.Position = headerSize + 48L + index * 64L;
            var id = reader.ReadUInt64();
            var rootDirectory = reader.ReadUInt32();
            var exefs = reader.ReadUInt32();
            var romfs = reader.ReadUInt32();
            var rawVersion = reader.ReadUInt32();
            var kind = reader.ReadByte();
            var flags = reader.ReadByte();
            stream.Position += 6;
            var metaOffset = reader.ReadUInt64();
            var metaSize = reader.ReadUInt32();
            Require(kind <= 4 && (flags & ~1) == 0 && rootDirectory < directoryCount &&
                (exefs == uint.MaxValue || exefs < directoryCount) &&
                (romfs == uint.MaxValue || romfs < directoryCount));

            var package = new NupPackageItem(root, id.ToString("X16"), rawVersion, kind, rootDirectory);
            if (kind == 0 || root.PrimaryPackage == null)
                root.PrimaryPackage = package;
            if (metaSize == 0) continue;
            Require(metaOffset >= blobOffset && metaOffset <= fileSize && metaSize <= fileSize - metaOffset);
            stream.Position = (long)metaOffset;
            var end = stream.Position + metaSize;
            while (stream.Position < end)
            {
                token.ThrowIfCancellationRequested();
                Require(end - stream.Position >= 8);
                var type = reader.ReadUInt16();
                reader.ReadUInt16();
                var size = reader.ReadUInt32();
                Require(size <= end - stream.Position);
                var next = stream.Position + size;
                if (type == 3 && size <= 1024 * 1024)
                    package.Icon = reader.ReadBytes((int)size);
                else if (size <= 65536)
                {
                    switch (type)
                    {
                        case 4: package.DisplayVersion = Encoding.UTF8.GetString(reader.ReadBytes((int)size)); break;
                        case 5: package.Title = Encoding.UTF8.GetString(reader.ReadBytes((int)size)); break;
                        case 6: package.Publisher = Encoding.UTF8.GetString(reader.ReadBytes((int)size)); break;
                        case 7 when size == 32: package.BuildId = Convert.ToHexString(reader.ReadBytes(32)); break;
                    }
                }
                stream.Position = next;
            }
        }

        if (progress != null)
            VerifyFiles(stream, tocOffset + tables, fileCount, tocOffset + poolOffset, poolSize, blobOffset, token, progress);
        else
            NupContentTree.Attach(root, tocOffset, tocSize, hash, directories, tables, poolOffset, poolSize, directoryCount, fileCount);

        var overview = new FileOverview(root) { FileSize = stream.Length };
        return new NxFile(path, root, overview);
    }

    private static void VerifyFiles(FileStream stream, ulong table, ulong count, ulong pool, ulong poolSize,
        ulong blobOffset, CancellationToken token, Action<double> progress)
    {
        using var tableStream = File.OpenRead(stream.Name);
        using var reader = new BinaryReader(tableStream);
        tableStream.Position = (long)table;
        var length = (ulong)stream.Length;
        var buffer = new byte[1024 * 1024];
        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var checkedExtents = new Dictionary<ulong, (ulong Stored, ulong Size, byte Codec, string Hash)>();
        var lastProgress = 0d;
        void Report(double fraction)
        {
            if (fraction - lastProgress < 0.001) return;
            lastProgress = fraction;
            progress(fraction);
        }

        for (ulong index = 0; index < count; index++)
        {
            token.ThrowIfCancellationRequested();
            var nameOffset = reader.ReadUInt32();
            var nameSize = reader.ReadUInt32();
            var size = reader.ReadUInt64();
            var offset = reader.ReadUInt64();
            var stored = reader.ReadUInt64();
            var expectedHash = reader.ReadBytes(32);
            var codec = reader.ReadByte();
            var codecFlags = reader.ReadByte();
            reader.ReadBytes(14);
            Require(nameOffset <= poolSize && nameSize <= poolSize - nameOffset &&
                size <= long.MaxValue && codec <= 1 && codecFlags == 0 &&
                (codec != 0 || stored == size) &&
                (size == 0 ? offset == 0 && stored == 0 && codec == 0 :
                    stored > 0 && offset >= blobOffset && offset % 16 == 0 && offset <= length && stored <= length - offset));

            var extent = (stored, size, codec, Convert.ToHexString(expectedHash));
            if (size > 0 && checkedExtents.TryGetValue(offset, out var previous))
            {
                Require(extent == previous);
                Report((index + 1d) / count);
                continue;
            }

            stream.Position = (long)offset;
            using var bounded = new ExtentStream(stream, (long)stored);
            using var decoder = codec == 1 ? new DecompressionStream(bounded, leaveOpen: true) : null;
            Stream payload = decoder ?? (Stream)bounded;
            ulong processed = 0;
            try
            {
                while (processed < size)
                {
                    token.ThrowIfCancellationRequested();
                    var read = payload.Read(buffer, 0, (int)Math.Min((ulong)buffer.Length, size - processed));
                    if (read == 0) break;
                    hasher.AppendData(buffer, 0, read);
                    processed += (ulong)read;
                    Report((index + (double)processed / size) / count);
                }
                token.ThrowIfCancellationRequested();
                if (processed != size || payload.ReadByte() != -1 || bounded.Remaining != 0 ||
                    !hasher.GetHashAndReset().AsSpan().SequenceEqual(expectedHash))
                    throw new InvalidDataException("NUP payload size or SHA-256 mismatch.");
            }
            catch (Exception ex) when (ex is InvalidDataException or EndOfStreamException or ZstdException)
            {
                tableStream.Position = (long)(pool + nameOffset);
                var name = Encoding.UTF8.GetString(reader.ReadBytes((int)Math.Min(nameSize, 1024u)));
                throw new InvalidDataException($"NUP file #{index + 1} ({name}): {ex.Message}", ex);
            }
            if (size > 0) checkedExtents.Add(offset, extent);
            Report((index + 1d) / count);
        }
        token.ThrowIfCancellationRequested();
        progress(1);
    }

    private sealed class ExtentStream(Stream source, long length) : Stream
    {
        public long Remaining { get; private set; } = length;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length { get; } = length;
        public override long Position { get => Length - Remaining; set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            var read = source.Read(buffer[..(int)Math.Min(Remaining, buffer.Length)]);
            Remaining -= read;
            return read;
        }

        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private static void Require(bool valid)
    {
        if (!valid) throw new InvalidDataException("Invalid NUP header, table of contents or package metadata.");
    }
}
