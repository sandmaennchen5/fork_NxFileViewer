using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
namespace NxFileViewer.LogViewer;

public partial class MainWindow : Window
{
    private IReadOnlyList<LogEntry> _entries = Array.Empty<LogEntry>();
    private CancellationTokenSource? _readCancellation, _filterCancellation;
    private string? _path;
    private int _loadVersion;
    private bool _ready, _reading, _closed;
    public record LevelChoice(string Name, EntryLevel Level);

    public MainWindow()
    {
        InitializeComponent();
        AddHandler(DragDrop.DragOverEvent, FilesDragOver);
        AddHandler(DragDrop.DropEvent, FilesDropped);
        AddHandler(KeyDownEvent, WindowKeyDown, RoutingStrategies.Tunnel);
        LevelFilter.ItemsSource = new[] { new LevelChoice(UiText.All, EntryLevel.Unknown) }
            .Concat(Enum.GetValues<EntryLevel>().Where(x => x != EntryLevel.Unknown)
                .Select(x => new LevelChoice(x + " +", x))).ToArray();
        LevelFilter.SelectedIndex = 0;
        _ready = true;
        StatusText.Text = UiText.Count(0, 0);
        Closed += (_, _) =>
        {
            _closed = true;
            ++_loadVersion;
            _readCancellation?.Cancel();
            _filterCancellation?.Cancel();
        };
    }

    public async Task OpenPathAsync(string path)
    {
        if (_closed) return;
        var version = ++_loadVersion;
        _readCancellation?.Cancel();
        _filterCancellation?.Cancel();
        using var cancellation = new CancellationTokenSource();
        _readCancellation = cancellation;
        _reading = true;
        StatusText.Text = UiText.Loading;
        try
        {
            var fullPath = Path.GetFullPath(path);
            var entries = await Task.Run(() => LogDocument.ReadFile(fullPath, cancellation.Token), cancellation.Token);
            if (_closed || cancellation.IsCancellationRequested || version != _loadVersion) return;
            _entries = entries;
            _path = fullPath;
            PathText.Text = fullPath;
            Title = Path.GetFileName(fullPath) + " · NxFileViewer Fork LogViewer";
            ReloadButton.IsEnabled = true;
            _reading = false;
            await ApplyFilterAsync(false);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException
            or NotSupportedException or System.Security.SecurityException)
        {
            if (!_closed && version == _loadVersion)
            {
                StatusText.Text = UiText.Error + ": " + ex.Message;
                await ShowReadErrorAsync(ex.Message);
            }
        }
        finally
        {
            if (version == _loadVersion) { _reading = false; _readCancellation = null; }
        }
    }

    private async Task ApplyFilterAsync(bool debounce)
    {
        if (!_ready || _closed || _reading) return;
        _filterCancellation?.Cancel();
        using var cancellation = new CancellationTokenSource();
        _filterCancellation = cancellation;
        var entries = _entries;
        var search = SearchBox.Text;
        var minimum = (LevelFilter.SelectedItem as LevelChoice)?.Level ?? EntryLevel.Unknown;
        var memory = MemoryOnly.IsChecked == true;
        try
        {
            if (debounce) await Task.Delay(250, cancellation.Token);
            StatusText.Text = UiText.Filtering;
            var rows = await Task.Run(() => LogDocument.Filter(entries, search, minimum, memory, cancellation.Token), cancellation.Token);
            if (_closed || cancellation.IsCancellationRequested) return;
            Entries.ItemsSource = rows.Select(row => new DisplayRow(row)).ToArray();
            DetailText.Text = "";
            if (rows.Count > 0) Entries.SelectedIndex = 0;
            StatusText.Text = UiText.Count(rows.Count, entries.Count);
        }
        catch (OperationCanceledException) { }
        finally { if (ReferenceEquals(_filterCancellation, cancellation)) _filterCancellation = null; }
    }

    private async void FilterChanged(object? sender, RoutedEventArgs e) => await ApplyFilterAsync(true);
    private async void OpenClicked(object? sender, RoutedEventArgs e) => await ChooseFileAsync();
    private async Task ChooseFileAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = UiText.Open,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Log") { Patterns = new[] { "*.log", "*.txt" } },
                FilePickerFileTypes.All
            }
        });
        if (files.FirstOrDefault()?.TryGetLocalPath() is { } path)
            await OpenPathAsync(path);
    }
    private async void ReloadClicked(object? sender, RoutedEventArgs e)
    { if (_path != null) await OpenPathAsync(_path); }
    private void EntrySelected(object? sender, SelectionChangedEventArgs e)
    {
        DetailText.Text = Entries.SelectedItem is DisplayRow row
            ? (row.Entry.FilePath == null ? "" : row.Entry.FilePath + Environment.NewLine) + row.Entry.RawText : "";
    }
    private async void CopyClicked(object? sender, RoutedEventArgs e) => await CopySelectedAsync();

    private async Task CopySelectedAsync()
    {
        if (Entries.SelectedItem is DisplayRow && Clipboard is { } clipboard)
            await clipboard.SetTextAsync(DetailText.Text ?? "");
    }

    private void FilesDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.Contains(DataFormat.File) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void FilesDropped(object? sender, DragEventArgs e)
    {
        e.Handled = true;
        if (e.DataTransfer.TryGetFiles()?.FirstOrDefault()?.TryGetLocalPath() is { } path)
            await OpenPathAsync(path);
    }

    private async void WindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.O && e.KeyModifiers == KeyModifiers.Control) { e.Handled = true; await ChooseFileAsync(); }
        else if (e.Key == Key.F && e.KeyModifiers == KeyModifiers.Control) { e.Handled = true; SearchBox.Focus(); SearchBox.SelectAll(); }
        else if (e.Key == Key.F5 && _path != null) { e.Handled = true; await OpenPathAsync(_path); }
        else if (e.Key == Key.C && e.KeyModifiers == KeyModifiers.Control && Entries.IsKeyboardFocusWithin)
        { e.Handled = true; await CopySelectedAsync(); }
    }

    private Task ShowReadErrorAsync(string message)
    {
        var button = new Button { Content = "OK", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right };
        var dialog = new Window
        {
            Title = UiText.Error,
            Width = 480,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Thickness(16),
                Spacing = 12,
                Children =
                {
                    new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                    button
                }
            }
        };
        button.Click += (_, _) => dialog.Close();
        return dialog.ShowDialog(this);
    }

    private sealed record DisplayRow(LogDisplayEntry Row)
    {
        public LogEntry Entry => Row.Entry;
        public string? Header => Row.Header;
        public bool ShowHeader => Row.ShowHeader;
        public bool IsInformation => Entry.Level == EntryLevel.Information;
        public bool IsWarning => Entry.Level == EntryLevel.Warning;
        public bool IsError => Entry.Level is EntryLevel.Error or EntryLevel.Critical;
    }
}
