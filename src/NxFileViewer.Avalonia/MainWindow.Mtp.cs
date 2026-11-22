using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Emignatik.NxFileViewer.FileLoading;
using Emignatik.NxFileViewer.Services.BackgroundTask;
using Emignatik.NxFileViewer.Services.Mtp;
using Emignatik.NxFileViewer.Services.Nand;
namespace NxFileViewer.Avalonia;
public partial class MainWindow
{
    private MtpImportService? _mtpImport;
    private CancellationTokenSource? _mtpCancellation;
    private sealed class MtpProgress(IProgress<string> progress) : IProgressReporter
    { public void SetMode(bool value) { } public void SetText(string text) => progress.Report(text); public void SetPercentage(double value) { } }
    private void CancelMtp(object? sender, RoutedEventArgs e) => _mtpCancellation?.Cancel();
    private async void OpenMtp(object? sender, RoutedEventArgs e)
    {
        if (!OperatingSystem.IsWindows() || _mtpCancellation != null || _batchCancellation != null) return;
        var selection = await MtpWindow.Choose(this, _text);
        if (selection == null || _closed) return;
        using var cancellation = new CancellationTokenSource(); _mtpCancellation = cancellation; MtpCancelButton.IsVisible = true; SetBatchBusy(true); FileMtpButton.IsEnabled = BatchMtpButton.IsEnabled = false;
        var progress = new MtpProgress(new Progress<string>(message => { if (!_closed) StatusText.Text = message; }));
        _mtpImport ??= new MtpImportService(new ShellMtpBackend(), Path.Combine(AppContext.BaseDirectory, "Temp", "MTP"));
        try
        {
            var areas = SdBatchAreas.From(_preferences.SharedSettings);
            var path = await Task.Run(() => selection.Direct ? _mtpImport.PrepareDirect(selection.Entry, progress, cancellation.Token, areas) : _mtpImport.Import(selection.Entry, progress, cancellation.Token, selection.CacheDirectory, areas).Run(), cancellation.Token);
            if (_closed) return;
            if (Directory.Exists(path) || Path.GetExtension(path).ToLowerInvariant() is ".zip" or ".7z" || (sender as Button)?.Tag as string == "Batch") { BatchInputPath.Text = path; Workspace.SelectedItem = BatchTab; }
            else if (NandDetection.IsCandidateName(path)) { _nandSource = path; Workspace.SelectedItem = NandTab; ReadNand(sender, e); }
            else await OpenFileAsync(path);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { if (!_closed) StatusText.Text = _text.Nand_Cancelled; }
        catch (Exception ex) { if (!_closed) { StatusText.Text = ex.Message; LogSession("ERROR", ex.Message, "MTP"); } }
        finally { _mtpCancellation = null; if (!_closed) { MtpCancelButton.IsVisible = false; SetBatchBusy(false); FileMtpButton.IsEnabled = BatchMtpButton.IsEnabled = true; } else _mtpImport.Cleanup(); }
    }
}
