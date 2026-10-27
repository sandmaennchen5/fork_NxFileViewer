using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Emignatik.NxFileViewer.Localization;
using Emignatik.NxFileViewer.Services.BackgroundTask;
using Emignatik.NxFileViewer.Services.BackgroundTask.RunnableImpl;
using Emignatik.NxFileViewer.Services.KeysManagement;
using Emignatik.NxFileViewer.Settings;
using Emignatik.NxFileViewer.Utils.MVVM;
using Emignatik.NxFileViewer.Utils.MVVM.Commands;
using Emignatik.NxFileViewer.Views.Windows;
using Microsoft.Extensions.Logging;

namespace Emignatik.NxFileViewer.Services.PackageOperations;

public sealed class RustDesktopActions : NotifyPropertyChangedBase
{
    private readonly IAppSettings _settings;
    private readonly IAppSettingsManager _settingsManager;
    private readonly IMainBackgroundTaskRunnerService _background;
    private readonly IKeySetProviderService _keys;
    private readonly ILogger<RustDesktopActions> _logger;
    private string? _result;
    public RustDesktopActions(IAppSettings settings, IAppSettingsManager settingsManager,
        IMainBackgroundTaskRunnerService background, IKeySetProviderService keys, ILogger<RustDesktopActions> logger)
    {
        _settings = settings; _settingsManager = settingsManager; _background = background; _keys = keys; _logger = logger;
        InstallCommand = new RelayCommand(() => _ = InstallAsync(), () => IsEnabled && !background.IsRunning);
        OpenGuiCommand = new RelayCommand(Open, () => IsGuiEnabled && !background.IsRunning &&
            File.Exists(ExecutablePath) && File.Exists(PackageEnginePaths.ResolveRust(settings.RustPackageExecutablePath)));
        settings.PropertyChanged += (_, _) => Refresh();
        background.PropertyChanged += (_, _) => Refresh();
    }
    public bool IsEnabled => _settings.NszPluginActive || _settings.PackageOperationsActive;
    public bool IsGuiEnabled => IsEnabled && _settings.EnableRustDesktopGui;
    public string ExecutablePath => PackageEnginePaths.ResolveDesktop(_settings.RustDesktopExecutablePath);
    public RelayCommand InstallCommand { get; }
    public RelayCommand OpenGuiCommand { get; }
    public string Status => _result ?? (File.Exists(ExecutablePath)
        ? LocalizationManager.Instance.Current.Keys.Package_Ready + " — " + PackageVersionStore.VersionLabel(PackageComponent.Desktop, ExecutablePath) + Environment.NewLine + ExecutablePath
        : LocalizationManager.Instance.Current.Keys.Component_NotInstalled);
    private void Refresh()
    {
        InstallCommand.TriggerCanExecuteChanged(true);
        OpenGuiCommand.TriggerCanExecuteChanged(true);
        NotifyPropertyChanged(nameof(Status));
        NotifyPropertyChanged(nameof(IsEnabled));
        NotifyPropertyChanged(nameof(IsGuiEnabled));
    }
    private async Task InstallAsync()
    {
        if (!InstallCommand.CanExecute(null)) return;
        _result = null;
        try
        {
            string? desktop = null;
            var backend = PackageEnginePaths.ResolveRust(_settings.RustPackageExecutablePath);
            await _background.RunAsync(new RunnableRelay((progress, token) =>
            {
                var installer = new PackageEngineInstaller();
                if (!File.Exists(backend))
                    backend = installer.InstallAsync(PackageEngine.Rust, progress, token).GetAwaiter().GetResult();
                desktop = installer.InstallDesktopAsync(progress, token).GetAwaiter().GetResult();
            }) { SupportsCancellation = true, SupportProgress = true });
            if (desktop == null) return;
            _settings.RustPackageExecutablePath = backend;
            _settings.RustDesktopExecutablePath = desktop;
            _settingsManager.SaveSafe();
        }
        catch (OperationCanceledException) { _result = LocalizationManager.Instance.Current.Keys.Update_Cancelled; }
        catch (Exception ex)
        {
            _result = LocalizationManager.Instance.Current.Keys.Update_Failed + " " + ex.Message;
            _logger.LogError(ex, "NSCB Desktop installation failed.");
        }
        finally { Refresh(); }
    }
    private void Open()
    {
        try { PluginGuiLauncher.OpenDesktop(ExecutablePath, PackageEnginePaths.ResolveRust(_settings.RustPackageExecutablePath), _keys.ActualProdKeysFilePath); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NSCB Desktop launch failed.");
            ThemedDialog.Notice(ex.Message, "NSCB Desktop", MessageBoxImage.Error);
        }
    }
}