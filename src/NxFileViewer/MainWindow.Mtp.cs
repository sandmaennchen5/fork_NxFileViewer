using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Emignatik.NxFileViewer.FileLoading;
using Emignatik.NxFileViewer.Services.BackgroundTask;
using Emignatik.NxFileViewer.Services.Mtp;
namespace NxFileViewer.Avalonia;
public partial class MainWindow
{
    private MtpImportService? _mtpImport;
    private CancellationTokenSource? _mtpCancellation;
    private (string Display, string Local)? _mtpBatchPath;
    private sealed class MtpProgress(IProgress<string> progress) : IProgressReporter
    { public void SetMode(bool value) { } public void SetText(string text) => progress.Report(text); public void SetPercentage(double value) { } }
    private void CancelMtp(object? sender, RoutedEventArgs e) => _mtpCancellation?.Cancel();
    private async void OpenMtp(object? sender, RoutedEventArgs e)
    {
        if (!OperatingSystem.IsWindows() || !_preferences.SharedSettings.EnableMtp || _mtpCancellation != null || _batchCancellation != null) return;
        var batch = ReferenceEquals(sender, BatchMtpButton);
        var selection = await MtpWindow.Choose(this, _text, batch);
        if (selection == null || _closed) return;
        using var workspaceJob = LockWorkspace(batch ? BatchTab : FileTab);
        using var cancellation = new CancellationTokenSource(); _mtpCancellation = cancellation; MtpCancelButton.IsVisible = !batch; SetBatchBusy(batch); FileMtpButton.IsEnabled = BatchMtpButton.IsEnabled = false;
        var progress = new MtpProgress(new Progress<string>(message =>
        {
            if (_closed || !ReferenceEquals(_mtpCancellation, cancellation)) return;
            StatusText.Text = message;
            LogSession("DEBUG", message, "MTP");
        }));
        _mtpImport ??= new MtpImportService(new ShellMtpBackend(), Path.Combine(Path.GetTempPath(), "NxFileViewer-MTP"));
        try
        {
            LogSession("INFO", selection.Direct ? _text.Mtp_Title + " · Direct" : _text.Mtp_Title + " · Import", "MTP");
            var areas = batch && selection.SwitchMode ? SdBatchAreas.From(_preferences.SharedSettings) with { SourceMode = _batchSourceMode } : null;
            var path = await Task.Run(() => selection.Direct
                ? _mtpImport.PrepareDirect(selection.Entry, progress, cancellation.Token, areas, selection.BufferInMemory, selection.SwitchMode)
                : _mtpImport.Import(selection.Entry, progress, cancellation.Token,
                    string.IsNullOrWhiteSpace(selection.CacheDirectory) ? null : selection.CacheDirectory, areas, selection.SwitchMode).Run(), cancellation.Token);
            if (_closed) return;
            LogSession("INFO", selection.DisplayPath, "MTP");
            workspaceJob.Dispose();
            if (batch)
            {
                _mtpBatchPath = (selection.DisplayPath, path);
                _batchFolder = path;
                if (Directory.Exists(path)) BatchRecursive.IsChecked = true;
                BatchInputPath.Text = selection.DisplayPath;
                StatusText.Text = selection.DisplayPath;
            }
            else
            {
                if (Directory.Exists(path) && !MtpLocations.IsContents(selection.Entry))
                    path = Directory.EnumerateFiles(path).OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
                        .FirstOrDefault() ?? throw new InvalidDataException(_text.Mtp_NoFiles);
                await OpenFileAsync(path, displayPath: selection.DisplayPath);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { if (!_closed) StatusText.Text = _text.Nand_Cancelled; }
        catch (Exception ex) { if (!_closed) { StatusText.Text = ex.Message; LogSession("ERROR", ex.Message, "MTP"); } }
        finally { _mtpCancellation = null; if (!_closed) { MtpCancelButton.IsVisible = false; SetBatchBusy(false); FileMtpButton.IsEnabled = BatchMtpButton.IsEnabled = true; } else _mtpImport.Cleanup(); }
    }
}
