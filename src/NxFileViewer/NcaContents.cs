using System;
using System.Collections.Generic;
using System.IO;
using Path = System.IO.Path;
using System.Linq;
using System.Threading;
using Emignatik.NxFileViewer.FileLoading;
using Emignatik.NxFileViewer.FileLoading.QuickFileInfoLoading;
using Emignatik.NxFileViewer.Services.Mtp;
using Emignatik.NxFileViewer.Utils.LibHacExtensions;
using LibHac.Common.Keys;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.NSZ;
using LibHac.Tools.Es;
using LibHac.Tools.Fs;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;

namespace NxFileViewer.Avalonia;

internal sealed record NcaDirectoryEntry(string Name, string Path, long Size, bool Directory);

internal static class NcaContents
{
    internal sealed record Inspection(Dictionary<string, string> Properties, IReadOnlyList<Section> Sections);
    internal sealed record Section(int Index, Dictionary<string, string> Properties);

    internal static Inspection Inspect(string path, string keys, string? member, CancellationToken token) =>
        WithNca(path, keys, member, nca =>
        {
            var header = nca.Header;
            var properties = new Dictionary<string, string>
            {
                ["Format"] = Path.GetExtension(member ?? path).TrimStart('.').ToUpperInvariant(),
                ["HeaderSignatureValidity"] = nca.VerifyHeaderSignature().ToString(),
                ["ContentType"] = header.ContentType.ToString(), ["SdkVersion"] = header.SdkVersion.ToString(),
                ["DistributionType"] = header.DistributionType.ToString(), ["KeyGeneration"] = header.KeyGeneration.ToString(),
                ["ContentIndex"] = header.ContentIndex.ToString(), ["FormatVersion"] = header.FormatVersion.ToString(),
                ["IsEncrypted"] = header.IsEncrypted.ToString(), ["IsNca0"] = header.IsNca0().ToString()
            };
            var sections = new List<Section>();
            for (var index = 0; index < NcaExtension.MaxSections; index++)
            {
                token.ThrowIfCancellationRequested();
                if (!header.IsSectionEnabled(index)) continue;
                var fs = nca.GetFsHeader(index);
                sections.Add(new Section(index, new Dictionary<string, string>
                {
                    ["Format"] = fs.FormatType.ToString(), ["SectionIndex"] = index.ToString(),
                    ["NcaSectionType"] = Nca.TryGetSectionTypeFromIndex(index, header.ContentType, out var section) ? section.ToString() : "",
                    ["EncryptionType"] = fs.EncryptionType.ToString(), ["HashType"] = fs.HashType.ToString(),
                    ["Version"] = fs.Version.ToString()
                }));
            }
            return new Inspection(properties, sections);
        });

    internal static Dictionary<string, string> ReadTicket(string path, string member) =>
        WithSource<Dictionary<string, string>>(path, member, (source, _) =>
        {
            if (source.Length is <= 0 or > 1048576) throw new InvalidDataException("Invalid ticket size.");
            var ticket = new Ticket(source);
            return new()
            {
                ["Format"] = "Ticket", ["RightsId"] = new LibHac.Fs.RightsId(ticket.RightsId).ToString(),
                ["Issuer"] = ticket.Issuer, ["AccountId"] = ticket.AccountId.ToString("X8"),
                ["DeviceId"] = ticket.DeviceId.ToString("X16"), ["TicketId"] = ticket.TicketId.ToString("X16"),
                ["CryptoType"] = ticket.CryptoType.ToString(), ["FormatVersion"] = ticket.FormatVersion.ToString(),
                ["TitleKeyType"] = ticket.TitleKeyType.ToString(), ["LicenseType"] = ticket.LicenseType.ToString(),
                ["TicketVersion"] = ticket.TicketVersion.ToString(), ["PropertyMask"] = ticket.PropertyMask.ToString()
            };
        });

    internal static IReadOnlyList<NcaDirectoryEntry> ReadDirectory(string path, string keys, string? member,
        int section, string directory, CancellationToken token) =>
        WithFileSystem<IReadOnlyList<NcaDirectoryEntry>>(path, keys, member, section, fs =>
        {
            var results = new List<NcaDirectoryEntry>();
            foreach (var entry in fs.EnumerateEntries(directory, "*"))
            {
                token.ThrowIfCancellationRequested();
                results.Add(new(entry.Name, entry.FullPath, entry.Size, entry.Type == DirectoryEntryType.Directory));
            }
            return results;
        });

    internal static void ExportRaw(string path, string? member, string destination, bool reconstruct, CancellationToken token)
    {
        if (member != null)
        {
            PackageFile.ExtractEntry(path, member, destination, token, reconstruct);
            return;
        }
        WithSource(path, null, (source, _) =>
        {
            using var original = reconstruct ? Ncz.OpenOriginalStream(source) : null;
            SaveStream(original ?? source, destination, token);
            return true;
        });
    }

