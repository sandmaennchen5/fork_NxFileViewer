using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Emignatik.NxFileViewer.FileLoading;
using Emignatik.NxFileViewer.Models.Overview;
namespace Emignatik.NxFileViewer.Services.Integrity;

public sealed record BatchHistoryEntry(Guid Id, DateTime UpdatedUtc, string Source, bool IncludeSubdirectories,
    bool IncludeArchives, bool VerifyIntegrity, bool IgnoreMissingDeltaFragments, string State,
    BatchIntegrityResult[] Results, Dictionary<string, string> Fingerprints)
{
    public string[] SourcePaths { get; init; } = Array.Empty<string>();
    public string Label => Id == Guid.Empty ? "" : $"{UpdatedUtc.ToLocalTime():g} — {Source} ({Results.Length}) — {State}";
    public bool CanSkip(string path, bool ignoreMissingDeltaFragments)
    {
        if (IgnoreMissingDeltaFragments != ignoreMissingDeltaFragments) return false;
        var result = Results.FirstOrDefault(r => r.FilePath.Equals(path, StringComparison.OrdinalIgnoreCase));
        if (result == null || result.HasMissingKeys || result.Integrity is NcasIntegrity.Error or NcasIntegrity.InProgress ||
            (VerifyIntegrity && result.Integrity == NcasIntegrity.Unchecked)) return false;
        return Fingerprints.TryGetValue(path, out var saved) && saved == BatchHistoryStore.Fingerprint(path);
    }
}
public sealed class BatchHistoryStore(string path)
{
    public static string DefaultPath => Path.Combine(AppContext.BaseDirectory, "History", "batch-integrity.json");
    public string EntriesDirectory => Path.GetDirectoryName(Path.GetFullPath(path))!;
    private string LegacyEntriesDirectory => Path.Combine(EntriesDirectory, Path.GetFileNameWithoutExtension(path) + "-entries");
    private string? ExistingPath(Guid id) =>
        Directory.Exists(EntriesDirectory) ? Directory.EnumerateFiles(EntriesDirectory, "*_" + id.ToString("N") + ".zip").FirstOrDefault()
            ?? (File.Exists(Path.Combine(LegacyEntriesDirectory, id.ToString("N") + ".zip"))
                ? Path.Combine(LegacyEntriesDirectory, id.ToString("N") + ".zip") : null) : null;
    public string EntryPath(Guid id) => ExistingPath(id) ?? Path.Combine(EntriesDirectory,
        DateTime.Now.ToString("yyyy-MM-dd_HHmm", System.Globalization.CultureInfo.InvariantCulture) + "_" + id.ToString("N") + ".zip");
    private sealed record ArchiveManifest(int Version, BatchHistoryEntry Scan, string[] Results);
    private static string ResultPath(string filePath) => "roms/" +
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(filePath.ToUpperInvariant()))).ToLowerInvariant() + ".json";

    private static BatchHistoryEntry? ReadArchive(System.IO.Compression.ZipArchive zip)
    {
        if (zip.GetEntry("scan.json") is { } index)
        {
            using var input = index.Open();
            var manifest = JsonSerializer.Deserialize<ArchiveManifest>(input);
            if (manifest is not { Version: 2 } || manifest.Scan == null || manifest.Results == null) return null;
            var results = new List<BatchIntegrityResult>();
            foreach (var name in manifest.Results)
            {
                var metadata = zip.GetEntry(name) ?? throw new InvalidDataException("Missing ROM metadata.");
                using var item = metadata.Open();
                results.Add(JsonSerializer.Deserialize<BatchIntegrityResult>(item) ?? throw new InvalidDataException("Invalid ROM metadata."));
            }
            return Normalize(manifest.Scan with { Results = results.ToArray() });
        }
        if (zip.GetEntry("overview.json") is { } legacy)
        {
            using var input = legacy.Open();
            return Normalize(JsonSerializer.Deserialize<BatchHistoryEntry>(input));
        }
        return null;
    }

    /// <summary>Reads one stored ROM without deserializing other ROM results or opening its source.</summary>
    public BatchIntegrityResult? ReadResult(Guid scanId, string filePath)
    {
        try
        {
            using var zip = System.IO.Compression.ZipFile.OpenRead(EntryPath(scanId));
            if (zip.GetEntry(ResultPath(filePath)) is { } metadata)
            {
                using var input = metadata.Open();
                return JsonSerializer.Deserialize<BatchIntegrityResult>(input);
            }
            return ReadArchive(zip)?.Results.FirstOrDefault(result => result.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or JsonException) { return null; }
    }
    private readonly Dictionary<Guid, Dictionary<string, byte[]>> _pendingIcons = new();

    public BatchIconReference? StageIcon(Guid id, byte[]? bytes)
    {
        if (bytes == null || bytes.Length == 0) return null;
        var key = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();
        if (!_pendingIcons.TryGetValue(id, out var icons)) _pendingIcons[id] = icons = new();
        icons.TryAdd(key, bytes);
        return new BatchIconReference(id, key);
    }

    public byte[]? ReadIcon(BatchIconReference reference)
    {
        if (string.IsNullOrEmpty(reference.Key) || reference.Key.Length != 64 || reference.Key.Any(c => !Uri.IsHexDigit(c))) return null;
        if (_pendingIcons.TryGetValue(reference.EntryId, out var pending) && pending.TryGetValue(reference.Key, out var bytes)) return bytes;
        try
        {
            using var zip = System.IO.Compression.ZipFile.OpenRead(EntryPath(reference.EntryId));
            var entry = zip.GetEntry("icons/" + reference.Key);
            if (entry == null || entry.Length > 16 * 1024 * 1024) return null;
            using var input = entry.Open();
            using var output = new MemoryStream();
            input.CopyTo(output);
            return output.ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { return null; }
    }

    private static BatchHistoryEntry? Normalize(BatchHistoryEntry? entry) =>
        entry is { Results: not null, Fingerprints: not null } && !string.IsNullOrWhiteSpace(entry.Source)
            ? entry with { Fingerprints = new Dictionary<string, string>(entry.Fingerprints, StringComparer.OrdinalIgnoreCase) } : null;

    private IEnumerable<BatchHistoryEntry> ReadEntries()
    {
        var files = (Directory.Exists(EntriesDirectory) ? Directory.EnumerateFiles(EntriesDirectory, "*.zip") : Array.Empty<string>())
            .Concat(Directory.Exists(LegacyEntriesDirectory) ? Directory.EnumerateFiles(LegacyEntriesDirectory, "*.zip") : Array.Empty<string>());
        var seen = new HashSet<Guid>();
        foreach (var file in files)
        {
            BatchHistoryEntry? entry = null;
            try
            {
                using var zip = System.IO.Compression.ZipFile.OpenRead(file);
                entry = ReadArchive(zip);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or JsonException) { }
            if (entry != null && seen.Add(entry.Id)) yield return entry;
        }
    }
    public IReadOnlyList<BatchHistoryEntry> Load(int limit = 5)
    {
        var entries = ReadEntries().ToList();
        // Read-only compatibility until the first successful save migrates the old array.
        if (File.Exists(path))
            entries.AddRange((JsonSerializer.Deserialize<BatchHistoryEntry[]>(File.ReadAllText(path)) ?? Array.Empty<BatchHistoryEntry>())
                .Select(Normalize).OfType<BatchHistoryEntry>());
        return entries.DistinctBy(e => e.Id).OrderByDescending(e => e.UpdatedUtc)
            .Take(Math.Clamp(limit, 1, 100)).ToArray();
    }

    private void WriteEntry(BatchHistoryEntry entry, bool includeLogos = true)
    {
        Directory.CreateDirectory(EntriesDirectory);
        var source = ExistingPath(entry.Id);
        var target = source != null && Path.GetDirectoryName(source) == EntriesDirectory ? source : Path.Combine(EntriesDirectory,
            entry.UpdatedUtc.ToLocalTime().ToString("yyyy-MM-dd_HHmm", System.Globalization.CultureInfo.InvariantCulture)
                + "_" + entry.Id.ToString("N") + ".zip");
        var temp = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            if (source != null) File.Copy(source, temp);
            using (var zip = System.IO.Compression.ZipFile.Open(temp,
                File.Exists(temp) ? System.IO.Compression.ZipArchiveMode.Update : System.IO.Compression.ZipArchiveMode.Create))
            {
                if (zip.Mode == System.IO.Compression.ZipArchiveMode.Update)
                    foreach (var old in zip.Entries.Where(e => e.FullName is "overview.json" or "scan.json" || e.FullName.StartsWith("roms/", StringComparison.Ordinal)).ToArray())
                        old.Delete();
                var names = entry.Results.Select(result => ResultPath(result.FilePath)).ToArray();
                using (var output = zip.CreateEntry("scan.json", System.IO.Compression.CompressionLevel.Fastest).Open())
                    JsonSerializer.Serialize(output, new ArchiveManifest(2, entry with { Results = Array.Empty<BatchIntegrityResult>() }, names));
                for (var i = 0; i < entry.Results.Length; i++)
                {
                    using var output = zip.CreateEntry(names[i], System.IO.Compression.CompressionLevel.Fastest).Open();
                    JsonSerializer.Serialize(output, entry.Results[i]);
                }
                if (!includeLogos && zip.Mode == System.IO.Compression.ZipArchiveMode.Update)
                    foreach (var old in zip.Entries.Where(e => e.FullName.StartsWith("icons/", StringComparison.Ordinal)).ToArray())
                        old.Delete();
                if (includeLogos && _pendingIcons.TryGetValue(entry.Id, out var icons))
                    foreach (var icon in icons)
                    {
                        var name = "icons/" + icon.Key;
                        if (zip.Mode == System.IO.Compression.ZipArchiveMode.Update && zip.GetEntry(name) != null) continue;
                        using var output = zip.CreateEntry(name, System.IO.Compression.CompressionLevel.NoCompression).Open();
                        output.Write(icon.Value);
                    }
            }
            File.Move(temp, target, true);
            if (source != null && source != target) File.Delete(source);
            _pendingIcons.Remove(entry.Id);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    public void Save(BatchHistoryEntry entry, int limit = 5, bool includeLogos = true)
    {
        var valid = Normalize(entry) ?? throw new ArgumentException("Invalid history entry.", nameof(entry));
        if (!includeLogos)
            valid = valid with
            {
                Results = valid.Results.Select(result => result with
                {
                    Titles = result.Titles.Select(title => title with { Icon = null }).ToArray(),
                    Packages = result.Packages.Select(package => package with
                    {
                        Titles = package.Titles.Select(title => title with { Icon = null }).ToArray()
                    }).ToArray()
                }).ToArray()
            };
        // Migrate all old entries before archiving the legacy file; keep the backup until a completed scan is saved.
        if (File.Exists(path))
        {
            var legacy = JsonSerializer.Deserialize<BatchHistoryEntry[]>(File.ReadAllText(path)) ?? Array.Empty<BatchHistoryEntry>();
            foreach (var old in legacy.Select(Normalize).OfType<BatchHistoryEntry>())
                if (!File.Exists(EntryPath(old.Id))) WriteEntry(old);
            File.Move(path, path + "." + Guid.NewGuid().ToString("N") + ".legacy");
        }
        // Upgrade previous per-scan archives, preserving logos and scan IDs.
        foreach (var old in ReadEntries().Where(e => e.Id != valid.Id).ToArray())
        {
            var existing = ExistingPath(old.Id);
            if (existing != null && Path.GetDirectoryName(existing) == LegacyEntriesDirectory) WriteEntry(old);
        }
        WriteEntry(valid, includeLogos);
        var keep = ReadEntries().OrderByDescending(e => e.UpdatedUtc).Take(Math.Clamp(limit, 1, 100)).Select(e => e.Id).ToHashSet();
        foreach (var old in ReadEntries().Where(e => !keep.Contains(e.Id)).ToArray())
            File.Delete(EntryPath(old.Id));
        if (valid.State == "Completed") CleanupLegacyBackups();
    }
    // A legacy JSON backup is redundant only after the completed scan and retention were saved successfully.
    // Failed/locked cleanup never turns a successful history save into an error; a later save retries it.
    private void CleanupLegacyBackups()
    {
        var prefix = Path.GetFileName(path) + ".";
        try
        {
            foreach (var backup in Directory.EnumerateFiles(EntriesDirectory, prefix + "*.legacy", SearchOption.TopDirectoryOnly))
            {
                var name = Path.GetFileName(backup);
                if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !name.EndsWith(".legacy", StringComparison.OrdinalIgnoreCase) ||
                    !Guid.TryParseExact(name[prefix.Length..^7], "N", out _)) continue;
                try
                {
                    if ((File.GetAttributes(backup) & FileAttributes.ReparsePoint) == 0) File.Delete(backup);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    public static string? Fingerprint(string path)
    {
        try
        {
            path = PackageZip.ArchivePath(path);
            if (File.Exists(path))
            {
                var parts = Services.Nand.NandDetection.SplitFiles(path);
                if (parts.Count > 1) return string.Join("|", parts.Select(part =>
                { var item = new FileInfo(part); return $"{item.Name}:{item.Length}:{item.LastWriteTimeUtc.Ticks}"; }));
                var file = new FileInfo(path); return $"{file.Length}:{file.LastWriteTimeUtc.Ticks}";
            }
            if (Directory.Exists(path)) return string.Join("|", Directory.GetFiles(path, "*", SearchOption.AllDirectories)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase).Select(p =>
                { var file = new FileInfo(p); return $"{Path.GetRelativePath(path,p)}:{file.Length}:{file.LastWriteTimeUtc.Ticks}"; }));
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        return null;
    }
}
