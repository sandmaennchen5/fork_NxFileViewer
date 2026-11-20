using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
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
        InitializePreferences();
        DataContext = _text;
        BatchResults.ItemsSource = _batchRows;
        AddHandler(DragDrop.DragOverEvent, FilesDragOver);
        AddHandler(DragDrop.DropEvent, FilesDropped);
        AddHandler(KeyDownEvent, WindowKeyDown, RoutingStrategies.Tunnel);
        Closed += (_, _) =>
        {
            _closed = true;
            _batchCancellation?.Cancel();
            _firmwareCancellation?.Cancel();
        };
    }

    internal async Task OpenFileAsync(string path)
    {
        var version = ++_openVersion;
        var keysPath = _keysPath;
        StatusText.Text = Path.GetFileName(path) + "…";
        try
        {
            var package = await Task.Run(() => PackageFile.Read(path, keysPath));
            if (_closed || version != _openVersion) return;

            _openedPath = package.Path;
            FileNameText.Text = Path.GetFileName(package.Path);
            FileInfoText.Text = $"{_text.Title_FileInfo_FileType}  {package.Type}{Environment.NewLine}" +
                $"{_text.FileInfo_FileSize}  {package.Size:N0} B{Environment.NewLine}{package.Path}";
            Entries.ItemsSource = package.Entries;
            EntryInfoText.Text = "";
            MetadataStatus.Text = package.NeedsProdKeys ? _text.WarnNoProdKeysFileFound : package.MetadataError;
            MetadataStatus.IsVisible = !string.IsNullOrEmpty(MetadataStatus.Text);
            TitleDetails.ItemsSource = package.Titles.Select(CreateTitleDetails).ToArray();
            Workspace.SelectedIndex = 1;
            StatusText.Text = FileNameText.Text;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or LibHac.Common.LibHacException)
        {
            if (!_closed && version == _openVersion)
                StatusText.Text = ex.Message;
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
                    Patterns = new[] { "*.nsp", "*.nsz", "*.xci", "*.xcz", "*.nro" }
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
        if (_openedPath is { } openedPath)
            await OpenFileAsync(openedPath);
    }

    private void CloseClicked(object? sender, RoutedEventArgs e)
    {
        ++_openVersion;
        _openedPath = null;
        Entries.ItemsSource = null;
        TitleDetails.ItemsSource = null;
        MetadataStatus.IsVisible = false;
        FileNameText.Text = FileInfoText.Text = EntryInfoText.Text = MetadataStatus.Text = StatusText.Text = "";
        Workspace.SelectedIndex = 0;
    }

    private void ExitClicked(object? sender, RoutedEventArgs e) => Close();

    private void EntrySelected(object? sender, SelectionChangedEventArgs e)
    {
        EntryInfoText.Text = Entries.SelectedItem is PackageEntry entry ? $"{entry.Name} · {entry.Size:N0} B" : "";
    }

    private TitleDetailsRow CreateTitleDetails(Content content)
    {
        var language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName switch
        {
            "de" => NacpLanguage.German,
            "es" => NacpLanguage.Spanish,
            "fr" => NacpLanguage.French,
            _ => NacpLanguage.AmericanEnglish
        };
        var titles = content.NacpData?.Titles;
        var title = titles?[(int)language] ?? titles?.FirstOrDefault(candidate => candidate != null);
        var details = string.Join(" · ", new[] { title?.Publisher, content.NacpData?.DisplayVersion }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
        return new TitleDetailsRow(title?.Name ?? content.TitleId, $"{_text.CnmtOverview_ContentType}  {content.Type} · {content.TitleId}", details);
    }

    private sealed record TitleDetailsRow(string Name, string TypeAndId, string PublisherAndVersion);

    private async void WindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.O || e.KeyModifiers != KeyModifiers.Control) return;
        e.Handled = true;
        await PickFileAsync();
    }

    private static void FilesDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.Contains(DataFormat.File) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void FilesDropped(object? sender, DragEventArgs e)
    {
        e.Handled = true;
        if (e.DataTransfer.TryGetFiles()?.FirstOrDefault()?.TryGetLocalPath() is { } path)
            await OpenFileAsync(path);
    }
}
