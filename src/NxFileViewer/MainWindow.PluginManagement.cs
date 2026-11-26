using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using Emignatik.NxFileViewer.Services.BackgroundTask;
using Emignatik.NxFileViewer.Services.Nand;
using Emignatik.NxFileViewer.Services.Nsz;
using Emignatik.NxFileViewer.Services.PackageOperations;
using Emignatik.NxFileViewer.Services.Updates;
using Emignatik.NxFileViewer.Settings;
using Microsoft.Extensions.Logging.Abstractions;

namespace NxFileViewer.Avalonia;

public partial class MainWindow
{
    private void RefreshManagedPluginRows()
    {
        ManagedPluginRows.Children.Clear();
        _pluginUpdateMessages.Clear();
        var config = Emignatik.NxFileViewer.Services.ExternalSources.Current;

        var settings = new AppSettings
        {
            EnableNand = _preferences.NandActive,
            EnableNandPlugin = _preferences.SharedSettings.EnableNandPlugin,
            EnableNszPlugin = _preferences.NszActive,
            NandExecutablePath = _preferences.NandGuiPath ?? "",
            NszExecutablePath = _preferences.NszCliPath ?? "",
            RustPackageExecutablePath = _preferences.RustBackendPath ?? "",
            RustDesktopExecutablePath = _preferences.RustDesktopPath ?? "",
            KronosPackageDirectory = _preferences.SharedSettings.KronosPackageDirectory
        };
        var pluginRoot = Path.Combine(ViewerDataDirectory.Root, "Plugins");
        var nand = new NandPluginManager(settings, Path.Combine(pluginRoot, "NxNandManager"));
        var engineRoot = Path.Combine(pluginRoot, "PackageEngines");
        var nsz = new NszPluginManager(settings, NullLogger<NszPluginManager>.Instance, Path.Combine(pluginRoot, "NSZ"));
        var versions = new PackageVersionStore(settings, engineRoot);
        var installer = new PackageEngineInstaller(rootDirectory: engineRoot);
        var windows = OperatingSystem.IsWindows();
        var windows64 = windows && Environment.Is64BitOperatingSystem;

        AddPluginRow(CreateManagedPluginSection("NxNandManager", config.NandRepository,
            nand.InstalledVersion ?? ReadInstalledVersion(settings.NandExecutablePath), settings.NandPluginActive,
            nand.UpdateAsync, nand.CanRollback, nand.Rollback, nand.OldVersionDirectories, nand.RemoveOldVersions, SaveNand,
            supportsManagement: windows));
        AddPluginRow(CreateManagedPluginSection("nicoboss/nsz", config.NszRepository,
            nsz.InstalledVersion ?? ReadInstalledVersion(settings.NszExecutablePath), settings.NszPluginActive,
            (progress, token) => nsz.UpdateAsync(progress, token), nsz.CanRollback, nsz.Rollback,
            nsz.OldVersionDirectories, nsz.RemoveOldVersions, SaveNsz, nsz.RootDirectory,
            supportsManagement: windows || NszPluginManager.SupportsLinuxPackages));
        var rust = CreatePackageSection("nscb_rust", config.RustPackageRepository, PackageComponent.Rust,
            (progress, token) => installer.InstallAsync(PackageEngine.Rust, progress, token));
        var desktop = CreatePackageSection("NSCB Desktop", config.RustDesktopRepository, PackageComponent.Desktop, installer.InstallDesktopAsync);
        var desktopColumn = new Border
        {
            Child = desktop,
            BorderThickness = new Thickness(1, 0, 0, 0),
            Padding = new Thickness(12, 0, 0, 0)
        };
        desktopColumn.Classes.Add("update-plugin-row");
        var rustRow = new Grid { ColumnDefinitions = new ColumnDefinitions("2*,3*"), ColumnSpacing = 12 };
        Grid.SetColumn(desktopColumn, 1);
        rustRow.Children.Add(rust);
        rustRow.Children.Add(desktopColumn);
        AddPluginRow(rustRow);
        AddPluginRow(CreatePackageSection("Kronos / NSC_BUILDER", config.KronosPackageRepository, PackageComponent.Kronos,
            (progress, token) => installer.InstallAsync(PackageEngine.Kronos, progress, token)));

        void SaveNand()
        {
            if (string.IsNullOrWhiteSpace(NandGuiPath.Text) || NandGuiPath.Text == _preferences.NandGuiPath)
                NandGuiPath.Text = settings.NandExecutablePath;
            _editedSharedSettings.NandExecutablePath = settings.NandExecutablePath;
            _preferences = _preferences with { NandGuiPath = settings.NandExecutablePath };
        }

        void SaveNsz()
        {
            NszCliPath.Text = settings.NszExecutablePath;
            NszGuiPath.Text = nsz.GuiExecutablePath;
            _editedSharedSettings.NszExecutablePath = settings.NszExecutablePath;
            _preferences = _preferences with { NszCliPath = settings.NszExecutablePath, NszGuiPath = nsz.GuiExecutablePath };
        }

        StackPanel CreatePackageSection(string name, string repository, PackageComponent component,
            Func<IProgressReporter, CancellationToken, Task<string>> install)
        {
            var path = versions.Selected(component);
            var version = PackageVersionStore.VersionLabel(component, path);
            if (string.IsNullOrEmpty(version))
                version = ReadInstalledVersion(component == PackageComponent.Kronos
                    ? Path.Combine(path, "runtime", "python.exe") : path);
            var supportsManagement = windows64 || (component != PackageComponent.Kronos && PackageEngineInstaller.SupportsLinuxPackages);
            return CreateManagedPluginSection(name, repository, version, supportsManagement,
                async (progress, token) =>
                {
                    versions.Synchronize();
                    versions.Select(component, await install(progress, token));
                    versions.Synchronize();
                },
                versions.CanRollback(component), () => versions.Rollback(component),
                () => versions.OldVersions(component), paths => versions.Remove(component, paths), () =>
                {
                    var selected = versions.Selected(component);
                    switch (component)
                    {
                        case PackageComponent.Rust:
                            RustBackendPath.Text = _editedSharedSettings.RustPackageExecutablePath = selected;
                            _preferences = _preferences with { RustBackendPath = selected };
                            break;
                        case PackageComponent.Desktop:
                            RustDesktopPath.Text = _editedSharedSettings.RustDesktopExecutablePath = selected;
                            _preferences = _preferences with { RustDesktopPath = selected };
                            break;
                        case PackageComponent.Kronos:
                            _preferences.SharedSettings.KronosPackageDirectory = _editedSharedSettings.KronosPackageDirectory = selected;
                            KronosGuiPath.Text = Emignatik.NxFileViewer.Services.PluginGuiLauncher.KronosLauncherPath(selected);
                            _preferences = _preferences with { KronosGuiPath = KronosGuiPath.Text };
                            break;
                    }
                }, supportsManagement: supportsManagement, sessionsRoot: component == PackageComponent.Kronos
                    ? Path.Combine(Emignatik.NxFileViewer.Utils.PathHelper.CurrentAppDir, "Plugins", "KronosGuiSessions") : null);
        }
    }

