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
    private async void ChooseBatchColumns(object? sender, RoutedEventArgs e)
    {
        var panel = new StackPanel { Margin = new global::Avalonia.Thickness(16), Spacing = 8 };
        foreach (var column in BatchResults.Columns)
        {
            var check = new CheckBox { Content = column.Header?.ToString(), IsChecked = column.IsVisible };
            check.IsCheckedChanged += (_, _) => column.IsVisible = check.IsChecked == true;
            panel.Children.Add(check);
        }
        var dialog = new Window { Title = _text.BatchTable_Columns, Width = 360, Height = 520, WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = new ScrollViewer { Content = panel } };
        dialog.RequestedThemeVariant = RequestedThemeVariant;
        dialog.Styles.Add(new global::Avalonia.Markup.Xaml.Styling.StyleInclude(new Uri("avares://NxFileViewerFork.Avalonia/")) { Source = new Uri("avares://NxFileViewerFork.Avalonia/WorkspaceStyles.axaml") });
        WpfTheme.Apply(dialog);
        var close = new Button { Content = _text.MenuItem_Close }; close.Click += (_, _) => dialog.Close(); panel.Children.Add(close);
        await dialog.ShowDialog(this);
    }
    private void ResetBatchColumns(object? sender, RoutedEventArgs e)
    { for (var i = 0; i < BatchResults.Columns.Count; i++) { var column = BatchResults.Columns[i]; column.IsVisible = true; column.DisplayIndex = i; column.Width = new DataGridLength(i == 0 ? 260 : 150); } }
    private async Task<RenameBatchRow[]> PrepareBatchNames(bool updateStatus)
    {
        var rows = FilteredBatchRows(); var keys = _keysPath; var result = new List<RenameBatchRow>();
        foreach (var row in rows)
        {
            if (row.DisplayPath != row.Path) continue;
            try
            {
                var target = await Task.Run(() => GetRenameDestination(row.Path, CreateOfflineName(row.Path, keys, null)));
                var info = new FileInfo(row.Path); var changed = !string.Equals(target, row.Path, StringComparison.OrdinalIgnoreCase);
                var available = !File.Exists(target) && !Directory.Exists(target);
                result.Add(new(row.Path, target, info.Length, info.LastWriteTimeUtc, changed ? available ? _text.RenamingTool_StatusSimulation : _text.RenamingTool_StatusError : _text.RenamingTool_StatusUnchanged, changed && available));
                if (updateStatus) { var index = _batchRows.IndexOf(row); if (index >= 0) _batchRows[index] = row with { Naming = changed ? _text.BatchNaming_Differs : _text.BatchNaming_Matches }; }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or LibHac.Common.LibHacException or LibHac.NSZ.NczFormatException or ZstdSharp.ZstdException)
            { result.Add(new(row.Path, null, 0, default, _text.RenamingTool_StatusError, false) { Error = ex.Message }); var index = _batchRows.IndexOf(row); if (index >= 0) _batchRows[index] = row with { Naming = _text.BatchNaming_Error, Error = ex.Message }; }
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
        RenameBatchRows.ItemsSource = _renameBatch; RenameSimulation.IsChecked = true;
        RenameBatchApplyButton.IsEnabled = _renameBatch.Any(row => row.CanApply);
        RenameBatchStatus.Text = _renameBatch.Length.ToString("N0"); Workspace.SelectedItem = RenameTab;
    }
    private async void MoveValidBatch(object? sender, RoutedEventArgs e)
    {
        if (_batchCancellation != null) return;
        var rows = FilteredBatchRows().Where(row => row.Verified && row.Original && !row.HasError && row.DisplayPath == row.Path).ToArray();
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
                var index = _batchRows.IndexOf(row); if (index >= 0) _batchRows[index] = row with { Path = destination, DisplayPath = destination };
                LogSession("INFO", row.Path + " → " + destination);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { StatusText.Text = ex.Message; LogSession("ERROR", ex.Message); }
        }
        RefreshBatchFilter();
    }
}
