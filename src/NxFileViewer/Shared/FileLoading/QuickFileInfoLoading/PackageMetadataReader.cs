using System;
using System.IO;
using Path = System.IO.Path;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Emignatik.NxFileViewer.Utils;
using Emignatik.NxFileViewer.Utils.LibHacExtensions;
using LibHac.Common.Keys;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Tools.Fs;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;
using LibHac.Tools.Npdm;
using LibHac.Loader;
using LibHac.NSZ;
using Emignatik.NxFileViewer.Models.Overview;
using Emignatik.NxFileViewer.Services.Security;
using LibHac.Spl;
using LibHac.Tools.Es;
using ContentType = LibHac.Ncm.ContentType;

namespace Emignatik.NxFileViewer.FileLoading.QuickFileInfoLoading;

internal static class PackageMetadataReader
{
    internal static List<Content> Read(IFileSystem partition, KeySet keySet, out bool containsNcz, bool injectTicketKeys = false, bool readDetails = false,
        Action<RightsId, AccessKey>? saveTicketKey = null, Action<MissingKeyException>? missingKey = null, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if (injectTicketKeys) ImportTicketKeys(partition, keySet, saveTicketKey, token);
        containsNcz = partition.EnumerateEntries()
            .Where(e => e.Type == DirectoryEntryType.File)
            .Any(entry => entry.Name.EndsWith(".ncz", StringComparison.OrdinalIgnoreCase));

        return ReadContents(partition, partition.EnumerateEntries().Where(entry => entry.Type == DirectoryEntryType.File).ToArray(),
            partition.FindCnmtEntries().SelectMany(entry =>
            {
                token.ThrowIfCancellationRequested();
                return partition.LoadCnmts(entry, keySet);
            }), keySet, readDetails, missingKey, token);
    }

    internal static List<Content> ReadContents(IFileSystem partition, IReadOnlyList<DirectoryEntryEx> entries,
        IEnumerable<LibHac.Tools.Ncm.Cnmt> metadata, KeySet keySet, bool readDetails, Action<MissingKeyException>? missingKey, CancellationToken token = default)
    {
        var contents = new List<Content>();
        foreach (var cnmt in metadata)
        {
            token.ThrowIfCancellationRequested();
            var content = new Content(cnmt);
            contents.Add(content);

            var ncaControlEntry = cnmt.ContentEntries.FirstOrDefault(entry => entry.Type == ContentType.Control);

            if (ncaControlEntry != null)
            {
                var ncaId = ncaControlEntry.NcaId.ToStrId();

                try
                {
                    ReadNca(partition, entries, ncaId, keySet, nca =>
                    {
                        if (nca.LoadNacp() is not { } nacp) return;
                        content.NacpData = new NacpData(nacp);
                        if (readDetails) LoadIcons(nca, content.NacpData, token);
                    });
                }
                catch (Exception ex) when (ex is IOException or LibHac.Common.LibHacException or InvalidDataException or NczFormatException or ZstdSharp.ZstdException)
                { if (ex is MissingKeyException missing) missingKey?.Invoke(missing); content.DetailsError = ex.Message; }
            }
            var program = cnmt.ContentEntries.FirstOrDefault(entry => entry.Type == ContentType.Program);
            if (program != null && readDetails)
            {
                try { ReadNca(partition, entries, program.NcaId.ToStrId(), keySet, nca => LoadProgram(nca, content, keySet, token)); }
                catch (Exception ex) when (ex is IOException or LibHac.Common.LibHacException or InvalidDataException or NczFormatException or ZstdSharp.ZstdException)
                { if (ex is MissingKeyException missing) missingKey?.Invoke(missing); content.DetailsError = ex.Message; }
            }
        }

        return contents;
    }

