using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Emignatik.NxFileViewer.Localization;
using Emignatik.NxFileViewer.Services.BackgroundTask;
using Emignatik.NxFileViewer.Services.KeysManagement;
using Emignatik.NxFileViewer.Settings;
using Emignatik.NxFileViewer.Utils.MVVM;
using Emignatik.NxFileViewer.Utils.MVVM.Commands;
using Emignatik.NxFileViewer.Views.Windows;
using Microsoft.Extensions.Logging;

namespace Emignatik.NxFileViewer.Services.PackageOperations;

public sealed class KronosGuiActions : NotifyPropertyChangedBase
{
    private readonly IAppSettings _settings;
    private bool _launching;
    public KronosGuiActions(IAppSettings settings, IMainBackgroundTaskRunnerService background,
        IKeySetProviderService keys, ILogger<KronosGuiActions> logger)
    {
        _settings = settings;
        OpenGuiCommand = new RelayCommand(async () =>
        {
            if (!OpenGuiCommand!.CanExecute(null)) return;
            _launching = true; Refresh();
            try
            {
                var directory = PackageEnginePaths.ResolveKronos(settings.KronosPackageDirectory);
                var keyPath = keys.ActualProdKeysFilePath;
                await Task.Run(() => PluginGuiLauncher.OpenKronos(directory, keyPath));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Kronos GUI launch failed.");
                ThemedDialog.Notice(ex.Message, "Kronos / NSC_BUILDER", MessageBoxImage.Error);
            }
            finally { _launching = false; Refresh(); }
        }, () => IsGuiEnabled && !_launching && !background.IsRunning &&
            PluginGuiLauncher.KronosLauncherPath(PackageEnginePaths.ResolveKronos(settings.KronosPackageDirectory)) != null);
        settings.PropertyChanged += (_, _) => Refresh();
        background.PropertyChanged += (_, _) => Refresh();
    }
    public bool IsGuiEnabled => _settings.PackageOperationsActive && _settings.EnableKronosGui;
    public RelayCommand OpenGuiCommand { get; }
    private void Refresh()
    {
        OpenGuiCommand.TriggerCanExecuteChanged(true);
        NotifyPropertyChanged(nameof(IsGuiEnabled));
    }
}