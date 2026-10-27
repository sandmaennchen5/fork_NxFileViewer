using System;
using Emignatik.NxFileViewer.Views.Windows;
using Emignatik.NxFileViewer.Services.KeysManagement;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Emignatik.NxFileViewer.Localization;
using Emignatik.NxFileViewer.Services.BackgroundTask;
using Emignatik.NxFileViewer.Services.BackgroundTask.RunnableImpl;
using Emignatik.NxFileViewer.Services.FileOpening;
using Emignatik.NxFileViewer.Services.Prompting;
using Emignatik.NxFileViewer.Settings;
using Emignatik.NxFileViewer.Services.PackageOperations;
using Emignatik.NxFileViewer.Utils.MVVM;
using Emignatik.NxFileViewer.Utils.MVVM.Commands;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Emignatik.NxFileViewer.Styling.Theme;

namespace Emignatik.NxFileViewer.Services.Nsz;

public sealed record ConversionAttempt(string SourcePath, PackageConversionResult? Result, string? Error);

public sealed class NszActions : NotifyPropertyChangedBase
{
    private readonly NszPluginManager _manager;
    private readonly PackageConversionService _converter;
    private readonly IMainBackgroundTaskRunnerService _background;
    private readonly IPromptService _prompts;
    private readonly IFileOpeningService _opening;
    private readonly ILogger<NszActions> _logger;
    private readonly IAppSettings _settings;
    private readonly IAppSettingsManager? _settingsManager;

    public NszActions(NszPluginManager manager, PackageConversionService converter,
        IMainBackgroundTaskRunnerService background, IPromptService prompts, IFileOpeningService opening,
        ILogger<NszActions> logger, IAppSettings settings, IKeySetProviderService keys, IAppSettingsManager? settingsManager = null)
    {
        _manager = manager; _converter = converter; _background = background; _prompts = prompts;
        _opening = opening; _logger = logger; _settings = settings;
        _settingsManager = settingsManager;
        OpenGuiCommand = new RelayCommand(() => { try { PluginGuiLauncher.Open(_manager.GuiExecutablePath, false, keys.ActualProdKeysFilePath); } catch (Exception ex) { ShowError(ex); } }, () => IsGuiEnabled && !_background.IsRunning && File.Exists(_manager.GuiExecutablePath));
        CompressCommand = new RelayCommand(() => ConvertOpened(NszOperation.Compress), () => CanConvertOpened(NszOperation.Compress));
        DecompressCommand = new RelayCommand(() => ConvertOpened(NszOperation.Decompress), () => CanConvertOpened(NszOperation.Decompress));
        UpdateCommand = new RelayCommand(Update, () => _settings.NszPluginActive && !_background.IsRunning && manager.UsesManagedInstallation);
        CleanupCommand = Services.Updates.OldVersionCleanupCommand.Create("NSZ", background, () => settings.NszPluginActive, manager.OldVersionDirectories, manager.RemoveOldVersions, Refresh);
        RollbackCommand = new RelayCommand(Rollback, () => _settings.NszPluginActive && !_background.IsRunning && _manager.CanRollback);
        _background.PropertyChanged += (_, args) => { if (args.PropertyName == nameof(IBackgroundTaskRunner.IsRunning)) Refresh(); };
        _opening.OpenedFileChanged += (_, _) => Refresh();
        settings.PropertyChanged += (_, _) => Refresh();
    }

    public string ExecutablePath => _manager.ExecutablePath;
    public bool IsGuiEnabled => _settings.NszPluginActive && _settings.EnableNszGui;
    public RelayCommand OpenGuiCommand { get; }
    public RelayCommand CompressCommand { get; }
    public RelayCommand DecompressCommand { get; }
    public RelayCommand UpdateCommand { get; }
    public RelayCommand RollbackCommand { get; }
    public RelayCommand CleanupCommand { get; }
    private string EngineName => _settings.NszCompressionEngine.PrimaryRust() ? "nscb_rust" : "nicoboss/nsz";
    public string Status => !_settings.NszCompressionEngine.PrimaryRust() ? _manager.Status :
        EngineName + " — " + (File.Exists(PackageEnginePaths.ResolveRust(_settings.RustPackageExecutablePath))
            ? LocalizationManager.Instance.Current.Keys.Package_Ready : LocalizationManager.Instance.Current.Keys.Package_Missing) +
        " — " + PackageVersionStore.VersionLabel(PackageComponent.Rust, _settings.RustPackageExecutablePath) + Environment.NewLine + _settings.RustPackageExecutablePath;

    private void Refresh()
    {
        CompressCommand.TriggerCanExecuteChanged(true); DecompressCommand.TriggerCanExecuteChanged(true);
        UpdateCommand.TriggerCanExecuteChanged(true); RollbackCommand.TriggerCanExecuteChanged(true);
        OpenGuiCommand.TriggerCanExecuteChanged(true);
        CleanupCommand.TriggerCanExecuteChanged(true);
        NotifyPropertyChanged(nameof(Status));
        NotifyPropertyChanged(nameof(IsGuiEnabled));
    }

