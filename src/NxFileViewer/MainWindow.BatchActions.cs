using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
namespace NxFileViewer.Avalonia;
public partial class MainWindow
{
    private sealed record BatchHistoryEntry(DateTime Time, string Source, BatchSource[] Paths, BatchRow[]? Results = null, Guid Id = default)
    {
        public BatchResumeOptions? Options { get; init; }
        public override string ToString() => $"{Time:g} · {Paths.Length} · {Source}";
    }
    private sealed record BatchResumeOptions(string[] Sources, bool Verify, bool Recursive, bool IncludeArchives,
        bool IgnoreMissingDeltaFragments, Emignatik.NxFileViewer.FileLoading.SdBatchAreas Areas);
    private List<BatchHistoryEntry> _batchHistory = new();
    private static readonly JsonSerializerOptions BatchHistoryJson = new() { IgnoreReadOnlyProperties = true };
    private static string BatchHistoryPath => Path.Combine(ViewerDataDirectory.Root, "History", "batch-history.json");
    private void LoadBatchHistory()
    {
        try
        {
            var oldDirectory = Path.Combine(BatchHistoryDirectory, "Avalonia");
            if (Directory.Exists(oldDirectory))
            {
                foreach (var archive in Directory.EnumerateFiles(oldDirectory, "scan-*.zip"))
                {
                    var destination = Path.Combine(BatchHistoryDirectory, Path.GetFileName(archive));
                    if (!File.Exists(destination)) File.Move(archive, destination);
                }
            }
            var path = new[] { BatchHistoryPath, Path.Combine(BatchHistoryDirectory, "batch-history.avalonia.json"),
                Path.Combine(AppContext.BaseDirectory, "Cache", "Avalonia", "batch-history.json") }.FirstOrDefault(File.Exists);
            var archives = Directory.Exists(BatchHistoryDirectory)
                ? Directory.GetFiles(BatchHistoryDirectory, "scan-*.zip") : [];
            foreach (var archive in archives)
            {
                try
                {
                    if (ReadBatchScanArchive(archive, headerOnly: true) is { } entry &&
                        string.Equals(Path.GetFullPath(BatchScanArchive(entry.Id)), Path.GetFullPath(archive), StringComparison.Ordinal))
                        _batchHistory.Add(entry);
                }
                catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or JsonException)
                {
                    LogSession("WARN", $"Could not read batch history archive: {error.Message}");
                }
            }
            if (archives.Length == 0 && path != null)
                _batchHistory = (JsonSerializer.Deserialize<List<BatchHistoryEntry>>(File.ReadAllText(path)) ?? [])
                    .Select(entry => entry.Id == Guid.Empty ? entry with { Id = Guid.NewGuid() } : entry).ToList();
            var imported = ReadWpfBatchHistory();
            _batchHistory = imported.Concat(_batchHistory).DistinctBy(entry => entry.Id).OrderByDescending(entry => entry.Time)
                .Take(Math.Clamp(_preferences.BatchHistoryLimit, 1, 100)).ToList();
            foreach (var scan in _batchHistory.Where(imported.Contains))
                WriteBatchScanArchive(scan, includeLogos: true);
            if (imported.Count > 0) LogSession("INFO", $"WPF batch history: {imported.Count} scan(s)");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            SettingsStatus.Text = ex.Message;
            LogSession("WARN", $"Could not load batch history: {ex.Message}");
        }
        BatchHistoryChoice.ItemsSource = _batchHistory.ToArray();
        if (_batchHistory.Count > 0) BatchHistoryChoice.SelectedIndex = 0;
    }
    private BatchRow StoreBatchRow(BatchRow row, bool includeLogos)
    {
        var metadata = new Dictionary<string, string>();
        foreach (var property in typeof(BatchRow).GetProperties().Where(property => property.PropertyType == typeof(string) && !property.CanWrite))
            if (property.GetValue(row) is string value) metadata[property.Name] = value;
        var content = row.ContentOverride ?? row.Package?.Titles.FirstOrDefault();
        var languages = row.StoredLanguages ?? (row.Package?.Homebrew is { } homebrew
            ? GetHomebrewLanguages(homebrew) : GetOverviewLanguages(content));
        return row with
        {
            StoredMetadata = metadata, StoredSize = row.Package?.Size ?? row.StoredSize,
            StoredDemo = row.StoredDemo ?? content?.NacpData?.IsDemo,
            Path = PersistentBatchPath(row),
            StoredLanguages = includeLogos ? languages : languages.Select(language => language with { Icon = null, IconReference = null }).ToArray(),
            StoredParts = row.ShowPackageDetails ? row.PackageDetails.Select(part => StoreBatchRow(part, includeLogos)).ToArray() : null,
            Package = null, ContentOverride = null
        };
    }

    private void SaveBatchHistory(string source, BatchResumeOptions? options = null)
    {
        if (!_preferences.BatchHistoryEnabled) return;
        try
        {
            var keepLogos = _preferences.SharedSettings.SaveBatchHistoryLogos;
            var scan = new BatchHistoryEntry(DateTime.Now, source,
                _batchRows.Select(row => new BatchSource(PersistentBatchPath(row), row.DisplayPath)).ToArray(),
                _batchRows.Select(row => StoreBatchRow(row, keepLogos)).ToArray(), _batchScanId)
            { Options = options ?? _batchHistory.FirstOrDefault(entry => entry.Id == _batchScanId)?.Options };
            var previous = _batchHistory.Where(entry => entry.Id != scan.Id).ToArray();
            var history = previous.Prepend(scan).Take(Math.Clamp(_preferences.BatchHistoryLimit, 1, 100)).ToList();
            foreach (var legacy in history.Skip(1).Where(entry => !File.Exists(BatchScanArchive(entry.Id))))
                WriteBatchScanArchive(legacy, keepLogos);
            WriteBatchScanArchive(scan, keepLogos);
            _batchHistory = history.Select(entry => entry with { Results = null }).ToList();
            BatchHistoryChoice.ItemsSource = _batchHistory.ToArray();
            BatchHistoryChoice.SelectedIndex = 0;
            foreach (var entry in previous.Where(entry => !history.Contains(entry)))
                File.Delete(BatchScanArchive(entry.Id));
            LogSession("TRACE", $"Saved batch history: results={_batchRows.Count}; logos={keepLogos}");
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or JsonException)
        {
            LogSession("WARN", $"Could not save batch history: {error.Message}");
        }
    }

    private static string PersistentBatchPath(BatchRow row) =>
        row.DisplayPath.Contains(" :: ", StringComparison.Ordinal) ? row.DisplayPath : row.Path;
    private async void ResumeBatchHistory(object? sender, RoutedEventArgs e)
    {
        if (_batchCancellation != null || BatchHistoryChoice.SelectedItem is not BatchHistoryEntry history) return;
        var archive = BatchScanArchive(history.Id);
        try
        {
            if (File.Exists(archive)) history = await Task.Run(() => ReadBatchScanArchive(archive, headerOnly: false)) ?? history;
            if (_closed || _batchCancellation != null) return;
            var options = history.Options;
            if (options == null && (Directory.Exists(history.Source) || File.Exists(history.Source)))
                options = new([history.Source], history.Results?.Any(row => row.Verified) == true,
                    BatchRecursive.IsChecked == true, BatchIncludeArchives.IsChecked == true,
                    _preferences.SharedSettings.IgnoreMissingDeltaFragments,
                    Emignatik.NxFileViewer.FileLoading.SdBatchAreas.From(_preferences.SharedSettings) with { SourceMode = _batchSourceMode });
            if (options == null || !options.Sources.Any(path => Directory.Exists(path) || File.Exists(path)))
            {
                StatusText.Text = string.Format(_text.FileNotSupported_Log, history.Source);
                return;
            }
            BatchInputPath.Text = history.Source;
            BatchRecursive.IsChecked = options.Recursive;
            BatchIncludeArchives.IsChecked = options.IncludeArchives;
            BatchSourceMode.SelectedIndex = (int)options.Areas.SourceMode;
            await RunBatchAsync(options.Sources, options.Verify, checkpoint: history with { Options = options });
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or JsonException)
        { if (!_closed) StatusText.Text = error.Message; }
    }

    private async void ShowBatchHistory(object? sender, RoutedEventArgs e)
    {
        if (_batchCancellation != null || BatchHistoryChoice.SelectedItem is not BatchHistoryEntry history) return;
        using var workspaceJob = LockWorkspace(BatchTab);
        using var cancellation = new System.Threading.CancellationTokenSource(); _batchCancellation = cancellation; SetBatchBusy(true);
        ResetBatchWorkingFiles();
        _batchRows.Clear(); var keys = _keysPath;
        try
        {
            var archive = BatchScanArchive(history.Id);
            if (File.Exists(archive))
                history = await Task.Run(() => ReadBatchScanArchive(archive, headerOnly: false), cancellation.Token) ?? history;
            cancellation.Token.ThrowIfCancellationRequested();
            LogSession("DEBUG", $"Restoring batch history: entries={history.Paths.Length}; storedResults={history.Results != null}");
            if (history.Results != null)
            {
                foreach (var row in history.Results)
                    _batchRows.Add(RestoreBatchPaths(row));
                RefreshBatchFilter(followResult: _batchRows.FirstOrDefault());
            }
            else
            {
                foreach (var path in history.Paths)
                {
                    cancellation.Token.ThrowIfCancellationRequested();
                    var input = path.DisplayPath.Contains(" :: ", StringComparison.Ordinal) ? path.DisplayPath : path.Path;
                    var row = await Task.Run(() => ReadBatchRow(input, path.DisplayPath, keys, false, cancellation.Token));
                    if (_closed) return;
                    _batchRows.Add(StoreActiveBatchArtwork(row));
                    LogBatchFile(row);
                    RefreshBatchFilter(followResult: row);
                }
            }
            BatchInputPath.Text = history.Source;
            FlushBatchArtwork();
            StatusText.Text = _text.BatchHistory_LoadedNotice;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or JsonException)
        {
            if (!_closed)
            {
                StatusText.Text = error.Message;
                LogSession("WARN", $"Could not restore batch history: {error.Message}");
            }
        }
        finally
        {
            _batchCancellation = null;
            ClearBatchExtraction();
            if (!_closed) SetBatchBusy(false);
            else ResetBatchWorkingFiles();
        }
    }
    private static BatchRow RestoreBatchPaths(BatchRow row) =>
        row with { Path = PersistentBatchPath(row), StoredParts = row.StoredParts?.Select(RestoreBatchPaths).ToArray() };
    private void OpenBatchContextMenu(object? sender, ContextRequestedEventArgs e)
    {
        if (IsBatchPackageTableEvent(e.Source)) return;
        if (_workspaceJobs.Count > 0 || _batchCancellation != null || _mtpCancellation != null) return;
        var clicked = (e.Source as global::Avalonia.Visual)?.FindAncestorOfType<DataGridRow>(includeSelf: true);
        if (clicked?.DataContext is BatchRow clickedRow && !BatchResults.SelectedItems.Contains(clickedRow))
            BatchResults.SelectedItem = clickedRow;
        else if (clicked == null && e.TryGetPosition(BatchResults, out _))
            return;

        var selected = BatchResults.SelectedItems.OfType<BatchRow>().ToArray();
        var files = selected.DistinctBy(row => row.Path).ToArray();
        if (files.Length == 0) return;

        var menu = new ContextMenu();
        void Add(string label, Func<Task> action, bool enabled = true)
        {
            var item = new MenuItem { Header = label, IsEnabled = enabled };
            item.Click += async (_, _) => await action();
            menu.Items.Add(item);
        }

        if (files.Length == 1)
        {
            var row = clicked?.DataContext as BatchRow ?? files[0];
            var source = row.DisplayPath.Split(" :: ", 2)[0];
            var folder = Path.GetDirectoryName(source);
            Add(_text.BatchTable_OpenExplorer, () => OpenLocalDirectoryAsync(folder!), Directory.Exists(folder));
            Add(_text.BatchIntegrity_OpenSelected, () => OpenBatchFileAsync(row), IsAvailableBatchSource(row.Path));
            if (selected.Length == 1 && row.TitleId.Length == 16 && row.TitleId.All(Uri.IsHexDigit))
                Add(_text.MenuItem_OpenTitleWebPage, () => { OpenTitlePageForId(row.TitleId); return Task.CompletedTask; });
        }

        var paths = files.Select(row => row.Path).ToHashSet();
        var verifiable = _batchRows.Where(row => paths.Contains(row.Path) && row.Nand == null && IsAvailableBatchSource(row.Path)).ToArray();
        Add(_text.MenuItem_CheckIntegrity, () => VerifyBatchSelectionAsync(verifiable), verifiable.Length > 0);

        var renameable = _batchRows.Where(row => paths.Contains(row.Path) && row.RawFileType is "NSP" or "NSZ" or "XCI" or "XCZ" &&
            row.DisplayPath == row.Path && File.Exists(row.Path)).ToArray();
        Add(_text.BatchNaming_Check, async () => { await PrepareBatchNames(true, renameable); }, renameable.Length > 0);
        Add(_text.RenamingTool_Button_Rename, () => OpenBatchRenamingAsync(renameable), renameable.Length > 0);
        Add(_text.BatchIntegrity_MoveSelected, () => MoveValidBatchAsync(files), files.Any(CanMoveBatchRow));

        void AddPackageAction(string label, string action, BatchRow[] inputs, string? target = null)
        {
            if (inputs.Length == 0) return;
            var source = inputs[0].DisplayPath.Split(" :: ", 2)[0];
            Add(label, () => RunPackageActionAsync(action, inputs.Select(row => row.Path).ToArray(), source, label, target));
        }

        var packages = files.Where(row => !row.HasError && IsAvailableBatchSource(row.Path) && row.RawFileType is "NSP" or "NSZ" or "XCI" or "XCZ").ToArray();
        if (_preferences.NszActive)
        {
            AddPackageAction(_text.Nsz_Compress, "Compress", packages.Where(row => row.RawFileType is "NSP" or "XCI").ToArray());
            AddPackageAction(_text.Nsz_Decompress, "Decompress", packages.Where(row => row.RawFileType is "NSZ" or "XCZ").ToArray());
        }
        if (_preferences.PackageOperationsActive && packages.Length > 0)
        {
            menu.Items.Add(new Separator());
            AddPackageAction(_text.Package_ConvertNsp, "Nsp", packages);
            AddPackageAction(_text.Package_ConvertXci, "Xci", packages);
            var combined = packages.Where(row => row.HasMultiplePackages).ToArray();
            AddPackageAction(_text.Package_Split, "Split", combined, "nsp");
            AddPackageAction(_text.Package_SplitXci, "Split", combined, "xci");
            if (packages.Length > 1)
            {
                AddPackageAction(_text.Package_MergeNsp, "Merge", packages, "nsp");
                AddPackageAction(_text.Package_MergeXci, "Merge", packages, "xci");
            }
        }

        e.Handled = true;
        menu.Open((Control?)clicked ?? BatchResults);
    }

    private async Task OpenLocalDirectoryAsync(string folder)
    {
        try
        {
            await Launcher.LaunchDirectoryInfoAsync(new DirectoryInfo(folder));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            StatusText.Text = ex.Message;
        }
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
    private async void ResetBatchColumns(object? sender, RoutedEventArgs e)
    {
        if (!await ConfirmPluginCleanupAsync(_text.BatchTable_ResetColumns, _text.BatchTable_ConfirmResetColumns,
            _text.Dialog_Yes, _text.Dialog_No)) return;

        _resettingBatchColumns = true;
        try
        {
            for (var i = 0; i < BatchResults.Columns.Count; i++)
            {
                var column = BatchResults.Columns[i];
                var defaults = _defaultBatchColumns[column];
                column.DisplayIndex = i;
                column.IsVisible = defaults.Visible;
                column.Width = defaults.Width;
            }
        }
        finally { _resettingBatchColumns = false; }
        RefreshBatchPackageTables();
        SaveResetBatchLayout();
    }
    private async Task<RenameBatchRow[]> PrepareBatchNames(bool updateStatus, BatchRow[]? selection = null)
    {
        var rows = (selection ?? FilteredBatchRows()).DistinctBy(row => row.Path).ToArray(); var keys = _keysPath; var result = new List<RenameBatchRow>();
        var lookup = CreateRenameTitleLookup();
        foreach (var row in rows)
        {
            if (row.DisplayPath != row.Path || Emignatik.NxFileViewer.Services.Mtp.MtpDirectSource.IsDirect(row.Path)) continue;
            try
            {
                var target = await Task.Run(() =>
                {
                    var current = new FileInfo(row.Path);
                    var package = current.LastWriteTimeUtc.Ticks == row.Modified && row.Package?.Size == current.Length
                        ? row.Package : null;
                    return GetRenameDestination(row.Path, CreateRenameName(row.Path, keys, null, package, lookup));
                });
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
    private async void RenameBatchNames(object? sender, RoutedEventArgs e) => await OpenBatchRenamingAsync();

    private async Task OpenBatchRenamingAsync(BatchRow[]? selection = null)
    {
        if (_batchCancellation != null) return;
        _renameBatch = await PrepareBatchNames(true, selection); if (_closed) return;
        _settingRenameOverride = true;
        _renameOverrideLabel = _renameBatch.Length + " · " + _text.BatchIntegrity_Title;
        RenameInputPath.Text = _renameOverrideLabel;
        _settingRenameOverride = false;
        _renameBatchSourcesOverride = _renameBatch.Select(row => row.Source).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        RenameBatchRows.ItemsSource = _renameBatch;
        RenameBatchApplyButton.IsEnabled = false;
        RenameBatchStatus.Text = _renameBatch.Length.ToString("N0"); Workspace.SelectedItem = RenameTab;
    }
    private async void MoveValidBatch(object? sender, RoutedEventArgs e) => await MoveValidBatchAsync(FilteredBatchRows());

    private static bool CanMoveBatchRow(BatchRow row) => row.Verified && row.Original && !row.HasError &&
        row.DisplayPath == row.Path && !row.DisplayPath.Contains(" :: ", StringComparison.Ordinal) &&
        !Emignatik.NxFileViewer.Services.Mtp.MtpDirectSource.IsDirect(row.Path);

    private async Task MoveValidBatchAsync(BatchRow[] selection)
    {
        if (_batchCancellation != null) return;
        var rows = selection.DistinctBy(row => row.Path).Where(CanMoveBatchRow).ToArray();
        if (rows.Length == 0) { StatusText.Text = _text.NcasIntegrity_Unchecked; return; }
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = _text.BatchIntegrity_MoveValid });
        if (folders.FirstOrDefault()?.TryGetLocalPath() is not { } folder) return;
        foreach (var row in rows)
        {
            try
            {
                var info = new FileInfo(row.Path);
                if (!info.Exists || info.Length != (row.Package?.Size ?? row.StoredSize) || info.LastWriteTimeUtc.Ticks != row.Modified) throw new IOException("Source changed since verification: " + row.Path);
                var destination = Path.Combine(folder, info.Name); File.Move(row.Path, destination, false);
                var index = Array.FindIndex(_batchRows.ToArray(), candidate => candidate.Path == row.Path); if (index >= 0) _batchRows[index] = row with { Path = destination, DisplayPath = destination };
                LogSession("INFO", row.Path + " → " + destination);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { StatusText.Text = ex.Message; LogSession("ERROR", ex.Message); }
        }
        RefreshBatchFilter();
    }
}
