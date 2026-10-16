using System.IO;
using System.Windows;
using Emignatik.NxFileViewer.Localization;
using Emignatik.NxFileViewer.Services.Prompting;
using Emignatik.NxFileViewer.Views.Windows;

namespace Emignatik.NxFileViewer.Services.Nsz;

public sealed record XciRepairDecision(bool CancelOpening, string? Destination = null);

public interface IXciRepairPrompt
{
    XciRepairDecision Ask(XciRepairCandidate candidate);
    void ShowFailure(string reason);
}

public sealed class XciRepairPrompt(IPromptService prompts) : IXciRepairPrompt
{
    public XciRepairDecision Ask(XciRepairCandidate candidate)
    {
        var dispatcher = Application.Current.Dispatcher;
        if (!dispatcher.CheckAccess()) return dispatcher.Invoke(() => Ask(candidate));
        var keys = LocalizationManager.Instance.Current.Keys;
        var issue = candidate.LayoutChanges > 0 ? keys.XciRepair_LayoutIssue : keys.XciRepair_HeaderIssue;
        var answer = ThemedDialog.Confirm(keys.XciRepair_Title,
            string.Format(keys.XciRepair_Prompt, Path.GetFileName(candidate.Path), issue));
        if (answer == MessageBoxResult.No) return new(false);
        if (answer != MessageBoxResult.Yes) return new(true);
        var destination = prompts.PromptSaveFile(Path.GetFileNameWithoutExtension(candidate.Path) + " (repaired).xci",
            keys.XciRepair_SaveCopy, "XCI (*.xci)|*.xci");
        return destination == null ? new(true) : new(false, destination);
    }

    public void ShowFailure(string reason)
    {
        var dispatcher = Application.Current.Dispatcher;
        if (!dispatcher.CheckAccess()) { dispatcher.Invoke(() => ShowFailure(reason)); return; }
        var keys = LocalizationManager.Instance.Current.Keys;
        ThemedDialog.Notice(string.Format(keys.XciRepair_Failed, reason), keys.XciRepair_Title, MessageBoxImage.Error);
    }
}