    private bool CanConvertOpened(NszOperation operation) => _settings.NszPluginActive && !_background.IsRunning && _opening.OpenedFile != null &&
        PackageConversionService.Supports(_opening.OpenedFile.FilePath, operation);

    private async void ConvertOpened(NszOperation operation)
    {
        if (!CanConvertOpened(operation)) return;
        var source = _opening.OpenedFile!.FilePath;
        var attempts = await ConvertFilesAsync(new[] { source }, operation);
        var output = attempts.FirstOrDefault()?.Result?.OutputPath;
        if (output != null)
        {
            await _opening.SafeOpenFile(output);
            if (_opening.OpenedFile?.FilePath == output)
                _opening.OpenedFile.Overview.NcasIntegrity = attempts[0].Result!.Verification.Integrity;
        }
    }

    public async Task<IReadOnlyList<ConversionAttempt>> ConvertFilesAsync(string[] paths, NszOperation operation, string? sourceRoot = null)
    {
        if (!_settings.NszPluginActive || _background.IsRunning || paths.Length == 0) return Array.Empty<ConversionAttempt>();
        var outputSelection = OperationOutputDialog.Select(_settings, operation == NszOperation.Compress ? "Compression" : "Decompression", paths[0], _prompts);
        var destination = outputSelection?.Directory;
        if (destination == null) return Array.Empty<ConversionAttempt>();
        destination = Path.GetFullPath(destination);
        var deleteSource = Emignatik.NxFileViewer.Views.Windows.ThemedDialog.Confirm(EngineName, LocalizationManager.Instance.Current.Keys.Nsz_DeleteSourcePrompt);
        if (deleteSource == MessageBoxResult.Cancel) return Array.Empty<ConversionAttempt>();
        if (deleteSource == MessageBoxResult.Yes && _opening.OpenedFile != null &&
            paths.Contains(_opening.OpenedFile.FilePath, StringComparer.OrdinalIgnoreCase)) _opening.SafeClose();
        var attempts = new List<ConversionAttempt>();
        var runnable = new RunnableRelay((progress, token) =>
        {
            if (_settings.NszCompressionEngine.PrimaryRust() && !_settings.NszCompressionEngine.HasBackup() &&
                !File.Exists(PackageEnginePaths.ResolveRust(_settings.RustPackageExecutablePath)))
                throw new FileNotFoundException("nscb_rust: " + LocalizationManager.Instance.Current.Keys.Package_Missing,
                    _settings.RustPackageExecutablePath);
            if (!_settings.NszCompressionEngine.PrimaryRust())
            {
                try { _manager.Prepare(progress, token); }
                catch (Exception ex) when (_settings.NszCompressionEngine.HasBackup() && !token.IsCancellationRequested)
                { _logger.LogWarning(ex, "Primary NSZ preparation failed; backup engine remains available."); }
            }
            for (var index = 0; index < paths.Length; index++)
            {
                token.ThrowIfCancellationRequested();
                var source = paths[index];
                try
                {
                    var outputDirectory = outputSelection!.ForSource(source);
                    if (sourceRoot != null && outputSelection.Location == OutputLocation.Custom)
                    {
                        var relative = Path.GetRelativePath(Path.GetFullPath(sourceRoot), Path.GetDirectoryName(Path.GetFullPath(source))!);
                        outputDirectory = Path.GetFullPath(Path.Combine(destination, relative));
                        if (outputDirectory != destination && !outputDirectory.StartsWith(destination.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                            throw new IOException("Source is outside the batch directory.");
                    }
                    var result = _converter.Convert(source, outputDirectory, operation, new NszProgressScope(progress, $"[{index + 1}/{paths.Length}]", (double)index / paths.Length, 1.0 / paths.Length), token,
                        output => Application.Current.Dispatcher.Invoke(() => PromptConflict(output)),
                        deleteSource == MessageBoxResult.Yes);
                    attempts.Add(new(source, result, null));
                    _logger.LogInformation("NSZ: {Source} -> {Output}; {Before} -> {After} bytes; integrity: {Integrity}",
                        source, result.OutputPath, result.SourceSize, result.OutputSize, result.Verification.Integrity);
                    if (result.SourceDeleted) _logger.LogInformation("{Notice}: {Source}",
                        LocalizationManager.Instance.Current.Keys.Nsz_SourceDeleted, source);
                    if (result.SourceDeletionError != null) _logger.LogWarning("{Notice}: {Source}: {Error}",
                        LocalizationManager.Instance.Current.Keys.Nsz_SourceDeleteFailed, source, result.SourceDeletionError);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    attempts.Add(new(source, null, ex.Message));
                    _logger.LogError(ex, "NSZ conversion failed: {File}", source);
                }
                progress.SetPercentage((index + 1.0) / paths.Length);
            }
        }) { SupportsCancellation = true, SupportProgress = true };
        var cancelled = false;
        try { await _background.RunAsync(runnable); }
        catch (OperationCanceledException) { cancelled = true; }
        catch (Exception ex) { ShowError(ex); }
        finally { Refresh(); }
        var success = attempts.Count(a => a.Result != null);
        var deletionErrors = attempts.Where(a => a.Result?.SourceDeletionError != null).ToArray();
        if (deletionErrors.Length > 0)
            Emignatik.NxFileViewer.Views.Windows.ThemedDialog.Notice(LocalizationManager.Instance.Current.Keys.Nsz_SourceDeleteFailed + Environment.NewLine +
                string.Join(Environment.NewLine, deletionErrors.Select(a => a.SourcePath + ": " + a.Result!.SourceDeletionError)),
                EngineName, MessageBoxImage.Warning);
        if (attempts.Count > 0 || cancelled)
            Emignatik.NxFileViewer.Views.Windows.ThemedDialog.Notice(string.Format(LocalizationManager.Instance.Current.Keys.Nsz_Summary,
                success, attempts.Count(a => a.Error != null), paths.Length - attempts.Count),
                EngineName, MessageBoxImage.Information, LocalizationManager.Instance.Current.Keys.Output_OpenBatch,
                success == 0 ? null : () => OperationOutputDialog.OpenInBatch(attempts.Where(a => a.Result != null).Select(a => a.Result!.OutputPath)));
        return attempts;
    }

    private static OutputConflictResolution PromptConflict(string output)
    {
        var keys = LocalizationManager.Instance.Current.Keys;
        var result = OutputConflictResolution.Cancel;
        var panel = new System.Windows.Controls.StackPanel { Margin = new Thickness(18) };
        var dialog = new Window { Title = "NSZ", Content = panel, SizeToContent = SizeToContent.WidthAndHeight,
            ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = Application.Current.MainWindow };
        dialog.Style = Application.Current.TryFindResource("WindowStyle") as Style;
        panel.Children.Add(new System.Windows.Controls.TextBlock { Text = keys.Nsz_OutputExists + Environment.NewLine + output,
            TextWrapping = TextWrapping.Wrap, MaxWidth = 650, Margin = new Thickness(0, 0, 0, 16) });
        var buttons = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
        panel.Children.Add(buttons);
        foreach (var option in new[] { (keys.Nsz_Replace, OutputConflictResolution.Replace),
            (keys.Nsz_Number, OutputConflictResolution.Number), (keys.Nsz_Cancel, OutputConflictResolution.Cancel) })
        {
            var button = new System.Windows.Controls.Button { Content = option.Item1, Padding = new Thickness(12, 5, 12, 5),
                Margin = new Thickness(0, 0, 8, 0), IsCancel = option.Item2 == OutputConflictResolution.Cancel,
                IsDefault = option.Item2 == OutputConflictResolution.Number };
            button.IsEnabled = option.Item2 != OutputConflictResolution.Replace || !Directory.Exists(output);
            button.Click += (_, _) => { result = option.Item2; dialog.Close(); };
            buttons.Children.Add(button);
        }
        App.ServiceProvider.GetRequiredService<IThemeService>().RegisterWindow(dialog);
        dialog.ShowDialog();
        return result;
    }

    private async void Update()
    {
        if (!UpdateCommand.CanExecute(null)) return;
        try
        {
            await _background.RunAsync(new RunnableRelay((progress, token) =>
                _manager.UpdateAsync(progress, token).GetAwaiter().GetResult()) { SupportsCancellation = true });
            _settingsManager?.SaveSafe();
            Emignatik.NxFileViewer.Views.Windows.ThemedDialog.Notice(Status, "NSZ", MessageBoxImage.Information);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { ShowError(ex); }
        finally { Refresh(); }
    }

    private void Rollback()
    {
        if (!RollbackCommand.CanExecute(null)) return;
        try
        {
            _manager.Rollback(); _settingsManager?.SaveSafe();
            // Keep the chosen previous version until updates are explicitly requested again.
            _settings.NszCheckUpdates = false;
            _settingsManager?.SaveSafe();
            Emignatik.NxFileViewer.Views.Windows.ThemedDialog.Notice(Status, "NSZ", MessageBoxImage.Information);
        }
        catch (Exception ex) { ShowError(ex); }
        finally { Refresh(); }
    }

    private void ShowError(Exception ex)
    {
        _logger.LogError(ex, "NSZ operation failed.");
        Emignatik.NxFileViewer.Views.Windows.ThemedDialog.Notice(ex.Message, "NSZ", MessageBoxImage.Error);
    }
}
