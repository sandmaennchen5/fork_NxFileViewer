using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using Emignatik.NxFileViewer.Services.Integrity;

namespace NxFileViewer.Avalonia;

public partial class MainWindow
{
    private ActiveBatchLogoStore? _activeBatchLogos;
    private (Guid ScanId, string Path)? _resumeArtwork;
    private static string BatchHistoryDirectory => Path.Combine(ViewerDataDirectory.Root, "History");
    private static string BatchScanArchive(Guid id) => Path.Combine(BatchHistoryDirectory, "scan-" + id.ToString("N") + ".zip");

    private void PreserveResumeArtwork(Guid scanId)
    {
        var source = BatchScanArchive(scanId);
        if (!File.Exists(source)) return;
        Directory.CreateDirectory(BatchExtractionDirectory);
        var copy = Path.Combine(BatchExtractionDirectory, "resume-" + Guid.NewGuid().ToString("N") + ".tmp");
        File.Copy(source, copy);
        _resumeArtwork = (scanId, copy);
    }

    private void ReleaseResumeArtwork()
    {
        if (_resumeArtwork is not { } saved) return;
        _resumeArtwork = null;
        try { File.Delete(saved.Path); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { LogSession("WARN", error.Message); }
    }

    private BatchRow StoreActiveBatchArtwork(BatchRow row)
    {
        var content = row.ContentOverride ?? row.Package?.Titles.FirstOrDefault();
        var languages = row.StoredLanguages ?? (row.Package?.Homebrew is { } homebrew
            ? GetHomebrewLanguages(homebrew) : GetOverviewLanguages(content));
        if (languages.Any(language => language.Icon?.Length > 0))
        {
            _activeBatchLogos ??= new ActiveBatchLogoStore(BatchHistoryDirectory);
            languages = languages.Select(language => language.Icon?.Length > 0
                ? language with { Icon = null, IconReference = _activeBatchLogos.StageIcon(_batchScanId, language.Icon) }
                : language).ToArray();
        }

        var parts = row.ShowPackageDetails ? row.PackageDetails.Select(StoreActiveBatchArtwork).ToArray() : null;
        if (row.PartNumber == 0 && row.Package != null)
            foreach (var package in row.Package.Titles)
                if (package.NacpData is { } nacp)
                    foreach (var title in nacp.Titles)
                        if (title != null) title.Icon = null;
        return row with { Path = PersistentBatchPath(row), StoredLanguages = languages, StoredParts = parts };
    }

    private byte[]? ReadBatchArtwork(OverviewLanguage? language)
    {
        if (language?.Icon is { Length: > 0 } inline) return inline;
        if (language?.IconReference is not { } reference) return null;
        var active = _activeBatchLogos?.ReadIcon(reference);
        if (active != null) return active;
        try
        {
            var path = _resumeArtwork is { } saved && saved.ScanId == reference.EntryId
                ? saved.Path : BatchScanArchive(reference.EntryId);
            if (!File.Exists(path)) return null;
            using var zip = ZipFile.OpenRead(path);
            var entry = zip.GetEntry("icons/" + reference.Key);
            if (entry == null || entry.Length > 16 * 1024 * 1024) return null;
            using var input = entry.Open();
            using var output = new MemoryStream();
            input.CopyTo(output);
            return output.ToArray();
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            LogSession("WARN", $"Could not read batch artwork: {error.Message}");
            return null;
        }
    }

    private void FlushBatchArtwork()
    {
        try
        {
            if (_activeBatchLogos?.Flush() == true)
                LogSession("WARN", "The temporary batch artwork archive was rebuilt.");
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            LogSession("WARN", $"Could not save temporary batch artwork: {error.Message}");
        }
    }

    private void ReleaseBatchArtwork()
    {
        try { _activeBatchLogos?.Dispose(); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            LogSession("WARN", $"Could not remove temporary batch artwork: {error.Message}");
        }
        finally { _activeBatchLogos = null; }
    }

    private void WriteBatchScanArchive(BatchHistoryEntry scan, bool includeLogos)
    {
        Directory.CreateDirectory(BatchHistoryDirectory);
        var target = BatchScanArchive(scan.Id);
        var temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        var written = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            using (var zip = ZipFile.Open(temporary, ZipArchiveMode.Create))
            {
                OverviewLanguage StoreLanguage(OverviewLanguage language)
                {
                    if (!includeLogos || ReadBatchArtwork(language) is not { Length: > 0 } bytes)
                        return language with { Icon = null, IconReference = null };
                    var key = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();
                    if (written.Add(key))
                    {
                        using var output = zip.CreateEntry("icons/" + key, CompressionLevel.NoCompression).Open();
                        output.Write(bytes);
                    }
                    return language with { Icon = null, IconReference = new BatchIconReference(scan.Id, key) };
                }

                BatchRow StoreRow(BatchRow row) => row with
                {
                    StoredLanguages = row.StoredLanguages?.Select(StoreLanguage).ToArray(),
                    StoredParts = row.StoredParts?.Select(StoreRow).ToArray()
                };

                var stored = scan with { Results = scan.Results?.Select(StoreRow).ToArray() };
                using (var metadata = zip.CreateEntry("scan.json", CompressionLevel.Fastest).Open())
                    JsonSerializer.Serialize(metadata, stored, BatchHistoryJson);
                using var header = zip.CreateEntry("header.json", CompressionLevel.Fastest).Open();
                JsonSerializer.Serialize(header, scan with { Results = null }, BatchHistoryJson);
            }
            File.Move(temporary, target, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static BatchHistoryEntry? ReadBatchScanArchive(string path, bool headerOnly)
    {
        using var zip = ZipFile.OpenRead(path);
        var entry = headerOnly ? zip.GetEntry("header.json") ?? zip.GetEntry("scan.json") : zip.GetEntry("scan.json");
        using var data = entry?.Open();
        return data == null ? null : JsonSerializer.Deserialize<BatchHistoryEntry>(data, BatchHistoryJson);
    }
}
