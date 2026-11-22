using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Emignatik.NxFileViewer.FileLoading;
using Emignatik.NxFileViewer.FileLoading.QuickFileInfoLoading;
using LibHac.FsSystem;
using System.Threading;
using Emignatik.NxFileViewer.Utils.LibHacExtensions;
using LibHac.Common.Keys;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.NSZ;
using LibHac.Tools.Fs;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;

namespace NxFileViewer.Avalonia;

internal sealed record NcaContentEntry(int Section, string Path, long Size)
{
    public override string ToString() => $"{Section} · {Path} · {Size:N0} B";
}

internal static class NcaContents
{
    internal static IReadOnlyDictionary<string, string> Properties(string path, string keysPath, string packageEntry)
    {
        return WithSource<IReadOnlyDictionary<string, string>>(path, packageEntry, (source, partition) =>
        {
            var keys = KeySet.CreateDefaultKeySet();
            ExternalKeyReader.ReadKeyFile(keys, keysPath, null, null, null);
            if (partition != null) PackageMetadataReader.ImportTicketKeys(partition, keys);
            using var original = packageEntry.EndsWith(".ncz", StringComparison.OrdinalIgnoreCase) ? Ncz.OpenOriginalStream(source) : null;
            var nca = new Nca(keys, (original ?? source).AsStorage());
            var h = nca.Header;
            return new Dictionary<string, string> { ["Format"] = "Nca", ["HeaderSignatureValidity"] = nca.VerifyHeaderSignature().ToString(), ["ContentType"] = h.ContentType.ToString(), ["SdkVersion"] = h.SdkVersion.ToString(), ["DistributionType"] = h.DistributionType.ToString(), ["KeyGeneration"] = h.KeyGeneration.ToString(), ["ContentIndex"] = h.ContentIndex.ToString(), ["FormatVersion"] = h.FormatVersion.ToString(), ["IsEncrypted"] = h.IsEncrypted.ToString(), ["IsNca0"] = h.IsNca0().ToString() };
        });
    }
    internal static void Extract(string path, string keysPath, NcaContentEntry entry, string destination, CancellationToken token, string? packageEntry = null)
    {
        if (string.Equals(System.IO.Path.GetFullPath(path), System.IO.Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase))
            throw new IOException("The destination is the source file.");
        WithSource(path, packageEntry, (source, partition) =>
            {
            var keys = KeySet.CreateDefaultKeySet();
            ExternalKeyReader.ReadKeyFile(keys, keysPath, null, null, null);
            if (partition != null) PackageMetadataReader.ImportTicketKeys(partition, keys);
            using var original = System.IO.Path.GetExtension(packageEntry ?? path).Equals(".ncz", StringComparison.OrdinalIgnoreCase)
                ? Ncz.OpenOriginalStream(source) : null;
            var nca = new Nca(keys, (original ?? source).AsStorage());
            if (entry.Section < 0 || entry.Section >= NcaExtension.MaxSections || !nca.Header.IsSectionEnabled(entry.Section))
                throw new InvalidDataException("The NCA section is unavailable.");
            using var fileSystem = nca.OpenFileSystem(entry.Section, IntegrityCheckLevel.ErrorOnInvalid);
            var found = System.Linq.Enumerable.SingleOrDefault(
                fileSystem.EnumerateEntries("/", "*", SearchOptions.RecurseSubdirectories),
                item => item.Type == DirectoryEntryType.File && item.FullPath == entry.Path)
                ?? throw new FileNotFoundException(entry.Path);
            using var file = fileSystem.LoadFile(found);
            using var input = file.AsStream();
            var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    input.CopyToAsync(output, token).GetAwaiter().GetResult();
                    if (output.Length != found.Size) throw new InvalidDataException("The NCA entry is truncated.");
                }
                token.ThrowIfCancellationRequested();
                File.Move(temporary, destination, overwrite: true);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            return true;
        });
    }

    internal static IReadOnlyList<NcaContentEntry> Read(string path, string keysPath, CancellationToken token, string? packageEntry = null)
    {
        return WithSource<IReadOnlyList<NcaContentEntry>>(path, packageEntry, (source, partition) =>
            {
            var keys = KeySet.CreateDefaultKeySet();
            ExternalKeyReader.ReadKeyFile(keys, keysPath, null, null, null);
            if (partition != null) PackageMetadataReader.ImportTicketKeys(partition, keys);
            using var original = System.IO.Path.GetExtension(packageEntry ?? path).Equals(".ncz", StringComparison.OrdinalIgnoreCase)
                ? Ncz.OpenOriginalStream(source) : null;
            var nca = new Nca(keys, (original ?? source).AsStorage());
            var results = new List<NcaContentEntry>();
            for (var index = 0; index < NcaExtension.MaxSections; index++)
            {
                token.ThrowIfCancellationRequested();
                if (!nca.Header.IsSectionEnabled(index)) continue;
                using var fileSystem = nca.OpenFileSystem(index, IntegrityCheckLevel.ErrorOnInvalid);
                foreach (var entry in fileSystem.EnumerateEntries("/", "*", SearchOptions.RecurseSubdirectories))
                {
                    token.ThrowIfCancellationRequested();
                    if (entry.Type == DirectoryEntryType.File)
                        results.Add(new NcaContentEntry(index, entry.FullPath, entry.Size));
                }
            }
            return results;
        });
    }

    private static T WithSource<T>(string path, string? entryName, Func<Stream, IFileSystem?, T> read)
    {
        using var source = File.OpenRead(path);
        if (entryName == null) return read(source, null);
        var type = PackageSignature.Read(source);
        using var storage = new LocalStorage(path, FileAccess.Read);
        if (type == PackageType.NSP)
        {
            using var partition = storage.LoadPartition();
            return ReadEntry(partition, entryName, read);
        }
        if (type == PackageType.XCI)
        {
            var gameCard = new Xci(new KeySet(), storage);
            if (!gameCard.HasPartition(XciPartitionType.Secure))
                throw new InvalidDataException("The secure partition is missing.");
            using var partition = gameCard.OpenPartition(XciPartitionType.Secure);
            return ReadEntry(partition, entryName, read);
        }
        throw new InvalidDataException("Unsupported package format.");
    }

    private static T ReadEntry<T>(IFileSystem partition, string name, Func<Stream, IFileSystem?, T> read)
    {
        var entry = partition.EnumerateEntries().SingleOrDefault(item => item.Type == DirectoryEntryType.File && item.Name == name)
            ?? throw new FileNotFoundException(name);
        using var file = partition.LoadFile(entry);
        using var stream = file.AsStream();
        return read(stream, partition);
    }
}
