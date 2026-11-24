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

public sealed record CleanupMessages(string Title, string Confirm, string Empty, string Done);

public static class OldVersionCleanupCommand
{
    public static RelayCommand Create(string component, IMainBackgroundTaskRunnerService runner, Func<bool> enabled,
        Func<IReadOnlyList<string>> candidates, Action<IReadOnlyList<string>> remove, Action refresh, Func<CleanupMessages>? messages = null)
    {
        var checking = false;
        RelayCommand? command = null;
        command = new(async () => {
            if (checking || !enabled() || runner.IsRunning) return;
            checking = true;
            command!.TriggerCanExecuteChanged(true);
            try { await Run(component, runner, enabled, candidates, remove, refresh, messages); }
            finally { checking = false; command.TriggerCanExecuteChanged(true); }
        }, () => !checking && enabled() && !runner.IsRunning);
        return command;
    }

    private static async Task Run(string component, IMainBackgroundTaskRunnerService runner, Func<bool> enabled,
        Func<IReadOnlyList<string>> candidates, Action<IReadOnlyList<string>> remove, Action refresh, Func<CleanupMessages>? messages = null)
    {
        if (!enabled() || runner.IsRunning) return;
        var keys = LocalizationManager.Instance.Current.Keys;
        var text = messages?.Invoke() ?? new CleanupMessages(component + " — " + keys.Update_Cleanup,
            keys.Update_CleanupConfirm, keys.Update_CleanupEmpty, keys.Update_CleanupDone);
        var title = text.Title;
        try
        {
            // Directory enumeration must never run during WPF command requery/navigation.
            var paths = await Task.Run(candidates);
            if (!enabled() || runner.IsRunning) return;
            if (paths.Count == 0) { ThemedDialog.Notice(text.Empty, title, MessageBoxImage.Information); return; }
            if (ThemedDialog.Confirm(title, string.Format(text.Confirm, string.Join(Environment.NewLine, paths)), false) != MessageBoxResult.Yes) return;
            if (!enabled() || runner.IsRunning) return;
            await runner.RunAsync(new RunnableRelay((progress, token) =>
            {
                token.ThrowIfCancellationRequested();
                progress.SetText(title);
                remove(paths);
            }));
            ThemedDialog.Notice(string.Format(text.Done, paths.Count), title, MessageBoxImage.Information);
        }
        catch (Exception ex) { ThemedDialog.Notice(ex.Message, title, MessageBoxImage.Error); }
        finally { refresh(); }
    }
}