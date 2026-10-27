using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using Emignatik.NxFileViewer.Localization;
using Emignatik.NxFileViewer.Services.BackgroundTask;
using Emignatik.NxFileViewer.Services.BackgroundTask.RunnableImpl;
using Emignatik.NxFileViewer.Utils.MVVM.Commands;
using Emignatik.NxFileViewer.Views.Windows;

namespace Emignatik.NxFileViewer.Services.Updates;

public static class OldVersionCleanupCommand
{
    public static RelayCommand Create(string component, IMainBackgroundTaskRunnerService runner, Func<bool> enabled,
        Func<IReadOnlyList<string>> candidates, Action<IReadOnlyList<string>> remove, Action refresh)
    {
        var checking = false;
        RelayCommand? command = null;
        command = new(async () => {
            if (checking || !enabled() || runner.IsRunning) return;
            checking = true;
            command!.TriggerCanExecuteChanged(true);
            try { await Run(component, runner, enabled, candidates, remove, refresh); }
            finally { checking = false; command.TriggerCanExecuteChanged(true); }
        }, () => !checking && enabled() && !runner.IsRunning);
        return command;
    }

    private static async Task Run(string component, IMainBackgroundTaskRunnerService runner, Func<bool> enabled,
        Func<IReadOnlyList<string>> candidates, Action<IReadOnlyList<string>> remove, Action refresh)
    {
        if (!enabled() || runner.IsRunning) return;
        var keys = LocalizationManager.Instance.Current.Keys;
        var title = component + " — " + keys.Update_Cleanup;
        try
        {
            // Directory enumeration must never run during WPF command requery/navigation.
            var paths = await Task.Run(candidates);
            if (!enabled() || runner.IsRunning) return;
            if (paths.Count == 0) { ThemedDialog.Notice(keys.Update_CleanupEmpty, title, MessageBoxImage.Information); return; }
            if (ThemedDialog.Confirm(title, string.Format(keys.Update_CleanupConfirm, string.Join(Environment.NewLine, paths)), false) != MessageBoxResult.Yes) return;
            if (!enabled() || runner.IsRunning) return;
            await runner.RunAsync(new RunnableRelay((progress, token) =>
            {
                token.ThrowIfCancellationRequested();
                progress.SetText(keys.Update_Cleanup);
                remove(paths);
            }));
            ThemedDialog.Notice(string.Format(keys.Update_CleanupDone, paths.Count), title, MessageBoxImage.Information);
        }
        catch (Exception ex) { ThemedDialog.Notice(ex.Message, title, MessageBoxImage.Error); }
        finally { refresh(); }
    }
}