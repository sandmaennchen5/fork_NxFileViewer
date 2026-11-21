using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace NxFileViewer.Avalonia;

public partial class MainWindow
{
    private sealed record RenameBatchRow(string Source, string? Destination, long Size, DateTime Modified, string Status, bool CanApply) { public string? Error { get; init; } }
    private RenameBatchRow[] _renameBatch = Array.Empty<RenameBatchRow>();
    private bool _renamingBatch;
    private CancellationTokenSource? _renameBatchCancellation;
    private void CancelRenameBatch(object? sender, RoutedEventArgs e) => _renameBatchCancellation?.Cancel();

    private async void ChooseRenameFolder(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = _text.RenamingTool_WindowTitle });
        if (folders.FirstOrDefault()?.TryGetLocalPath() is { } path) RenameInputPath.Text = path;
    }
    private async void PreviewRenameBatch(object? sender, RoutedEventArgs e)
    {
        if (_renamingBatch) return;
        using var cancellation = new CancellationTokenSource();
        _renameBatchCancellation = cancellation;
        RenameBatchCancelButton.IsEnabled = true;
        _renamingBatch = true;
        RenameBatchPreviewButton.IsEnabled = RenameBatchApplyButton.IsEnabled = false;
        try
        {
            string[] paths;
            if (Directory.Exists(RenameInputPath.Text))
            {
                var root = RenameInputPath.Text!;
                var recursive = RenameRecursive.IsChecked == true;
                var patterns = (RenameFilter.Text ?? "*.nsp;*.nsz;*.xci;*.xcz").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                paths = await Task.Run(() => patterns.SelectMany(pattern => Directory.EnumerateFiles(root, pattern, new EnumerationOptions { RecurseSubdirectories = recursive, IgnoreInaccessible = true })).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
            }
            else
            {
                var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = _text.RenamingTool_WindowTitle, AllowMultiple = true, FileTypeFilter = new[] { new FilePickerFileType("Nintendo Switch") { Patterns = new[] { "*.nsp", "*.nsz", "*.xci", "*.xcz" } } } });
                paths = files.Select(file => file.TryGetLocalPath()).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            }
            if (paths.Length == 0 || _closed) return;
            var keys = _keysPath;
            string? pattern = null;
            var outputFolder = RenameOutputPath.Text;
            var rows = await Task.Run(() =>
            {
                var proposals = new List<RenameBatchRow>();
                foreach (var path in paths)
                {
                    cancellation.Token.ThrowIfCancellationRequested();
                    try
                    {
                        var info = new FileInfo(path);
                        var size = info.Length;
                        var modified = info.LastWriteTimeUtc;
                        var destination = GetRenameDestination(path, CreateOfflineName(path, keys, pattern));
                        if (!string.IsNullOrWhiteSpace(outputFolder)) destination = Path.Combine(Path.GetFullPath(outputFolder), Path.GetFileName(destination));
                        var changed = !string.Equals(path, destination, StringComparison.OrdinalIgnoreCase);
                        var available = !File.Exists(destination) && !Directory.Exists(destination);
                        proposals.Add(new RenameBatchRow(path, destination, size, modified,
                            changed ? (available ? _text.RenamingTool_StatusSimulation : _text.RenamingTool_StatusError) : _text.RenamingTool_StatusUnchanged,
                            changed && available) { Error = changed && !available ? "Target already exists." : null });
                    }
                    catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or LibHac.Common.LibHacException or LibHac.NSZ.NczFormatException or ZstdSharp.ZstdException or OverflowException)
                    {
                        proposals.Add(new RenameBatchRow(path, null, 0, default, _text.RenamingTool_StatusError, false) { Error = ex.Message });
                    }
                }
                var duplicates = proposals.Where(row => row.CanApply).GroupBy(row => row.Destination!, StringComparer.OrdinalIgnoreCase)
                    .Where(group => group.Count() > 1).Select(group => group.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
                return proposals.Select(row => row.Destination != null && duplicates.Contains(row.Destination)
                    ? row with { CanApply = false, Status = _text.RenamingTool_StatusError } : row).ToArray();
            });
            if (_closed) return;
            _renameBatch = rows;
            foreach (var row in rows) LogSession(row.Error == null ? "INFO" : "ERROR", row.Source + " -> " + row.Destination + " : " + row.Status + " " + row.Error);
            RenameBatchRows.ItemsSource = rows;
            RenameBatchStatus.Text = rows.Length.ToString("N0");
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { if (!_closed) RenameBatchStatus.Text = _text.Log_SaveFileCanceled; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            if (!_closed) RenameBatchStatus.Text = ex.Message;
        }
        finally
        {
            _renameBatchCancellation = null;
            if (!_closed) RenameBatchCancelButton.IsEnabled = false;
            _renamingBatch = false;
            if (!_closed)
            {
                RenameBatchPreviewButton.IsEnabled = true;
                RenameBatchApplyButton.IsEnabled = _renameBatch.Any(row => row.CanApply);
            }
        }
    }

    private async void ApplyRenameBatch(object? sender, RoutedEventArgs e)
    {
        if (_renamingBatch) return;
        if (RenameSimulation.IsChecked == true) { RenameBatchStatus.Text = _text.RenamingTool_StatusSimulation; return; }
        using var cancellation = new CancellationTokenSource();
        _renameBatchCancellation = cancellation;
        RenameBatchCancelButton.IsEnabled = true;
        _renamingBatch = true;
        RenameBatchPreviewButton.IsEnabled = RenameBatchApplyButton.IsEnabled = false;
        var rows = _renameBatch.ToArray();
        try
        {
            for (var index = 0; index < rows.Length && !_closed && !cancellation.IsCancellationRequested; index++)
            {
                var row = rows[index];
                if (!row.CanApply) continue;
                try
                {
                    var info = new FileInfo(row.Source);
                    if (info.Length != row.Size || info.LastWriteTimeUtc != row.Modified)
                        throw new IOException("Source changed since preview.");
                    Directory.CreateDirectory(Path.GetDirectoryName(row.Destination!)!);
                    File.Move(row.Source, row.Destination!, overwrite: false);
                    rows[index] = row with { CanApply = false, Status = _text.RenamingTool_StatusRenamed };
                    if (string.Equals(_openedPath, row.Source, StringComparison.OrdinalIgnoreCase))
                        await OpenFileAsync(row.Destination!);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
                {
                    rows[index] = row with { CanApply = false, Status = ex.Message };
                }
            }
            _renameBatch = rows;
            if (!_closed) RenameBatchRows.ItemsSource = rows;
        }
        finally
        {
            _renameBatchCancellation = null;
            if (!_closed) RenameBatchCancelButton.IsEnabled = false;
            _renamingBatch = false;
            if (!_closed) RenameBatchPreviewButton.IsEnabled = true;
        }
    }
}
