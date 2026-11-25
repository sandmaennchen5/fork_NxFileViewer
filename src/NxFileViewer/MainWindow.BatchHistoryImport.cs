using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using Emignatik.NxFileViewer.Services.Integrity;
using Emignatik.NxFileViewer.Services.Nand;
using Emignatik.NxFileViewer.Services.PackageAnalysis;
using Emignatik.NxFileViewer.Services.Security;

namespace NxFileViewer.Avalonia;

public partial class MainWindow
{
    private sealed record WpfScan(Guid Id, DateTime UpdatedUtc, string Source, JsonElement[]? Results,
        bool VerifyIntegrity, bool IncludeSubdirectories, bool IncludeArchives, bool IgnoreMissingDeltaFragments,
        string[]? SourcePaths, Dictionary<string, string>? Fingerprints);

    private List<BatchHistoryEntry> ReadWpfBatchHistory()
    {
        var entries = new List<BatchHistoryEntry>();
        var oldEntries = Path.Combine(BatchHistoryDirectory, "batch-integrity-entries");
        var paths = Directory.Exists(BatchHistoryDirectory) ? Directory.EnumerateFiles(BatchHistoryDirectory, "*.zip") : [];
        if (Directory.Exists(oldEntries)) paths = paths.Concat(Directory.EnumerateFiles(oldEntries, "*.zip"));
        var seen = _batchHistory.Where(entry => entry.Options != null).Select(entry => entry.Id).ToHashSet();
        foreach (var path in paths.Where(path => !Path.GetFileName(path).StartsWith("scan-", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                using var zip = ZipFile.OpenRead(path);
                if (zip.GetEntry("scan.json") is { } index)
                {
                    using var stream = index.Open();
                    using var document = JsonDocument.Parse(stream);
                    var root = document.RootElement;
                    if (!root.TryGetProperty("Scan", out var scanData) || scanData.Deserialize<WpfScan>() is not { } scan ||
                        seen.Contains(scan.Id) || !root.TryGetProperty("Results", out var files)) continue;
                    var rows = new List<BatchRow>();
                    foreach (var name in files.EnumerateArray())
                    {
                        using var item = zip.GetEntry(name.GetString() ?? "")?.Open() ?? throw new InvalidDataException("Missing WPF batch result.");
                        using var result = JsonDocument.Parse(item);
                        if (ReadWpfBatchRow(result.RootElement, zip) is { } row) rows.Add(row);
                    }
                    Add(scan, rows.ToArray());
                }
                else if (zip.GetEntry("overview.json") is { } overview)
                {
                    using var stream = overview.Open();
                    if (JsonSerializer.Deserialize<WpfScan>(stream) is { } scan && !seen.Contains(scan.Id))
                        Add(scan, ReadRows(scan, zip));
                }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or JsonException or InvalidOperationException)
            { LogSession("WARN", $"Could not read WPF batch history: {error.Message}"); }
        }
        var legacy = Path.Combine(BatchHistoryDirectory, "batch-integrity.json");
        if (File.Exists(legacy))
        {
            using var stream = File.OpenRead(legacy);
            foreach (var scan in JsonSerializer.Deserialize<WpfScan[]>(stream) ?? [])
                if (!seen.Contains(scan.Id)) Add(scan, ReadRows(scan, null));
        }
        return entries;

        BatchRow[] ReadRows(WpfScan scan, ZipArchive? zip) => (scan.Results ?? [])
            .Select(row => ReadWpfBatchRow(row, zip)).OfType<BatchRow>().ToArray();

        void Add(WpfScan scan, BatchRow[] rows)
        {
            if (scan.Id == Guid.Empty || string.IsNullOrWhiteSpace(scan.Source) || !seen.Add(scan.Id)) return;
            if (scan.Fingerprints != null)
            {
                var fingerprints = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var pair in scan.Fingerprints)
                    fingerprints[string.Join(" :: ", pair.Key.Split("::", 2, StringSplitOptions.TrimEntries))] = pair.Value;
                rows = rows.Select(row => row with { SourceFingerprint = fingerprints.GetValueOrDefault(row.DisplayPath) }).ToArray();
            }
            entries.Add(new(scan.UpdatedUtc.ToLocalTime(), scan.Source,
                rows.Select(row => new BatchSource(row.Path, row.DisplayPath)).ToArray(), rows, scan.Id)
            {
                Options = new(scan.SourcePaths is { Length: > 0 } sources ? sources : [scan.Source], scan.VerifyIntegrity,
                    scan.IncludeSubdirectories, scan.IncludeArchives, scan.IgnoreMissingDeltaFragments,
                    Emignatik.NxFileViewer.FileLoading.SdBatchAreas.From(_preferences.SharedSettings) with { SourceMode = _batchSourceMode })
            });
        }
    }