    internal static void ExportPlaintext(string path, string keys, string? member, string destination, CancellationToken token) =>
        WithNca(path, keys, member, nca =>
        {
            using var storage = nca.OpenDecryptedNca();
            using var stream = storage.AsStream();
            SaveStream(stream, destination, token);
            return true;
        });

    internal static void ExportFile(string path, string keys, string? member, int section, string entryPath,
        string destination, CancellationToken token) =>
        WithFileSystem(path, keys, member, section, fs =>
        {
            var parent = entryPath[..Math.Max(1, entryPath.LastIndexOf('/'))];
            var entry = fs.EnumerateEntries(parent, "*").SingleOrDefault(item => item.FullPath == entryPath && item.Type == DirectoryEntryType.File)
                ?? throw new FileNotFoundException(entryPath);
            using var file = fs.LoadFile(entry);
            using var stream = file.AsStream();
            SaveStream(stream, destination, token);
            return true;
        });

    internal static void ExportDirectory(string path, string keys, string? member, int section, string directory,
        string parent, string name, CancellationToken token) =>
        WithFileSystem(path, keys, member, section, fs =>
        {
            var stage = Path.Combine(parent, ".nxfv-content-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stage);
            try
            {
                var root = Path.GetFullPath(stage) + Path.DirectorySeparatorChar;
                var prefix = directory.TrimEnd('/') + "/";
                foreach (var entry in fs.EnumerateEntries(directory, "*", SearchOptions.RecurseSubdirectories))
                {
                    token.ThrowIfCancellationRequested();
                    if (!entry.FullPath.StartsWith(prefix, StringComparison.Ordinal)) throw new InvalidDataException(entry.FullPath);
                    var relative = entry.FullPath[prefix.Length..];
                    if (relative.Split('/').Any(part => part is "" or "." or ".." || part.Contains('\\') || part.Contains(':')))
                        throw new InvalidDataException(entry.FullPath);
                    var target = Path.GetFullPath(Path.Combine(stage, relative));
                    if (!target.StartsWith(root, StringComparison.Ordinal)) throw new InvalidDataException(entry.FullPath);
                    if (entry.Type == DirectoryEntryType.Directory) Directory.CreateDirectory(target);
                    else
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                        using var file = fs.LoadFile(entry);
                        using var stream = file.AsStream();
                        using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write);
                        stream.CopyToAsync(output, token).GetAwaiter().GetResult();
                    }
                }
                token.ThrowIfCancellationRequested();
                Directory.Move(stage, Path.Combine(parent, name));
            }
            finally { if (Directory.Exists(stage)) Directory.Delete(stage, recursive: true); }
            return true;
        });

    private static void SaveStream(Stream input, string destination, CancellationToken token)
    {
        var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write))
                input.CopyToAsync(output, token).GetAwaiter().GetResult();
            token.ThrowIfCancellationRequested();
            File.Move(temporary, destination, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static T WithFileSystem<T>(string path, string keys, string? member, int section, Func<IFileSystem, T> read) =>
        WithNca(path, keys, member, nca =>
        {
            using var fs = nca.OpenFileSystem(section, IntegrityCheckLevel.ErrorOnInvalid);
            return read(fs);
        });

    private static T WithNca<T>(string path, string keysPath, string? member, Func<Nca, T> read)
    {
        var keys = PackageFile.LoadKeys(keysPath);
        if (member == null && Nax0Contents.IsNax0(path))
            return Nax0Contents.Read(path, keys, source => read(new Nca(keys, source.AsStorage())));

        return WithSource(path, member, (source, partition) =>
        {
            if (partition != null && PackageFile.InjectTicketKeys) PackageMetadataReader.ImportTicketKeys(partition, keys);
            using var original = Path.GetExtension(member ?? path).Equals(".ncz", StringComparison.OrdinalIgnoreCase)
                ? Ncz.OpenOriginalStream(source) : null;
            return read(new Nca(keys, (original ?? source).AsStorage()));
        });
    }

    private static T WithSource<T>(string path, string? entryName, Func<Stream, IFileSystem?, T> read)
    {
        using var source = MtpDirectSource.OpenRead(path);
        if (entryName == null) return read(source, null);
        var type = PackageSignature.Read(source);
        using var storage = MtpDirectSource.OpenStorage(path);
        if (type == PackageType.NSP)
        {
            using var partition = storage.LoadPartition();
            return ReadEntry(partition, entryName, read);
        }
        if (type == PackageType.XCI)
        {
            var gameCard = new Xci(new KeySet(), storage);
            if (!gameCard.HasPartition(XciPartitionType.Secure)) throw new InvalidDataException("The secure partition is missing.");
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

