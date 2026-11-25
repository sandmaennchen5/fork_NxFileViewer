using LibHac.Common.Keys;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Ns;
using LibHac.Tools.Fs;
using LibHac.Tools.FsSystem.NcaUtils;
using LibHac.Tools.FsSystem;
using LibHac.Tools.Ncm;
using System.Collections.Generic;
using System;
using System.IO;
using System.Linq;
using LibHac.Fs.Fsa;

namespace Emignatik.NxFileViewer.Utils.LibHacExtensions;

public static class IFileSystemExtension
{
    public static IEnumerable<DirectoryEntryEx> FindCnmtEntries(this IFileSystem fileSystem)
    {
        foreach (var fileEntry in fileSystem.EnumerateEntries().Where(e => e.Type == DirectoryEntryType.File))
        {
            var fileName = fileEntry.Name;
            if (fileName.EndsWith("cnmt.nca", StringComparison.OrdinalIgnoreCase))
                yield return fileEntry;
        }
    }

    public static Nca? LoadNca(this IFileSystem fileSystem, string ncaId, KeySet keySet)
    {
        var partitionFileEntry = fileSystem.EnumerateEntries()
            .Where(e => e.Type == DirectoryEntryType.File)
            .FirstOrDefault(entry => entry.Name.StartsWith(ncaId + ".", StringComparison.OrdinalIgnoreCase));

        if (partitionFileEntry == null)
            return null;

        var ncaFile = fileSystem.LoadFile(partitionFileEntry);

        return new Nca(keySet, new FileStorage(ncaFile));
    }

    public static IEnumerable<Cnmt> LoadCnmts(this IFileSystem fileSystem, KeySet keySet) =>
        fileSystem.FindCnmtEntries().SelectMany(entry => fileSystem.LoadCnmts(entry, keySet));

    public static IEnumerable<Cnmt> LoadCnmts(this IFileSystem fileSystem, DirectoryEntryEx entry, KeySet keySet)
    {
        using var file = fileSystem.LoadFile(entry);
        using var stream = file.AsStream();
        using var storage = stream.AsStorage();
        var nca = new Nca(keySet, storage);
        if (nca.Header.ContentType != NcaContentType.Meta) return [];

        var contents = new List<Cnmt>();
        for (var index = 0; index < NcaExtension.MaxSections; index++)
        {
            if (!nca.CanOpenSection(index)) continue;
            using var section = nca.OpenFileSystem(index, IntegrityCheckLevel.ErrorOnInvalid);
            foreach (var cnmt in section.EnumerateEntries("/", "*.cnmt", SearchOptions.RecurseSubdirectories))
            {
                if (cnmt.Type != DirectoryEntryType.File) continue;
                using var cnmtFile = section.LoadFile(cnmt);
                using var input = cnmtFile.AsStream();
                contents.Add(new Cnmt(input));
            }
        }
        return contents;
    }

    /// <summary>
    /// Loads control.nacp file contained in the specified NCA.
    /// Transparently handles the compressed title block format introduced in newer Switch firmware,
    /// where the flag byte at offset 0x3215 signals that the first 0x3000 bytes hold a
    /// raw-DEFLATE-compressed blob rather than plain NacpLanguageEntry structs.
    /// </summary>
    public static ApplicationControlProperty? LoadNacp(this IFileSystem fileSystem, string ncaId, KeySet keySet)
    {
        var nca = fileSystem.LoadNca(ncaId, keySet);
        return nca?.LoadNacp();
    }

    public static ApplicationControlProperty? LoadNacp(this Nca nca)
    {
        for (var index = 0; index < NcaExtension.MaxSections; index++)
        {
            if (!nca.Header.IsSectionEnabled(index)) continue;
            using var section = nca.OpenFileSystem(index, IntegrityCheckLevel.ErrorOnInvalid);
            var entry = section.EnumerateEntries("/", "control.nacp", SearchOptions.RecurseSubdirectories | SearchOptions.CaseInsensitive)
                .FirstOrDefault(candidate => candidate.Type == DirectoryEntryType.File);
            if (entry == null) continue;
            using var file = section.LoadFile(entry);
            return ReadNacp(file);
        }
        return null;
    }

    private static ApplicationControlProperty ReadNacp(IFile nacpFile)
    {
        // ── Read raw bytes ────────────────────────────────────────────────────────────────────
        // We can't read directly into blitStruct.ByteSpan because we may need to decompress
        // the title block first.  Read into a temporary buffer instead.
        nacpFile.GetSize(out long nacpFileSize).ThrowIfFailure();

        byte[] rawBytes = new byte[nacpFileSize];
        nacpFile.Read(out _, 0, rawBytes, ReadOption.None).ThrowIfFailure();

        // ── Decompress title block if the new firmware format flag is set ─────────────────────
        // New format: flag byte at 0x3215 != 0 means the first 0x3000 bytes contain
        //   u16 LE  compressed_size
        //   u8[]    compressed_data[compressed_size]   (raw DEFLATE, wbits = -15)
        // Legacy format: first 0x3000 bytes are 16 × NacpLanguageEntry structs verbatim.
        // DecompressIfNeeded returns a buffer with the same layout as the legacy format so that
        // LibHac's ApplicationControlProperty struct can be populated without any further changes.
        byte[] nacpBytes;
        try
        {
            nacpBytes = NacpTitleBlockDecompressor.DecompressIfNeeded(rawBytes);
        }
        catch (InvalidDataException)
        {
            // Decompression failed — fall back to raw bytes so callers can still read metadata.
            nacpBytes = rawBytes;
        }

        // ── Populate LibHac struct ────────────────────────────────────────────────────────────
        var blitStruct = new BlitStruct<ApplicationControlProperty>(1);

        // Copy only as many bytes as the struct can hold (handles both exact-size and oversized
        // decompressed buffers safely).
        int copyLen = Math.Min(nacpBytes.Length, blitStruct.ByteSpan.Length);
        nacpBytes.AsSpan(0, copyLen).CopyTo(blitStruct.ByteSpan);

        return blitStruct.Value;
    }

    public static IFile LoadFile(this IFileSystem fileSystem, DirectoryEntryEx directoryEntryEx, OpenMode openMode = OpenMode.Read)
    {
        using var uniqueRefFile = new UniqueRef<IFile>();
        fileSystem.OpenFile(ref uniqueRefFile.Ref, directoryEntryEx.FullPath.ToU8Span(), openMode).ThrowIfFailure();
        return uniqueRefFile.Release();
    }
}
