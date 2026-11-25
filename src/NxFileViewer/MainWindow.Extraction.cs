using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;

namespace NxFileViewer.Avalonia;

public partial class MainWindow
{
    private enum ExportMode { Raw, Reconstructed, Plaintext, Directory, File }
    private CancellationTokenSource? _extractionCancellation;

    private async Task ExportContentAsync(string source, ContentNode node, ExportMode mode)
    {
        if (_closed || _openedPath != source || _workspaceJobs.Count > 0 || _extractionCancellation != null) return;
        using var job = LockWorkspace(FileTab);
        using var cancellation = new CancellationTokenSource();
        var keys = _keysPath;
        _extractionCancellation = cancellation;
        VerifyFileCancelButton.IsVisible = VerifyFileCancelButton.IsEnabled = true;
        try
        {
            var name = mode is ExportMode.Reconstructed or ExportMode.Plaintext
                ? Path.ChangeExtension(Path.GetFileName(node.Name), ".nca") : Path.GetFileName(node.Name);
            if (string.IsNullOrEmpty(name) || name is "." or "..") return;
            var title = mode switch
            {
                ExportMode.Raw when node.Kind == ContentKind.Nca => _text.ContextMenu_SaveNcaFileRaw,
                ExportMode.Raw => _text.ContextMenu_SavePartitionFileItem,
                ExportMode.Plaintext => _text.ContextMenu_SaveNcaFilePlaintext,
                ExportMode.Directory when node.Kind == ContentKind.Section => _text.ContextMenu_SaveSectionItem,
                ExportMode.Directory => _text.ContextMenu_SaveDirectoryItem,
                _ => _text.ContextMenu_SaveFileItem
            };
            string? destination;
            if (mode == ExportMode.Directory)
            {
                var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = title });
                destination = folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
            }
            else
            {
                var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = title, SuggestedFileName = name, ShowOverwritePrompt = true
                });
                destination = file?.TryGetLocalPath();
                if (destination != null)
                {
                    var targetPath = Path.GetFullPath(destination);
                    var archivePath = _openedDisplayPath?.Split(" :: ", 2)[0];
                    var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
                    if (string.Equals(Path.GetFullPath(source), targetPath, comparison) ||
                        (archivePath != null && string.Equals(Path.GetFullPath(archivePath), targetPath, comparison)))
                        throw new IOException("The destination is the source file.");
                }
            }
            if (destination == null || _closed || _openedPath != source) return;
            var token = cancellation.Token;
            StatusText.Text = title + " " + name;
            FileStatusProgress.IsVisible = FileStatusProgress.IsIndeterminate = true;
            await Task.Run(() =>
            {
                switch (mode)
                {
                    case ExportMode.Raw:
                    case ExportMode.Reconstructed:
                        NcaContents.ExportRaw(source, node.Member, destination, mode == ExportMode.Reconstructed, token);
                        break;
                    case ExportMode.Plaintext:
                        NcaContents.ExportPlaintext(source, keys!, node.Member, destination, token);
                        break;
                    case ExportMode.Directory:
                        NcaContents.ExportDirectory(source, keys!, node.Member, node.Section, node.InnerPath ?? "/", destination, name, token);
                        break;
                    case ExportMode.File:
                        NcaContents.ExportFile(source, keys!, node.Member, node.Section, node.InnerPath!, destination, token);
                        break;
                }
            }, token);
            if (!_closed) StatusText.Text = mode == ExportMode.Directory ? Path.Combine(destination, name) : destination;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            if (!_closed) StatusText.Text = _text.Log_SaveFileCanceled;
        }
        catch (Exception error) when (IsContentError(error))
        {
            if (!_closed) { StatusText.Text = error.Message; LogSession("ERROR", error.Message); }
        }
        finally
        {
            _extractionCancellation = null;
            if (!_closed)
            {
                FileStatusProgress.IsVisible = false;
                VerifyFileCancelButton.IsEnabled = VerifyFileCancelButton.IsVisible = false;
            }
        }
    }
}