    private void AddPluginRow(Control content)
    {
        var row = new Border
        {
            Child = content,
            Padding = new Thickness(12, 8),
            BorderThickness = new Thickness(0, ManagedPluginRows.Children.Count == 0 ? 0 : 1, 0, 0)
        };
        row.Classes.Add("update-plugin-row");
        ManagedPluginRows.Children.Add(row);
    }

    internal StackPanel CreateManagedPluginSection(string name, string repository, string installed, bool canInstall,
        Func<IProgressReporter, CancellationToken, Task> install, bool canRollback, Action rollback,
        Func<IReadOnlyList<string>> oldVersions, Action<IReadOnlyList<string>> remove, Action apply, string? nszRoot = null,
        bool supportsManagement = true, string? sessionsRoot = null)
    {
        var actions = new WrapPanel();
        var installButton = new Button { Content = _text.Nsz_Update, IsEnabled = canInstall, Margin = new Thickness(0, 0, 8, 0) };
        var rollbackButton = new Button { Content = _text.Nsz_Rollback, IsEnabled = canRollback, Margin = new Thickness(0, 0, 8, 0) };
        var cleanupButton = new Button { Content = _text.Update_Cleanup, Margin = new Thickness(0, 0, 8, 0) };
        void Apply()
        {
            apply();
            _pluginUpdateStatuses.Remove(name);
            _availablePluginUpdates.Remove(name);
            RefreshHomeUpdateNotice();
        }
        installButton.Click += async (_, _) => await RunManagedPluginJob(install, Apply);
        rollbackButton.Click += async (_, _) => await RunManagedPluginJob((_, _) => { rollback(); return Task.CompletedTask; }, Apply);
        cleanupButton.Click += async (_, _) => await RunManagedPluginJob(async (_, _) =>
        {
            var paths = oldVersions();
            if (paths.Count > 0 && await ConfirmPluginCleanupAsync(_text.Update_Cleanup, string.Format(_text.Update_CleanupConfirm, string.Join(Environment.NewLine, paths))))
                remove(paths);
        });

        actions.Children.Add(installButton);
        actions.Children.Add(rollbackButton);
        actions.Children.Add(cleanupButton);
        installButton.IsVisible = rollbackButton.IsVisible = cleanupButton.IsVisible = supportsManagement;
        if (nszRoot != null)
        {
            var cacheButton = new Button { Content = _text.Storage_Downloads, IsVisible = supportsManagement, Margin = new Thickness(0, 0, 8, 0) };
            cacheButton.Click += async (_, _) => await RunManagedPluginJob(async (_, _) =>
            {
                var paths = PluginStorageCleanup.Downloads(nszRoot);
                if (paths.Count > 0 && await ConfirmPluginCleanupAsync(_text.Storage_Downloads,
                    string.Format(_text.Storage_DownloadConfirm, string.Join(Environment.NewLine, paths))))
                    PluginStorageCleanup.DeleteDownloads(nszRoot, paths);
            });
            actions.Children.Add(cacheButton);
        }
        if (sessionsRoot != null && supportsManagement)
        {
            var sessions = new Button { Content = _text.Storage_Sessions, Margin = new Thickness(0, 0, 8, 0) };
            sessions.Click += async (_, _) =>
            {
                string? result = null;
                await RunManagedPluginJob(async (_, token) =>
                {
                    var paths = await Task.Run(() => PluginStorageCleanup.Sessions(sessionsRoot), token);
                    if (paths.Count == 0)
                    {
                        result = _text.Storage_SessionEmpty;
                        return;
                    }
                    if (!await ConfirmPluginCleanupAsync(_text.Storage_Sessions,
                        string.Format(_text.Storage_SessionConfirm, string.Join(Environment.NewLine, paths)))) return;
                    token.ThrowIfCancellationRequested();
                    await Task.Run(() => PluginStorageCleanup.DeleteSessions(sessionsRoot, paths), token);
                    result = string.Format(_text.Storage_Done, paths.Count);
                });
                if (!_closed && result != null) UpdateStatus.Text = result;
            };
            actions.Children.Add(sessions);
        }
        actions.Children.Add(CreateReleaseButton(repository));
        var panel = new StackPanel { Spacing = 4 };
        var heading = new TextBlock();
        SetUpdateHeading(heading, name, InstalledUpdateLabel(installed));
        panel.Children.Add(heading);
        var status = new TextBlock { TextWrapping = global::Avalonia.Media.TextWrapping.Wrap };
        if (supportsManagement) _pluginUpdateMessages[name] = status;
        SetUpdateMessage(status, supportsManagement ? _pluginUpdateStatuses.GetValueOrDefault(name) :
            _text.CultureName.StartsWith("de", StringComparison.OrdinalIgnoreCase)
                ? "Für dieses System ist kein automatischer Installer verfügbar."
                : "No automatic installer is available for this system.", _availablePluginUpdates.Contains(name));
        panel.Children.Add(new Border { MinHeight = 16, Child = status });
        panel.Children.Add(actions);
        return panel;
    }

