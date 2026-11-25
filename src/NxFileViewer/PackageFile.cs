using System;
using Emignatik.NxFileViewer.Services.PackageAnalysis;
using Emignatik.NxFileViewer.Services.Mtp;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using Emignatik.NxFileViewer.FileLoading;
using Emignatik.NxFileViewer.FileLoading.QuickFileInfoLoading;
using Emignatik.NxFileViewer.Services.Integrity;
using Emignatik.NxFileViewer.Services.KeysManagement;
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
    public bool IsRoot { get; init; }
    public IReadOnlyList<PackageEntry> Children { get; init; } = Array.Empty<PackageEntry>();
    public string IconPath => IsRoot ? "/Icons/NspItem.png" :
        Name.EndsWith(".nca", StringComparison.OrdinalIgnoreCase) || Name.EndsWith(".ncz", StringComparison.OrdinalIgnoreCase)
            ? "/Icons/PartitionFileEntryItem.png" : "/Icons/PartitionFileEntryItem.png";
    public global::Avalonia.Media.IImage Icon => IconCache.Get(IconPath);
    public override string ToString() => Name;
}

internal sealed record NcaHashResult(string Name, bool? Matches, Validity? Signature = null, string? Error = null, bool Missing = false);

internal sealed record PackageFile(string Path, string Type, long Size, IReadOnlyList<PackageEntry> Entries, IReadOnlyList<Content> Titles, bool NeedsProdKeys, string? MetadataError, string? HeaderDetails = null)
{
    internal string? Compression { get; init; }
    internal long? UncompressedSize { get; init; }
    internal string? SystemUpdateVersion { get; init; }
    internal PackageStructure Structure { get; init; }
    internal static IReadOnlyDictionary<string, string> ContentDigests(string path, bool secureOnly, CancellationToken token)
    {
        var digests = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using var storage = MtpDirectSource.OpenStorage(path);
        using var input = MtpDirectSource.OpenRead(path);
        void Add(IFileSystem partition, string prefix)
        {
            foreach (var entry in partition.EnumerateEntries().Where(entry => entry.Type == DirectoryEntryType.File))
            {
                token.ThrowIfCancellationRequested();
                using var file = partition.LoadFile(entry);
                using var stream = file.AsStream();
                var compressed = entry.Name.EndsWith(".ncz", StringComparison.OrdinalIgnoreCase);
                using var original = compressed ? Ncz.OpenOriginalStream(stream) : null;
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                var content = original ?? stream; var buffer = new byte[1024 * 1024]; int count;
                while ((count = content.Read(buffer, 0, buffer.Length)) > 0) { token.ThrowIfCancellationRequested(); hash.AppendData(buffer, 0, count); }
                var name = compressed ? entry.Name[..^4] + ".nca" : entry.Name;
                digests.Add(prefix + name, Convert.ToHexString(hash.GetHashAndReset()));
            }
        }
        if (PackageSignature.Read(input) == PackageType.NSP) { using var partition = storage.LoadPartition(); Add(partition, ""); }
        else
        {
            var gameCard = new Xci(new KeySet(), storage);
            foreach (var kind in new[] { XciPartitionType.Update, XciPartitionType.Normal, XciPartitionType.Secure, XciPartitionType.Logo })
            {
                if (!gameCard.HasPartition(kind) || secureOnly && kind != XciPartitionType.Secure) continue;
                using var partition = gameCard.OpenPartition(kind);
                Add(partition, kind == XciPartitionType.Secure ? "" : kind + "/");
            }
        }
        return digests;
    }
    internal static void ExtractEntry(string source, string entryName, string destination, CancellationToken token) =>
        ExtractEntry(source, entryName, destination, token, false);

