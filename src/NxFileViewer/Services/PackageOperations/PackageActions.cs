using System;
using System.Linq;
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
            return text.Package_Apply + (config.Engine.Primary() == PackageEngine.Rust ? "cxfcxf / nscb_rust" : "Kronos / NSC_BUILDER") +
                " — " + (config.IsAvailable ? text.Package_Ready : text.Package_Missing);
        }
    }
    private bool AnyEngineAvailable()
    {
        var configuration = PackageEngineConfiguration.Capture(_settings);
        return configuration.IsEngineAvailable(PackageEngine.Rust) || configuration.IsEngineAvailable(PackageEngine.Kronos);
    }
    public bool CanRun => IsEnabled && !_background.IsRunning && AnyEngineAvailable();
    public void RunSelection(PackageOperation operation, string type, string[] sources) => _ = Run(operation, type, sources);
    public System.Threading.Tasks.Task RunSelectionAsync(PackageOperation operation, string type, string[] sources) => Run(operation, type, sources);
    public System.Threading.Tasks.Task RunJobsAsync(PackageJob[] jobs, string? destination = null, OperationOutputSelection? outputSelection = null) => jobs.Length == 0
        ? System.Threading.Tasks.Task.CompletedTask
        : Run(jobs.All(j => j.Operation == PackageOperation.Split) ? PackageOperation.Split : PackageOperation.Merge,
            jobs[0].Type, jobs.SelectMany(j => j.Sources).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(), jobs, destination, outputSelection);
    private RelayCommand Create(PackageOperation operation, string type) => new(() => _ = Run(operation, type),
        () => CanRun);
    private void Refresh()
    {
        foreach (var command in new[] { MergeNspCommand, MergeXciCommand, ConvertNspCommand, ConvertXciCommand, SplitCommand })
            command.TriggerCanExecuteChanged(true);
        NotifyPropertyChanged(nameof(Status));
        NotifyPropertyChanged(nameof(IsEnabled));
    }
    private async System.Threading.Tasks.Task Run(PackageOperation operation, string type, string[]? selectedSources = null, PackageJob[]? plannedJobs = null, string? destinationOverride = null, OperationOutputSelection? outputSelection = null)
    {
        if (!IsEnabled || _background.IsRunning) return;
        var text = LocalizationManager.Instance.Current.Keys;
        try
        {
            var config = PackageEngineConfiguration.Capture(_settings);
            var keyPath = _keys.ActualProdKeysFilePath;
            if (keyPath == null || !File.Exists(keyPath)) throw new FileNotFoundException(text.Nsz_KeysMissing);
            var sources = selectedSources?.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (sources == null)
            {
                var dialog = new OpenFileDialog
                {
                    Title = text.Package_Inputs, Multiselect = operation == PackageOperation.Merge,
                    Filter = "NSP / XCI / NSZ / XCZ (*.nsp;*.xci;*.nsz;*.xcz)|*.nsp;*.xci;*.nsz;*.xcz"
                };
                if (dialog.ShowDialog() != true) return;
                sources = dialog.FileNames;
            }
            if (sources.Length == 0) return;
            var skipped = new System.Collections.Generic.List<string>();
            if (operation == PackageOperation.Split)
            {
                var eligible = new System.Collections.Generic.List<string>();
                await _background.RunAsync(new RunnableRelay((progress, token) =>
                {
                    foreach (var source in sources)
                    {
                        try { _service.EnsureSplitSource(source, progress, token); eligible.Add(source); }
                        catch (PackageSplitNotRequiredException ex) { skipped.Add(ex.Message); }
                    }
                }) { SupportsCancellation = true, SupportProgress = true });
                sources = eligible.ToArray();
                if (sources.Length == 0)
                {
                    ThemedDialog.Notice(string.Join(Environment.NewLine, skipped), text.Package_Title, MessageBoxImage.Information);
                    return;
                }
            }
            if (!_settings.NszPluginActive && sources.Any(source =>
                    Nsz.PackageConversionService.Supports(source, Nsz.NszOperation.Decompress)))
                throw new InvalidOperationException(text.Features_PluginsDisabled);
            outputSelection ??= destinationOverride == null
                ? OperationOutputDialog.Select(_settings, operation.ToString(), sources[0], _prompts, true)
                : new OperationOutputSelection(OutputLocation.Custom, destinationOverride, operation.ToString(), config.Engine);
            if (outputSelection == null) return;
            config = config with { Engine = outputSelection.Engine };
            var destination = outputSelection.Directory;
            var producedFiles = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string? output = null;
            var completedJobs = 0;
            var totalJobs = 0;
            var failedJobs = 0;

            var runnable = new RunnableRelay((progress, token) =>
            {
                var jobs = plannedJobs?.Where(j => j.Sources.All(s => sources.Contains(s, StringComparer.OrdinalIgnoreCase))).ToArray()
                    ?? (operation == PackageOperation.Merge
                        ? new[] { new PackageJob(operation, type, sources) }
                        : sources.Select(source => new PackageJob(operation, type, new[] { source })).ToArray());
                totalJobs = jobs.Length;
                for (var jobIndex = 0; jobIndex < jobs.Length; jobIndex++)
                {
                    token.ThrowIfCancellationRequested();
                    var job = jobs[jobIndex];
                    var jobProgress = new Nsz.NszProgressScope(progress, $"[{jobIndex + 1}/{jobs.Length}]", (double)jobIndex / jobs.Length, 1.0 / jobs.Length);
                    _logger?.LogInformation("Package job {Index}/{Count}: {Operation} -> {Type}; sources={Sources}", jobIndex + 1, jobs.Length, job.Operation, job.Type, string.Join("; ", job.Sources));
                    try
                    {
                        var jobDestination = job.Operation == PackageOperation.Merge ? destination : outputSelection.ForSource(job.Sources[0]);
                        output = _service.Run(config, job.Operation, job.Sources, jobDestination, job.Type, keyPath, jobProgress, token, job.ExpectedGameId,
                            files => { foreach (var path in files) producedFiles.Add(path); });
                        completedJobs++;
                    }
                    catch (PackageSplitNotRequiredException ex)
                    {
                        skipped.Add(ex.Message);
                        _logger?.LogInformation("Split skipped: {Source}", job.Sources[0]);
                    }
                    catch (Exception ex) when (plannedJobs != null && ex is not OperationCanceledException)
                    {
                        failedJobs++;
                        skipped.Add(text.Update_Failed + " " + string.Join(", ", job.Sources.Select(Path.GetFileName)) + ": " + ex.Message);
                        _logger?.LogError(ex, "Batch package job failed: {Operation}", job.Operation);
                    }
                }
            })
                { SupportsCancellation = true, SupportProgress = true };
            _logger?.LogInformation("Package operation {Operation} started with {Engine} for {Count} input files.", operation, config.Engine, sources.Length);
            await _background.RunAsync(runnable);
            _logger?.LogInformation("Package operation {Operation} completed: {Output}", operation, output);
            var message = output == null ? "" : text.Package_Done + (plannedJobs != null || sources.Length > 1 && operation != PackageOperation.Merge ? destination : output) +
                (plannedJobs != null ? $" ({completedJobs}/{totalJobs})" : "");
            if (skipped.Count > 0) message += (message.Length == 0 ? "" : Environment.NewLine + Environment.NewLine) + string.Join(Environment.NewLine, skipped);
            if (message.Length > 0) ThemedDialog.Notice(message, text.Package_Title, failedJobs > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information,
                text.Output_OpenBatch, producedFiles.Count == 0 ? null : () => OperationOutputDialog.OpenInBatch(producedFiles));
        }
        catch (OperationCanceledException) { _logger?.LogInformation("Package operation canceled."); }
        catch (Exception ex) { _logger?.LogError(ex, "Package operation failed."); ThemedDialog.Notice(ex.Message, text.Package_Title, MessageBoxImage.Error); }
        finally { Refresh(); }
    }
}
