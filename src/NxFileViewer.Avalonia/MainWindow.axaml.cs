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
            var screen = Screens.ScreenFromWindow(this);
            if (screen == null) return;
            Width = Math.Min(Width, screen.WorkingArea.Width / screen.Scaling - 16);
            Height = Math.Min(Height, screen.WorkingArea.Height / screen.Scaling - 48);
            Position = new PixelPoint(
                screen.WorkingArea.X + Math.Max(0, (screen.WorkingArea.Width - (int)(Width * screen.Scaling)) / 2),
                screen.WorkingArea.Y + Math.Max(0, (screen.WorkingArea.Height - (int)(Height * screen.Scaling)) / 2));
        };
        ActualThemeVariantChanged += (_, _) => WpfTheme.Apply(this);
        WpfTheme.Apply(this);
        InitializePreferences();
        InitializeBatch();
        InitializeInfo();
        StatusText.Text = _text.Status_Ready;
        DataContext = _text;
        BatchResults.ItemsSource = _batchRows;
        AddHandler(DragDrop.DragOverEvent, FilesDragOver);
        AddHandler(DragDrop.DropEvent, FilesDropped);
        AddHandler(KeyDownEvent, WindowKeyDown, RoutingStrategies.Tunnel);
        Closed += (_, _) =>
        {
            _closed = true;
            _batchCancellation?.Cancel();
            _renameBatchCancellation?.Cancel();
            _firmwareCancellation?.Cancel();
            _extractionCancellation?.Cancel();
            _fileVerificationCancellation?.Cancel();
            _ncaContentsCancellation?.Cancel();
            _updateCancellation?.Cancel();
            _supportIdentityCancellation?.Cancel();
            _overviewBitmap?.Dispose();
            _batchIcon?.Dispose();
        };
    }

    internal async Task OpenFileAsync(string path)
    {
        var version = ++_openVersion;
        ResetFileVerification();
        ResetNcaContents();
        var keysPath = _keysPath;
        StatusText.Text = Path.GetFileName(path) + "…";
        try
        {
            var package = await Task.Run(() => PackageFile.Read(path, keysPath));
            if (_closed || version != _openVersion) return;

            _openedPath = package.Path;
            _lastOpenedPath = package.Path;
            RefreshHome();
            FileBody.IsVisible = true;
            FileEmpty.IsVisible = false;
            BrowseNcaButton.IsVisible = package.Type is "NCA" or "NCZ";
            VerifyFileButton.IsEnabled = _fileVerificationCancellation == null;
            FileNameText.Text = Path.GetFileName(package.Path);
            FileInfoText.Text = $"{_text.Title_FileInfo_FileType}  {package.Type}{Environment.NewLine}" +
                $"{_text.FileInfo_FileSize}  {package.Size:N0} B{Environment.NewLine}{package.Path}" +
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
            StatusText.Text = FileNameText.Text;
            LogSession("INFO", package.Path);
            if (_verifyOnOpen) VerifyOpenedFile(this, new RoutedEventArgs());
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or LibHac.Common.LibHacException or LibHac.NSZ.NczFormatException or ZstdSharp.ZstdException)
        {
            if (!_closed && version == _openVersion)
                { StatusText.Text = ex.Message; LogSession("ERROR", ex.Message); }
        }
    }

    private async Task PickFileAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = _text.MenuItem_Open,
            FileTypeFilter = new[]
            {
                new FilePickerFileType(_text.OpenFile_Filter.Split('|', 2)[0].Split('(', 2)[0].TrimEnd())
                {
                    Patterns = new[] { "*.nsp", "*.nsz", "*.xci", "*.xcz", "*.nro", "*.nca", "*.ncz" }
                },
                FilePickerFileTypes.All
            }
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

        _keysPath = path;
        SettingsKeysPath.Text = path;
        RefreshHome();
        if (_openedPath is { } openedPath)
            await OpenFileAsync(openedPath);
    }

    private void CloseClicked(object? sender, RoutedEventArgs e)
    {
        ++_openVersion;
        ResetFileVerification();
        ResetNcaContents();
        _openedPath = null;
        FileBody.IsVisible = false;
        FileEmpty.IsVisible = true;
        Entries.ItemsSource = null;
        ClearOverview();
        MetadataStatus.IsVisible = false;
        FileNameText.Text = FileInfoText.Text = EntryInfoText.Text = MetadataStatus.Text = StatusText.Text = "";
        StatusText.Text = _text.Status_Ready;
        Workspace.SelectedIndex = 0;
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
        var paths = e.DataTransfer.TryGetFiles()?.Select(file => file.TryGetLocalPath()).OfType<string>().ToArray() ?? Array.Empty<string>();
        if (paths.Length == 1 && File.Exists(paths[0]) && IsBatchPackage(paths[0]) && Workspace.SelectedItem != BatchTab)
        { await OpenFileAsync(paths[0]); return; }
        if (paths.Length == 0 || _batchCancellation != null) return;
        Workspace.SelectedItem = BatchTab;
        if (paths.Length == 1) { BatchInputPath.Text = paths[0]; StartBatch(_preferences.AutoVerifyBatchDrop ? BatchHashButton : BatchStartButton, new RoutedEventArgs()); return; }
        using var cancellation = new System.Threading.CancellationTokenSource(); _batchCancellation = cancellation; SetBatchBusy(true);
        var keys = _keysPath; var verify = _preferences.AutoVerifyBatchDrop; _batchRows.Clear();
        try
        {
            foreach (var path in paths)
                foreach (var source in EnumerateBatchSources(path, true, true, cancellation.Token))
                {
                    var row = await Task.Run(() => ReadBatchRow(source.Path, source.DisplayPath, keys, verify, cancellation.Token));
                    if (_closed) return; _batchRows.Add(row); RefreshBatchFilter();
                }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { StatusText.Text = ex.Message; }
        finally { _batchCancellation = null; if (!_closed) SetBatchBusy(false); }
    }
}