    private async Task<bool> ConfirmPluginCleanupAsync(string title, string message, string? confirmLabel = null, string? cancelLabel = null)
    {
        var dialog = new Window
        {
            Title = title,
            Icon = Icon,
            Width = 520,
            CanResize = false,
            ShowInTaskbar = false,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            RequestedThemeVariant = ActualThemeVariant
        };
        var confirm = new Button { Content = confirmLabel ?? title, MinWidth = 88, HorizontalContentAlignment = HorizontalAlignment.Center };
        var cancel = new Button { Content = cancelLabel ?? _text.CancelAction, IsCancel = true, IsDefault = cancelLabel != null,
            MinWidth = 88, HorizontalContentAlignment = HorizontalAlignment.Center };
        confirm.Click += (_, _) => dialog.Close(true);
        cancel.Click += (_, _) => dialog.Close(false);
        dialog.Content = new StackPanel
        {
            Margin = new Thickness(16), Spacing = 14,
            Children =
            {
                new ScrollViewer { MaxHeight = 280, Content = new TextBlock { Text = message, TextWrapping = global::Avalonia.Media.TextWrapping.Wrap } },
                new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8, Children = { confirm, cancel } }
            }
        };
        WpfTheme.Apply(dialog);
        return await dialog.ShowDialog<bool>(this);
    }

    private async Task RunManagedPluginJob(Func<IProgressReporter, CancellationToken, Task> action, Action? apply = null)
    {
        if (_updateCancellation != null) return;
        using var workspaceJob = LockWorkspace(SettingsTab);
        using var cancellation = new CancellationTokenSource();
        _updateCancellation = cancellation;
        UpdateCheckButton.IsEnabled = ManagedPluginRows.IsEnabled = false;
        UpdateCancelButton.IsEnabled = true;
        try
        {
            await action(new PluginProgress(message => Dispatcher.UIThread.Post(() =>
            {
                if (ReferenceEquals(_updateCancellation, cancellation)) UpdateStatus.Text = message;
            })), cancellation.Token);
            apply?.Invoke();
            if (apply != null) _preferences.Save();
            if (!_closed)
            {
                RefreshHome();
                UpdateStatus.Text = _text.Status_Ready;
            }
        }
        catch (OperationCanceledException) { if (!_closed) UpdateStatus.Text = _text.Update_Cancelled; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or HttpRequestException or InvalidOperationException or ArgumentException or NotSupportedException or System.Text.Json.JsonException or System.ComponentModel.Win32Exception or System.Security.Cryptography.CryptographicException)
        {
            if (!_closed) { UpdateStatus.Text = ex.Message; LogSession("ERROR", ex.Message); }
        }
        finally
        {
            _updateCancellation = null;
            if (!_closed)
            {
                UpdateCheckButton.IsEnabled = ManagedPluginRows.IsEnabled = true;
                UpdateCancelButton.IsEnabled = false;
                RefreshManagedPluginRows();
            }
        }
    }

    private sealed class PluginProgress(Action<string> report) : IProgressReporter
    {
        public void SetMode(bool isIndeterminate) { }
        public void SetText(string text) => report(text);
        public void SetPercentage(double value) { }
    }
}
