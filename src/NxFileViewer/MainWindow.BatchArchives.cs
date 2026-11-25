using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using Emignatik.NxFileViewer.FileLoading;
using Emignatik.NxFileViewer.Services.Integrity;
using Emignatik.NxFileViewer.Settings;
namespace NxFileViewer.Avalonia;
public partial class MainWindow
{
    private sealed record BatchSource(string Path, string DisplayPath, string? Error = null);
    private SdBatchSourceMode _batchSourceMode;
    private Guid _batchScanId = Guid.NewGuid();
    private static string BatchExtractionRoot => Path.Combine(ViewerDataDirectory.Root, "Temp", "Batch");
    private string BatchExtractionDirectory => Path.Combine(BatchExtractionRoot, _batchScanId.ToString("N"));

    private void ResetBatchWorkingFiles()
    {
        ReleaseBatchArtwork();
        ClearBatchExtraction();
        _batchScanId = Guid.NewGuid();
    }

    private void ClearBatchExtraction()
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(BatchExtractionRoot));
        var directory = Path.GetFullPath(BatchExtractionDirectory);
        if (string.Equals(Path.GetDirectoryName(directory), root, StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                if (Directory.Exists(directory) && (File.GetAttributes(root) & FileAttributes.ReparsePoint) == 0 &&
                    (File.GetAttributes(directory) & FileAttributes.ReparsePoint) == 0)
                    Directory.Delete(directory, recursive: true);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                LogSession("WARN", $"Could not remove the batch cache: {error.Message}");
            }
        }
    }

    private void ReleaseScannedArchiveMember(BatchSource source)
    {
        if (source.Path == source.DisplayPath || !source.DisplayPath.Contains(" :: ", StringComparison.Ordinal)) return;
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(BatchExtractionDirectory));
        var path = Path.GetFullPath(source.Path);
        if (!string.Equals(Path.GetDirectoryName(Path.GetDirectoryName(path)), root, StringComparison.OrdinalIgnoreCase)) return;
        try { File.Delete(path); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            QueuePluginLog("WARN", null, $"Could not remove temporary archive member: {error.Message}");
        }
    }

    private static bool IsAvailableBatchSource(string path)
    {
        var parts = path.Split(" :: ", 2);
        return parts.Length == 2 ? File.Exists(parts[0]) : IsReopenableSource(path) || Directory.Exists(path);
    }

    private string MaterializeBatchSource(string path, CancellationToken token,
        Action<BatchArchiveSources.CopyProgress>? copyProgress = null)
    {
        var parts = path.Split(" :: ", 2);
        if (parts.Length < 2) return path;
        var member = BatchArchiveSources.Enumerate(parts[0], false, true, BatchExtractionDirectory, token,
            isPackage: entry => string.Equals(entry, parts[1], StringComparison.Ordinal), copyProgress: copyProgress).FirstOrDefault();
        if (member == null || member.Error != null) throw new IOException(member?.Error ?? path);
        return member.Path;
    }

    private async System.Threading.Tasks.Task OpenBatchFileAsync(BatchRow row)
    {
        var parts = row.DisplayPath.Split(" :: ", 2);
        if (parts.Length == 2) await OpenArchiveAsync(parts[0], false, false, requestedMember: parts[1]);
        else await OpenFileAsync(row.Path);
    }

    private void BatchSourceModeChanged(object? sender, global::Avalonia.Controls.SelectionChangedEventArgs e)
    {
        if (BatchSourceMode.SelectedIndex >= 0) _batchSourceMode = (SdBatchSourceMode)BatchSourceMode.SelectedIndex;
    }
    private bool IsBatchPackage(string path) => BatchArchiveSources.IsPackage(path) || Nax0Contents.IsCandidate(path) ||
        (_preferences.SharedSettings.EnableNup && Emignatik.NxFileViewer.FileLoading.NupReader.IsNup(path)) ||
        (_preferences.NandActive && File.Exists(path) && Emignatik.NxFileViewer.Services.Nand.NandDetection.IsCandidateName(path) &&
            !Emignatik.NxFileViewer.Services.Nand.NandDetection.IsContinuation(path));
    private IEnumerable<BatchSource> EnumerateBatchSources(string source, bool recursive, bool includeArchives, CancellationToken token,
        Action<BatchArchiveSources.CopyProgress>? copyProgress = null, SdBatchAreas? scanAreas = null)
    {
        var areas = scanAreas ?? SdBatchAreas.From(_preferences.SharedSettings) with { SourceMode = _batchSourceMode };
        var restrictAreas = areas.AppliesTo(source);
        var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        IEnumerable<string>? files = restrictAreas ? areas.EnumerateFiles(source, option) : null;
        var sdContents = areas.SourceMode == SdBatchSourceMode.Folder ? [] : SdCardSource.FindAllContents(source)
            .Where(contents => !restrictAreas || areas.IncludesPath(source, contents.ContentsPath)).ToArray();
        var sdRoots = sdContents.Select(contents => Path.TrimEndingDirectorySeparator(Path.GetFullPath(contents.ContentsPath))
            + Path.DirectorySeparatorChar).ToArray();
        foreach (var contents in sdContents)
        {
            token.ThrowIfCancellationRequested();
            yield return new(contents.ContentsPath, contents.ContentsPath);
        }
        if (files == null && _preferences.FirmwareActive && Directory.Exists(source))
        {
            var entries = Directory.EnumerateFiles(source).Select(Path.GetFileName).OfType<string>().ToArray();
            if (LooksLikeFirmwareSource(source, entries))
            {
                yield return new(source, source);
                yield break;
            }
        }
        if (sdRoots.Length > 0)
            files = (files ?? Directory.EnumerateFiles(source, "*", new EnumerationOptions { RecurseSubdirectories = recursive, IgnoreInaccessible = true }))
                .Where(path => !sdRoots.Any(root => Path.GetFullPath(path).StartsWith(root, StringComparison.OrdinalIgnoreCase)));
        foreach (var entry in BatchArchiveSources.Enumerate(source, recursive, includeArchives || PackageZip.IsArchive(source), BatchExtractionDirectory, token, files,
            archive => SaveBackupDetection.IsBackup(archive, token) || _preferences.FirmwareActive && IsBatchFirmwareArchive(archive), IsBatchPackage, copyProgress))
            yield return new(entry.Path, entry.Path == entry.DisplayPath ? Nax0Contents.LogicalPath(entry.Path) : entry.DisplayPath, entry.Error);
    }
}
