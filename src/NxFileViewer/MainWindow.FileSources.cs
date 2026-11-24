using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Emignatik.NxFileViewer.FileLoading;
using Emignatik.NxFileViewer.Services.Nsz;

namespace NxFileViewer.Avalonia;

public partial class MainWindow
{
    private readonly Dictionary<(string Archive, string Member, long Size, long Modified), ExtractedPackage> _openArchiveMembers = new();
    private CancellationTokenSource? _sourceCancellation;

    private void DisposeOpenedArchives()
    {
        _sourceCancellation?.Cancel();
        foreach (var extraction in _openArchiveMembers.Values)
        {
            try { extraction.Dispose(); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        _openArchiveMembers.Clear();
    }

    private async Task OpenArchiveAsync(string path, bool fromWindows, bool forceVerify)
    {
        if (_sourceCancellation != null || _batchCancellation != null || _mtpCancellation != null) return;
        using var job = LockWorkspace(FileTab);
        using var cancellation = new CancellationTokenSource();
        _sourceCancellation = cancellation;
        VerifyFileCancelButton.IsEnabled = true;
        FileStatusProgress.IsVisible = FileStatusProgress.IsIndeterminate = true;
        StatusText.Text = string.Format(_text.Archive_Reading, Path.GetFileName(path));
        string? selectedPath = null;
        string? displayPath = null;
        try
        {
            var token = cancellation.Token;
            var backupAndMembers = await Task.Run(() =>
            {
                using var operation = PackageZip.BeginOperation();
                var backup = SaveBackupDetection.IsBackup(path, token);
                var members = backup ? PackageZip.GetFileEntries(path)
                    : PackageZip.GetEntries(path, token: token, includeFirmware: false, includeNand: _preferences.NandActive);
                return (backup, members);
            }, token);
            if (_closed) return;
            if (backupAndMembers.backup)
            {
                ShowSaveBackup(path, backupAndMembers.members);
                return;
            }
            if (_preferences.FirmwareActive && backupAndMembers.members.Count > 0 &&
                backupAndMembers.members.All(name => name.EndsWith(".nca", StringComparison.OrdinalIgnoreCase)) &&
                await Task.Run(() => IsBatchFirmwareArchive(path), token))
            {
                job.Dispose();
                Workspace.SelectedItem = BatchTab;
                BatchInputPath.Text = path;
                await RunBatchAsync([path], forceVerify);
                if (!_closed && _batchRows.Count > 0) RememberOpenedPath(path);
                return;
            }
            if (backupAndMembers.members.Count == 0)
                throw new InvalidDataException(string.Format(_text.FileNotSupported_Log, path));
            var member = backupAndMembers.members.Count == 1 ? backupAndMembers.members[0]
                : await ChooseArchiveMemberAsync(path, backupAndMembers.members);
            if (member == null || _closed) return;
            var info = new FileInfo(path);
            var key = (info.FullName, member, info.Length, info.LastWriteTimeUtc.Ticks);
            if (!_openArchiveMembers.TryGetValue(key, out var extracted))
            {
                var progress = new Progress<string>(message => { if (!_closed) StatusText.Text = message; });
                extracted = await Task.Run(() =>
                {
                    using var reporting = PackageZip.ReportProgress(new MtpProgress(progress));
                    return PackageZip.Extract(path, member, token, Path.Combine(ViewerDataDirectory.Root, "Temp", "ZIP"));
                }, token);
                if (_closed) { extracted.Dispose(); return; }
                _openArchiveMembers.Add(key, extracted);
            }
            selectedPath = extracted.FilePath;
            displayPath = path + " :: " + member;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or SharpCompress.Common.SharpCompressException)
        {
            if (!_closed) { StatusText.Text = error.Message; LogFileSection(path, [("ERROR", error.Message)]); }
        }
        finally
        {
            _sourceCancellation = null;
            if (!_closed)
            {
                FileStatusProgress.IsVisible = false;
                VerifyFileCancelButton.IsEnabled = false;
            }
        }
        if (_closed || selectedPath == null) return;
        job.Dispose();
        await OpenFileAsync(selectedPath, fromWindows, forceVerify, displayPath);
        if (!_closed && _openedPath == selectedPath) RememberOpenedPath(path);
    }

    private async Task<string?> ChooseArchiveMemberAsync(string path, IReadOnlyList<string> members)
    {
        var list = new ListBox { ItemsSource = members, SelectedIndex = 0, MinHeight = 160 };
        var open = new Button { Content = _text.MenuItem_Open, IsDefault = true };
        var cancel = new Button { Content = _text.CancelAction, IsCancel = true };
        var buttons = new StackPanel { Orientation = global::Avalonia.Layout.Orientation.Horizontal, Spacing = 8, Children = { open, cancel } };
        var panel = new DockPanel { Margin = new global::Avalonia.Thickness(12), Children = { buttons, list } };
        DockPanel.SetDock(buttons, Dock.Bottom);
        var dialog = new Window { Title = Path.GetFileName(path), Width = 650, Height = 340, Content = panel, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        open.Click += (_, _) => dialog.Close(list.SelectedItem as string);
        list.DoubleTapped += (_, _) => { if (list.SelectedItem is string selected) dialog.Close(selected); };
        cancel.Click += (_, _) => dialog.Close(null);
        WpfTheme.Apply(dialog);
        return await dialog.ShowDialog<string?>(this);
    }

    private void ShowSaveBackup(string path, IReadOnlyList<string> members)
    {
        CloseClicked(this, new global::Avalonia.Interactivity.RoutedEventArgs());
        _openedPath = path;
        RememberOpenedPath(path);
        FileEmpty.IsVisible = false;
        FileBody.IsVisible = true;
        FileNameText.Text = Path.GetFileName(path);
        OverviewType.Text = _text.File_SaveBackup;
        OverviewSize.Text = Emignatik.NxFileViewer.Utils.FileSizeHelper.ToFileSize(new FileInfo(path).Length);
        MetadataStatus.Text = string.Join(Environment.NewLine, members);
        MetadataStatus.IsVisible = true;
        Workspace.SelectedItem = FileTab;
        FileBody.SelectedIndex = 0;
        FileCloseButton.IsEnabled = true;
        StatusText.Text = _text.File_SaveBackup;
        LogFileSection(path, [("INFO", _text.File_SaveBackup)]);
    }

    private enum XciRepairChoice { Cancel, OpenOriginal, Repair }

    private async Task<XciRepairChoice> ChooseXciRepairAsync(string message)
    {
        var repair = new Button { Content = _text.XciRepair_SaveCopy, IsDefault = true };
        var original = new Button { Content = _text.MenuItem_Open };
        var cancel = new Button { Content = _text.CancelAction, IsCancel = true };
        var label = new TextBlock { Text = message, TextWrapping = global::Avalonia.Media.TextWrapping.Wrap };
        var actions = new StackPanel
        {
            Orientation = global::Avalonia.Layout.Orientation.Horizontal, Spacing = 8, Children = { repair, original, cancel }
        };
        var panel = new StackPanel { Margin = new global::Avalonia.Thickness(16), Spacing = 12, Children = { label, actions } };
        var dialog = new Window
        {
            Title = _text.XciRepair_Title, Width = 590, SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = panel
        };
        repair.Click += (_, _) => dialog.Close(XciRepairChoice.Repair);
        original.Click += (_, _) => dialog.Close(XciRepairChoice.OpenOriginal);
        cancel.Click += (_, _) => dialog.Close(XciRepairChoice.Cancel);
        WpfTheme.Apply(dialog);
        return await dialog.ShowDialog<XciRepairChoice>(this);
    }

    private async Task<string?> PrepareXciAsync(string path)
    {
        var keys = _keysPath;
        if (!_preferences.NszActive || !File.Exists(keys) || !XciRepairService.Supports(path)) return path;
        if (_sourceCancellation != null) return null;
        using var cancellation = new CancellationTokenSource();
        _sourceCancellation = cancellation;
        VerifyFileCancelButton.IsEnabled = true;
        try
        {
            var progress = new MtpProgress(new Progress<string>(message => { if (!_closed && !cancellation.IsCancellationRequested) StatusText.Text = message; }));
            var repair = new XciRepairService((staged, verification, token) =>
            {
                verification.SetMode(true);
                verification.SetText(_text.XciRepair_Verifying);
                VerifyPackageJobFile(staged, keys!, token);
            });
            var candidate = await Task.Run(() => repair.Inspect(path, progress, cancellation.Token), cancellation.Token);
            if (_closed || cancellation.IsCancellationRequested) return null;
            if (candidate == null) return path;

            var issues = new List<string>();
            if (candidate.HeaderRepair) issues.Add(_text.XciRepair_HeaderIssue);
            if (candidate.LayoutChanges > 0) issues.Add(_text.XciRepair_LayoutIssue);
            var decision = await ChooseXciRepairAsync(
                string.Format(_text.XciRepair_Prompt, Path.GetFileName(path), string.Join(Environment.NewLine, issues)));
            if (_closed || cancellation.IsCancellationRequested || decision == XciRepairChoice.Cancel) return null;
            if (decision == XciRepairChoice.OpenOriginal) return path;
            var target = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = _text.XciRepair_SaveCopy, SuggestedFileName = Path.GetFileNameWithoutExtension(path) + "-repaired.xci",
                SuggestedStartLocation = await GetStartFolderAsync(path), DefaultExtension = "xci",
                FileTypeChoices = [new FilePickerFileType("XCI") { Patterns = ["*.xci"] }]
            });
            if (target?.TryGetLocalPath() is not { } destination || _closed) return null;
            return await Task.Run(() => repair.RepairCopy(candidate, destination, progress, cancellation.Token), cancellation.Token);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { return null; }
        finally
        {
            _sourceCancellation = null;
            if (!_closed) VerifyFileCancelButton.IsEnabled = false;
        }
    }
}
