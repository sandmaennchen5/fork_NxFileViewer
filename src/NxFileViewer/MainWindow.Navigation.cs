using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Emignatik.NxFileViewer.Services.FileLocationOpening;

namespace NxFileViewer.Avalonia;

public partial class MainWindow
{
    private string? _lastOpenedPath;
    private string? _openedDisplayPath;

    private string? OpenedLocalSource()
    {
        var source = _openedDisplayPath?.Split(" :: ", 2)[0] ?? _openedPath;
        if (source == null || source.StartsWith("MTP: ", StringComparison.Ordinal)) return null;
        return Directory.Exists(source) || File.Exists(source) ? source : null;
    }

    private void OverviewLocationMenuOpening(object? sender, System.ComponentModel.CancelEventArgs e) =>
        e.Cancel = OpenedLocalSource() == null;

    private async void OpenOverviewLocation(object? sender, RoutedEventArgs e)
    {
        if (OpenedLocalSource() is { } source) await RevealLocalSourceAsync(source);
    }

    private async System.Threading.Tasks.Task RevealLocalSourceAsync(string source)
    {
        if (Directory.Exists(source))
        {
            await OpenLocalDirectoryAsync(source);
            return;
        }
        if (OperatingSystem.IsWindows() && File.Exists(source))
        {
            try
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (OperatingSystem.IsWindows()) WindowsFileLocation.Reveal(source);
                }, DispatcherPriority.Background);
                return;
            }
            catch (System.Runtime.InteropServices.COMException error) { LogSession("WARN", error.Message); }
        }
        if (Path.GetDirectoryName(source) is { } directory && Directory.Exists(directory)) await OpenLocalDirectoryAsync(directory);
    }

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
        RefreshStatusKeyWarning();
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
        OpenLastButton.IsEnabled = _lastOpenedPath != null && IsReopenableSource(_lastOpenedPath);
    }

    private void RememberOpenedPath(string path)
    {
        if (!IsReopenableSource(path)) { RefreshHome(); return; }
        _lastOpenedPath = Path.GetFullPath(path);
        RefreshHome();
    }

    private static bool IsReopenableSource(string path) =>
        File.Exists(path) || Directory.Exists(Path.Combine(path, "registered"));

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
