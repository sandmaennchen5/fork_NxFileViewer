using System;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using Emignatik.NxFileViewer.Localization;
using Emignatik.NxFileViewer.Services.BackgroundTask;
using Emignatik.NxFileViewer.Services.BackgroundTask.RunnableImpl;
using Emignatik.NxFileViewer.Services.KeysManagement;
using Emignatik.NxFileViewer.Services.Prompting;
using Emignatik.NxFileViewer.Settings;
using Emignatik.NxFileViewer.Utils.MVVM;
using Emignatik.NxFileViewer.Utils.MVVM.Commands;
using Emignatik.NxFileViewer.Views.Windows;

namespace Emignatik.NxFileViewer.Services.PackageOperations;

public sealed class PackageActions : NotifyPropertyChangedBase
{
    private readonly ILogger<PackageActions>? _logger;
    private readonly IAppSettings _settings;
    private readonly IMainBackgroundTaskRunnerService _background;
    private readonly PackageOperationService _service;
    private readonly IKeySetProviderService _keys;
    private readonly IPromptService _prompts;

    public PackageActions(IAppSettings settings, IMainBackgroundTaskRunnerService background,
        PackageOperationService service, IKeySetProviderService keys, IPromptService prompts, ILogger<PackageActions>? logger = null)
    {
        _logger = logger;
        _settings = settings; _background = background; _service = service; _keys = keys; _prompts = prompts;
        MergeNspCommand = Create(PackageOperation.Merge, "nsp");
        MergeXciCommand = Create(PackageOperation.Merge, "xci");
        ConvertNspCommand = Create(PackageOperation.Convert, "nsp");
        ConvertXciCommand = Create(PackageOperation.Convert, "xci");
        SplitCommand = Create(PackageOperation.Split, "nsp");
        settings.PropertyChanged += (_, _) => Refresh();
        background.PropertyChanged += (_, _) => Refresh();
    }
    public RelayCommand MergeNspCommand { get; }
    public RelayCommand MergeXciCommand { get; }
    public RelayCommand ConvertNspCommand { get; }
    public RelayCommand ConvertXciCommand { get; }
    public RelayCommand SplitCommand { get; }
    public bool IsEnabled => _settings.PackageOperationsActive;
    public string Status
    {
        get
        {
            if (!IsEnabled) return LocalizationManager.Instance.Current.Keys.Features_PackageOperationsDisabled;
            var config = PackageEngineConfiguration.Capture(_settings);
            var text = LocalizationManager.Instance.Current.Keys;
            return text.Package_Apply + (config.Engine == PackageEngine.Rust ? "cxfcxf / nscb_rust" : "Kronos / NSC_BUILDER") +
                " — " + (config.IsAvailable ? text.Package_Ready : text.Package_Missing);
        }
    }
    private RelayCommand Create(PackageOperation operation, string type) => new(() => Run(operation, type),
        () => IsEnabled && !_background.IsRunning && PackageEngineConfiguration.Capture(_settings).IsAvailable);
    private void Refresh()
    {
        foreach (var command in new[] { MergeNspCommand, MergeXciCommand, ConvertNspCommand, ConvertXciCommand, SplitCommand })
            command.TriggerCanExecuteChanged(true);
        NotifyPropertyChanged(nameof(Status));
        NotifyPropertyChanged(nameof(IsEnabled));
    }
    private async void Run(PackageOperation operation, string type)
    {
        if (!IsEnabled || _background.IsRunning) return;
        var text = LocalizationManager.Instance.Current.Keys;
        try
        {
            var config = PackageEngineConfiguration.Capture(_settings);
            var keyPath = _keys.ActualProdKeysFilePath;
            if (keyPath == null || !File.Exists(keyPath)) throw new FileNotFoundException(text.Nsz_KeysMissing);
            var dialog = new OpenFileDialog
            {
                Title = text.Package_Inputs, Multiselect = operation == PackageOperation.Merge,
                Filter = "NSP / XCI (*.nsp;*.xci)|*.nsp;*.xci"
            };
            if (dialog.ShowDialog() != true) return;
            var destination = _prompts.PromptSelectDir(text.Package_Destination);
            if (destination == null) return;
            string? output = null;
            var runnable = new RunnableRelay((progress, token) =>
                output = _service.Run(config, operation, dialog.FileNames, destination, type, keyPath, progress, token))
                { SupportsCancellation = true, SupportProgress = true };
            _logger?.LogInformation("Package operation {Operation} started with {Engine} for {Count} input files.", operation, config.Engine, dialog.FileNames.Length);
            await _background.RunAsync(runnable);
            _logger?.LogInformation("Package operation {Operation} completed: {Output}", operation, output);
            if (output != null) ThemedDialog.Notice(text.Package_Done + output, text.Package_Title, MessageBoxImage.Information);
        }
        catch (OperationCanceledException) { _logger?.LogInformation("Package operation canceled."); }
        catch (Exception ex) { _logger?.LogError(ex, "Package operation failed."); ThemedDialog.Notice(ex.Message, text.Package_Title, MessageBoxImage.Error); }
        finally { Refresh(); }
    }
}
