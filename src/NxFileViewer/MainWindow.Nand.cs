using System;
using System.IO;
using System.Linq;
using System.Security.Principal;
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
    private NandExplorerEntry? _nandSave;
    private NandFolder? _nandRoot;
    private System.Threading.CancellationTokenSource? _nandCancellation;
    private void CancelNand(object? sender, RoutedEventArgs e) => _nandCancellation?.Cancel();
    private void RefreshNandActions()
    {
        NandInfoButton.IsEnabled = !_nandBusy && _nandSource != null;
        NandCloseButton.IsEnabled = !_nandBusy && _nandSource != null;
        NandPartitions.IsEnabled = !_nandBusy;
        NandExportPartitionButton.IsEnabled = !_nandBusy && NandPartitions.SelectedItem is NandExplorerPartition;
        NandCancelButton.IsEnabled = _nandCancellation != null;
        NandExportFileButton.IsEnabled = !_nandBusy && NandFiles.SelectedItem is NandExplorerEntry { IsDirectory: false };
        NandOpenSaveButton.IsEnabled = !_nandBusy && _nandSave == null && NandFiles.SelectedItem is NandExplorerEntry { IsSave: true };
        NandLeaveSaveButton.IsEnabled = NandExportSaveButton.IsEnabled = !_nandBusy && _nandSave != null;
        NandUpButton.IsEnabled = !_nandBusy && _nandSource != null && _nandDirectory != "/";
    }
    private void NandSelectionChanged(object? sender, SelectionChangedEventArgs e) => RefreshNandActions();

    private void CloseNand(object? sender, RoutedEventArgs e)
    {
        if (_nandBusy || _nandSource == null) return;
        _nandSource = null;
        NandPartitions.ItemsSource = null;
        ResetNandExplorer();
        NandInformation.Text = "";
        NandViews.SelectedIndex = 0;
        StatusText.Text = _text.Status_Ready;
        RefreshNandActions();
    }
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
        using var workspaceJob = LockWorkspace(NandTab);
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
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or LibHac.Common.LibHacException) { if (!_closed) NandLocation.Text = ex.Message; }
        finally { if (File.Exists(temporary)) File.Delete(temporary); _nandCancellation = null; _nandBusy = false; if (!_closed) RefreshNandActions(); }
    }
    private async void OpenNandDrive(object? sender, RoutedEventArgs e)
    {
        if (_nandBusy || !OperatingSystem.IsWindows()) return;
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
        using (var identity = WindowsIdentity.GetCurrent())
        {
            if (!new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator))
            {
                var warning = new TextBlock { Text = _text.Nand_DriveNotElevated, TextWrapping = global::Avalonia.Media.TextWrapping.Wrap };
                warning.Classes.Add("permission-warning");
                panel.Children.Add(warning);
            }
        }
        panel.Children.Add(choices); panel.Children.Add(path); panel.Children.Add(refresh); panel.Children.Add(error); panel.Children.Add(open);
        WpfTheme.Apply(dialog); dialog.Opened += async (_, _) => await Refresh();
        if (await dialog.ShowDialog<string?>(this) is { } selected && !_nandBusy && !_closed) { _nandSource = selected; ReadNand(sender, e); }
    }
    private async void OpenNand(object? sender, RoutedEventArgs e)
    {
        if (_nandBusy) return;
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = _text.Nand_Open });
        if (files.FirstOrDefault()?.TryGetLocalPath() is not { } path || _nandBusy || _closed) return;
        _nandSource = path;
        ReadNand(sender, e);
    }
    private async void ReadNand(object? sender, RoutedEventArgs e)
    {
        if (_nandBusy || _nandSource == null) return;
        using var workspaceJob = LockWorkspace(NandTab);
        using var cancellation = new System.Threading.CancellationTokenSource();
        _nandCancellation = cancellation;
        _nandBusy = true; RefreshNandActions();
        NandViews.SelectedIndex = 0;
        try
        {
            var source = _nandSource;
            var requestPlugin = ReferenceEquals(sender, NandInfoButton);
            var progress = new MtpProgress(new Progress<string>(message =>
            {
                if (_closed || !ReferenceEquals(_nandCancellation, cancellation)) return;
                StatusText.Text = message;
                LogSession("DEBUG", message, "NAND");
            }));
            var result = await Task.Run(() =>
            {
                var token = cancellation.Token;
                var detection = NandExplorer.IsDevice(source) ? null : NandDetection.Detect(source, token);
                return ReadNandReport(source, detection, requestPlugin, progress, token);
            });
            if (_closed) return;
            if (result.Partitions.Count > 0) RememberOpenedPath(source);
            NandInformation.Text = source + Environment.NewLine + result.Information;
            LogSession("INFO", $"{source} · {string.Join(", ", result.Partitions.Select(partition => partition.Name))}", "NAND");
            NandPartitions.ItemsSource = result.Partitions;
            ResetNandExplorer();
            NandPartitions.SelectedIndex = result.Partitions.Count > 0 ? 0 : -1;
        }
        catch (OperationCanceledException) { if (!_closed) NandInformation.Text = _text.Nand_Cancelled; }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        { if (!_closed) { NandPartitions.ItemsSource = null; ResetNandExplorer(); NandInformation.Text = ex.Message; LogSession("ERROR", ex.Message, "NAND"); } }
        finally { _nandCancellation = null; _nandBusy = false; if (!_closed) RefreshNandActions(); }
    }
    private async void ExportNandFile(object? sender, RoutedEventArgs e)
    {
        if (_nandBusy || _nandSource == null || NandPartitions.SelectedItem is not NandExplorerPartition partition || NandFiles.SelectedItem is not NandExplorerEntry { IsDirectory: false } entry) return;
        var target = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { SuggestedFileName = entry.Name, ShowOverwritePrompt = true });
        if (target?.TryGetLocalPath() is not { } destination) return;
        var source = _nandSource;
        if (IsNandSourcePath(source, destination)) return;
        using var workspaceJob = LockWorkspace(NandTab);
        _nandBusy = true; RefreshNandActions();
        var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var service = CreateNandService();
            var save = _nandSave;
            await Task.Run(() =>
            {
                if (save == null) service.ExportExplorer(source, partition, entry, temporary, System.Threading.CancellationToken.None);
                else service.ExportSaveFile(source, partition, save, entry.Path, temporary, System.Threading.CancellationToken.None);
            });
            File.Move(temporary, destination, true);
            if (!_closed) NandLocation.Text = destination;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or LibHac.Common.LibHacException)
        { if (!_closed) NandLocation.Text = ex.Message; }
        finally { if (File.Exists(temporary)) File.Delete(temporary); _nandBusy = false; if (!_closed) RefreshNandActions(); }
    }
}