    internal static void ExtractEntry(string source, string entryName, string destination, CancellationToken token, bool reconstructNcz)
    {
        if (string.Equals(System.IO.Path.GetFullPath(source), System.IO.Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase))
            throw new IOException("The destination is the source package.");
        using var input = MtpDirectSource.OpenRead(source);
        var type = PackageSignature.Read(input);
        using var storage = MtpDirectSource.OpenStorage(source);
        if (type == PackageType.NSP)
        {
            using var partition = storage.LoadPartition();
            CopyEntry(partition, entryName, destination, token, reconstructNcz);
        }
        else if (type == PackageType.XCI)
        {
            var gameCard = new Xci(new KeySet(), storage);
            if (!gameCard.HasPartition(XciPartitionType.Secure))
                throw new InvalidDataException("The secure partition is missing.");
            using var partition = gameCard.OpenPartition(XciPartitionType.Secure);
            CopyEntry(partition, entryName, destination, token, reconstructNcz);
        }
        else
            throw new InvalidDataException("Unsupported package format.");
    }

    private static void CopyEntry(IFileSystem partition, string name, string destination, CancellationToken token, bool reconstructNcz)
    {
        var entry = partition.EnumerateEntries().SingleOrDefault(entry => entry.Type == DirectoryEntryType.File && entry.Name == name)
            ?? throw new FileNotFoundException(name);
        var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var file = partition.LoadFile(entry))
            using (var stream = file.AsStream())
            using (var original = reconstructNcz && name.EndsWith(".ncz", StringComparison.OrdinalIgnoreCase) ? Ncz.OpenOriginalStream(stream) : null)
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                (original ?? stream).CopyToAsync(output, token).GetAwaiter().GetResult();
                if (output.Length != (original?.Length ?? entry.Size)) throw new InvalidDataException("The package entry is truncated.");
            }
            token.ThrowIfCancellationRequested();
            File.Move(temporary, destination, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    internal static void ExtractAll(string source, string parentDirectory, bool reconstructNcz, CancellationToken token)
    {
        var destination = System.IO.Path.Combine(parentDirectory, System.IO.Path.GetFileNameWithoutExtension(source));
        if (Directory.Exists(destination) || File.Exists(destination))
            throw new IOException("The output folder already exists: " + destination);
        var stage = System.IO.Path.Combine(parentDirectory, ".nx-extract-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        try
        {
            var package = Read(source, null);
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in package.Entries)
            {
                token.ThrowIfCancellationRequested();
                if (entry.Name != System.IO.Path.GetFileName(entry.Name) || entry.Name.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0 ||
                    entry.Name is "." or ".." || entry.Name.EndsWith('.') || entry.Name.EndsWith(' '))
                    throw new InvalidDataException("Unsafe package entry name.");
                var name = reconstructNcz && entry.Name.EndsWith(".ncz", StringComparison.OrdinalIgnoreCase)
                    ? System.IO.Path.ChangeExtension(entry.Name, ".nca") : entry.Name;
                if (!names.Add(name)) throw new InvalidDataException("Duplicate output name: " + name);
                ExtractEntry(source, entry.Name, System.IO.Path.Combine(stage, name), token, reconstructNcz);
            }
            token.ThrowIfCancellationRequested();
            Directory.Move(stage, destination);
        }
        finally
        {
            var root = System.IO.Path.GetFullPath(parentDirectory);
            var resolvedStage = System.IO.Path.GetFullPath(stage);
            if (Directory.Exists(resolvedStage) && System.IO.Path.GetDirectoryName(resolvedStage) == System.IO.Path.TrimEndingDirectorySeparator(root) &&
                System.IO.Path.GetFileName(resolvedStage).StartsWith(".nx-extract-", StringComparison.Ordinal))
                Directory.Delete(resolvedStage, recursive: true);
        }
    }

    internal static PackageFile Read(string path, string? keysPath) =>
        Read(path, keysPath, null, default);

    internal static PackageFile Read(string path, string? keysPath, List<NcaHashResult>? hashes, CancellationToken token, bool includeDetails = true,
        Action<string, int, int, bool>? hashProgress = null, Action<double>? nupProgress = null)
    {
        token.ThrowIfCancellationRequested();
        var package = ReadPackage(path, keysPath, hashes, token, includeDetails, hashProgress, nupProgress);
        if (!includeDetails) return package;
        if (package.Type is not ("NSZ" or "XCZ" or "NCZ")) return package;
        try
        {
            if (package.Type == "NCZ")
            {
                using var stream = MtpDirectSource.OpenRead(path);
                var header = NczHeader.Read(stream);
                return package with { Compression = header.IsUsingBlockCompression ? "Block" : "Blockless", UncompressedSize = header.NcaSize };
            }
            using var storage = MtpDirectSource.OpenStorage(path);
            using IFileSystem partition = package.Type == "NSZ" ? storage.LoadPartition() :
                new Xci(new KeySet(), storage).OpenPartition(XciPartitionType.Secure);
            var blockless = false;
            var originalSize = package.Size;
            foreach (var entry in partition.EnumerateEntries().Where(entry => entry.Type == DirectoryEntryType.File &&
                entry.Name.EndsWith(".ncz", StringComparison.OrdinalIgnoreCase)))
            {
                using var file = partition.LoadFile(entry);
                using var stream = file.AsStream();
                token.ThrowIfCancellationRequested();
                var header = NczHeader.Read(stream);
                blockless |= !header.IsUsingBlockCompression;
                originalSize = checked(originalSize + header.NcaSize - entry.Size);
            }
            return package with { Compression = blockless ? "Blockless" : "Block", UncompressedSize = originalSize };
        }
        catch (Exception error) when (error is IOException or InvalidDataException or NczFormatException or LibHacException or OverflowException)
        {
            return package;
        }
    }

    private static PackageFile ReadPackage(string path, string? keysPath, List<NcaHashResult>? hashes, CancellationToken token, bool includeDetails,
        Action<string, int, int, bool>? hashProgress, Action<double>? nupProgress)
    {
        if (Directory.Exists(System.IO.Path.Combine(path, "registered")))
            return ReadSdContents(path, keysPath, hashes, token, includeDetails, hashProgress);
        if (Nax0Contents.IsNax0(path))
        {
            var name = System.IO.Path.GetFileName(Nax0Contents.LogicalPath(path));
            var size = new FileInfo(path).Length;
            if (keysPath == null)
            {
                hashes?.Add(new NcaHashResult(name, null, null,
                    Emignatik.NxFileViewer.Localization.LocalizationManager.Instance.Current.Keys.WarnNoProdKeysFileFound));
                return new PackageFile(path, "NAX0", size, [], [], true, null) { MissingKeys = ["prod.keys"] };
            }
            var keys = LoadKeys(keysPath);
            try
            {
                return Nax0Contents.Read(path, keys, stream =>
                    ReadStandaloneContent(path, name, "NAX0", stream.Length, stream, () => keys, hashes, token, hashProgress));
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or LibHacException)
            {
                hashes?.Add(new NcaHashResult(name, null, null, ex.Message));
                return new PackageFile(path, "NAX0", size, [], [], false, ex.Message);
            }
        }
        if (NupReader.IsNup(path))
        {
            using var nup = NupReader.ReadRoot(path, token, out var size, hashes == null ? null : nupProgress ?? (_ => { }));
            var details = nup.ChildItems.OfType<Emignatik.NxFileViewer.Models.TreeItems.Impl.NupPackageItem>()
                .Select(item => string.Join(" · ", new[] { item.Format, item.TitleId, "v" + item.Version, item.Title, item.Publisher }
                    .Where(value => !string.IsNullOrWhiteSpace(value))));
            return new PackageFile(path, "NUP", size, Array.Empty<PackageEntry>(), Array.Empty<Content>(), false, null,
                string.Join(Environment.NewLine, details));
        }
        using var stream = MtpDirectSource.OpenRead(path);
        var type = PackageSignature.Read(stream);
        var extension = System.IO.Path.GetExtension(path);
        if (type == PackageType.UNKNOWN && (extension.Equals(".nca", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".ncz", StringComparison.OrdinalIgnoreCase)))
            return ReadStandalone(path, keysPath, hashes, token, hashProgress);
        var (entries, titles, needsProdKeys, metadataError, missingKeys, systemUpdateVersion, xciStructure) = type switch
        {
            PackageType.NSP => ReadNsp(path, keysPath, hashes, token, includeDetails, hashProgress),
            PackageType.XCI => ReadXci(path, keysPath, hashes, token, includeDetails, hashProgress),
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
        return new PackageFile(path, label, stream.Length, entries, titles, needsProdKeys, metadataError)
        {
            MissingKeys = missingKeys ?? (needsProdKeys ? ["prod.keys"] : []),
            SystemUpdateVersion = systemUpdateVersion,
            Structure = type switch
            {
                PackageType.NSP => PackageStructureAnalyzer.AnalyzeNspNames(entries.Select(entry => entry.Name)),
                PackageType.XCI => xciStructure,
                PackageType.NRO => PackageStructure.Homebrew,
                _ => PackageStructure.Unknown
            },
            Homebrew = type == PackageType.NRO
                ? new Emignatik.NxFileViewer.Models.TreeItems.Impl.NroFileItem(path, token) : null
        };
    }

    private static PackageFile ReadSdContents(string path, string? keysPath, List<NcaHashResult>? hashes, CancellationToken token,
        bool includeDetails, Action<string, int, int, bool>? progress)
    {
        var entries = Nax0Contents.Enumerate(path, token).ToArray();
        var package = new PackageFile(path, "NAX0", entries.Sum(entry => entry.Size),
            entries.Select(entry => new PackageEntry(entry.FullPath.TrimStart('/'), entry.Size)).ToArray(), [], keysPath == null, null)
            { Structure = PackageStructure.Filesystem, Compression = "None" };
        if (entries.Length == 0) return package with { NeedsProdKeys = false };
        if (keysPath == null) return package with { MissingKeys = ["prod.keys"] };

        var missingKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var errors = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            var keys = LoadKeys(keysPath);
            var physicalPaths = entries.Select(entry => System.IO.Path.Combine(path, entry.FullPath.TrimStart('/'))).ToArray();
            var chunkSizes = physicalPaths.Where(file => File.Exists(System.IO.Path.Combine(file, "01")))
                .Select(file => new FileInfo(System.IO.Path.Combine(file, "00")).Length).Distinct().ToArray();
            if (chunkSizes.Length > 1 || chunkSizes.Any(size => size <= 0))
                throw new InvalidDataException("Inconsistent SD split-file chunk sizes.");
            var plain = File.Exists(System.IO.Path.Combine(path, ".nxfileviewer-mtp")) && !physicalPaths.Any(Nax0Contents.IsNax0);
            using var fs = Nax0Contents.OpenFileSystem(path, keys, chunkSizes.FirstOrDefault(0xffff0000L), plain);

            IEnumerable<LibHac.Tools.Ncm.Cnmt> ReadMetadata()
            {
                foreach (var entry in entries)
                {
                    token.ThrowIfCancellationRequested();
                    IEnumerable<LibHac.Tools.Ncm.Cnmt> contents;
                    try { contents = fs.LoadCnmts(entry, keys); }
                    catch (Exception error) when (error is IOException or InvalidDataException or LibHacException)
                    {
                        if (error is MissingKeyException missing) missingKeys.Add(missing.Name);
                        else errors.Add(error.Message);
                        continue;
                    }
                    foreach (var content in contents) yield return content;
                }
            }

            var titles = PackageMetadataReader.ReadContents(fs, entries, ReadMetadata(), keys, includeDetails,
                missing => missingKeys.Add(missing.Name), token);
            if (hashes != null) CheckNcaHashes(fs, hashes, keys, entries, token, progress);
            package = package with { Titles = titles };
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or LibHacException)
        {
            if (error is MissingKeyException missing) missingKeys.Add(missing.Name);
            errors.Add(error.Message);
        }
        return package with { MissingKeys = missingKeys.ToArray(), MetadataError = errors.Count == 0 ? null : string.Join(Environment.NewLine, errors) };
    }

    private static PackageFile ReadStandalone(string path, string? keysPath, List<NcaHashResult>? hashes, CancellationToken token, Action<string, int, int, bool>? progress)
    {
        using var source = MtpDirectSource.OpenRead(path);
        var compressed = System.IO.Path.GetExtension(path).Equals(".ncz", StringComparison.OrdinalIgnoreCase);
        if (compressed && hashes == null) EnsureCompressionAllowed(source);
        using var original = compressed ? Ncz.OpenOriginalStream(source) : null;
        var name = System.IO.Path.GetFileName(path);
        return ReadStandaloneContent(path, name, compressed ? "NCZ" : "NCA", source.Length, original ?? source,
            keysPath == null ? null : () => LoadKeys(keysPath), hashes, token, progress);
    }

    private static PackageFile ReadStandaloneContent(string path, string name, string type, long size, Stream stream,
        Func<KeySet>? loadKeys, List<NcaHashResult>? hashes, CancellationToken token, Action<string, int, int, bool>? progress)
    {
        bool? matches = null;
        Validity? signature = null;
        string? details = null;
        string? error = null;
        string? missingKey = null;
        if (hashes != null)
        {
            progress?.Invoke(name, 1, 1, false);
            var id = name.Split('.', 2)[0];
            if (id.Length == 32 && id.All(Uri.IsHexDigit))
            {
                var hash = SHA256.HashDataAsync(stream, token).AsTask().GetAwaiter().GetResult();
                matches = NcaHashPrefix.Matches(hash, Convert.FromHexString(id));
            }
        }
        token.ThrowIfCancellationRequested();
        if (loadKeys != null)
        {
            try
            {
                stream.Position = 0;
                var nca = new Nca(loadKeys(), stream.AsStorage());
                details = $"{nca.Header.TitleId:X16} · {nca.Header.ContentType} · Key generation {nca.Header.KeyGeneration}";
                if (hashes != null) signature = nca.VerifyHeaderSignature();
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or LibHacException)
            {
                error = ex.Message;
                missingKey = (ex as MissingKeyException)?.Name;
            }
        }
        hashes?.Add(new NcaHashResult(name, matches, signature, error));
        if (hashes != null) progress?.Invoke(name, 1, 1, true);
        return new PackageFile(path, type, size, [], [], loadKeys == null, error, details)
        { MissingKeys = loadKeys == null ? ["prod.keys"] : missingKey == null ? [] : [missingKey] };
    }

    private static PartitionContents ReadNsp(string path, string? keysPath, List<NcaHashResult>? hashes, CancellationToken token, bool includeDetails, Action<string, int, int, bool>? hashProgress)
    {
        using var storage = MtpDirectSource.OpenStorage(path);
        using var partition = storage.LoadPartition();
        return ReadPartition(partition, keysPath, hashes, token, includeDetails, hashProgress);
    }

    private static PartitionContents ReadXci(string path, string? keysPath, List<NcaHashResult>? hashes, CancellationToken token, bool includeDetails, Action<string, int, int, bool>? hashProgress)
    {
        using var storage = MtpDirectSource.OpenStorage(path);
        var gameCard = new Xci(new KeySet(), storage);
        var structure = PackageStructureAnalyzer.AnalyzeXciPartitions(
            Enum.GetValues<XciPartitionType>().Where(gameCard.HasPartition));
        if (!gameCard.HasPartition(XciPartitionType.Secure)) return EmptyPartition with { Structure = structure };
        using var partition = gameCard.OpenPartition(XciPartitionType.Secure);
        var result = ReadPartition(partition, keysPath, hashes, token, includeDetails, hashProgress) with { Structure = structure };
        if (!includeDetails || keysPath == null || !gameCard.HasPartition(XciPartitionType.Update)) return result;
        try
        {
            token.ThrowIfCancellationRequested();
            using var update = gameCard.OpenPartition(XciPartitionType.Update);
            var metadata = update.LoadCnmts(LoadKeys(keysPath)).FirstOrDefault(cnmt => cnmt.Type == LibHac.Ncm.ContentMetaType.SystemUpdate);
            return result with { SystemUpdateVersion = metadata?.TitleVersion.ToString() };
        }
        catch (Exception error) when (error is IOException or InvalidDataException or LibHacException or NczFormatException)
        { return result; }
    }

    public string[] MissingKeys { get; init; } = [];
    public Emignatik.NxFileViewer.Models.TreeItems.Impl.NroFileItem? Homebrew { get; init; }

    public bool IsMultiPackage => Titles.Count > 1 && Type is "NSP" or "NSZ" or "XCI" or "XCZ" or "NAX0";
    public string DisplayType => IsMultiPackage && Type != "NAX0" ? "Super" + Type : Type;

    private sealed record PartitionContents(IReadOnlyList<PackageEntry> Entries, IReadOnlyList<Content> Titles, bool NeedsProdKeys = false, string? MetadataError = null, string[]? MissingKeys = null, string? SystemUpdateVersion = null, PackageStructure Structure = PackageStructure.Unknown);

    private static readonly PartitionContents EmptyPartition = new(Array.Empty<PackageEntry>(), Array.Empty<Content>());

    private static PartitionContents ReadPartition(IFileSystem partition, string? keysPath, List<NcaHashResult>? hashes, CancellationToken token, bool includeDetails, Action<string, int, int, bool>? hashProgress)
    {
        if (!AllowBlockless && hashes == null)
            foreach (var entry in partition.EnumerateEntries().Where(entry => entry.Type == DirectoryEntryType.File && entry.Name.EndsWith(".ncz", StringComparison.OrdinalIgnoreCase)))
            {
                token.ThrowIfCancellationRequested();
                using var file = partition.LoadFile(entry);
                using var stream = file.AsStream();
                EnsureCompressionAllowed(stream);
            }
        if (hashes != null)
            CheckNcaHashes(partition, hashes, keysPath, token, hashProgress);

        var entries = ListFiles(partition);
        if (!partition.FindCnmtEntries().Any())
            return new PartitionContents(entries, Array.Empty<Content>());

        if (keysPath == null)
            return new PartitionContents(entries, Array.Empty<Content>(), NeedsProdKeys: true);

        var missingKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var keys = LoadKeys(keysPath);
            var contents = PackageMetadataReader.Read(partition, keys, out _, injectTicketKeys: InjectTicketKeys, readDetails: includeDetails,
                saveTicketKey: SaveTicketKeys ? (id, key) => StoreTicketKey(keysPath, id.ToString(), key.ToString()) : null,
                missingKey: missing => missingKeys.Add(missing.Name), token: token);
            var present = entries.Where(entry => entry.Name.EndsWith(".nca", StringComparison.OrdinalIgnoreCase) || entry.Name.EndsWith(".ncz", StringComparison.OrdinalIgnoreCase)).Select(entry => entry.Name[..^4]).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var missing = contents.SelectMany(content => content.ReferencedContents).Where(reference => !present.Contains(reference.Id) && (!IgnoreMissingDeltaFragments || reference.Type != LibHac.Ncm.ContentType.DeltaFragment)).Distinct().ToArray();
            var errors = missing.Select(reference => string.Format(Emignatik.NxFileViewer.Localization.LocalizationManager.Instance.Current.Keys.LoadingError_NcaFileMissing_Log, reference.Id, reference.Type)).ToArray();
            if (hashes != null) foreach (var reference in missing) hashes.Add(new NcaHashResult(reference.Id + ".nca", null, null, string.Format(Emignatik.NxFileViewer.Localization.LocalizationManager.Instance.Current.Keys.LoadingError_NcaFileMissing_Log, reference.Id, reference.Type), Missing: true));
            return new PartitionContents(entries, contents, MetadataError: errors.Length == 0 ? null : string.Join(Environment.NewLine, errors), MissingKeys: missingKeys.ToArray());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or LibHac.Common.LibHacException)
        {
            if (ex is MissingKeyException missing) missingKeys.Add(missing.Name);
            return new PartitionContents(entries, Array.Empty<Content>(), MetadataError: ex.Message, MissingKeys: missingKeys.ToArray());
        }
    }

    internal static bool AllowBlockless { get; set; } = true;
    internal static bool IgnoreMissingDeltaFragments { get; set; } = true;
    private static void EnsureCompressionAllowed(Stream input)
    {
        if (AllowBlockless) return;
        var position = input.Position;
        try { if (!NczHeader.Read(input).IsUsingBlockCompression) throw new InvalidDataException(Emignatik.NxFileViewer.Localization.LocalizationManager.Instance.Current.Keys.LoadingError_NczBlocklessCompressionDisabled); }
        finally { input.Position = position; }
    }
    internal static bool InjectTicketKeys { get; set; } = true;
    internal static bool SaveTicketKeys { get; set; }
    internal static string? CustomTitleKeysPath { get; set; }
    internal static string? TitleKeysFilePath { get; set; }
    private static string DefaultTitleKeysPath => System.IO.Path.Combine(ViewerDataDirectory.Root, "title.keys");

    private static void StoreTicketKey(string prodPath, string rightsId, string key)
    {
        try
        {
            var titlePath = KeyDownloads.Destination(TitleKeysFilePath ?? CustomTitleKeysPath, DefaultTitleKeysPath);
            if (!string.Equals(System.IO.Path.GetFullPath(prodPath), titlePath,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                TicketKeyStore.AddMissing(titlePath, rightsId, key);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            System.Diagnostics.Trace.TraceWarning("Unable to save a ticket key: {0}", error.GetType().Name);
        }
    }

    internal static KeySet LoadKeys(string keysPath)
    {
        var keys = KeySet.CreateDefaultKeySet();
        var titlePath = TitleKeysFilePath ?? DefaultTitleKeysPath;
        ExternalKeyReader.ReadKeyFile(keys, keysPath, File.Exists(titlePath) ? titlePath : null, null, null);
        return keys;
    }

    private static void CheckNcaHashes(IFileSystem partition, List<NcaHashResult> results, string? keysPath, CancellationToken token, Action<string, int, int, bool>? progress)
    {
        var keys = keysPath == null ? null : LoadKeys(keysPath);
        var entries = partition.EnumerateEntries().Where(entry => entry.Type == DirectoryEntryType.File &&
            (entry.Name.EndsWith(".nca", StringComparison.OrdinalIgnoreCase) || entry.Name.EndsWith(".ncz", StringComparison.OrdinalIgnoreCase))).ToArray();
        CheckNcaHashes(partition, results, keys, entries, token, progress);
    }

    private static void CheckNcaHashes(IFileSystem partition, List<NcaHashResult> results, KeySet? keys,
        DirectoryEntryEx[] entries, CancellationToken token, Action<string, int, int, bool>? progress)
    {
        for (var index = 0; index < entries.Length; index++)
        {
            token.ThrowIfCancellationRequested();
            var entry = entries[index];
            progress?.Invoke(entry.Name, index + 1, entries.Length, false);
            var compressed = entry.Name.EndsWith(".ncz", StringComparison.OrdinalIgnoreCase);

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
            catch (Exception ex) when (ex is IOException or InvalidDataException or LibHacException or NczFormatException or ZstdSharp.ZstdException)
            {
                results.Add(new NcaHashResult(entry.Name, matches, Error: ex.Message));
            }
            progress?.Invoke(entry.Name, index + 1, entries.Length, true);
        }
    }

    private static PackageEntry[] ListFiles(IFileSystem partition) => partition.EnumerateEntries()
        .Where(entry => entry.Type == DirectoryEntryType.File)
        .Select(entry => new PackageEntry(entry.Name, entry.Size))
        .ToArray();
}