    private BatchRow? ReadWpfBatchRow(JsonElement data, ZipArchive? zip)
    {
        if (WpfText(data, "FilePath") is not { Length: > 0 } source) return null;
        var path = string.Join(" :: ", source.Split("::", 2, StringSplitOptions.TrimEntries));
        var status = WpfNumber(data, "Integrity");
        var size = WpfNumber(data, "FileSize");
        var firmware = WpfFlag(data, "IsFirmware");
        var nand = WpfFlag(data, "IsNand");
        var structure = WpfText(data, "Structure") ?? "—";
        var row = new BatchRow(path, WpfText(data, "FirmwareDetails") ?? WpfText(data, "PackageType") ?? "",
            status is 3 or 4 or 5 or 6 || WpfText(data, "Error") is { Length: > 0 })
        {
            StoredSize = size, Verified = status is >= 2, Original = status == 2,
            Integrity = status switch
            {
                1 => _text.NcasIntegrity_InProgress, 2 => _text.NcasIntegrity_Original, 3 => _text.NcasIntegrity_Incomplete,
                4 => _text.NcasIntegrity_Modified, 5 => _text.NcasIntegrity_Corrupted, 6 => _text.NcasIntegrity_Error,
                7 => _text.NcasIntegrity_NoNca, _ => _text.NcasIntegrity_Unchecked
            },
            Signature = WpfNumber(data, "NcaSignatureValidity") switch
            { (int)LibHac.Common.Validity.Valid => _text.Signature_Passed, (int)LibHac.Common.Validity.Invalid => _text.Signature_NotPassed, _ => _text.Signature_Unchecked },
            Structure = firmware || nand ? structure : PackageStructureAnalyzer.GetDisplayName(structure, _text),
            Error = WpfText(data, "Error") ?? "", NamingError = WpfText(data, "NamingError") ?? "",
            Naming = WpfText(data, "NamingStatus") ?? "—", NewName = WpfText(data, "ProposedPath") ?? "",
            ConversionStatus = WpfText(data, "ConversionStatus") ?? "", OutputPath = WpfText(data, "ConvertedPath") ?? "",
            OutputSize = WpfNumber(data, "OutputSize"),
            FirmwareArchiveType = firmware ? WpfText(data, "FileType") ?? "Folder" : null,
            Nand = nand ? new NandDetectionResult(structure, [], true, size ?? 0, 1) : null,
            NandDetails = WpfText(data, "NandDetails")
        };
        row = FillWpfHistoryMetadata(row, data, zip);
        if (data.TryGetProperty("Packages", out var packages) && packages.ValueKind == JsonValueKind.Array && packages.GetArrayLength() > 1)
            row = row with { StoredParts = packages.EnumerateArray().Select((package, index) =>
                FillWpfHistoryMetadata(row with { PartNumber = index + 1 }, package, zip)).ToArray() };
        return row;
    }

    private BatchRow FillWpfHistoryMetadata(BatchRow row, JsonElement data, ZipArchive? zip)
    {
        var metadata = row.StoredMetadata == null ? new Dictionary<string, string>() : new(row.StoredMetadata);
        foreach (var property in data.EnumerateObject().Where(property => property.Value.ValueKind == JsonValueKind.String))
            metadata[property.Name] = property.Value.GetString()!;
        foreach (var (oldName, newName) in new[] { ("PackageType", "ContainerType"), ("SystemVersion", "MinimumSystemVersion"),
            ("FileSystemPermissions", "Permissions"), ("AcidSignatureValidity", "AcidSignature") })
            if (metadata.TryGetValue(oldName, out var value)) metadata[newName] = value;
        if (WpfText(data, "Type") is { } type)
            metadata["ContentType"] = type switch { "Application" => "Base", "Patch" => "Update", "AddOnContent" => "DLC", _ => type };
        if (WpfNumber(data, "Permission") is >= 0 and <= 2 and var permission)
            metadata["Security"] = BatchContentMetadata.PermissionDisplay((ProgramSecurityLevel)permission);
        var titles = data.TryGetProperty("Titles", out var languages) && languages.ValueKind == JsonValueKind.Array
            ? languages.EnumerateArray().Select(title => new OverviewLanguage(WpfText(title, "Language") ?? "",
                WpfText(title, "Title") ?? "", WpfText(title, "Publisher") ?? "", ReadWpfIcon(zip, title))).ToArray() : row.StoredLanguages;
        return row with { StoredMetadata = metadata, StoredLanguages = titles, StoredDemo = WpfFlag(data, "IsDemo") || row.StoredDemo == true };
    }

    private static byte[]? ReadWpfIcon(ZipArchive? zip, JsonElement title)
    {
        if (zip == null || !title.TryGetProperty("Icon", out var icon) || icon.ValueKind != JsonValueKind.Object ||
            WpfText(icon, "Key") is not { Length: 64 } key || !key.All(Uri.IsHexDigit) ||
            zip.GetEntry("icons/" + key) is not { Length: > 0 and <= 16777216 } entry) return null;
        using var input = entry.Open();
        using var output = new MemoryStream((int)entry.Length);
        input.CopyTo(output);
        return output.ToArray();
    }

    private static string? WpfText(JsonElement data, string name) =>
        data.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static long? WpfNumber(JsonElement data, string name) =>
        data.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number) ? number : null;
    private static bool WpfFlag(JsonElement data, string name) =>
        data.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
}
