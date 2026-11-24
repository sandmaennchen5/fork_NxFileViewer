using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Emignatik.NxFileViewer.FileLoading.QuickFileInfoLoading;
using Emignatik.NxFileViewer.Localization.Keys;
using Emignatik.NxFileViewer.Models.Overview;

namespace NxFileViewer.Avalonia;

public partial class MainWindow : Window
{
    private ILocalizationKeys _text = GetText("");

    private static ILocalizationKeys GetText(string language) => (string.IsNullOrEmpty(language)
        ? CultureInfo.CurrentUICulture.TwoLetterISOLanguageName : language) switch
    {
        "de" => new LocalizationKeys_DE(),
        "es" => new LocalizationKeys_ES(),
        "fr" => new LocalizationKeys_FR(),
        _ => new LocalizationKeys_EN()
    };
    private int _openVersion;
    private bool _closed;
    private string? _openedPath;
    private string? _keysPath;

    public MainWindow()
    {
        InitializeComponent();
        NandTabIcon.Source = HomeNandIcon.Source = NandPluginIcon.Source = WorkspaceIcons.Read("NandIcon");
        NszPluginIcon.Source = WorkspaceIcons.Read("NszPluginIcon");
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Opened += (_, _) =>
        {
            if (_preferences.CheckUpdatesOnStart) CheckUpdates(this, new RoutedEventArgs());

        };
        ActualThemeVariantChanged += (_, _) => WpfTheme.Apply(this);
        WpfTheme.Apply(this);
        InitializePreferences();
        RestoreRenameInputs();
        InitializeWindowPlacement();
        Opened += async (_, _) => await ConfirmSystemReport();
        InitializeBatch();
        _lastOpenedPath = _preferences.LastOpenedPath ?? _preferences.SharedSettings.LastOpenedFile;
        BatchInputPath.Text = _preferences.LastBatchPath ?? _preferences.SharedSettings.LastUsedDir;
        RefreshHome();
        InitializeInfo();
        InitializeApplicationUpdateRow();
        StatusText.Text = _text.Status_Ready;
        DataContext = _text;
        AddHandler(DragDrop.DragOverEvent, FilesDragOver);
        AddHandler(DragDrop.DropEvent, FilesDropped);
        AddHandler(KeyDownEvent, WindowKeyDown, RoutingStrategies.Tunnel);
        Closed += (_, _) =>
        {
            _closed = true;
            _batchCancellation?.Cancel();
            _nandCancellation?.Cancel();
            _mtpCancellation?.Cancel();
            _mtpImport?.Cleanup();
            _renameBatchCancellation?.Cancel();
            _firmwareCancellation?.Cancel();
            _extractionCancellation?.Cancel();
            _fileVerificationCancellation?.Cancel();
            _ncaContentsCancellation?.Cancel();
            _updateCancellation?.Cancel();
            _keyDownloadCancellation?.Cancel();
            _supportIdentityCancellation?.Cancel();
            _overviewBitmap?.Dispose();
            _batchIcon?.Dispose();
            DisposeOpenedArchives();
        };
    }

