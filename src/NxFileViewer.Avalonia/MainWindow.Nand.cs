using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Emignatik.NxFileViewer.Services.Nand;
namespace NxFileViewer.Avalonia;
public partial class MainWindow
{
    private string? _nandSource;
    private string _nandDirectory = "/";
    private bool _nandBusy;
    private async void OpenNand(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = _text.Nand_Open });
        if (files.FirstOrDefault()?.TryGetLocalPath() is not { } path) return;
        _nandSource = path;
        ReadNand(sender, e);
    }
    private async void ReadNand(object? sender, RoutedEventArgs e)
    {
        if (_nandBusy || _nandSource == null) return;
        _nandBusy = true;
        try
        {
            var source = _nandSource;
            var result = await Task.Run(() =>
            {
                var detection = NandDetection.Detect(source);
                using var reader = new NandExplorer(source);
                return (detection, partitions: reader.GetPartitions(source));
            });
            if (_closed) return;
            NandInformation.Text = source + Environment.NewLine + (result.detection?.Information ?? "Unknown NAND layout");
            LogSession("INFO", NandInformation.Text, "NAND");
            NandPartitions.ItemsSource = result.partitions;
            NandFiles.ItemsSource = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        { if (!_closed) { NandInformation.Text = ex.Message; LogSession("ERROR", ex.Message, "NAND"); } }
        finally { _nandBusy = false; }
    }
    private byte[]? NandKey(string partition)
    {
        var index = partition switch { "PRODINFOF" => "00", "SAFE" => "01", "SYSTEM" => "02", "USER" => "03", _ => null };
        if (index == null || _keysPath == null) return null;
        foreach (var line in File.ReadLines(_keysPath))
        {
            var match = Regex.Match(line, @"^\s*bis_key_" + index + @"\s*=\s*([0-9a-fA-F]{64})\s*(?:[#;].*)?$", RegexOptions.IgnoreCase);
            if (match.Success) return Convert.FromHexString(match.Groups[1].Value);
        }
        return null;
    }
    private void NandPartitionSelected(object? sender, SelectionChangedEventArgs e)
    { _nandDirectory = "/"; ListNandDirectory(); }
    private async void ListNandDirectory()
    {
        if (_nandBusy || _nandSource == null || NandPartitions.SelectedItem is not NandExplorerPartition partition) return;
        _nandBusy = true;
        var source = _nandSource; var directory = _nandDirectory;
        try
        {
            var key = NandKey(partition.Name);
            var entries = await Task.Run(() => { using var reader = new NandExplorer(source); reader.OpenPartition(partition, key); return reader.List(directory); });
            if (!_closed) { NandFiles.ItemsSource = entries; NandLocation.Text = source + " · " + partition.Name + ":" + directory; }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or System.Security.Cryptography.CryptographicException)
        { if (!_closed) NandLocation.Text = ex.Message; }
        finally { _nandBusy = false; }
    }
    private void OpenNandFolder(object? sender, TappedEventArgs e)
    { if (NandFiles.SelectedItem is NandExplorerEntry { IsDirectory: true } entry) { _nandDirectory = entry.Path; ListNandDirectory(); } }
    private void NandUp(object? sender, RoutedEventArgs e)
    { var slash = _nandDirectory.TrimEnd('/').LastIndexOf('/'); _nandDirectory = slash <= 0 ? "/" : _nandDirectory[..slash]; ListNandDirectory(); }
    private async void ExportNandFile(object? sender, RoutedEventArgs e)
    {
        if (_nandBusy || _nandSource == null || NandPartitions.SelectedItem is not NandExplorerPartition partition || NandFiles.SelectedItem is not NandExplorerEntry { IsDirectory: false } entry) return;
        var target = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { SuggestedFileName = entry.Name, ShowOverwritePrompt = true });
        if (target?.TryGetLocalPath() is not { } destination) return;
        var source = _nandSource;
        if (string.Equals(Path.GetFullPath(source), Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase)) return;
        _nandBusy = true;
        var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var key = NandKey(partition.Name);
            await Task.Run(() => { using var reader = new NandExplorer(source); reader.OpenPartition(partition, key); using var output = new FileStream(temporary, FileMode.CreateNew); reader.Export(entry, output); });
            File.Move(temporary, destination, true);
            if (!_closed) NandLocation.Text = destination;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        { if (!_closed) NandLocation.Text = ex.Message; }
        finally { if (File.Exists(temporary)) File.Delete(temporary); _nandBusy = false; }
    }
}
