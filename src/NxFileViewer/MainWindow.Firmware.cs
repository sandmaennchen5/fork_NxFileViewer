using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Emignatik.NxFileViewer.Services;
using Emignatik.NxFileViewer.Services.Integrity;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Common;

namespace NxFileViewer.Avalonia;

public partial class MainWindow
{
    private CancellationTokenSource? _firmwareCancellation;

    private async void ChooseFirmwareFolder(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = _text.BatchIntegrity_SelectDirectory
        });
        if (folders.FirstOrDefault()?.TryGetLocalPath() is { } path)
            await CheckFirmwareAsync(path, identifyNca: false);
    }

    private async void ChooseFirmwareArchive(object? sender, RoutedEventArgs e) =>
        await ChooseFirmwareFileAsync(_text.Firmware_BrowseZip, new[] { "*.zip", "*.7z" }, identifyNca: false);

    private async void ChooseFirmwareNca(object? sender, RoutedEventArgs e) =>
        await ChooseFirmwareFileAsync(_text.Firmware_Title, new[] { "*.nca" }, identifyNca: true);

    private async Task ChooseFirmwareFileAsync(string title, string[] patterns, bool identifyNca)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            FileTypeFilter = new[] { new FilePickerFileType(string.Join(" / ", patterns)) { Patterns = patterns } }
        });
        if (files.FirstOrDefault()?.TryGetLocalPath() is { } path)
            await CheckFirmwareAsync(path, identifyNca);
    }

    private async Task CheckFirmwareAsync(string path, bool identifyNca)
    {
        if (_closed || _firmwareCancellation != null) return;

        using var workspaceJob = LockWorkspace(BatchTab);
        using var cancellation = new CancellationTokenSource();
        _firmwareCancellation = cancellation;
        FirmwareActions.IsEnabled = false;
        FirmwareCancel.IsEnabled = true;
        FirmwareResult.Text = path + "…";
        try
        {
            var result = await Task.Run(() =>
            {
                var comparison = CreateFirmwareComparison();
                return identifyNca
                    ? comparison.IdentifyNca(path, _text, cancellation.Token)
                    : comparison.Verify(path, _text, cancellation.Token);
            });
            if (_closed) return;

            var status = result.Match switch
            {
                FirmwareMatch.Exact => _text.NcasIntegrity_Original,
                FirmwareMatch.Unchecked => _text.NcasIntegrity_Unknown,
                _ => _text.NcasIntegrity_Error
            };
            FirmwareResult.Text = string.Join(Environment.NewLine, path, status,
                $"{_text.Firmware_Versions}  {result.Version}", result.Details);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            if (!_closed) FirmwareResult.Text = _text.Log_NcasIntegrityCanceled;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or SharpCompressException)
        {
            if (!_closed) FirmwareResult.Text = ex.Message;
        }
        finally
        {
            _firmwareCancellation = null;
            if (!_closed)
            {
                FirmwareActions.IsEnabled = true;
                FirmwareCancel.IsEnabled = false;
            }
        }
    }

    private static FirmwareComparison CreateFirmwareComparison()
    {
        var directory = ApplicationDataPaths.FirmwareHashes(ViewerDataDirectory.Root);
        var manifests = Directory.Exists(directory) ? Directory.EnumerateFiles(directory, "*.json").Select(File.ReadAllText) : Enumerable.Empty<string>();
        return new FirmwareComparison(manifests, ListFirmwareArchiveEntries, ReadFirmwareArchive);
    }

    private static bool IsBatchFirmwareArchive(string path)
    {
        if (!Path.GetFileName(path).Contains("firmware", StringComparison.OrdinalIgnoreCase)) return false;
        var entries = ListFirmwareArchiveEntries(path);
        return entries.Any(entry => entry.EndsWith(".cnmt.nca", StringComparison.OrdinalIgnoreCase)) &&
            entries.All(entry => entry.EndsWith(".nca", StringComparison.OrdinalIgnoreCase));
    }

    private BatchRow ReadBatchFirmware(string path, CancellationToken token)
    {
        var fileType = Path.GetExtension(path).TrimStart('.').ToUpperInvariant();
        try
        {
            var result = CreateFirmwareComparison().Verify(path, _text, token);
            return new BatchRow(path, result.Details, result.Match == FirmwareMatch.Error)
            {
                FirmwareArchiveType = fileType,
                Structure = result.Version,
                Signature = _text.Signature_Unchecked,
                Integrity = result.Match == FirmwareMatch.Exact ? _text.NcasIntegrity_Original : _text.NcasIntegrity_Error,
                Error = result.Error ?? ""
            };
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or SharpCompressException)
        {
            return new BatchRow(path, ex.Message, true)
            {
                FirmwareArchiveType = fileType,
                Signature = _text.Signature_Unchecked,
                Integrity = _text.NcasIntegrity_Unchecked,
                Error = ex.Message
            };
        }
    }

    private void CancelFirmware(object? sender, RoutedEventArgs e) => _firmwareCancellation?.Cancel();

    private static IReadOnlyList<string> ListFirmwareArchiveEntries(string path)
    {
        if (Path.GetExtension(path).Equals(".7z", StringComparison.OrdinalIgnoreCase))
        {
            using var archive = SevenZipArchive.OpenArchive(path);
            return archive.Entries.Where(entry => !entry.IsDirectory).Select(entry => entry.Key!).ToArray();
        }

        using var zip = ZipFile.OpenRead(path);
        return zip.Entries.Where(entry => entry.Name.Length > 0).Select(entry => entry.FullName).ToArray();
    }

    private static void ReadFirmwareArchive(string path, Func<string, bool> include, Action<string, Stream, long> read, CancellationToken token)
    {
        if (Path.GetExtension(path).Equals(".7z", StringComparison.OrdinalIgnoreCase))
        {
            using var archive = SevenZipArchive.OpenArchive(path);
            using var reader = archive.ExtractAllEntries();
            var buffer = new byte[128 * 1024];
            while (reader.MoveToNextEntry())
            {
                token.ThrowIfCancellationRequested();
                if (reader.Entry.IsDirectory) continue;

                using var input = reader.OpenEntryStream();
                var name = reader.Entry.Key!;
                if (include(name))
                    read(name, input, reader.Entry.Size);
                else
                    while (input.Read(buffer, 0, buffer.Length) > 0) token.ThrowIfCancellationRequested();
            }
            return;
        }

        using var zip = ZipFile.OpenRead(path);
        foreach (var entry in zip.Entries.Where(entry => entry.Name.Length > 0 && include(entry.FullName)))
        {
            token.ThrowIfCancellationRequested();
            using var input = entry.Open();
            read(entry.FullName, input, entry.Length);
        }
    }
}
