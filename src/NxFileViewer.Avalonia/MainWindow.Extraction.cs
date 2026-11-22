using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace NxFileViewer.Avalonia;

public partial class MainWindow
{
    private CancellationTokenSource? _extractionCancellation;

    private async void ExtractSelectedEntry(object? sender, RoutedEventArgs e)
    {
        if (_extractionCancellation != null || _openedPath is not { } source || SelectedPackageEntry is not PackageEntry entry)
            return;
        using var cancellation = new CancellationTokenSource();
        _extractionCancellation = cancellation;
        ExtractEntryButton.IsEnabled = false;
        ExtractAllButton.IsEnabled = false;
        var reconstructNcz = ExtractOriginalNca.IsChecked == true;
        try
        {
            var target = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = _text.ContextMenu_SaveFileItem,
                SuggestedFileName = reconstructNcz && entry.Name.EndsWith(".ncz", StringComparison.OrdinalIgnoreCase)
                    ? Path.ChangeExtension(Path.GetFileName(entry.Name), ".nca") : Path.GetFileName(entry.Name),
                ShowOverwritePrompt = true
            });
            if (_closed || target?.TryGetLocalPath() is not { } destination) return;
            ExtractCancelButton.IsEnabled = true;
            StatusText.Text = entry.Name + "…";
            await Task.Run(() => PackageFile.ExtractEntry(source, entry.Name, destination, cancellation.Token, reconstructNcz));
            if (!_closed) StatusText.Text = destination;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            if (!_closed) StatusText.Text = _text.Log_SaveFileCanceled;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or LibHac.Common.LibHacException or LibHac.NSZ.NczFormatException or ZstdSharp.ZstdException)
        {
            if (!_closed) StatusText.Text = ex.Message;
        }
        finally
        {
            _extractionCancellation = null;
            if (!_closed)
            {
                ExtractEntryButton.IsEnabled = SelectedPackageEntry != null;
                ExtractAllButton.IsEnabled = true;
                ExtractCancelButton.IsEnabled = false;
            }
        }
    }

    private async void ExtractAllEntries(object? sender, RoutedEventArgs e)
    {
        if (_extractionCancellation != null || _openedPath is not { } source) return;
        using var cancellation = new CancellationTokenSource();
        _extractionCancellation = cancellation;
        ExtractEntryButton.IsEnabled = ExtractAllButton.IsEnabled = false;
        var original = ExtractOriginalNca.IsChecked == true;
        try
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = _text.ContextMenu_SaveDirectoryItem });
            if (_closed || folders.Count == 0 || folders[0].TryGetLocalPath() is not { } parent) return;
            ExtractCancelButton.IsEnabled = true;
            StatusText.Text = Path.GetFileName(source) + "…";
            await Task.Run(() => PackageFile.ExtractAll(source, parent, original, cancellation.Token));
            if (!_closed) StatusText.Text = Path.Combine(parent, Path.GetFileNameWithoutExtension(source));
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            if (!_closed) StatusText.Text = _text.Log_SaveToDirCanceled;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or LibHac.Common.LibHacException or LibHac.NSZ.NczFormatException or ZstdSharp.ZstdException)
        {
            if (!_closed) StatusText.Text = ex.Message;
        }
        finally
        {
            _extractionCancellation = null;
            if (!_closed)
            {
                ExtractEntryButton.IsEnabled = SelectedPackageEntry != null;
                ExtractAllButton.IsEnabled = true;
                ExtractCancelButton.IsEnabled = false;
            }
        }
    }

    private void CancelExtraction(object? sender, RoutedEventArgs e) => _extractionCancellation?.Cancel();
}
