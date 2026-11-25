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
    private CancellationTokenSource? _fileLoadCancellation;

    private CancellationTokenSource? FileProgressCancellation =>
        _extractionCancellation ?? _sourceCancellation ?? _fileLoadCancellation ?? _fileVerificationCancellation ?? _fileFirmwareCancellation;

    private void FileProgressMenuOpening(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = FileProgressCancellation is not { IsCancellationRequested: false };
    }

    private void CancelFileProgress(object? sender, RoutedEventArgs e) => FileProgressCancellation?.Cancel();

    private void ResetFileVerification()
    {
        _fileVerificationCancellation?.Cancel();
        VerifyFileButton.IsEnabled = false;
    }

    private async void VerifyOpenedFile(object? sender, RoutedEventArgs e)
    {
        if (_fileLoadCancellation != null || _fileVerificationCancellation != null || _batchCancellation != null ||
            _openedPath is not { } path || _overviewPackage is null or { Type: "NRO" }) return;
        using var workspaceJob = LockWorkspace(FileTab);
        using var cancellation = new CancellationTokenSource();
        _fileVerificationCancellation = cancellation;
        _contentVerificationError = null;
        var version = _openVersion;
        var keys = _keysPath;
        VerifyFileButton.IsEnabled = false;
        VerifyFileCancelButton.IsEnabled = VerifyFileCancelButton.IsVisible = true;
        var fileName = Path.GetFileName(path);
        var runningStatus = $"{_text.NcasIntegrity_InProgress} {fileName}";
        StatusText.Text = runningStatus;
        FileStatusProgress.IsIndeterminate = true;
        FileStatusProgress.IsVisible = true;
        void ReportProgress(double fraction, string message)
        {
            if (_closed || version != _openVersion || !ReferenceEquals(_fileVerificationCancellation, cancellation)) return;
            FileStatusProgress.IsIndeterminate = false;
            FileStatusProgress.Value = fraction * 100;
            StatusText.Text = $"{message} · {fraction:P0}";
        }
        IProgress<double> progress = new Progress<double>(fraction => ReportProgress(fraction, runningStatus));
        IProgress<(string Name, int Index, int Total, bool Completed)> hashProgress =
            new Progress<(string Name, int Index, int Total, bool Completed)>(step =>
                ReportProgress((double)(step.Index - (step.Completed ? 0 : 1)) / step.Total,
                    $"{string.Format(_text.NcaHash_ProgressText, step.Index, step.Total)} {step.Name}"));
        try
        {
            if (Emignatik.NxFileViewer.FileLoading.NupReader.IsNup(path))
            {
                if (!_preferences.SharedSettings.EnableNup) throw new InvalidDataException(_text.Features_NupDisabled);
                await Task.Run(() => Emignatik.NxFileViewer.FileLoading.NupReader.Verify(path, cancellation.Token, ((IProgress<double>)progress).Report));
                if (!_closed && version == _openVersion)
                {
                    OverviewIntegrity.Text = _text.Signature_Passed;
                    ApplyIntegrityColor(OverviewIntegrity);
                    StatusText.Text = $"{fileName} · {OverviewIntegrity.Text}";
                    LogFileSection(_openedDisplayPath ?? path, [("INFO", OverviewIntegrity.Text)]);
                }
                return;
            }
            var results = await Task.Run(() =>
            {
                var checks = new List<NcaHashResult>();
                PackageFile.Read(path, keys, checks, cancellation.Token,
                    hashProgress: (name, index, total, completed) => hashProgress.Report((name, index, total, completed)));
                return checks;
            });
            if (_closed || version != _openVersion) return;
            LogFileSection(_openedDisplayPath ?? path, results.Count == 0
                ? [("INFO", _text.NcasIntegrity_NoNca)]
                : results.Select(DescribeCheckLog));
            _contentChecks = results;
            ShowEntryProperties();
            OverviewSignature.Text = results.Any(result => result.Signature is LibHac.Common.Validity.Invalid)
                ? _text.Signature_NotPassed : results.Count > 0 && results.All(result => result.Signature == LibHac.Common.Validity.Valid)
                    ? _text.Signature_Passed : _text.Signature_Unchecked;
            var present = results.Where(result => !result.Missing).ToArray();
            var missing = present.Length != results.Count;
            var allValid = present.Length > 0 && present.All(result => result.Matches == true && result.Signature == LibHac.Common.Validity.Valid);
            OverviewIntegrity.Text = results.Any(result => result.Matches == false) ? _text.NcasIntegrity_Corrupted :
                missing && allValid ? _text.NcasIntegrity_Incomplete :
                results.Any(result => result.Error != null) ? _text.NcasIntegrity_Error :
                allValid ? _text.NcasIntegrity_Original :
                !missing && present.Length > 0 && present.All(result => result.Matches == true) &&
                    present.Any(result => result.Signature == LibHac.Common.Validity.Invalid) ? _text.NcasIntegrity_Modified :
                results.Count == 0 ? _text.NcasIntegrity_NoNca : _text.NcasIntegrity_Unknown;
            ApplyIntegrityColor(OverviewIntegrity);
            LogSession(OverviewIntegrity.Text == _text.NcasIntegrity_Error || OverviewIntegrity.Text == _text.NcasIntegrity_Corrupted
                ? "ERROR" : OverviewIntegrity.Text == _text.NcasIntegrity_Modified || OverviewIntegrity.Text == _text.NcasIntegrity_Incomplete
                    ? "WARN" : "INFO", $"{fileName} · {OverviewIntegrity.Text}");
            StatusText.Text = $"{fileName} · {OverviewIntegrity.Text}";
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            if (!_closed && version == _openVersion)
                StatusText.Text = _text.Log_NcasIntegrityCanceled;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or
            LibHac.Common.LibHacException or LibHac.NSZ.NczFormatException or ZstdSharp.ZstdException)
        {
            if (!_closed && version == _openVersion)
            {
                StatusText.Text = ex.Message;
                OverviewIntegrity.Text = _text.NcasIntegrity_Error;
                ApplyIntegrityColor(OverviewIntegrity);
                _contentVerificationError = ex.Message;
                ShowEntryProperties();
                LogFileSection(_openedDisplayPath ?? path, [("ERROR", ex.Message)]);
            }
        }
        finally
        {
            _fileVerificationCancellation = null;
            if (!_closed)
            {
                if (version == _openVersion) FileStatusProgress.IsVisible = false;
                VerifyFileButton.IsEnabled = _openedPath != null && _overviewPackage is { Type: not "NRO" };
                VerifyFileCancelButton.IsEnabled = VerifyFileCancelButton.IsVisible = false;
            }
        }
    }

    private void CancelFileVerification(object? sender, RoutedEventArgs e)
    {
        _fileLoadCancellation?.Cancel();
        _fileVerificationCancellation?.Cancel();
        _sourceCancellation?.Cancel();
        _extractionCancellation?.Cancel();
    }
}
