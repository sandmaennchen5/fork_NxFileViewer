using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Interactivity;

namespace NxFileViewer.Avalonia;

public partial class MainWindow
{
    private CancellationTokenSource? _fileVerificationCancellation;

    private void ResetFileVerification()
    {
        _fileVerificationCancellation?.Cancel();
        VerifyFileButton.IsEnabled = false;
        FileVerificationResult.IsVisible = false;
        FileVerificationResult.Text = "";
    }

    private async void VerifyOpenedFile(object? sender, RoutedEventArgs e)
    {
        if (_fileVerificationCancellation != null || _batchCancellation != null || _openedPath is not { } path || _overviewPackage is null or { Type: "NRO" }) return;
        using var workspaceJob = LockWorkspace(FileTab);
        using var cancellation = new CancellationTokenSource();
        _fileVerificationCancellation = cancellation;
        var version = _openVersion;
        var keys = _keysPath;
        VerifyFileButton.IsEnabled = false;
        VerifyFileCancelButton.IsEnabled = true;
        FileVerificationResult.IsVisible = true;
        FileVerificationResult.Text = _text.NcasIntegrity_InProgress;
        var fileName = Path.GetFileName(path);
        var runningStatus = $"{_text.NcasIntegrity_InProgress} {fileName}";
        StatusText.Text = runningStatus;
        FileStatusProgress.IsIndeterminate = true;
        FileStatusProgress.IsVisible = true;
        var progress = new Progress<double>(fraction =>
        {
            if (_closed || version != _openVersion || !ReferenceEquals(_fileVerificationCancellation, cancellation)) return;
            FileStatusProgress.IsIndeterminate = false;
            FileStatusProgress.Value = fraction * 100;
            StatusText.Text = $"{runningStatus} · {fraction:P0}";
        });
        try
        {
            if (Emignatik.NxFileViewer.FileLoading.NupReader.IsNup(path))
            {
                if (!_preferences.SharedSettings.EnableNup) throw new InvalidDataException(_text.Features_NupDisabled);
                await Task.Run(() => Emignatik.NxFileViewer.FileLoading.NupReader.Verify(path, cancellation.Token, ((IProgress<double>)progress).Report));
                if (!_closed && version == _openVersion)
                {
                    FileVerificationResult.Text = OverviewIntegrity.Text = _text.Signature_Passed;
                    StatusText.Text = $"{fileName} · {OverviewIntegrity.Text}";
                    LogFileSection(_openedDisplayPath ?? path, [("INFO", OverviewIntegrity.Text)]);
                }
                return;
            }
            var results = await Task.Run(() =>
            {
                var checks = new List<NcaHashResult>();
                PackageFile.Read(path, keys, checks, cancellation.Token,
                    hashProgress: (completed, total) => ((IProgress<double>)progress).Report((double)completed / total));
                return checks;
            });
            if (_closed || version != _openVersion) return;
            FileVerificationResult.Text = results.Count == 0
                ? _text.NcasIntegrity_NoNca
                : string.Join(Environment.NewLine, results.Select(FormatCheck));
            LogFileSection(_openedDisplayPath ?? path, results.Count == 0
                ? [("INFO", _text.NcasIntegrity_NoNca)]
                : results.Select(result =>
                    (result.Error != null || result.Matches == false || result.Signature == LibHac.Common.Validity.Invalid
                        ? "ERROR" : "INFO", FormatCheck(result))));
            if (keys == null && results.Count > 0)
                FileVerificationResult.Text += Environment.NewLine + _text.WarnNoProdKeysFileFound;
            OverviewSignature.Text = results.Any(result => result.Signature is LibHac.Common.Validity.Invalid)
                ? _text.Signature_NotPassed : results.Count > 0 && results.All(result => result.Signature == LibHac.Common.Validity.Valid)
                    ? _text.Signature_Passed : _text.Signature_Unchecked;
            OverviewIntegrity.Text = results.Any(result => result.Matches == false) ? _text.NcasIntegrity_Corrupted :
                results.Any(result => result.Error != null) ? _text.NcasIntegrity_Error :
                results.Count > 0 && results.All(result => result.Matches == true && result.Signature == LibHac.Common.Validity.Valid)
                    ? _text.NcasIntegrity_Original :
                results.Count > 0 && results.All(result => result.Matches == true) && results.Any(result => result.Signature == LibHac.Common.Validity.Invalid)
                    ? _text.NcasIntegrity_Modified : _text.NcasIntegrity_Unknown;
            OverviewIntegrity.Classes.Set("overview-success", OverviewIntegrity.Text == _text.NcasIntegrity_Original);
            StatusText.Text = $"{fileName} · {OverviewIntegrity.Text}";
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            if (!_closed && version == _openVersion)
                StatusText.Text = FileVerificationResult.Text = _text.Log_NcasIntegrityCanceled;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or
            LibHac.Common.LibHacException or LibHac.NSZ.NczFormatException or ZstdSharp.ZstdException)
        {
            if (!_closed && version == _openVersion)
            {
                StatusText.Text = FileVerificationResult.Text = ex.Message;
                LogFileSection(_openedDisplayPath ?? path, [("ERROR", ex.Message)]);
            }
        }
        finally
        {
            _fileVerificationCancellation = null;
            if (!_closed)
            {
                if (version == _openVersion) FileStatusProgress.IsVisible = false;
                VerifyFileButton.IsEnabled = _openedPath != null;
                VerifyFileCancelButton.IsEnabled = false;
            }
        }
    }

    private void CancelFileVerification(object? sender, RoutedEventArgs e)
    {
        _fileVerificationCancellation?.Cancel();
        _sourceCancellation?.Cancel();
    }
}
