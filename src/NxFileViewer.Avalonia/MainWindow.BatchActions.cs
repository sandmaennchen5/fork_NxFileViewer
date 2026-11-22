using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
namespace NxFileViewer.Avalonia;
public partial class MainWindow
{
    private sealed record BatchHistoryEntry(DateTime Time, string Source, BatchSource[] Paths)
    { public override string ToString() => $"{Time:g} · {Paths.Length} · {Source}"; }
    private List<BatchHistoryEntry> _batchHistory = new();
    private static string BatchHistoryPath => Path.Combine(AppContext.BaseDirectory, "Cache", "Avalonia", "batch-history.json");
    private void LoadBatchHistory()
    {
        try { if (File.Exists(BatchHistoryPath)) _batchHistory = JsonSerializer.Deserialize<List<BatchHistoryEntry>>(File.ReadAllText(BatchHistoryPath)) ?? new(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { SettingsStatus.Text = ex.Message; }
        BatchHistoryChoice.ItemsSource = _batchHistory.ToArray();
        if (_batchHistory.Count > 0) BatchHistoryChoice.SelectedIndex = 0;
    }
    private void SaveBatchHistory(string source)
    {
        if (!_preferences.BatchHistoryEnabled) return;
        _batchHistory.Insert(0, new(DateTime.Now, source, _batchRows.Select(row => new BatchSource(row.Path, row.DisplayPath)).ToArray()));
        _batchHistory = _batchHistory.Take(Math.Clamp(_preferences.BatchHistoryLimit, 1, 100)).ToList();
        Directory.CreateDirectory(Path.GetDirectoryName(BatchHistoryPath)!);
        var temporary = BatchHistoryPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temporary, JsonSerializer.Serialize(_batchHistory)); File.Move(temporary, BatchHistoryPath, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        BatchHistoryChoice.ItemsSource = _batchHistory.ToArray(); BatchHistoryChoice.SelectedIndex = 0;
    }
    private async void ResumeBatchHistory(object? sender, RoutedEventArgs e)
    {
        if (_batchCancellation != null || BatchHistoryChoice.SelectedItem is not BatchHistoryEntry history) return;
        using var cancellation = new System.Threading.CancellationTokenSource(); _batchCancellation = cancellation; SetBatchBusy(true);
        _batchRows.Clear(); var keys = _keysPath;
        try
        {
            foreach (var path in history.Paths)
            {
                cancellation.Token.ThrowIfCancellationRequested();
                var row = await Task.Run(() => ReadBatchRow(path.Path, path.DisplayPath, keys, false, cancellation.Token));
                if (_closed) return; _batchRows.Add(row); RefreshBatchFilter();
            }
            BatchInputPath.Text = history.Source;
            StatusText.Text = _text.BatchHistory_LoadedNotice;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        finally { _batchCancellation = null; if (!_closed) SetBatchBusy(false); }
    }
    private void ChooseBatchColumns(object? sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu();
        foreach (var column in BatchResults.Columns.OrderBy(column => column.DisplayIndex))
        {
            var item = new MenuItem { Header = column.Header?.ToString(), ToggleType = MenuItemToggleType.CheckBox, IsChecked = column.IsVisible };
            item.Click += (_, _) => column.IsVisible = item.IsChecked;
            menu.Items.Add(item);
        }
        if (sender is Control target) menu.Open(target);
    }
    private void ResetBatchColumns(object? sender, RoutedEventArgs e)
    { for (var i = 0; i < BatchResults.Columns.Count; i++) { var column = BatchResults.Columns[i]; column.IsVisible = _defaultBatchColumns.GetValueOrDefault(column).Visible; column.DisplayIndex = i; column.Width = _defaultBatchColumns.GetValueOrDefault(column).Width; } }
    private async Task<RenameBatchRow[]> PrepareBatchNames(bool updateStatus)
    {
        var rows = FilteredBatchRows().DistinctBy(row => row.Path).ToArray(); var keys = _keysPath; var result = new List<RenameBatchRow>();
        foreach (var row in rows)
        {
            if (row.DisplayPath != row.Path || Emignatik.NxFileViewer.Services.Mtp.MtpDirectSource.IsDirect(row.Path)) continue;
            try
            {
                var target = await Task.Run(() => GetRenameDestination(row.Path, CreateOfflineName(row.Path, keys, null)));
                var info = new FileInfo(row.Path); var changed = !string.Equals(target, row.Path, StringComparison.OrdinalIgnoreCase);
                var available = !File.Exists(target) && !Directory.Exists(target);
                result.Add(new(row.Path, target, info.Length, info.LastWriteTimeUtc, changed ? available ? _text.RenamingTool_StatusSimulation : _text.RenamingTool_StatusError : _text.RenamingTool_StatusUnchanged, changed && available));
                if (updateStatus) { var index = Array.FindIndex(_batchRows.ToArray(), candidate => candidate.Path == row.Path); if (index >= 0) _batchRows[index] = row with { Naming = changed ? _text.BatchNaming_Differs : _text.BatchNaming_Matches, NewName = System.IO.Path.GetFileName(target), NamingError = "" }; }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or LibHac.Common.LibHacException or LibHac.NSZ.NczFormatException or ZstdSharp.ZstdException)
            { result.Add(new(row.Path, null, 0, default, _text.RenamingTool_StatusError, false) { Error = ex.Message }); var index = Array.FindIndex(_batchRows.ToArray(), candidate => candidate.Path == row.Path); if (index >= 0) _batchRows[index] = row with { Naming = _text.BatchNaming_Error, NamingError = ex.Message }; }
        }
        var collisions = result.Where(row => row.CanApply).GroupBy(row => row.Destination!, StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1).Select(group => group.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        RefreshBatchFilter();
        return result.Select(row => row.Destination != null && collisions.Contains(row.Destination) ? row with { CanApply = false, Status = _text.RenamingTool_StatusError, Error = "Target collision" } : row).ToArray();
    }
    private async void CheckBatchNames(object? sender, RoutedEventArgs e) { if (_batchCancellation == null) await PrepareBatchNames(true); }
    private async void RenameBatchNames(object? sender, RoutedEventArgs e)
    {
        if (_batchCancellation != null) return;
        _renameBatch = await PrepareBatchNames(true); if (_closed) return;
        _settingRenameOverride = true;
        RenameInputPath.Text = _renameBatch.Length + " · " + _text.BatchIntegrity_Title;
        _settingRenameOverride = false;
        _renameBatchSourcesOverride = _renameBatch.Select(row => row.Source).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        RenameBatchRows.ItemsSource = _renameBatch; RenameSimulation.IsChecked = true;
        RenameBatchApplyButton.IsEnabled = _renameBatch.Any(row => row.CanApply);
        RenameBatchStatus.Text = _renameBatch.Length.ToString("N0"); Workspace.SelectedItem = RenameTab;
    }
    private async void MoveValidBatch(object? sender, RoutedEventArgs e)
    {
        if (_batchCancellation != null) return;
        var rows = FilteredBatchRows().DistinctBy(row => row.Path).Where(row => row.Verified && row.Original && !row.HasError && row.DisplayPath == row.Path && !Emignatik.NxFileViewer.Services.Mtp.MtpDirectSource.IsDirect(row.Path)).ToArray();
        if (rows.Length == 0) { StatusText.Text = _text.NcasIntegrity_Unchecked; return; }
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = _text.BatchIntegrity_MoveValid });
        if (folders.FirstOrDefault()?.TryGetLocalPath() is not { } folder) return;
        foreach (var row in rows)
        {
            try
            {
                var info = new FileInfo(row.Path);
                if (!info.Exists || info.Length != row.Package?.Size || info.LastWriteTimeUtc.Ticks != row.Modified) throw new IOException("Source changed since verification: " + row.Path);
                var destination = Path.Combine(folder, info.Name); File.Move(row.Path, destination, false);
                var index = Array.FindIndex(_batchRows.ToArray(), candidate => candidate.Path == row.Path); if (index >= 0) _batchRows[index] = row with { Path = destination, DisplayPath = destination };
                LogSession("INFO", row.Path + " → " + destination);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { StatusText.Text = ex.Message; LogSession("ERROR", ex.Message); }
        }
        RefreshBatchFilter();
    }
}
