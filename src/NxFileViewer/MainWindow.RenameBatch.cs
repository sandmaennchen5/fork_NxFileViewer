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
    private sealed record RenameBatchRow(string Source, string? Destination, long Size, DateTime Modified, string Status, bool CanApply)
    {
        public string? Error { get; init; }
        public string? SourceRoot { get; init; }
        public string? TargetRoot { get; init; }
        private string Root => SourceRoot ?? Path.GetDirectoryName(Source)!;
        public string CompactSource => "QUELL::" + Path.GetRelativePath(Root, Source);
        public string? CompactDestination => Destination == null ? null :
            (TargetRoot == null ? "QUELL::" : "ZIEL::") + Path.GetRelativePath(TargetRoot ?? Root, Destination);
    }

    private void RenamePathDisplayChanged(object? sender, RoutedEventArgs e)
    {
        if (RenameBatchRows == null) return;
        var compact = RenameCompactPaths.IsChecked == true;
        foreach (var column in RenameBatchRows.Columns.OfType<global::Avalonia.Controls.DataGridTextColumn>())
        {
            var display = column.SortMemberPath switch
            {
                nameof(RenameBatchRow.Source) => compact ? nameof(RenameBatchRow.CompactSource) : nameof(RenameBatchRow.Source),
                nameof(RenameBatchRow.Destination) => compact ? nameof(RenameBatchRow.CompactDestination) : nameof(RenameBatchRow.Destination),
                _ => null
            };
            if (display != null) column.Binding = new global::Avalonia.Data.Binding(display);
        }
        RenameBatchRows.ItemsSource = null;
        RenameBatchRows.ItemsSource = _renameBatch;
    }
    private RenameBatchRow[] _renameBatch = Array.Empty<RenameBatchRow>();
    private bool _renamingBatch;
    private bool _settingRenameOverride;
    private string? _renameOverrideLabel;
    private string[]? _renameBatchSourcesOverride;
    private string _lastRenameInput = "";
    private int _renameInputRevision;

    private void RenameInputChanged(object? sender, global::Avalonia.Controls.TextChangedEventArgs e)
    {
        if (_settingRenameOverride || (_renameBatchSourcesOverride != null && RenameInputPath.Text == _renameOverrideLabel)) return;
        _renameBatchSourcesOverride = null;
        _renameOverrideLabel = null;
        _lastRenameInput = RenameInputPath.Text ?? "";
        InvalidateRenamePreview();
    }

    private void RenamePreviewInputChanged(object? sender, RoutedEventArgs e) => InvalidateRenamePreview();

    private void InvalidateRenamePreview()
    {
        ++_renameInputRevision;
        _renameBatch = Array.Empty<RenameBatchRow>();
        if (RenameBatchRows != null) RenameBatchRows.ItemsSource = _renameBatch;
        if (RenameBatchApplyButton != null) RenameBatchApplyButton.IsEnabled = false;
        if (RenameBatchStatus != null) RenameBatchStatus.Text = "";
    }

    private SavedRenameInputs CaptureRenameInputs() => new(
        _lastRenameInput, RenameOutputPath.Text ?? "", RenameFilter.Text ?? "",
        RenameRecursive.IsChecked == true, RenameAutoClose.IsChecked == true, RenameCompactPaths.IsChecked == true);

    private void RestoreRenameInputs()
    {
        if (_preferences.RenameInputs is not { } saved) return;
        _lastRenameInput = saved.Source;
        RenameInputPath.Text = saved.Source;
        RenameOutputPath.Text = saved.Destination;
        RenameFilter.Text = saved.Filter;
        RenameRecursive.IsChecked = saved.Recursive;
        RenameAutoClose.IsChecked = saved.AutoClose;
        RenameCompactPaths.IsChecked = saved.CompactPaths;
    }
    private CancellationTokenSource? _renameBatchCancellation;
    private void CancelRenameBatch(object? sender, RoutedEventArgs e) => _renameBatchCancellation?.Cancel();

    private async void ChooseRenameFolder(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = _text.RenamingTool_WindowTitle });
        if (folders.FirstOrDefault()?.TryGetLocalPath() is { } path) RenameInputPath.Text = path;
    }
    private async void ChooseRenameDestination(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = _text.RenamingTool_WindowTitle });
        if (folders.FirstOrDefault()?.TryGetLocalPath() is { } folder) RenameOutputPath.Text = folder;
    }
    private async void PreviewRenameBatch(object? sender, RoutedEventArgs e) => await PrepareRenameBatch();
    private async Task PrepareRenameBatch()
    {
        if (_renamingBatch) return;
        using var workspaceJob = LockWorkspace(RenameTab);
        using var cancellation = new CancellationTokenSource();
        _renameBatchCancellation = cancellation;
        RenameBatchCancelButton.IsEnabled = true;
        RenameBatchProgress.Value = 0;
        RenameBatchProgress.IsVisible = RenameBatchProgress.IsIndeterminate = true;
        _renamingBatch = true;
        _renameBatch = Array.Empty<RenameBatchRow>();
        RenameBatchRows.ItemsSource = _renameBatch;
        var revision = _renameInputRevision;
        RenameBatchPreviewButton.IsEnabled = RenameBatchApplyButton.IsEnabled = false;
        try
        {
            string[] paths;
            if (_renameBatchSourcesOverride != null) paths = _renameBatchSourcesOverride.ToArray();
            else if (Directory.Exists(RenameInputPath.Text))
            {
                var root = RenameInputPath.Text!;
                var recursive = RenameRecursive.IsChecked == true;
                var patterns = (RenameFilter.Text ?? "*.nsp;*.nsz;*.xci;*.xcz").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                paths = await Task.Run(() => patterns.SelectMany(pattern => Directory.EnumerateFiles(root, pattern, new EnumerationOptions { RecurseSubdirectories = recursive, IgnoreInaccessible = true })).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
            }
            else if (File.Exists(RenameInputPath.Text)) paths = new[] { Path.GetFullPath(RenameInputPath.Text!) };
            else
            {
                var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = _text.RenamingTool_WindowTitle, AllowMultiple = true, FileTypeFilter = new[] { new FilePickerFileType("Nintendo Switch") { Patterns = new[] { "*.nsp", "*.nsz", "*.xci", "*.xcz" } } } });
                paths = files.Select(file => file.TryGetLocalPath()).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                if (paths.Length == 1) _lastRenameInput = paths[0];
            }
            if (paths.Length == 0 || _closed) return;
            cancellation.Token.ThrowIfCancellationRequested();
            RenameBatchProgress.IsIndeterminate = false;
            var completed = 0;
            IProgress<int> progress = new Progress<int>(percent =>
            {
                if (!_closed && ReferenceEquals(_renameBatchCancellation, cancellation))
                    RenameBatchProgress.Value = Math.Max(RenameBatchProgress.Value, percent);
            });
            var keys = _keysPath;
            var sourceRoot = Directory.Exists(RenameInputPath.Text) ? Path.GetFullPath(RenameInputPath.Text!) : null;
            var outputFolder = RenameOutputPath.Text;
            var targetRoot = string.IsNullOrWhiteSpace(outputFolder) ? null : Path.GetFullPath(outputFolder);
            if (sourceRoot != null && targetRoot != null && string.Equals(Path.TrimEndingDirectorySeparator(sourceRoot),
                Path.TrimEndingDirectorySeparator(targetRoot), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                targetRoot = null;
            var lookup = CreateRenameTitleLookup();
            var rows = await Task.Run(() =>
            {
                var proposals = new RenameBatchRow[paths.Length];
                Parallel.For(0, paths.Length, new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = cancellation.Token }, index =>
                {
                    var path = paths[index];
                    try
                    {
                        var info = new FileInfo(path);
                        var size = info.Length;
                        var modified = info.LastWriteTimeUtc;
                        var destination = GetRenameDestination(path, CreateRenameName(path, keys, null, lookup: lookup, token: cancellation.Token), outputFolder);
                        var changed = !string.Equals(path, destination, StringComparison.OrdinalIgnoreCase);
                        var available = !File.Exists(destination) && !Directory.Exists(destination);
                        proposals[index] = new RenameBatchRow(path, destination, size, modified,
                            changed ? (available ? _text.RenamingTool_StatusSimulation : _text.RenamingTool_StatusError) : _text.RenamingTool_StatusUnchanged,
                            changed && available) { SourceRoot = sourceRoot, TargetRoot = targetRoot, Error = changed && !available ? "Target already exists." : null };
                    }
                    catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or LibHac.Common.LibHacException or LibHac.NSZ.NczFormatException or ZstdSharp.ZstdException or OverflowException)
                    {
                        proposals[index] = new RenameBatchRow(path, null, 0, default, _text.RenamingTool_StatusError, false) { SourceRoot = sourceRoot, Error = ex.Message };
                    }
                    var done = Interlocked.Increment(ref completed);
                    var percent = (int)(100L * done / paths.Length);
                    if (percent != 100L * (done - 1) / paths.Length) progress.Report(percent);
                });
                var duplicates = proposals.Where(row => row.CanApply).GroupBy(row => row.Destination!, StringComparer.OrdinalIgnoreCase)
                    .Where(group => group.Count() > 1).Select(group => group.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
                return proposals.Select(row => row.Destination != null && duplicates.Contains(row.Destination)
                    ? row with { CanApply = false, Status = _text.RenamingTool_StatusError } : row).ToArray();
            });
            if (_closed || revision != _renameInputRevision) return;
            _renameBatch = rows;
            RenameBatchRows.ItemsSource = rows;
            RenameBatchStatus.Text = rows.Length.ToString("N0");
            LogSessionEntries(rows.Select(row => (row.Error == null ? "INFO" : "ERROR",
                row.Source + " -> " + row.Destination + " : " + row.Status + " " + row.Error)), _preferences.LogMinimumLevel);
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
                RenameBatchProgress.IsVisible = false;
                RenameBatchPreviewButton.IsEnabled = true;
                RenameBatchApplyButton.IsEnabled = _renameBatch.Any(row => row.CanApply);
            }
        }
    }

    private async void ApplyRenameBatch(object? sender, RoutedEventArgs e) => await ApplyPreparedRenameBatch();
    private async Task ApplyPreparedRenameBatch()
    {
        if (_renamingBatch || !_renameBatch.Any(row => row.CanApply)) return;
        using var workspaceJob = LockWorkspace(RenameTab);
        using var cancellation = new CancellationTokenSource();
        _renameBatchCancellation = cancellation;
        RenameBatchCancelButton.IsEnabled = true;
        RenameBatchProgress.Value = 0;
        RenameBatchProgress.IsIndeterminate = false;
        RenameBatchProgress.IsVisible = true;
        _renamingBatch = true;
        RenameBatchPreviewButton.IsEnabled = RenameBatchApplyButton.IsEnabled = false;
        var rows = _renameBatch.ToArray();
        var total = rows.Count(row => row.CanApply);
        var completed = 0;
        var revision = _renameInputRevision;
        var autoClose = RenameAutoClose.IsChecked == true;
        try
        {
            for (var index = 0; index < rows.Length && !_closed && revision == _renameInputRevision; index++)
            {
                cancellation.Token.ThrowIfCancellationRequested();
                var row = rows[index];
                if (!row.CanApply) continue;
                try
                {
                    if (string.Equals(_openedPath, row.Source, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!autoClose) throw new IOException("The file is currently open. Close it or enable automatic closing.");
                        CloseClicked(null, new RoutedEventArgs()); Workspace.SelectedItem = RenameTab;
                    }
                    await Task.Run(() =>
                    {
                        cancellation.Token.ThrowIfCancellationRequested();
                        var info = new FileInfo(row.Source);
                        if (info.Length != row.Size || info.LastWriteTimeUtc != row.Modified)
                            throw new IOException("Source changed since preview.");
                        EnsureRenameDirectoryIsLocal(row.Destination!);
                        Directory.CreateDirectory(Path.GetDirectoryName(row.Destination!)!);
                        File.Move(row.Source, row.Destination!, overwrite: false);
                    });
                    rows[index] = row with { CanApply = false, Status = _text.RenamingTool_StatusRenamed };
                    if (string.Equals(_openedPath, row.Source, StringComparison.OrdinalIgnoreCase))
                        await OpenFileAsync(row.Destination!);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
                {
                    rows[index] = row with { CanApply = false, Status = ex.Message };
                }
                if (!_closed) RenameBatchProgress.Value = 100.0 * ++completed / total;
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            if (!_closed) RenameBatchStatus.Text = _text.Log_SaveFileCanceled;
        }
        finally
        {
            if (revision == _renameInputRevision)
            {
                _renameBatch = rows;
                if (!_closed) RenameBatchRows.ItemsSource = rows;
            }
            _renameBatchCancellation = null;
            if (!_closed) RenameBatchCancelButton.IsEnabled = false;
            _renamingBatch = false;
            if (!_closed)
            {
                RenameBatchProgress.IsVisible = false;
                RenameBatchPreviewButton.IsEnabled = true;
            }
        }
    }
}
