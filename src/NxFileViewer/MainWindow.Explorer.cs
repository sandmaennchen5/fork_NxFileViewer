using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.Interactivity;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Emignatik.NxFileViewer.Services.FileAssociations;
using Emignatik.NxFileViewer.Services.PackageOperations;

namespace NxFileViewer.Avalonia;

public partial class MainWindow
{
    private readonly Queue<ExplorerLaunchRequest> _explorerRequests = new();
    private DispatcherTimer? _explorerRequestTimer;
    private bool _handlingExplorerRequest;
    private bool _explorerReady;

    internal void InitializeExplorerQueue(ExplorerLaunchRequest? initialRequest, string? startupError)
    {
        _explorerRequestTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _explorerRequestTimer.Tick += async (_, _) => await ProcessExplorerQueueAsync();
        if (initialRequest is { Paths.Length: > 0 }) EnqueueExplorerRequest(initialRequest);
        Opened += (_, _) =>
        {
            _explorerReady = true;
            if (startupError != null) ReportExplorerRequestError(startupError);
        };
        Closed += (_, _) =>
        {
            _explorerReady = false;
            _explorerRequestTimer.Stop();
            _explorerRequests.Clear();
        };
    }

    internal void EnqueueExplorerRequest(ExplorerLaunchRequest request)
    {
        if (_closed) return;
        _explorerRequests.Enqueue(request);
        _explorerRequestTimer?.Start();
    }

    private async Task ProcessExplorerQueueAsync()
    {
        if (!_explorerReady || _handlingExplorerRequest || _workspaceJobs.Count > 0 || _applicationUpdatePending ||
            _batchCancellation != null || _nandCancellation != null || _sourceCancellation != null ||
            _fileVerificationCancellation != null || _updateCancellation != null) return;
        if (!_explorerRequests.TryDequeue(out var request))
        {
            _explorerRequestTimer?.Stop();
            return;
        }
        _handlingExplorerRequest = true;
        try
        {
            if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
            Activate();
            await OpenExplorerRequestAsync(request);
        }
        finally { _handlingExplorerRequest = false; }
    }

    internal void ReportExplorerRequestError(string message)
    {
        if (_closed) return;
        StatusText.Text = message;
        LogSession("ERROR", message);
    }

