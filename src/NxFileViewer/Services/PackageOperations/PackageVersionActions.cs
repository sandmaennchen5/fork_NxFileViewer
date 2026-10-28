using System;
using System.Windows;
using Emignatik.NxFileViewer.Settings;
using Emignatik.NxFileViewer.Services.BackgroundTask;
using Emignatik.NxFileViewer.Services.Updates;
using Emignatik.NxFileViewer.Utils.MVVM.Commands;
using Emignatik.NxFileViewer.Views.Windows;

namespace Emignatik.NxFileViewer.Services.PackageOperations;

public sealed class PackageVersionActions
{
    public RelayCommand RustRollbackCommand { get; }
    public RelayCommand KronosRollbackCommand { get; }
    public RelayCommand DesktopRollbackCommand { get; }
    public RelayCommand RustCleanupCommand { get; }
    public RelayCommand KronosCleanupCommand { get; }
    public RelayCommand DesktopCleanupCommand { get; }
    public PackageVersionActions(IAppSettings settings, IAppSettingsManager manager, IMainBackgroundTaskRunnerService runner)
    {
        var store = new PackageVersionStore(settings);
        bool Enabled() => settings.NszPluginActive || settings.PackageOperationsActive;
        RelayCommand Rollback(PackageComponent component) => new(() => {
            try { store.Rollback(component); manager.SaveSafe(); }
            catch (Exception ex) { ThemedDialog.Notice(ex.Message, component.ToString(), MessageBoxImage.Error); }
            Refresh();
        }, () => Enabled() && !runner.IsRunning && store.CanRollback(component));
        RelayCommand Cleanup(PackageComponent component) => OldVersionCleanupCommand.Create(component.ToString(), runner, Enabled,
            () => store.OldVersions(component), paths => store.Remove(component, paths), Refresh);
        RustRollbackCommand = Rollback(PackageComponent.Rust);
        KronosRollbackCommand = Rollback(PackageComponent.Kronos);
        DesktopRollbackCommand = Rollback(PackageComponent.Desktop);
        RustCleanupCommand = Cleanup(PackageComponent.Rust);
        KronosCleanupCommand = Cleanup(PackageComponent.Kronos);
        DesktopCleanupCommand = Cleanup(PackageComponent.Desktop);
        settings.PropertyChanged += (_, _) => Refresh();
        runner.PropertyChanged += (_, _) => Refresh();
    }
    private void Refresh()
    {
        foreach (var command in new[] { RustRollbackCommand, KronosRollbackCommand, DesktopRollbackCommand, RustCleanupCommand, KronosCleanupCommand, DesktopCleanupCommand })
            command?.TriggerCanExecuteChanged(true);
    }
}