    internal async Task OpenFileAsync(string path, bool fromWindows = false, bool forceVerify = false, string? displayPath = null)
    {
        if (_applicationUpdatePending) return;
        if (Emignatik.NxFileViewer.FileLoading.PackageZip.IsArchive(path))
        {
            await OpenArchiveAsync(path, fromWindows, forceVerify);
            return;
        }
        if (_preferences.NandActive && Emignatik.NxFileViewer.Services.Nand.NandDetection.IsCandidateName(path))
        {
            _nandSource = path;
            Workspace.SelectedItem = NandTab;
            ReadNand(this, new RoutedEventArgs());
            return;
        }
        using var workspaceJob = LockWorkspace(FileTab);
        var version = ++_openVersion;
        ResetFileVerification();
        ResetNcaContents();
        var fileName = Path.GetFileName(path);
        StatusText.Text = $"{_text.LoadingFile_PleaseWait} {fileName}";
        FileStatusProgress.IsIndeterminate = true;
        FileStatusProgress.IsVisible = true;
        try
        {
            if (_preferences.SharedSettings.AlwaysReloadKeysBeforeOpen) RefreshActiveKeyPaths();
            var keysPath = _keysPath;
            if (Emignatik.NxFileViewer.FileLoading.NupReader.IsNup(path) && !_preferences.SharedSettings.EnableNup)
                throw new InvalidDataException(_text.Features_NupDisabled);
            var prepared = displayPath == null ? await PrepareXciAsync(path) : path;
            if (prepared == null || _closed || version != _openVersion) return;
            path = prepared;
            fileName = Path.GetFileName(path);
            var package = await Task.Run(() => PackageFile.Read(path, keysPath));
            if (_closed || version != _openVersion) return;

            _openedPath = package.Path;
            _openedDisplayPath = displayPath;
            if (displayPath == null) RememberOpenedPath(package.Path);
            else RefreshHome();
            FileBody.IsVisible = true;
            FileEmpty.IsVisible = false;
            BrowseNcaButton.IsVisible = package.Type is "NCA" or "NCZ";
            VerifyFileButton.IsEnabled = _fileVerificationCancellation == null && package.Type != "NRO";
            FileNameText.Text = Path.GetFileName(package.Path);
            FileInfoText.Text = $"{_text.Title_FileInfo_FileType}  {package.Type}{Environment.NewLine}" +
                $"{_text.FileInfo_FileSize}  {package.Size:N0} B{Environment.NewLine}{displayPath ?? package.Path}" +
                (package.HeaderDetails == null ? "" : Environment.NewLine + package.HeaderDetails);
            ExtractAllButton.IsEnabled = package.Entries.Count > 0 && _extractionCancellation == null;
            Entries.ItemsSource = new[] { new PackageEntry(Path.GetFileName(package.Path), package.Size) { IsRoot = true, Children = package.Entries } };
            BrowseNcaButton.IsVisible = package.Type is "NCA" or "NCZ";
            EntryInfoText.Text = "";
            MetadataStatus.Text = package.NeedsProdKeys ? _text.WarnNoProdKeysFileFound : package.MetadataError;
            MetadataStatus.IsVisible = !string.IsNullOrEmpty(MetadataStatus.Text);
            UpdateOverview(package);
            FileBody.SelectedIndex = 0;
            Workspace.SelectedIndex = 1;
            Title = ProgramVersionText.Text + " · Avalonia - " + FileNameText.Text;
            StatusText.Text = fileName;
            LogFileSection(displayPath ?? package.Path);
            if (package.Type != "NRO" && (forceVerify || (fromWindows ? _preferences.SharedSettings.VerifyFilesOpenedFromWindows : _verifyOnOpen))) VerifyOpenedFile(this, new RoutedEventArgs());
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or LibHac.Common.LibHacException or LibHac.NSZ.NczFormatException or ZstdSharp.ZstdException or InvalidOperationException)
        {
            if (!_closed && version == _openVersion)
                { StatusText.Text = ex.Message; LogFileSection(path, [("ERROR", ex.Message)]); }
        }
        finally
        {
            if (!_closed && version == _openVersion && _fileVerificationCancellation == null)
                FileStatusProgress.IsVisible = false;
        }
    }

