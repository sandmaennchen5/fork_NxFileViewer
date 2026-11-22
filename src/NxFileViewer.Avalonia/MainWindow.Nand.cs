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
    private System.Threading.CancellationTokenSource? _nandCancellation;
    private void CancelNand(object? sender, RoutedEventArgs e) => _nandCancellation?.Cancel();
    private void RefreshNandActions()
    {
        NandInfoButton.IsEnabled = !_nandBusy && _nandSource != null;
        NandExportPartitionButton.IsEnabled = !_nandBusy && NandPartitions.SelectedItem is NandExplorerPartition;
        NandCancelButton.IsEnabled = _nandCancellation != null;
        NandExportFileButton.IsEnabled = !_nandBusy && NandFiles.SelectedItem is NandExplorerEntry { IsDirectory: false };
        NandUpButton.IsEnabled = !_nandBusy && _nandSource != null && _nandDirectory != "/";
    }
    private void NandSelectionChanged(object? sender, SelectionChangedEventArgs e) => RefreshNandActions();
    private static bool IsNandSourcePath(string source, string destination)
    {
        var target = Path.GetFullPath(destination);
        return (NandExplorer.IsDevice(source) ? new[] { source } : NandDetection.SplitFiles(source)).Any(part => string.Equals(Path.GetFullPath(part), target, StringComparison.OrdinalIgnoreCase));
    }
    private async void ExportNandPartition(object? sender, RoutedEventArgs e)
    {
        if (_nandBusy || _nandSource == null || NandPartitions.SelectedItem is not NandExplorerPartition partition) return;
        var target = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { Title = _text.Nand_Export, SuggestedFileName = partition.Name + ".bin", ShowOverwritePrompt = true });
        if (target?.TryGetLocalPath() is not { } destination) return;
        var source = _nandSource;
        if (IsNandSourcePath(source, destination)) return;
        using var cancellation = new System.Threading.CancellationTokenSource(); _nandCancellation = cancellation;
        _nandBusy = true; RefreshNandActions();
        var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await Task.Run(() => { using var reader = new NandExplorer(source, cancellation.Token); using var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write); reader.ExportPartition(partition, output); }, cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            File.Move(temporary, destination, true);
            if (!_closed) NandLocation.Text = destination;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { if (!_closed) NandLocation.Text = _text.Nand_Cancelled; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { if (!_closed) NandLocation.Text = ex.Message; }
        finally { if (File.Exists(temporary)) File.Delete(temporary); _nandCancellation = null; _nandBusy = false; if (!_closed) RefreshNandActions(); }
    }
    private async void OpenNandDrive(object? sender, RoutedEventArgs e)
    {
        if (!OperatingSystem.IsWindows()) return;
        var panel = new StackPanel { Margin = new global::Avalonia.Thickness(16), Spacing = 8 };
        var choices = new ComboBox { HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch, DisplayMemberBinding = new global::Avalonia.Data.Binding(nameof(NandDriveChoice.DisplayName)) };
        var path = new TextBox { PlaceholderText = @"\\.\PhysicalDrive0" };
        var open = new Button { Content = _text.Nand_OpenDrive, IsDefault = true, HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Right };
        var refresh = new Button { Content = _text.Nand_DriveRefresh, HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Left };
        var error = new TextBlock { TextWrapping = global::Avalonia.Media.TextWrapping.Wrap };
        var dialog = new Window { Title = _text.Nand_OpenDrive, Width = 540, SizeToContent = SizeToContent.Height, CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner, RequestedThemeVariant = ActualThemeVariant, Content = panel };
        async Task Refresh()
        {
            refresh.IsEnabled = false;
            try { choices.ItemsSource = await Task.Run(() => OperatingSystem.IsWindows() ? NandDriveDiscovery.List() : Array.Empty<NandDriveChoice>()); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception) { error.Text = ex.Message; }
            finally { refresh.IsEnabled = true; }
        }
        choices.SelectionChanged += (_, _) => { if (choices.SelectedItem is NandDriveChoice drive) path.Text = drive.Path; };
        refresh.Click += async (_, _) => await Refresh();
        open.Click += (_, _) => { if (NandExplorer.IsDevice(path.Text?.Trim() ?? "")) dialog.Close(path.Text!.Trim()); };
        panel.Children.Add(new TextBlock { Text = _text.Nand_DriveTip, TextWrapping = global::Avalonia.Media.TextWrapping.Wrap });
        panel.Children.Add(choices); panel.Children.Add(path); panel.Children.Add(refresh); panel.Children.Add(error); panel.Children.Add(open);
        WpfTheme.Apply(dialog); dialog.Opened += async (_, _) => await Refresh();
        if (await dialog.ShowDialog<string?>(this) is { } selected) { _nandSource = selected; ReadNand(sender, e); }
    }
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
        _nandBusy = true; RefreshNandActions();
        try
        {
            var source = _nandSource;
            var result = await Task.Run(() =>
            {
                var detection = NandExplorer.IsDevice(source) ? null : NandDetection.Detect(source);
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
        finally { _nandBusy = false; if (!_closed) RefreshNandActions(); }
    }
    private byte[]? NandKey(string partition)
    {
        var index = partition switch { "PRODINFOF" => "00", "SAFE" => "01", "SYSTEM" => "02", "USER" => "03", _ => null };
        var keyPath = ResolveKeyFile(_preferences.SharedSettings.NandBisKeysPath, "prod.keys") ?? _keysPath;
        if (index == null || keyPath == null || !File.Exists(keyPath)) return null;
        foreach (var line in File.ReadLines(keyPath))
        {
            var match = Regex.Match(line, @"^\s*bis_key_" + index + @"\s*=\s*([0-9a-fA-F]{64})\s*(?:[#;].*)?$", RegexOptions.IgnoreCase);
            if (match.Success) return Convert.FromHexString(match.Groups[1].Value);
        }
        return null;
    }
    private void NandPartitionSelected(object? sender, SelectionChangedEventArgs e)
    { _nandDirectory = "/"; RefreshNandActions(); ListNandDirectory(); }
    private async void ListNandDirectory()
    {
        if (_nandBusy || _nandSource == null || NandPartitions.SelectedItem is not NandExplorerPartition partition) return;
        _nandBusy = true; RefreshNandActions();
        var source = _nandSource; var directory = _nandDirectory;
        try
        {
            var key = NandKey(partition.Name);
            var entries = await Task.Run(() => { using var reader = new NandExplorer(source); reader.OpenPartition(partition, key); return reader.List(directory); });
            if (!_closed) { NandFiles.ItemsSource = entries; NandLocation.Text = source + " · " + partition.Name + ":" + directory; }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or System.Security.Cryptography.CryptographicException)
        { if (!_closed) NandLocation.Text = ex.Message; }
        finally { _nandBusy = false; if (!_closed) RefreshNandActions(); }
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
        if (IsNandSourcePath(source, destination)) return;
        _nandBusy = true; RefreshNandActions();
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
        finally { if (File.Exists(temporary)) File.Delete(temporary); _nandBusy = false; if (!_closed) RefreshNandActions(); }
    }
}
