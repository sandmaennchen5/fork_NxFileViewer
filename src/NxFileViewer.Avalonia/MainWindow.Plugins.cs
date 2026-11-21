using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Emignatik.NxFileViewer.Services;

namespace NxFileViewer.Avalonia;

public partial class MainWindow
{
    private TextBox PluginPathBox(object? sender) => ((sender as Button)?.Tag as string) switch
    {
        "NSZ" => NszGuiPath,
        "NAND" => NandGuiPath,
        "Rust" => RustDesktopPath,
        "Backend" => RustBackendPath,
        "Kronos" => KronosGuiPath,
        _ => throw new ArgumentException("Unknown plugin.")
    };

    private async void BrowsePlugin(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = _text.Workspace_Plugins,
                FileTypeFilter = new[] { new FilePickerFileType("Windows") { Patterns = new[] { "*.exe", "*.bat" } } }
        });
        if (files.FirstOrDefault()?.TryGetLocalPath() is { } path)
            PluginPathBox(sender).Text = path;
    }

    private async void LaunchPlugin(object? sender, RoutedEventArgs e)
    {
        try
        {
            var selected = PluginPathBox(sender).Text;
            if (string.IsNullOrWhiteSpace(selected)) throw new FileNotFoundException(_text.Workspace_Plugins);
            var path = Path.GetFullPath(selected);
            var plugin = (sender as Button)?.Tag as string;
            if (!File.Exists(path))
                throw new FileNotFoundException(path);
            var keys = _keysPath;
            var backend = RustBackendPath.Text;
            if (sender is Button button) button.IsEnabled = false;
            try
            {
                await System.Threading.Tasks.Task.Run(() =>
                {
                    switch (plugin)
                    {
                        case "NSZ":
                            PluginGuiLauncher.Open(path, false, keys);
                            break;
                        case "NAND":
                            PluginGuiLauncher.Open(path, true, keys);
                            break;
                        case "Rust":
                            if (string.IsNullOrWhiteSpace(backend) || !File.Exists(backend))
                                throw new FileNotFoundException("Rust Backend");
                            PluginGuiLauncher.OpenDesktop(path, Path.GetFullPath(backend), keys);
                            break;
                        case "Kronos":
                            PluginGuiLauncher.OpenKronos(Path.GetDirectoryName(path)!, keys);
                            break;
                    }
                });
            }
            finally
            {
                if (!_closed && sender is Button launchButton) launchButton.IsEnabled = true;
            }
            if (_closed) return;
            PluginStatus.Text = path;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or Win32Exception)
        {
            PluginStatus.Text = ex.Message;
        }
    }
}