    internal static void ImportTicketKeys(IFileSystem partition, KeySet keys, Action<RightsId, AccessKey>? saveTicketKey = null, CancellationToken token = default)
    {
        foreach (var entry in partition.EnumerateEntries().Where(entry => entry.Type == DirectoryEntryType.File &&
            entry.Name.EndsWith(".tik", StringComparison.OrdinalIgnoreCase)))
        {
            token.ThrowIfCancellationRequested();
            if (entry.Size <= 0 || entry.Size > 1024 * 1024) throw new InvalidDataException("Invalid ticket size.");
            using var file = partition.LoadFile(entry);
            using var input = file.AsStream();
            using var memory = new MemoryStream();
            input.CopyTo(memory);
            memory.Position = 0;
            var ticket = new Ticket(memory);
            var id = new RightsId(ticket.RightsId);
            var access = new AccessKey(ticket.TitleKeyBlock);
            if (keys.ExternalKeySet.Get(id, out _) == LibHac.Result.Success)
                keys.ExternalKeySet.Remove(id);
            keys.ExternalKeySet.Add(id, access).ThrowIfFailure();
            saveTicketKey?.Invoke(id, access);
        }
    }

    private static void ReadNca(IFileSystem partition, IReadOnlyList<DirectoryEntryEx> entries, string id, KeySet keys, Action<Nca> read)
    {
        var entry = entries.FirstOrDefault(entry => entry.Name.StartsWith(id + ".", StringComparison.OrdinalIgnoreCase));
        if (entry == null) return;
        using var file = partition.LoadFile(entry);
        using var stream = file.AsStream();
        using var original = entry.Name.EndsWith(".ncz", StringComparison.OrdinalIgnoreCase) ? Ncz.OpenOriginalStream(stream) : null;
        read(new Nca(keys, (original ?? stream).AsStorage()));
    }

    private static void LoadIcons(Nca nca, NacpData data, CancellationToken token)
    {
        for (var section = 0; section < NcaExtension.MaxSections; section++)
        {
            if (!nca.Header.IsSectionEnabled(section)) continue;
            using var fs = nca.OpenFileSystem(section, IntegrityCheckLevel.ErrorOnInvalid);
            foreach (var entry in fs.EnumerateEntries("/", "icon_*.dat", SearchOptions.RecurseSubdirectories))
            {
                token.ThrowIfCancellationRequested();
                if (entry.Type != DirectoryEntryType.File || entry.Size <= 0 || entry.Size > 4 * 1024 * 1024) continue;
                var language = Path.GetFileNameWithoutExtension(entry.Name)[5..];
                if (!Enum.TryParse<NacpLanguage>(language, out var value) || (uint)value >= data.Titles.Count) continue;
                var title = data.Titles[(int)value];
                if (title == null) continue;
                using var file = fs.LoadFile(entry);
                using var stream = file.AsStream();
                var icon = new byte[(int)entry.Size];
                stream.ReadExactly(icon);
                title.Icon = icon;
            }
        }
    }

    private static void LoadProgram(Nca nca, Content content, KeySet keys, CancellationToken token)
    {
        content.KeyGeneration = nca.Header.KeyGeneration;
        content.Distribution = nca.Header.DistributionType.ToString();
        for (var section = 0; section < NcaExtension.MaxSections; section++)
        {
            if (!nca.Header.IsSectionEnabled(section)) continue;
            if (!Nca.TryGetSectionTypeFromIndex(section, nca.Header.ContentType, out var type) || type != NcaSectionType.Code) continue;
            using var fs = nca.OpenFileSystem(section, IntegrityCheckLevel.ErrorOnInvalid);
            foreach (var entry in fs.EnumerateEntries("/", "*"))
            {
                token.ThrowIfCancellationRequested();
                if (entry.Type != DirectoryEntryType.File) continue;
                if (entry.Name == "main")
                {
                    using var file = fs.LoadFile(entry);
                    var reader = new NsoReader();
                    reader.Initialize(file).ThrowIfFailure();
                    content.BuildId = reader.Header.ModuleId.Items.ToStrId();
                }
                else if (entry.Name == "main.npdm")
                {
                    using var file = fs.LoadFile(entry);
                    using var stream = file.AsStream();
                    var npdm = new NpdmBinary(stream, keys);
                    content.FileSystemPermissions = npdm.AciD.FsAccess.PermissionsBitmask;
                    content.AcidSignature = npdm.AciD.SignatureValidity.ToString();
                    var services = npdm.AciD.ServiceAccess?.Services?.Select(service => service.Item1) ?? Enumerable.Empty<string>();
                    content.SecurityLevel = ProgramPermissionAnalyzer.Analyze(content.FileSystemPermissions.Value,
                        ProgramPermissionAnalyzer.HasFileSystemServices(services)).ToString();
                }
            }
        }
    }
}

