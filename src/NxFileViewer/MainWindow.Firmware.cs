using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Interactivity;
using Emignatik.NxFileViewer.Services;
using Emignatik.NxFileViewer.Services.Integrity;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Common;

namespace NxFileViewer.Avalonia;

public partial class MainWindow
{
    private CancellationTokenSource? _fileFirmwareCancellation;

    private void ResetFileFirmware()
    {
        _fileFirmwareCancellation?.Cancel();
        FileFirmwareTab.IsVisible = false;
        FileFirmwareVersions.Text = FileFirmwareDetails.Text = "";
    }

    private async Task IdentifyOpenedFirmwareAsync(string path, int openVersion)
    {
        using var cancellation = new CancellationTokenSource();
        _fileFirmwareCancellation = cancellation;
        var token = cancellation.Token;
        var keys = _text;
        FileFirmwareTab.IsVisible = true;
        FileFirmwareDetails.Text = keys.DataUpdate_Working;
        try
        {
            FirmwareComparison comparison;
            string source;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromSeconds(15));
            try
            {
                var manifests = await new RepositoryMirrors().FirmwareManifestsAsync(ExternalSources.Current.FirmwareHashes, timeout.Token);
                comparison = await Task.Run(() => new FirmwareComparison(manifests.Values, ListFirmwareArchiveEntries, ReadFirmwareArchive), token);
                source = keys.Firmware_OnlineSource;
            }
            catch (Exception ex) when (!token.IsCancellationRequested && ex is (HttpRequestException or OperationCanceledException
                or IOException or InvalidDataException or UnauthorizedAccessException or JsonException or InvalidOperationException or ArgumentException or KeyNotFoundException))
            {
                LogSession("WARN", $"{keys.Firmware_OfflineSource} · {ex.Message}");
                comparison = await Task.Run(CreateFirmwareComparison, token);
                source = keys.Firmware_OfflineSource;
            }

            var result = await Task.Run(() => comparison.IdentifyNca(path, keys, token), token);
            if (_closed || openVersion != _openVersion) return;
            FileFirmwareVersions.Text = result.Version;
            FileFirmwareDetails.Text = source + Environment.NewLine + result.Details;
            LogFileSection(path, [("DEBUG", source), ("INFO", result.Details)]);
            if (result.IsFirmware && FileBody.SelectedIndex == 0)
                FileBody.SelectedItem = FileFirmwareTab;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException
            or InvalidOperationException or ArgumentException or KeyNotFoundException)
        {
            if (!_closed && openVersion == _openVersion)
            {
                FileFirmwareDetails.Text = ex.Message;
                LogFileSection(path, [("ERROR", ex.Message)]);
            }
        }
        finally
        {
            if (ReferenceEquals(_fileFirmwareCancellation, cancellation)) _fileFirmwareCancellation = null;
        }
    }

    private static FirmwareComparison CreateFirmwareComparison()
    {
        var directory = ApplicationDataPaths.FirmwareHashes(ViewerDataDirectory.Root);
        var manifests = Directory.Exists(directory) ? Directory.EnumerateFiles(directory, "*.json").Select(File.ReadAllText) : Enumerable.Empty<string>();
        return new FirmwareComparison(manifests, ListFirmwareArchiveEntries, ReadFirmwareArchive);
    }

    private static bool IsBatchFirmwareArchive(string path) => LooksLikeFirmwareSource(path, ListFirmwareArchiveEntries(path));

    private async Task OpenFirmwareArchiveAsync(string path, IReadOnlyList<(string Name, long Size)> files, CancellationToken token)
    {
        var size = new FileInfo(path).Length;
        var root = new ContentNode(Path.GetFileName(path), ContentKind.Archive, size) { IsRoot = true };
        var folders = new Dictionary<string, ContentNode>(StringComparer.Ordinal) { [""] = root };
        foreach (var file in files)
        {
            var parts = file.Name.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            var parent = root;
            var folderPath = "";
            foreach (var part in parts.SkipLast(1))
            {
                folderPath += "/" + part;
                if (!folders.TryGetValue(folderPath, out var folder))
                {
                    folder = new ContentNode(part, ContentKind.ArchiveDirectory);
                    parent.Children.Add(folder);
                    folders.Add(folderPath, folder);
                }
                parent = folder;
            }
            if (parts.Length > 0)
                parent.Children.Add(new ContentNode(parts[^1], ContentKind.ArchiveFile, file.Size));
        }

        CloseClicked(this, new RoutedEventArgs());
        var version = _openVersion;
        _openedPath = path;
        RememberOpenedPath(path);
        Title = ProgramVersionText.Text + " - " + root.Name;
        OverviewType.Text = _text.Firmware_Title;
        OverviewSize.Text = Emignatik.NxFileViewer.Utils.FileSizeHelper.ToFileSize(size);
        Entries.ItemsSource = new[] { root };
        Entries.SelectedItem = root;
        FileEmpty.IsVisible = false;
        FileBody.IsVisible = true;
        Workspace.SelectedItem = FileTab;
        FileFirmwareTab.IsVisible = true;
        FileFirmwareDetails.Text = _text.DataUpdate_Working;
        FileBody.SelectedItem = FileFirmwareTab;
        VerifyFileButton.IsEnabled = false;
        FileStatusProgress.IsVisible = true;
        StatusText.Text = _text.DataUpdate_Working;
        LogFileSection(path);

        try
        {
            var result = await Task.Run(() => ReadBatchFirmware(path, token, progress: null), token);
            if (_closed || _openVersion != version) return;
            FileFirmwareVersions.Text = result.Structure;
            FileFirmwareDetails.Text = result.Summary;
            StatusText.Text = root.Name + " · " + result.Integrity;
            LogFileSection(path, [(result.HasError ? "ERROR" : "INFO", result.Summary)]);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            if (!_closed && _openVersion == version)
                FileFirmwareDetails.Text = StatusText.Text = _text.Log_NcasIntegrityCanceled;
        }
    }

    private static bool LooksLikeFirmwareSource(string path, IReadOnlyList<string> entries) =>
        entries.Any(entry => entry.EndsWith(".cnmt.nca", StringComparison.OrdinalIgnoreCase)) &&
            entries.All(entry => entry.EndsWith(".nca", StringComparison.OrdinalIgnoreCase)) &&
            (Path.GetFileName(path).Contains("firmware", StringComparison.OrdinalIgnoreCase) ||
                entries.Count >= 10 && entries.All(entry =>
                {
                    var id = Path.GetFileName(entry.Replace('\\', '/')).Split('.', 2)[0];
                    return id.Length == 32 && id.All(Uri.IsHexDigit);
                }));

    private BatchRow ReadBatchFirmware(string path, CancellationToken token, Action<double>? progress)
    {
        var fileType = Directory.Exists(path) ? "Folder" : Path.GetExtension(path).TrimStart('.').ToUpperInvariant();
        try
        {
            progress?.Invoke(0);
            var result = CreateFirmwareComparison().Verify(path, _text, token, progress);
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

    private static IReadOnlyList<string> ListFirmwareArchiveEntries(string path) =>
        ListFirmwareArchiveFiles(path).Select(entry => entry.Name).ToArray();

    private static IReadOnlyList<(string Name, long Size)> ListFirmwareArchiveFiles(string path)
    {
        if (Path.GetExtension(path).Equals(".7z", StringComparison.OrdinalIgnoreCase))
        {
            using var archive = SevenZipArchive.OpenArchive(path);
            return archive.Entries.Where(entry => !entry.IsDirectory).Select(entry => (entry.Key!, entry.Size)).ToArray();
        }

        using var zip = ZipFile.OpenRead(path);
        return zip.Entries.Where(entry => entry.Name.Length > 0).Select(entry => (entry.FullName, entry.Length)).ToArray();
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
