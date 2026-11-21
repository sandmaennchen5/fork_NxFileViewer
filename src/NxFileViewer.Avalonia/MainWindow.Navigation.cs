using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace NxFileViewer.Avalonia;

public partial class MainWindow
{
    private string? _lastOpenedPath;

    private void NavigateWorkspace(object? sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not string name || this.FindControl<TabItem>(name) is not { } tab) return;
        if (name is "PluginsTab" or "UpdatesTab" or "NamingSettingsTab")
        {
            Workspace.SelectedItem = SettingsTab;
            SettingsPages.SelectedItem = tab;
        }
        else Workspace.SelectedItem = tab;
    }

    private void RefreshHome()
    {
        var missingKeys = string.IsNullOrWhiteSpace(_keysPath) || !File.Exists(_keysPath);
        HomeKeysWarning.IsVisible = FileKeysWarning.IsVisible = missingKeys;
        HomeNszLaunch.IsVisible = _preferences.NszDirectStart;
        HomeNandLaunch.IsVisible = _preferences.NandDirectStart;
        HomeRustLaunch.IsVisible = _preferences.RustDirectStart;
        HomeNandLaunch.IsEnabled = File.Exists(NandGuiPath.Text);
        HomeNszLaunch.IsEnabled = File.Exists(NszGuiPath.Text);
        HomeRustLaunch.IsEnabled = File.Exists(RustDesktopPath.Text) && File.Exists(RustBackendPath.Text);
        HomeKronosLaunch.IsVisible = _preferences.KronosDirectStart && File.Exists(KronosGuiPath.Text);
        HomePluginPanel.IsVisible = HomeNandLaunch.IsVisible || HomeNszLaunch.IsVisible ||
            HomeRustLaunch.IsVisible || HomeKronosLaunch.IsVisible;
        OpenLastButton.IsEnabled = _lastOpenedPath != null && File.Exists(_lastOpenedPath);
    }

    private async void OpenLastClicked(object? sender, RoutedEventArgs e)
    {
        if (_lastOpenedPath is { } path) await OpenFileAsync(path);
    }

    private async void ReloadKeysClicked(object? sender, RoutedEventArgs e)
    {
        if (_openedPath is { } path)
            await OpenFileAsync(path);
        else
            RefreshHome();
    }
}
