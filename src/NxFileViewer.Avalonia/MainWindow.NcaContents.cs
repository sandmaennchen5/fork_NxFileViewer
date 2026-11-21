using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Interactivity;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace NxFileViewer.Avalonia;

public partial class MainWindow
{
    private CancellationTokenSource? _ncaContentsCancellation;
    private string? _browsedPackageEntry;

    private void ResetNcaContents()
    {
        _ncaContentsCancellation?.Cancel();
        BrowseNcaButton.IsVisible = false;
        NcaContentsList.ItemsSource = null;
        NcaContentsList.IsVisible = false;
        NcaContentsStatus.Text = "";
        SaveNcaContentButton.IsVisible = false;
        _browsedPackageEntry = null;
    }

    private async void BrowseNcaContents(object? sender, RoutedEventArgs e)
    {
        if (_ncaContentsCancellation != null || _openedPath is not { } path) return;
        if (_keysPath is not { } keys)
        {
            NcaContentsStatus.Text = _text.WarnNoProdKeysFileFound;
            return;
        }
        using var cancellation = new CancellationTokenSource();
        _ncaContentsCancellation = cancellation;
        var version = _openVersion;
        var packageEntry = SelectedPackageEntry?.Name;
        BrowseNcaButton.IsEnabled = false;
        BrowseNcaCancel.IsVisible = true;
        NcaContentsList.ItemsSource = null;
        NcaContentsStatus.Text = path + "…";
        try
        {
            var entries = await Task.Run(() => NcaContents.Read(path, keys, cancellation.Token, packageEntry));
            if (_closed || version != _openVersion || cancellation.IsCancellationRequested) return;
            _browsedPackageEntry = packageEntry;
            NcaContentsList.ItemsSource = entries;
            NcaContentsList.IsVisible = true;
            SaveNcaContentButton.IsVisible = true;
            NcaContentsStatus.Text = entries.Count.ToString("N0");
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            if (!_closed && version == _openVersion) NcaContentsStatus.Text = _text.Log_SaveFileCanceled;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or
            LibHac.Common.LibHacException or LibHac.NSZ.NczFormatException or ZstdSharp.ZstdException)
        {
            if (!_closed && version == _openVersion) NcaContentsStatus.Text = ex.Message;
        }
        finally
        {
            _ncaContentsCancellation = null;
            if (!_closed)
            {
                BrowseNcaButton.IsEnabled = true;
                BrowseNcaCancel.IsVisible = false;
            }
        }
    }

    private void CancelNcaContents(object? sender, RoutedEventArgs e) => _ncaContentsCancellation?.Cancel();

    private void NcaContentSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (SaveNcaContentButton != null)
            SaveNcaContentButton.IsEnabled = NcaContentsList.SelectedItem is NcaContentEntry && _ncaContentsCancellation == null;
    }

    private async void SaveNcaContent(object? sender, RoutedEventArgs e)
    {
        if (_ncaContentsCancellation != null || _openedPath is not { } path || _keysPath is not { } keys ||
            NcaContentsList.SelectedItem is not NcaContentEntry entry) return;
        using var cancellation = new CancellationTokenSource();
        _ncaContentsCancellation = cancellation;
        var version = _openVersion;
        var packageEntry = _browsedPackageEntry;
        BrowseNcaButton.IsEnabled = SaveNcaContentButton.IsEnabled = false;
        try
        {
            var target = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = _text.ContextMenu_SaveFileItem,
                SuggestedFileName = Path.GetFileName(entry.Path),
                ShowOverwritePrompt = true
            });
            if (_closed || version != _openVersion || target?.TryGetLocalPath() is not { } destination) return;
            BrowseNcaCancel.IsVisible = true;
            NcaContentsStatus.Text = entry.Path + "…";
            await Task.Run(() => NcaContents.Extract(path, keys, entry, destination, cancellation.Token, packageEntry));
            if (!_closed && version == _openVersion) NcaContentsStatus.Text = destination;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            if (!_closed && version == _openVersion) NcaContentsStatus.Text = _text.Log_SaveFileCanceled;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or
            LibHac.Common.LibHacException or LibHac.NSZ.NczFormatException or ZstdSharp.ZstdException)
        {
            if (!_closed && version == _openVersion) NcaContentsStatus.Text = ex.Message;
        }
        finally
        {
            _ncaContentsCancellation = null;
            if (!_closed)
            {
                BrowseNcaButton.IsEnabled = true;
                BrowseNcaCancel.IsVisible = false;
                SaveNcaContentButton.IsEnabled = NcaContentsList.SelectedItem is NcaContentEntry;
            }
        }
    }
}
