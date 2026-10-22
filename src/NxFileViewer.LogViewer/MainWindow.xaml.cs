using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
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
                MessageBox.Show(this, ex.Message, UiText.Error, MessageBoxButton.OK, MessageBoxImage.Error);
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
            Entries.ItemsSource = rows;
            DetailText.Clear();
            if (rows.Count > 0) Entries.SelectedIndex = 0;
            StatusText.Text = UiText.Count(rows.Count, entries.Count);
        }
        catch (OperationCanceledException) { }
        finally { if (ReferenceEquals(_filterCancellation, cancellation)) _filterCancellation = null; }
    }

    private async void FilterChanged(object sender, RoutedEventArgs e) => await ApplyFilterAsync(true);
    private async void OpenClicked(object sender, RoutedEventArgs e) => await ChooseFileAsync();
    private async Task ChooseFileAsync()
    {
        var dialog = new OpenFileDialog { Filter = "Log (*.log;*.txt)|*.log;*.txt|All files (*.*)|*.*", CheckFileExists = true };
        if (dialog.ShowDialog(this) == true) await OpenPathAsync(dialog.FileName);
    }
    private async void ReloadClicked(object sender, RoutedEventArgs e)
    { if (_path != null) await OpenPathAsync(_path); }
    private void EntrySelected(object sender, SelectionChangedEventArgs e)
    {
        DetailText.Text = Entries.SelectedItem is LogDisplayEntry row
            ? (row.Entry.FilePath == null ? "" : row.Entry.FilePath + Environment.NewLine) + row.Entry.RawText : "";
    }
    private void CopyClicked(object sender, RoutedEventArgs e) => CopySelected();
    private void CopySelected()
    {
        if (Entries.SelectedItem is not LogDisplayEntry) return;
        try { Clipboard.SetText(DetailText.Text); }
        catch (System.Runtime.InteropServices.COMException) { /* Clipboard may be held by another application. */ }
    }
    private void FilesDragOver(object sender, DragEventArgs e)
    { e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; }
    private async void FilesDropped(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } paths) await OpenPathAsync(paths[0]);
        e.Handled = true;
    }
    private async void WindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.O && Keyboard.Modifiers == ModifierKeys.Control) { e.Handled = true; await ChooseFileAsync(); }
        else if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control) { e.Handled = true; SearchBox.Focus(); SearchBox.SelectAll(); }
        else if (e.Key == Key.F5 && _path != null) { e.Handled = true; await OpenPathAsync(_path); }
        else if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control && Entries.IsKeyboardFocusWithin)
        { e.Handled = true; CopySelected(); }
    }
}
