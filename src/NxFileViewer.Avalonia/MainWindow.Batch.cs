using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using LibHac.Common;

namespace NxFileViewer.Avalonia;

public partial class MainWindow
{
    private sealed record BatchRow(string Path, string Summary);

    private readonly ObservableCollection<BatchRow> _batchRows = new();
    private CancellationTokenSource? _batchCancellation;
    private string? _batchFolder;

    private async void ChooseBatchFolder(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = _text.BatchIntegrity_SelectDirectory
        });
        if (_closed || folders.FirstOrDefault()?.TryGetLocalPath() is not { } folder) return;

        _batchFolder = folder;
        BatchFolderText.Text = folder;
        BatchStartButton.IsEnabled = _batchCancellation == null;
        BatchHashButton.IsEnabled = BatchStartButton.IsEnabled;
    }

    private async void StartBatch(object? sender, RoutedEventArgs e)
    {
        if (_batchCancellation != null || _batchFolder is not { } folder) return;

        using var cancellation = new CancellationTokenSource();
        _batchCancellation = cancellation;
        _batchRows.Clear();
        BatchStatus.Text = "";
        BatchStartButton.IsEnabled = false;
        BatchHashButton.IsEnabled = false;
        BatchCancelButton.IsEnabled = true;
        var recursive = BatchRecursive.IsChecked == true;
        var checkHashes = ReferenceEquals(sender, BatchHashButton);
        var keysPath = _keysPath;
        var progress = new Progress<BatchRow>(row =>
        {
            if (_closed || !ReferenceEquals(_batchCancellation, cancellation)) return;
            _batchRows.Add(row);
            BatchStatus.Text = _batchRows.Count.ToString("N0");
        });

        try
        {
            await Task.Run(() => ScanFolder(folder, recursive, keysPath, checkHashes, progress, cancellation.Token));
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            if (!_closed) BatchStatus.Text = _text.Log_NcasIntegrityCanceled;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (!_closed) BatchStatus.Text = ex.Message;
        }
        finally
        {
            _batchCancellation = null;
            if (!_closed)
            {
                BatchCancelButton.IsEnabled = false;
                BatchStartButton.IsEnabled = true;
                BatchHashButton.IsEnabled = true;
            }
        }
    }

    private void ScanFolder(string folder, bool recursive, string? keysPath, bool checkHashes, IProgress<BatchRow> progress, CancellationToken token)
    {
        var options = new EnumerationOptions { RecurseSubdirectories = recursive, IgnoreInaccessible = true };
        foreach (var path in Directory.EnumerateFiles(folder, "*", options))
        {
            token.ThrowIfCancellationRequested();
            if (Path.GetExtension(path).ToLowerInvariant() is not (".nsp" or ".nsz" or ".xci" or ".xcz" or ".nro"))
                continue;

            string summary;
            try
            {
                var hashes = checkHashes ? new List<NcaHashResult>() : null;
                var package = PackageFile.Read(path, keysPath, hashes, token);
                summary = string.Join(" · ", new[] { package.Type, $"{package.Size:N0} B", package.MetadataError }
                    .Where(value => !string.IsNullOrWhiteSpace(value)));
                if (hashes != null)
                {
                    var details = hashes.Count == 0 ? _text.NcasIntegrity_NoNca : string.Join(Environment.NewLine, hashes.Select(FormatCheck));
                    summary += Environment.NewLine + details;
                    if (keysPath == null && hashes.Count > 0)
                        summary += Environment.NewLine + _text.WarnNoProdKeysFileFound;
                }
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or LibHac.Common.LibHacException)
            {
                summary = ex.Message;
            }
            progress.Report(new BatchRow(path, summary));
        }
    }

    private string FormatCheck(NcaHashResult result)
    {
        var hash = result.Matches switch
        {
            true => string.Format(_text.NcaHash_Valid_Log, result.Name),
            false => _text.NcaHash_NcaItem_Invalid,
            null => _text.NcasIntegrity_Unknown
        };
        var signature = result.Signature switch
        {
            Validity.Valid => string.Format(_text.NcaHeaderSignature_Valid_Log, result.Name),
            { } status => string.Format(_text.NcaHeaderSignature_Invalid, status),
            null => null
        };
        return string.Join(" · ", new[] { result.Name, hash, signature, result.Error }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private void CancelBatch(object? sender, RoutedEventArgs e) => _batchCancellation?.Cancel();

    private async void OpenBatchResult(object? sender, TappedEventArgs e)
    {
        if (BatchResults.SelectedItem is BatchRow row)
            await OpenFileAsync(row.Path);
    }
}