    private async Task PickFileAsync()
    {
        var filter = _text.OpenFile_Filter.Replace("*.nca", "*.nca;*.ncz", StringComparison.Ordinal);
        if (_preferences.SharedSettings.EnableNup)
            filter = filter.Replace("*.nro;", "*.nro;*.nup;", StringComparison.Ordinal) + "|NUP (*.nup)|*.nup";
        var fileTypes = filter.Split('|').Chunk(2).Select(parts => parts[1] == "*.*"
            ? FilePickerFileTypes.All
            : new FilePickerFileType(parts[0]) { Patterns = parts[1].Split(';') }).ToArray();
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = _text.MenuItem_Open,
            SuggestedStartLocation = await GetStartFolderAsync(_lastOpenedPath),
            FileTypeFilter = fileTypes
        });
        if (files.FirstOrDefault()?.TryGetLocalPath() is { } path)
            await OpenFileAsync(path);
    }

    private async void OpenClicked(object? sender, RoutedEventArgs e) => await PickFileAsync();

    private async void LoadKeysClicked(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = _text.BrowseKeysFile_ProdTitle,
            FileTypeFilter = new[] { new FilePickerFileType(_text.BrowseKeysFile_Filter.Split('|', 2)[0]) { Patterns = new[] { "*.keys" } } }
        });
        if (files.FirstOrDefault()?.TryGetLocalPath() is not { } path) return;

        SettingsKeysPath.Text = path;
        ValidateKeys(sender, e);
    }

    private void CloseClicked(object? sender, RoutedEventArgs e)
    {
        ++_openVersion;
        ResetFileVerification();
        ResetNcaContents();
        _openedPath = null;
        _openedDisplayPath = null;
        RefreshHome();
        FileBody.IsVisible = false;
        FileEmpty.IsVisible = true;
        Entries.ItemsSource = null;
        ClearOverview();
        MetadataStatus.IsVisible = false;
        Title = ProgramVersionText.Text + " · Avalonia";
        FileNameText.Text = FileInfoText.Text = EntryInfoText.Text = MetadataStatus.Text = StatusText.Text = "";
        FileStatusProgress.IsVisible = false;
        StatusText.Text = _text.Status_Ready;
        if (_workspaceJobs.Count == 0) Workspace.SelectedIndex = 0;
    }

    private void ExitClicked(object? sender, RoutedEventArgs e) => Close();

    private async void EntrySelected(object? sender, SelectionChangedEventArgs e)
    {
        ResetNcaContents();
        BrowseNcaButton.IsVisible = SelectedPackageEntry is PackageEntry selected &&
            (selected.Name.EndsWith(".nca", StringComparison.OrdinalIgnoreCase) || selected.Name.EndsWith(".ncz", StringComparison.OrdinalIgnoreCase));
        EntryInfoText.Text = SelectedPackageEntry is PackageEntry entry ? $"{entry.Name} · {entry.Size:N0} B" : "";
        ExtractEntryButton.IsEnabled = SelectedPackageEntry != null && _extractionCancellation == null;
        EntryProperties.ItemsSource = SelectedPackageEntry is { } detail ? new[] { new DetailRow("Name", detail.Name), new DetailRow("Type", "FileEntry"), new DetailRow("Format", Path.GetExtension(detail.Name).TrimStart('.')), new DetailRow("Size", Emignatik.NxFileViewer.Utils.FileSizeHelper.ToFileSize(detail.Size)) } : Array.Empty<DetailRow>();
        if (_openedPath is { } packagePath && _keysPath is { } keysPath && SelectedPackageEntry is { } selectedEntry && (selectedEntry.Name.EndsWith(".nca", StringComparison.OrdinalIgnoreCase) || selectedEntry.Name.EndsWith(".ncz", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                var properties = await Task.Run(() => NcaContents.Properties(packagePath, keysPath, selectedEntry.Name));
                if (!_closed && _openedPath == packagePath && ReferenceEquals(SelectedPackageEntry, selectedEntry))
                    EntryProperties.ItemsSource = new[] { new DetailRow("Name", selectedEntry.Name), new DetailRow("Type", "FileEntry"), new DetailRow("Size", Emignatik.NxFileViewer.Utils.FileSizeHelper.ToFileSize(selectedEntry.Size)) }.Concat(properties.Select(pair => new DetailRow(pair.Key, pair.Value))).ToArray();
            }
            catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException or LibHac.Common.LibHacException)
            { if (!_closed && ReferenceEquals(SelectedPackageEntry, selectedEntry)) EntryInfoText.Text = ex.Message; }
        }
    }

    private PackageEntry? SelectedPackageEntry => Entries.SelectedItem is PackageEntry { IsRoot: false } entry ? entry : null;

    private async void WindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (_workspaceJobs.Count > 0) return;
        if (e.Key == Key.O && e.KeyModifiers == KeyModifiers.Control)
        {
            e.Handled = true;
            await PickFileAsync();
        }
        else if (e.Key == Key.O && e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift))
        {
            e.Handled = true;
            OpenLastClicked(this, new RoutedEventArgs());
        }
        else if (e.Key == Key.C && e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift))
        {
            e.Handled = true;
            CloseClicked(this, new RoutedEventArgs());
        }
        else if (e.Key == Key.F5)
        {
            e.Handled = true;
            VerifyOpenedFile(this, new RoutedEventArgs());
        }
        else if (e.Key == Key.F10)
        {
            e.Handled = true;
            ReloadKeysClicked(this, new RoutedEventArgs());
        }
        else if (e.Key == Key.F2)
        {
            e.Handled = true;
            Workspace.SelectedItem = RenameTab;
        }
        else if (e.Key == Key.S && e.KeyModifiers == KeyModifiers.Alt)
        {
            e.Handled = true;
            Workspace.SelectedItem = SettingsTab;
        }
    }

    private static void FilesDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.Contains(DataFormat.File) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void FilesDropped(object? sender, DragEventArgs e)
    {
        e.Handled = true;
        if (_workspaceJobs.Count > 0) return;
        var paths = e.DataTransfer.TryGetFiles()?.Select(file => file.TryGetLocalPath()).OfType<string>().ToArray() ?? Array.Empty<string>();
        if (paths.Length == 1 && File.Exists(paths[0]) && IsBatchPackage(paths[0]) && Workspace.SelectedItem != BatchTab)
        { await OpenFileAsync(paths[0]); return; }
        if (paths.Length == 0 || _batchCancellation != null) return;
        Workspace.SelectedItem = BatchTab;
        if (paths.Length == 1) { BatchInputPath.Text = paths[0]; StartBatch(_preferences.AutoVerifyBatchDrop ? BatchHashButton : BatchStartButton, new RoutedEventArgs()); return; }
        using var workspaceJob = LockWorkspace(BatchTab);
        using var cancellation = new System.Threading.CancellationTokenSource(); _batchCancellation = cancellation; SetBatchBusy(true);
        var keys = _keysPath; var verify = _preferences.AutoVerifyBatchDrop; _batchRows.Clear();
        try
        {
            foreach (var path in paths)
                foreach (var source in EnumerateBatchSources(path, true, true, cancellation.Token))
                {
                    var row = await Task.Run(() => ReadBatchRow(source.Path, source.DisplayPath, keys, verify, cancellation.Token));
                    if (_closed) return; _batchRows.Add(row); LogBatchFile(row); RefreshBatchFilter(followResult: row);
                }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { StatusText.Text = ex.Message; }
        finally { _batchCancellation = null; if (!_closed) SetBatchBusy(false); }
    }
}
