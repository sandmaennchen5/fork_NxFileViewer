using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using Emignatik.NxFileViewer.Localization;
using Emignatik.NxFileViewer.Models.Overview;

namespace Emignatik.NxFileViewer.Services.Integrity;

/// <summary>Checks firmware against the bundled fw/hashes manifests without keys or extraction.</summary>
public sealed class FirmwareIntegrityVerifier
{
    private readonly List<Reference> _references = new();
    private readonly HashSet<string> _knownNames = new(StringComparer.OrdinalIgnoreCase);
    public FirmwareIntegrityVerifier(string? referenceDirectory = null)
    {
        referenceDirectory ??= Path.Combine(AppContext.BaseDirectory, "fw", "hashes");
        Load(Directory.EnumerateFiles(referenceDirectory, "*.json").Select(File.ReadAllText));
    }

    public FirmwareIntegrityVerifier(IEnumerable<string> manifests) => Load(manifests);

    private void Load(IEnumerable<string> manifests)
    {
        foreach (var manifest in manifests)
        {
            using var json = JsonDocument.Parse(manifest);
            var root = json.RootElement;
            var files = new Dictionary<string, Fingerprint>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in root.GetProperty("files").EnumerateObject())
            {
                var hash = entry.Value.GetProperty("sha256").GetString()!;
                var size = entry.Value.GetProperty("size").GetInt64();
                if (hash.Length != 64 || !hash.All(Uri.IsHexDigit) || size < 0 || Path.GetFileName(entry.Name) != entry.Name)
                    throw new InvalidDataException("Invalid firmware reference.");
                files.Add(entry.Name, new Fingerprint(size, hash));
                _knownNames.Add(entry.Name);
            }
            if (files.Count == 0 || files.Count != root.GetProperty("file_count").GetInt32())
                throw new InvalidDataException("Invalid firmware file count.");
            _references.Add(new Reference(root.GetProperty("name").GetString()!, files));
        }
        if (_references.Count == 0) throw new InvalidDataException("No firmware hash references available.");
    }

    public bool IsFirmwareFolder(string path) => Directory.EnumerateFiles(path).Any(p => _knownNames.Contains(Path.GetFileName(p)));
    public bool IsFirmwareZip(string path)
    {
        try
        {
            using var zip = ZipFile.OpenRead(path);
            return zip.Entries.Any(e => _knownNames.Contains(e.Name));
        }
        catch (InvalidDataException) { return Path.GetFileName(path).Contains("firmware", StringComparison.OrdinalIgnoreCase); }
    }

    public BatchIntegrityResult Verify(string path, CancellationToken cancellationToken = default, Action<double>? progress = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var actual = new Dictionary<string, Fingerprint>(StringComparer.OrdinalIgnoreCase);
        var duplicates = new List<string>();
        void Read(string name, Stream stream, long size)
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[128 * 1024];
            long length = 0;
            int read;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                read = stream.Read(buffer, 0, buffer.Length);
                if (read == 0) break;
                hash.AppendData(buffer, 0, read);
                length += read;
            }
            if (length != size) throw new InvalidDataException($"Truncated firmware file: {name}");
            if (!actual.TryAdd(name, new Fingerprint(length, Convert.ToHexString(hash.GetHashAndReset())))) duplicates.Add(name);
        }
        if (Directory.Exists(path))
        {
            var files = Directory.GetFiles(path).Where(p => p.EndsWith(".nca", StringComparison.OrdinalIgnoreCase)).ToArray();
            for (var i = 0; i < files.Length; i++)
            {
                using var stream = File.OpenRead(files[i]);
                Read(Path.GetFileName(files[i]), stream, stream.Length);
                progress?.Invoke((double)(i + 1) / files.Length);
            }
        }
        else
        {
            using var zip = ZipFile.OpenRead(path);
            var entries = zip.Entries.Where(e => e.Name.EndsWith(".nca", StringComparison.OrdinalIgnoreCase)).ToArray();
            for (var i = 0; i < entries.Length; i++)
            {
                using var stream = entries[i].Open();
                Read(entries[i].Name, stream, entries[i].Length);
                progress?.Invoke((double)(i + 1) / entries.Length);
            }
        }
        // Hash once, then compare all versions: shared files alone never prove completeness.
        var comparisons = _references.Select(r => new {
            Reference = r,
            Matching = r.Files.Count(e => actual.TryGetValue(e.Key, out var a) && Equal(a, e.Value)),
            Overlap = r.Files.Keys.Count(actual.ContainsKey),
            Difference = r.Files.Keys.Except(actual.Keys, StringComparer.OrdinalIgnoreCase).Count()
                + actual.Keys.Except(r.Files.Keys, StringComparer.OrdinalIgnoreCase).Count()
        }).OrderByDescending(c => c.Matching).ThenByDescending(c => c.Overlap).ThenBy(c => c.Difference).ToArray();
        var exact = comparisons.Where(c => c.Matching == c.Reference.Files.Count && c.Difference == 0).ToArray();
        var best = exact.FirstOrDefault() ?? comparisons[0];
        var keys = LocalizationManager.Instance.Current.Keys;
        if (best.Overlap == 0)
            return Result(path, "?", NcasIntegrity.Error, keys.Firmware_Unknown, keys.Firmware_Unknown);
        var expected = best.Reference.Files;
        var missing = expected.Keys.Except(actual.Keys, StringComparer.OrdinalIgnoreCase).ToArray();
        var extra = actual.Keys.Except(expected.Keys, StringComparer.OrdinalIgnoreCase).ToArray();
        var changed = expected.Where(e => actual.TryGetValue(e.Key, out var a) && !Equal(a, e.Value)).Select(e => e.Key).ToArray();
        var valid = exact.Length > 0 && duplicates.Count == 0;
        var version = valid ? string.Join(" / ", exact.Select(c => c.Reference.Name)) : best.Reference.Name;
        var summary = string.Format(keys.Firmware_Summary, best.Matching, expected.Count, missing.Length, changed.Length, extra.Length, duplicates.Count);
        var details = summary + Environment.NewLine + string.Join(Environment.NewLine,
            missing.Select(n => keys.Firmware_Missing + ": " + n)
                .Concat(changed.Select(n => keys.Firmware_Changed + ": " + n))
                .Concat(extra.Select(n => keys.Firmware_Extra + ": " + n))
                .Concat(duplicates.Select(n => keys.Firmware_Duplicate + ": " + n)));
        progress?.Invoke(1);
        return Result(path, version, valid ? NcasIntegrity.Original : NcasIntegrity.Error, valid ? null : summary, details);
    }
    private static bool Equal(Fingerprint a, Fingerprint b) => a.Size == b.Size && string.Equals(a.Hash, b.Hash, StringComparison.OrdinalIgnoreCase);
    private static BatchIntegrityResult Result(string path, string version, NcasIntegrity integrity, string? error, string details) =>
        new(path, Directory.Exists(path) ? "Folder" : "ZIP", "Firmware", version, "SHA-256", integrity, error) { IsFirmware = true, FirmwareDetails = details };
    private sealed record Fingerprint(long Size, string Hash);
    private sealed record Reference(string Name, Dictionary<string, Fingerprint> Files);
}