    private void InitializeExplorerRegistration()
    {
        ExplorerRegistrationPanel.IsVisible = OperatingSystem.IsWindows();
        ModernExplorerActions.IsVisible = OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000) &&
            RuntimeInformation.OSArchitecture is Architecture.X64 or Architecture.Arm64;
    }

    private async void ManageExplorer(object? sender, RoutedEventArgs e)
    {
        if (!OperatingSystem.IsWindows() || sender is not global::Avalonia.Controls.Button { Tag: string action }) return;
        using var workspaceJob = LockWorkspace(SettingsTab);
        ExplorerRegistrationActions.IsEnabled = false;
        ExplorerRegistrationStatus.Text = "";
        try
        {
            switch (action)
            {
                case "Associate":
                    ForkFileAssociations.RegisterCurrentExecutable();
                    ExplorerRegistrationStatus.Text = _text.Associations_Done;
                    return;
                case "Unassociate":
                    ForkFileAssociations.Unregister();
                    ExplorerRegistrationStatus.Text = _text.Associations_Removed;
                    return;
                case "Classic":
                    ExplorerIntegration.RegisterClassic();
                    ExplorerRegistration.SynchronizeFeatures(_preferences.NszActive, _preferences.PackageOperationsActive, nup: _preferences.SharedSettings.EnableNup);
                    break;
                case "RemoveClassic":
                    ExplorerIntegration.RemoveClassic();
                    break;
                case "Online" when ModernExplorerActions.IsVisible:
                    var progress = new Progress<ExplorerInstallStage>(stage =>
                    {
                        if (_closed || ExplorerRegistrationActions.IsEnabled) return;
                        ExplorerRegistrationStatus.Text = stage switch
                        {
                            ExplorerInstallStage.Downloading => _text.Update_Downloading,
                            ExplorerInstallStage.Trusting => _text.Explorer_Trusting,
                            ExplorerInstallStage.Installing => _text.Update_Installing,
                            _ => _text.Update_Checking
                        };
                    });
                    await ExplorerIntegration.InstallModernFromRelease(PreReleaseSetting.IsChecked == true, progress);
                    ExplorerRegistration.SynchronizeFeatures(_preferences.NszActive, _preferences.PackageOperationsActive, nup: _preferences.SharedSettings.EnableNup);
                    break;
                case "Local" when ModernExplorerActions.IsVisible:
                    var packages = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                    {
                        Title = _text.Explorer_Local,
                        FileTypeFilter = [new FilePickerFileType("MSIX") { Patterns = ["*.msix"] }]
                    });
                    if (packages.FirstOrDefault()?.TryGetLocalPath() is not { } package) return;
                    await ExplorerIntegration.InstallModern(package);
                    ExplorerRegistration.SynchronizeFeatures(_preferences.NszActive, _preferences.PackageOperationsActive, nup: _preferences.SharedSettings.EnableNup);
                    break;
                case "RemoveModern" when ModernExplorerActions.IsVisible:
                    await ExplorerIntegration.RemoveModern();
                    break;
                default:
                    return;
            }
            ExplorerRegistrationStatus.Text = _text.Explorer_Done;
        }
        catch (OperationCanceledException)
        {
            ExplorerRegistrationStatus.Text = _text.Update_Cancelled;
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or
            InvalidOperationException or ArgumentException or NotSupportedException or System.Net.Http.HttpRequestException or
            System.Text.Json.JsonException or System.ComponentModel.Win32Exception or System.Security.SecurityException or
            System.Security.Cryptography.CryptographicException)
        {
            ExplorerRegistrationStatus.Text = error.Message.Contains("0x800B0109", StringComparison.OrdinalIgnoreCase)
                ? (_text.CultureName.StartsWith("de", StringComparison.OrdinalIgnoreCase)
                    ? "Windows vertraut der Signatur dieser lokalen MSIX nicht (0x800B0109). Verwende das Online-Setup mit Zertifikatsbestätigung oder eine MSIX mit bereits vertrauenswürdiger Signatur."
                    : "Windows does not trust this local MSIX signature (0x800B0109). Use online setup with certificate confirmation, or a package with a trusted signature.")
                : error.Message;
            LogSession("ERROR", error.Message);
        }
        finally
        {
            ExplorerRegistrationActions.IsEnabled = true;
            if (!_closed) RefreshUpdateExplorerSummary();
        }
    }
    private async Task OpenExplorerRequestAsync(ExplorerLaunchRequest request)
    {
        try
        {
            if (request.Paths.Length == 0) return;
            if (request.Paths.Any(path => !File.Exists(path) && !Directory.Exists(path)))
                throw new FileNotFoundException("An Explorer selection no longer exists.");
            var automatic = request.Mode == ExplorerLaunchMode.Automatic;
            var automaticBatch = automatic && (request.Paths.Length > 1 || request.Paths.Any(Directory.Exists));
            if (request.Mode is ExplorerLaunchMode.FileOpen or ExplorerLaunchMode.FileVerify || (automatic && !automaticBatch))
            {
                await OpenFileAsync(request.Paths[0], fromWindows: true, forceVerify: request.Mode == ExplorerLaunchMode.FileVerify);
                return;
            }

            Workspace.SelectedItem = BatchTab;
            if (request.Mode is ExplorerLaunchMode.BatchOpen or ExplorerLaunchMode.BatchVerify || automaticBatch)
            {
                if (request.Paths.Length == 1) BatchInputPath.Text = request.Paths[0];
                await RunBatchAsync(request.Paths, request.Mode == ExplorerLaunchMode.BatchVerify ||
                    (automaticBatch && _preferences.SharedSettings.VerifyFilesOpenedFromWindows));
            }
            else if (request.Mode is ExplorerLaunchMode.Compress or ExplorerLaunchMode.Decompress)
            {
                var action = request.Mode.ToString();
                await RunPackageActionAsync(action, request.Paths, request.Paths[0], action);
            }
            else if (ExplorerLaunchRequest.TryGetPackageOperation(request.Mode, out var operation, out var type))
            {
                var action = operation == PackageOperation.Convert
                    ? (type == "xci" ? "Xci" : "Nsp") : operation.ToString();
                await RunPackageActionAsync(action, request.Paths, request.Paths[0], request.Mode.ToString(), type);
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            ReportExplorerRequestError(error.Message);
        }
    }
}
