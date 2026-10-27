using System;
using Emignatik.NxFileViewer.Services.KeysManagement;
using Emignatik.NxFileViewer.Localization;
using Emignatik.NxFileViewer.Services.BackgroundTask;
using Emignatik.NxFileViewer.Services.BackgroundTask.RunnableImpl;
using Emignatik.NxFileViewer.Settings;
using Emignatik.NxFileViewer.Utils.MVVM;
using Emignatik.NxFileViewer.Utils.MVVM.Commands;

namespace Emignatik.NxFileViewer.Services.Nand;

public sealed class NandPluginActions : NotifyPropertyChangedBase
{
    private readonly NandPluginManager _manager;
    private readonly IMainBackgroundTaskRunnerService _runner;
    private string _result = "";
    public NandPluginActions(NandPluginManager manager, IMainBackgroundTaskRunnerService runner, IAppSettings settings, IKeySetProviderService keys)
    {
        _manager = manager; _runner = runner;
        OpenGuiCommand = new RelayCommand(() => { try { PluginGuiLauncher.Open(manager.ExecutablePath, true, string.IsNullOrWhiteSpace(settings.NandBisKeysPath) ? keys.ActualProdKeysFilePath : settings.NandBisKeysPath); } catch (Exception ex) { _result = ex.Message; Refresh(); } }, () => settings.NandPluginActive && !runner.IsRunning && CanOpenGui());
        UpdateCommand = new RelayCommand(Update, () => settings.NandPluginActive && !runner.IsRunning && string.IsNullOrWhiteSpace(settings.NandExecutablePath));
        CleanupCommand = Updates.OldVersionCleanupCommand.Create("NxNandManager", runner, () => settings.NandPluginActive, manager.OldVersionDirectories, manager.RemoveOldVersions, Refresh);
        RollbackCommand = new RelayCommand(() =>
        {
            try { manager.Rollback(); _result = ""; } catch (Exception ex) { _result = ex.Message; }
            Refresh();
        }, () => settings.NandPluginActive && !runner.IsRunning && manager.CanRollback);
        runner.PropertyChanged += (_, _) => Refresh();
        settings.PropertyChanged += (_, _) => Refresh();
    }
    public RelayCommand OpenGuiCommand { get; }
    private bool CanOpenGui() { try { return System.IO.File.Exists(_manager.ExecutablePath); } catch { return false; } }
    public RelayCommand UpdateCommand { get; }
    public RelayCommand RollbackCommand { get; }
    public RelayCommand CleanupCommand { get; }
    public string Status => _manager.Status + (string.IsNullOrEmpty(_result) ? "" : Environment.NewLine + _result);
    private void Refresh()
    {
        NotifyPropertyChanged(nameof(Status));
        OpenGuiCommand.TriggerCanExecuteChanged(true);
        UpdateCommand.TriggerCanExecuteChanged(true);
        RollbackCommand.TriggerCanExecuteChanged(true);
        CleanupCommand.TriggerCanExecuteChanged(true);
    }
    private async void Update()
    {
        if (!UpdateCommand.CanExecute(null)) return;
        _result = "";
        try
        {
            await _runner.RunAsync(new RunnableRelay((progress, token) => _manager.UpdateAsync(progress, token).GetAwaiter().GetResult()) { SupportsCancellation = true });
        }
        catch (OperationCanceledException) { _result = LocalizationManager.Instance.Current.Keys.Update_Cancelled; }
        catch (Exception ex) { _result = LocalizationManager.Instance.Current.Keys.Update_Failed + " " + ex.Message; }
        finally { Refresh(); }
    }
}
