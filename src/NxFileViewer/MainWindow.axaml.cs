using System;
using System.Collections.Generic;
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
        Entries.AddHandler(TreeViewItem.ExpandedEvent, EntryExpanded);
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
        _memoryTrace = new Emignatik.NxFileViewer.Logging.MemoryTraceDiagnostics(new MemorySessionLogger(this));
        RestoreRenameInputs();
        InitializeWindowPlacement();
        Opened += async (_, _) => await ConfirmSystemReport();
        InitializeBatch();
        InitializeBatchDiagnostics();
        LogTab.PropertyChanged += (_, change) =>
        {
            if (change.Property == TabItem.IsSelectedProperty && LogTab.IsSelected) RefreshLog();
        };
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
            _memoryTrace?.Dispose();
            _batchCancellation?.Cancel();
            _fileLoadCancellation?.Cancel();
            _nandCancellation?.Cancel();
            _mtpCancellation?.Cancel();
            _mtpImport?.Cleanup();
            _renameBatchCancellation?.Cancel();
            _fileFirmwareCancellation?.Cancel();
            _extractionCancellation?.Cancel();
            _fileVerificationCancellation?.Cancel();
            _ncaContentsCancellation?.Cancel();
            _updateCancellation?.Cancel();
            _keyDownloadCancellation?.Cancel();
            _supportIdentityCancellation?.Cancel();
            _homePollCancellation?.Cancel();
            _overviewBitmap?.Dispose();
            _batchIcon?.Dispose();
            DisposeOpenedArchives();
            if (_batchCancellation == null) ResetBatchWorkingFiles();
        };
    }

    internal async Task OpenFileAsync(string path, bool fromWindows = false, bool forceVerify = false, string? displayPath = null,
        IReadOnlyList<Emignatik.NxFileViewer.FileLoading.SdContentSource>? sdSources = null)
    {
        if (_applicationUpdatePending) return;
        if (Emignatik.NxFileViewer.FileLoading.PackageZip.IsArchive(path))
        {
            await OpenArchiveAsync(path, fromWindows, forceVerify);
            return;
        }
        if (_preferences.NandActive && Emignatik.NxFileViewer.Services.Nand.NandDetection.IsCandidateName(path) &&
            !Nax0Contents.IsCandidate(path))
        {
            _nandSource = path;
            Workspace.SelectedItem = NandTab;
            ReadNand(this, new RoutedEventArgs());
            return;
        }
        using var workspaceJob = LockWorkspace(FileTab);
        _fileLoadCancellation?.Cancel();
        var version = ++_openVersion;
        using var loadCancellation = new System.Threading.CancellationTokenSource();
        ResetFileVerification();
        ResetNcaContents();
        ResetFileFirmware();
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
            _fileLoadCancellation = loadCancellation;
            VerifyFileCancelButton.IsVisible = VerifyFileCancelButton.IsEnabled = true;
            var token = loadCancellation.Token;
            var package = await Task.Run(() => PackageFile.Read(path, keysPath, null, token), token);
            if (package.Type == "NAX0" && Directory.Exists(package.Path))
            {
                sdSources ??= await Task.Run(() => Emignatik.NxFileViewer.FileLoading.SdCardSource.FindRelatedContents(package.Path));
                if (!sdSources.Any(source => string.Equals(source.ContentsPath, package.Path, StringComparison.OrdinalIgnoreCase)))
                    sdSources = [.. sdSources, new(package.Path, package.Path)];
            }
            token.ThrowIfCancellationRequested();
            if (_closed || version != _openVersion) return;
            if (package.Type == "NAX0") fileName = Path.GetFileName(Nax0Contents.LogicalPath(path));

            _openedPath = package.Path;
            ReleaseOpenedArchiveMembers(package.Path);
            UpdateSdContentsChoice(package.Path, sdSources);
            _openedDisplayPath = displayPath;
            if (displayPath == null) RememberOpenedPath(package.Path);
            else RefreshHome();
            FileBody.IsVisible = true;
            FileEmpty.IsVisible = false;
            VerifyFileButton.IsEnabled = _fileVerificationCancellation == null && package.Type != "NRO";
            ShowContentTree(package);
            MetadataStatus.Text = package.NeedsProdKeys ? _text.WarnNoProdKeysFileFound : package.MetadataError;
            MetadataStatus.IsVisible = !string.IsNullOrEmpty(MetadataStatus.Text);
            UpdateOverview(package);
            FileBody.SelectedIndex = 0;
            Workspace.SelectedIndex = 1;
            Title = ProgramVersionText.Text + " - " + fileName;
            StatusText.Text = fileName;
            LogFileSection(displayPath ?? package.Path,
                [("INFO", $"{package.DisplayType} · {OverviewSize.Text} · {OverviewStructure.Text}"),
                 .. package.MissingKeys.Select(key => ("WARN", key)),
                 .. package.MetadataError is { Length: > 0 } error ? new[] { ("ERROR", error) } : []]);
            if (_preferences.FirmwareActive && package.Type == "NCA" &&
                !Emignatik.NxFileViewer.Services.Mtp.MtpDirectSource.IsDirect(path))
                _ = IdentifyOpenedFirmwareAsync(path, version);
            _fileLoadCancellation = null;
            VerifyFileCancelButton.IsEnabled = VerifyFileCancelButton.IsVisible = false;
            if (package.Type != "NRO" && (forceVerify || (fromWindows ? _preferences.SharedSettings.VerifyFilesOpenedFromWindows : _verifyOnOpen))) VerifyOpenedFile(this, new RoutedEventArgs());
        }
        catch (OperationCanceledException) when (loadCancellation.IsCancellationRequested)
        {
            if (!_closed && version == _openVersion) StatusText.Text = _text.Update_Cancelled;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or LibHac.Common.LibHacException or LibHac.NSZ.NczFormatException or ZstdSharp.ZstdException or InvalidOperationException)
        {
            if (!_closed && version == _openVersion)
                { StatusText.Text = ex.Message; LogFileSection(path, [("ERROR", ex.Message)]); }
        }
        finally
        {
            if (ReferenceEquals(_fileLoadCancellation, loadCancellation))
            {
                _fileLoadCancellation = null;
                if (!_closed && version == _openVersion) VerifyFileCancelButton.IsEnabled = VerifyFileCancelButton.IsVisible = false;
            }
            if (!_closed && version == _openVersion && _fileVerificationCancellation == null)
                FileStatusProgress.IsVisible = false;
        }
    }

    private async Task PickFileAsync()
    {
        var filter = _text.OpenFile_Filter.Replace("*.nca", "*.nca;*.ncz;*.nax0;00", StringComparison.Ordinal);
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
        _fileLoadCancellation?.Cancel();
        ++_openVersion;
        ResetFileVerification();
        ResetNcaContents();
        ResetFileFirmware();
        _openedPath = null;
        ReleaseOpenedArchiveMembers();
        UpdateSdContentsChoice(null);
        _openedDisplayPath = null;
        RefreshHome();
        FileBody.IsVisible = false;
        FileEmpty.IsVisible = true;
        Entries.ItemsSource = null;
        ClearOverview();
        MetadataStatus.IsVisible = false;
        Title = ProgramVersionText.Text;
        MetadataStatus.Text = StatusText.Text = "";
        FileStatusProgress.IsVisible = false;
        StatusText.Text = _text.Status_Ready;
        if (_workspaceJobs.Count == 0) Workspace.SelectedIndex = 0;
    }

    private void ExitClicked(object? sender, RoutedEventArgs e) => Close();

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
        else if (e.Key == Key.F9 && e.KeyModifiers == KeyModifiers.None && FileTitlePageButton.IsVisible)
        {
            e.Handled = true;
            OpenTitlePage(this, new RoutedEventArgs());
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
        if (Workspace.SelectedItem == RenameTab)
        {
            if (paths.FirstOrDefault() is { } source) RenameInputPath.Text = source;
            return;
        }
        if (paths.Length == 1 && File.Exists(paths[0]) && IsBatchPackage(paths[0]) && Workspace.SelectedItem != BatchTab)
        { await OpenFileAsync(paths[0]); return; }
        if (paths.Length == 0 || _batchCancellation != null) return;
        Workspace.SelectedItem = BatchTab;
        if (paths.Length == 1) { BatchInputPath.Text = paths[0]; StartBatch(_preferences.AutoVerifyBatchDrop ? BatchHashButton : BatchStartButton, new RoutedEventArgs()); return; }
        await RunBatchAsync(paths, _preferences.AutoVerifyBatchDrop, forceRecursive: true);
    }
}
