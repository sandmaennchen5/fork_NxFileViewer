using System;
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

internal sealed record NcaHashResult(string Name, bool? Matches, Validity? Signature = null, string? Error = null);

internal sealed record PackageFile(string Path, string Type, long Size, IReadOnlyList<PackageEntry> Entries, IReadOnlyList<Content> Titles, bool NeedsProdKeys, string? MetadataError, string? HeaderDetails = null)
{
    internal string? Compression { get; init; }
    internal long? UncompressedSize { get; init; }
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

    internal static PackageFile Read(string path, string? keysPath, List<NcaHashResult>? hashes, CancellationToken token, bool includeDetails = true, Action<int, int>? hashProgress = null)
    {
        var package = ReadPackage(path, keysPath, hashes, token, includeDetails, hashProgress);
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

    private static PackageFile ReadPackage(string path, string? keysPath, List<NcaHashResult>? hashes, CancellationToken token, bool includeDetails, Action<int, int>? hashProgress)
    {
        if (NupReader.IsNup(path))
        {
            using var nup = NupReader.ReadRoot(path, token, out var size, hashes == null ? null : _ => { });
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
            return ReadStandalone(path, keysPath, hashes, token);
        var (entries, titles, needsProdKeys, metadataError, missingKeys) = type switch
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
            Homebrew = type == PackageType.NRO
                ? new Emignatik.NxFileViewer.Models.TreeItems.Impl.NroFileItem(path, token) : null
        };
    }

    private static PackageFile ReadStandalone(string path, string? keysPath, List<NcaHashResult>? hashes, CancellationToken token)
    {
        using var source = MtpDirectSource.OpenRead(path);
        var compressed = System.IO.Path.GetExtension(path).Equals(".ncz", StringComparison.OrdinalIgnoreCase);
        if (compressed && hashes == null) EnsureCompressionAllowed(source);
        using var original = compressed ? Ncz.OpenOriginalStream(source) : null;
        var stream = original ?? source;
        var name = System.IO.Path.GetFileName(path);
        bool? matches = null;
        Validity? signature = null;
        string? details = null;
        string? error = null;
        string? missingKey = null;
        if (hashes != null)
        {
            var id = name.Split('.', 2)[0];
            if (id.Length == 32 && id.All(Uri.IsHexDigit))
            {
                var hash = SHA256.HashDataAsync(stream, token).AsTask().GetAwaiter().GetResult();
                matches = NcaHashPrefix.Matches(hash, Convert.FromHexString(id));
            }
        }
        token.ThrowIfCancellationRequested();
        if (keysPath != null)
        {
            try
            {
                stream.Position = 0;
                var nca = new Nca(LoadKeys(keysPath), stream.AsStorage());
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
        return new PackageFile(path, compressed ? "NCZ" : "NCA", source.Length,
            Array.Empty<PackageEntry>(), Array.Empty<Content>(), keysPath == null, error, details)
        { MissingKeys = keysPath == null ? ["prod.keys"] : missingKey == null ? [] : [missingKey] };
    }

    private static PartitionContents ReadNsp(string path, string? keysPath, List<NcaHashResult>? hashes, CancellationToken token, bool includeDetails, Action<int, int>? hashProgress)
    {
        using var storage = MtpDirectSource.OpenStorage(path);
        using var partition = storage.LoadPartition();
        return ReadPartition(partition, keysPath, hashes, token, includeDetails, hashProgress);
    }

    private static PartitionContents ReadXci(string path, string? keysPath, List<NcaHashResult>? hashes, CancellationToken token, bool includeDetails, Action<int, int>? hashProgress)
    {
        using var storage = MtpDirectSource.OpenStorage(path);
        var gameCard = new Xci(new KeySet(), storage);
        if (!gameCard.HasPartition(XciPartitionType.Secure)) return EmptyPartition;
        using var partition = gameCard.OpenPartition(XciPartitionType.Secure);
        return ReadPartition(partition, keysPath, hashes, token, includeDetails, hashProgress);
    }

    public string[] MissingKeys { get; init; } = [];
    public Emignatik.NxFileViewer.Models.TreeItems.Impl.NroFileItem? Homebrew { get; init; }

    public bool IsMultiPackage => Titles.Count > 1 && Type is "NSP" or "NSZ" or "XCI" or "XCZ";
    public string DisplayType => IsMultiPackage ? "Super" + Type : Type;

    private sealed record PartitionContents(IReadOnlyList<PackageEntry> Entries, IReadOnlyList<Content> Titles, bool NeedsProdKeys = false, string? MetadataError = null, string[]? MissingKeys = null);

    private static readonly PartitionContents EmptyPartition = new(Array.Empty<PackageEntry>(), Array.Empty<Content>());

    private static PartitionContents ReadPartition(IFileSystem partition, string? keysPath, List<NcaHashResult>? hashes, CancellationToken token, bool includeDetails, Action<int, int>? hashProgress)
    {
        if (!AllowBlockless && hashes == null)
            foreach (var entry in partition.EnumerateEntries().Where(entry => entry.Type == DirectoryEntryType.File && entry.Name.EndsWith(".ncz", StringComparison.OrdinalIgnoreCase)))
            { using var file = partition.LoadFile(entry); using var stream = file.AsStream(); EnsureCompressionAllowed(stream); }
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
                missingKey: missing => missingKeys.Add(missing.Name));
            var present = entries.Where(entry => entry.Name.EndsWith(".nca", StringComparison.OrdinalIgnoreCase) || entry.Name.EndsWith(".ncz", StringComparison.OrdinalIgnoreCase)).Select(entry => entry.Name[..^4]).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var missing = contents.SelectMany(content => content.ReferencedContents).Where(reference => !present.Contains(reference.Id) && (!IgnoreMissingDeltaFragments || reference.Type != LibHac.Ncm.ContentType.DeltaFragment)).Distinct().ToArray();
            var errors = missing.Select(reference => string.Format(Emignatik.NxFileViewer.Localization.LocalizationManager.Instance.Current.Keys.LoadingError_NcaFileMissing_Log, reference.Id, reference.Type)).ToArray();
            if (hashes != null) foreach (var reference in missing) hashes.Add(new NcaHashResult(reference.Id + ".nca", null, null, string.Format(Emignatik.NxFileViewer.Localization.LocalizationManager.Instance.Current.Keys.LoadingError_NcaFileMissing_Log, reference.Id, reference.Type)));
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

    private static KeySet LoadKeys(string keysPath)
    {
        var keys = KeySet.CreateDefaultKeySet();
        var titlePath = TitleKeysFilePath ?? DefaultTitleKeysPath;
        ExternalKeyReader.ReadKeyFile(keys, keysPath, File.Exists(titlePath) ? titlePath : null, null, null);
        return keys;
    }

    private static void CheckNcaHashes(IFileSystem partition, List<NcaHashResult> results, string? keysPath, CancellationToken token, Action<int, int>? progress)
    {
        var keys = keysPath == null ? null : LoadKeys(keysPath);
        var entries = partition.EnumerateEntries().Where(entry => entry.Type == DirectoryEntryType.File &&
            (entry.Name.EndsWith(".nca", StringComparison.OrdinalIgnoreCase) || entry.Name.EndsWith(".ncz", StringComparison.OrdinalIgnoreCase))).ToArray();
        if (entries.Length > 0) progress?.Invoke(0, entries.Length);
        for (var index = 0; index < entries.Length; index++)
        {
            token.ThrowIfCancellationRequested();
            var entry = entries[index];
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
            catch (Exception ex) when (ex is IOException or InvalidDataException or LibHacException or NczFormatException)
            {
                results.Add(new NcaHashResult(entry.Name, matches, Error: ex.Message));
            }
            progress?.Invoke(index + 1, entries.Length);
        }
    }

    private static PackageEntry[] ListFiles(IFileSystem partition) => partition.EnumerateEntries()
        .Where(entry => entry.Type == DirectoryEntryType.File)
        .Select(entry => new PackageEntry(entry.Name, entry.Size))
        .ToArray();
}
