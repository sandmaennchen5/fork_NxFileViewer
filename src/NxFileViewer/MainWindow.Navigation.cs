using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace NxFileViewer.Avalonia;

public partial class MainWindow
{
    private string? _lastOpenedPath;
    private string? _openedDisplayPath;

    private void NavigateWorkspace(object? sender, RoutedEventArgs e)
    {
        if (_workspaceJobs.Count > 0) return;
        if ((sender as Button)?.Tag is not string name || this.FindControl<TabItem>(name) is not { } tab) return;
        if (name is "PluginsTab" or "UpdatesTab" or "NamingSettingsTab")
        {
            Workspace.SelectedItem = SettingsTab;
            SettingsPages.SelectedItem = tab;
        }
        else
        {
            Workspace.SelectedItem = tab;
            if (tab == SettingsTab) SettingsPages.SelectedItem = ProgramSettingsTab;
        }
    }

    private void RefreshHome()
    {
        var missingKeys = string.IsNullOrWhiteSpace(_keysPath) || !File.Exists(_keysPath);
        HomeKeysWarning.IsVisible = FileKeysWarning.IsVisible = missingKeys;
        HomeNszLaunch.IsVisible = _preferences.NszDirectStart;
        HomeNandLaunch.IsVisible = _preferences.NandDirectStart;
        HomeRustLaunch.IsVisible = _preferences.RustDirectStart;
        HomeNandLaunch.IsEnabled = File.Exists(_preferences.NandGuiPath);
        HomeNszLaunch.IsEnabled = File.Exists(NszGuiPath.Text);
        HomeRustLaunch.IsEnabled = File.Exists(RustDesktopPath.Text) && File.Exists(RustBackendPath.Text);
        HomeKronosLaunch.IsVisible = _preferences.KronosDirectStart && File.Exists(KronosGuiPath.Text);
        HomePluginPanel.IsVisible = HomeNandLaunch.IsVisible || HomeNszLaunch.IsVisible ||
            HomeRustLaunch.IsVisible || HomeKronosLaunch.IsVisible;
        FileCloseButton.IsEnabled = _openedPath != null;
        OpenLastButton.IsEnabled = _lastOpenedPath != null && File.Exists(_lastOpenedPath);
    }

    private void RememberOpenedPath(string path)
    {
        if (!File.Exists(path)) { RefreshHome(); return; }
        _lastOpenedPath = Path.GetFullPath(path);
        RefreshHome();
    }

    private async System.Threading.Tasks.Task<global::Avalonia.Platform.Storage.IStorageFolder?> GetStartFolderAsync(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        var folder = Directory.Exists(path) ? path : Path.GetDirectoryName(path);
        return Directory.Exists(folder)
            ? await StorageProvider.TryGetFolderFromPathAsync(new Uri(Path.GetFullPath(folder)))
            : null;
    }

    private async void OpenLastClicked(object? sender, RoutedEventArgs e)
    {
        if (_lastOpenedPath is { } path) await OpenFileAsync(path);
    }

    private async void ReloadKeysClicked(object? sender, RoutedEventArgs e)
    {
        RefreshActiveKeyPaths();
        if (_openedPath is { } path)
            await OpenFileAsync(path, displayPath: _openedDisplayPath);
        else
            RefreshHome();
    }
}
