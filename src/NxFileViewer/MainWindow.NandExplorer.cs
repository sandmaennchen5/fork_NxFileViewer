using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Emignatik.NxFileViewer.Services.Nand;

namespace NxFileViewer.Avalonia;

public partial class MainWindow
{
    private sealed record NandFolder(string Name, string Path)
    {
        public ObservableCollection<NandFolder> Children { get; } = new();
    }

    private void ResetNandExplorer()
    {
        _nandSave = null;
        _nandDirectory = "/";
        _nandRoot = null;
        NandFolders.ItemsSource = null;
        NandFiles.ItemsSource = null;
        NandLocation.Text = "";
    }

    private async void NandViewSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (ReferenceEquals(e.Source, NandViews) && NandViews.SelectedIndex == 1)
            await ListNandDirectory(_nandDirectory, _nandSave);
    }

    private async void NandPartitionSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (_nandBusy) return;
        ResetNandExplorer();
        RefreshNandActions();
        if (NandViews.SelectedIndex == 1) await ListNandDirectory("/", null);
    }

    private async void OpenNandFolder(object? sender, TappedEventArgs e)
    {
        if (NandFiles.SelectedItem is not NandExplorerEntry entry) return;
        if (entry.IsDirectory) await ListNandDirectory(entry.Path, _nandSave);
        else if (_nandSave == null && entry.IsSave) await ListNandDirectory("/", entry);
    }

    private async void SelectNandTreeFolder(object? sender, SelectionChangedEventArgs e)
    {
        if (NandFolders.SelectedItem is NandFolder folder && folder.Path != _nandDirectory)
            await ListNandDirectory(folder.Path, _nandSave);
    }

    private async void NandUp(object? sender, RoutedEventArgs e)
    {
        var slash = _nandDirectory.TrimEnd('/').LastIndexOf('/');
        await ListNandDirectory(slash <= 0 ? "/" : _nandDirectory[..slash], _nandSave);
    }

    private async void OpenNandSave(object? sender, RoutedEventArgs e)
    {
        if (_nandSave == null && NandFiles.SelectedItem is NandExplorerEntry { IsSave: true } save)
            await ListNandDirectory("/", save);
    }

    private async void LeaveNandSave(object? sender, RoutedEventArgs e)
    {
        if (_nandSave is { } save)
            await ListNandDirectory(save.Path[..save.Path.LastIndexOf('/')], null);
    }

    private async Task ListNandDirectory(string directory, NandExplorerEntry? save)
    {
        if (_nandBusy || _nandSource is not { } source || NandPartitions.SelectedItem is not NandExplorerPartition partition) return;
        using var workspaceJob = LockWorkspace(NandTab);
        using var cancellation = new CancellationTokenSource();
        _nandCancellation = cancellation;
        _nandBusy = true;
        RefreshNandActions();
        try
        {
            var service = CreateNandService();
            var token = cancellation.Token;
            var entries = await Task.Run(() => save == null
                ? service.ListExplorer(source, partition, directory, token)
                : service.ListSave(source, partition, save, directory, token), token);
            if (_closed) return;
            if (save != _nandSave || _nandRoot == null)
            {
                _nandRoot = new NandFolder("/", "/");
                NandFolders.ItemsSource = new[] { _nandRoot };
            }
            var current = _nandRoot;
            foreach (var name in directory.Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                var path = current.Path.TrimEnd('/') + "/" + name;
                var child = current.Children.FirstOrDefault(folder => folder.Path == path);
                if (child == null) current.Children.Add(child = new NandFolder(name, path));
                current = child;
            }
            foreach (var entry in entries.Where(entry => entry.IsDirectory))
                if (!current.Children.Any(folder => folder.Path == entry.Path))
                    current.Children.Add(new NandFolder(entry.Name, entry.Path));
            _nandSave = save;
            _nandDirectory = directory;
            NandFolders.SelectedItem = current;
            NandFiles.ItemsSource = entries;
            NandLocation.Text = source + " · " + partition.Name + ":" +
                (save == null ? directory : save.Path + " → " + directory);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        { if (!_closed) NandLocation.Text = _text.Nand_Cancelled; }
        catch (Exception error) when (IsNandExplorerError(error))
        { if (!_closed) NandLocation.Text = error.Message; }
        finally
        {
            _nandCancellation = null;
            _nandBusy = false;
            if (!_closed) RefreshNandActions();
        }
    }

    private async void ExportNandSave(object? sender, RoutedEventArgs e)
    {
        if (_nandBusy || _nandSource is not { } source || _nandSave is not { } save ||
            NandPartitions.SelectedItem is not NandExplorerPartition partition) return;
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = _text.Nand_ExportSave });
        if (folders.FirstOrDefault()?.TryGetLocalPath() is not { } folder || _closed) return;
        using var workspaceJob = LockWorkspace(NandTab);
        using var cancellation = new CancellationTokenSource();
        _nandCancellation = cancellation;
        _nandBusy = true;
        RefreshNandActions();
        try
        {
            var destination = NandSaveExplorer.ExportPath(folder, "/" + save.Name);
            var service = CreateNandService();
            await Task.Run(() => service.ExportSave(source, partition, save, destination, cancellation.Token), cancellation.Token);
            if (!_closed) NandLocation.Text = destination;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        { if (!_closed) NandLocation.Text = _text.Nand_Cancelled; }
        catch (Exception error) when (IsNandExplorerError(error))
        { if (!_closed) NandLocation.Text = error.Message; }
        finally
        {
            _nandCancellation = null;
            _nandBusy = false;
            if (!_closed) RefreshNandActions();
        }
    }

    private static bool IsNandExplorerError(Exception error) => error is IOException or InvalidDataException or
        UnauthorizedAccessException or ArgumentException or InvalidOperationException or LibHac.Common.LibHacException or
        System.Security.Cryptography.CryptographicException;
}
