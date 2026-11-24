using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Emignatik.NxFileViewer.Services.FileAssociations;
using Emignatik.NxFileViewer.Services.PackageOperations;

namespace NxFileViewer.Avalonia;

public partial class MainWindow
{
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
    internal async Task OpenExplorerRequestAsync(string[] args)
    {
        try
        {
            var request = ExplorerLaunchRequest.Parse(args);
            if (request.Mode is ExplorerLaunchMode.FileOpen or ExplorerLaunchMode.FileVerify)
            {
                await OpenFileAsync(request.Paths[0], fromWindows: true, forceVerify: request.Mode == ExplorerLaunchMode.FileVerify);
                return;
            }

            Workspace.SelectedItem = BatchTab;
            if (request.Mode is ExplorerLaunchMode.BatchOpen or ExplorerLaunchMode.BatchVerify)
            {
                if (request.Paths.Length == 1) BatchInputPath.Text = request.Paths[0];
                await RunBatchAsync(request.Paths, request.Mode == ExplorerLaunchMode.BatchVerify);
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
            StatusText.Text = error.Message;
            LogSession("ERROR", error.Message);
        }
    }
}
