using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using Emignatik.NxFileViewer.FileLoading;
using Emignatik.NxFileViewer.FileLoading.QuickFileInfoLoading;
using Emignatik.NxFileViewer.Services.Integrity;
using Emignatik.NxFileViewer.Utils.LibHacExtensions;
using LibHac.Common;
using LibHac.Common.Keys;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.NSZ;
using LibHac.Tools.Fs;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;

namespace NxFileViewer.Avalonia;

internal sealed record PackageEntry(string Name, long Size)
{
    public override string ToString() => Name;
}

internal sealed record NcaHashResult(string Name, bool? Matches, Validity? Signature = null, string? Error = null);

internal sealed record PackageFile(string Path, string Type, long Size, IReadOnlyList<PackageEntry> Entries, IReadOnlyList<Content> Titles, bool NeedsProdKeys, string? MetadataError)
{
    internal static PackageFile Read(string path, string? keysPath) => Read(path, keysPath, null, default);

    internal static PackageFile Read(string path, string? keysPath, List<NcaHashResult>? hashes, CancellationToken token)
    {
        using var stream = File.OpenRead(path);
        var type = PackageSignature.Read(stream);
        var (entries, titles, needsProdKeys, metadataError) = type switch
        {
            PackageType.NSP => ReadNsp(path, keysPath, hashes, token),
            PackageType.XCI => ReadXci(path, keysPath, hashes, token),
            PackageType.NRO => EmptyPartition,
            _ => throw new InvalidDataException("Unknown Nintendo Switch file format.")
        };
        var compressed = entries.Any(entry => entry.Name.EndsWith(".ncz", StringComparison.OrdinalIgnoreCase));
        var label = (type, compressed) switch
        {
            (PackageType.NSP, true) => "NSZ",
            (PackageType.XCI, true) => "XCZ",
            _ => type.ToString()
        };
        return new PackageFile(path, label, stream.Length, entries, titles, needsProdKeys, metadataError);
    }

    private static PartitionContents ReadNsp(string path, string? keysPath, List<NcaHashResult>? hashes, CancellationToken token)
    {
        using var storage = new LocalStorage(path, FileAccess.Read);
        using var partition = storage.LoadPartition();
        return ReadPartition(partition, keysPath, hashes, token);
    }

    private static PartitionContents ReadXci(string path, string? keysPath, List<NcaHashResult>? hashes, CancellationToken token)
    {
        using var file = new LocalFile(path, OpenMode.Read);
        using var storage = new FileStorage(file);
        var gameCard = new Xci(new KeySet(), storage);
        if (!gameCard.HasPartition(XciPartitionType.Secure)) return EmptyPartition;
        using var partition = gameCard.OpenPartition(XciPartitionType.Secure);
        return ReadPartition(partition, keysPath, hashes, token);
    }

    private sealed record PartitionContents(IReadOnlyList<PackageEntry> Entries, IReadOnlyList<Content> Titles, bool NeedsProdKeys = false, string? MetadataError = null);

    private static readonly PartitionContents EmptyPartition = new(Array.Empty<PackageEntry>(), Array.Empty<Content>());

    private static PartitionContents ReadPartition(IFileSystem partition, string? keysPath, List<NcaHashResult>? hashes, CancellationToken token)
    {
        if (hashes != null)
            CheckNcaHashes(partition, hashes, keysPath, token);

        var entries = ListFiles(partition);
        if (!partition.FindCnmtEntries().Any())
            return new PartitionContents(entries, Array.Empty<Content>());

        if (keysPath == null)
            return new PartitionContents(entries, Array.Empty<Content>(), NeedsProdKeys: true);

        try
        {
            var keys = LoadKeys(keysPath);
            return new PartitionContents(entries, PackageMetadataReader.Read(partition, keys, out _));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or LibHac.Common.LibHacException)
        {
            return new PartitionContents(entries, Array.Empty<Content>(), MetadataError: ex.Message);
        }
    }

    private static KeySet LoadKeys(string keysPath)
    {
        var keys = KeySet.CreateDefaultKeySet();
        ExternalKeyReader.ReadKeyFile(keys, keysPath, null, null, null);
        return keys;
    }

    private static void CheckNcaHashes(IFileSystem partition, List<NcaHashResult> results, string? keysPath, CancellationToken token)
    {
        var keys = keysPath == null ? null : LoadKeys(keysPath);
        foreach (var entry in partition.EnumerateEntries().Where(entry => entry.Type == DirectoryEntryType.File))
        {
            token.ThrowIfCancellationRequested();
            var extension = System.IO.Path.GetExtension(entry.Name);
            var compressed = extension.Equals(".ncz", StringComparison.OrdinalIgnoreCase);
            if (!compressed && !extension.Equals(".nca", StringComparison.OrdinalIgnoreCase))
                continue;

            var id = entry.Name.Split('.', 2)[0];
            bool? matches = null;
            try
            {
                using var file = partition.LoadFile(entry);
                using var source = file.AsStream();
                using var reconstructed = compressed ? Ncz.OpenOriginalStream(source) : null;
                var stream = reconstructed ?? source;
                if (id.Length == 32 && id.All(Uri.IsHexDigit))
                {
                    var hash = SHA256.HashDataAsync(stream, token).AsTask().GetAwaiter().GetResult();
                    matches = NcaHashPrefix.Matches(hash, Convert.FromHexString(id));
                }
                token.ThrowIfCancellationRequested();
                Validity? signature = null;
                if (keys != null)
                {
                    stream.Position = 0;
                    var nca = new Nca(keys, stream.AsStorage());
                    signature = nca.VerifyHeaderSignature();
                }
                results.Add(new NcaHashResult(entry.Name, matches, signature));
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or LibHacException or NczFormatException)
            {
                results.Add(new NcaHashResult(entry.Name, matches, Error: ex.Message));
            }
        }
    }

    private static PackageEntry[] ListFiles(IFileSystem partition) => partition.EnumerateEntries()
        .Where(entry => entry.Type == DirectoryEntryType.File)
        .Select(entry => new PackageEntry(entry.Name, entry.Size))
        .ToArray();
}
