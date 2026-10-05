using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Data;
using Emignatik.NxFileViewer.Services.BackgroundTask;
using Emignatik.NxFileViewer.Localization;
using Emignatik.NxFileViewer.Services.BackgroundTask.RunnableImpl;
using Emignatik.NxFileViewer.Services.Integrity;
using Emignatik.NxFileViewer.Models.Overview;
using Emignatik.NxFileViewer.Services.Prompting;
using Emignatik.NxFileViewer.Utils.MVVM;
using Emignatik.NxFileViewer.Utils.MVVM.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Emignatik.NxFileViewer.Settings;
using Emignatik.NxFileViewer.Models;
using Emignatik.NxFileViewer.Views.UserControls;
using System.Windows;

namespace Emignatik.NxFileViewer.Views.Windows;

public sealed class BatchIntegrityWindowViewModel : WindowViewModelBase
{
    private readonly IPromptService _promptService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BatchIntegrityWindowViewModel> _logger;
    private string _inputDirectory = "";
    private bool _showOnlyErrors;
    private NxFile? _previewFile;
    private FileOverviewViewModel? _previewOverview;

    public BatchIntegrityWindowViewModel(IPromptService promptService, IServiceProvider serviceProvider,
        IMainBackgroundTaskRunnerService backgroundTaskRunner, ILogger<BatchIntegrityWindowViewModel> logger,
        IAppSettings appSettings)
    {
        _promptService = promptService;
        _serviceProvider = serviceProvider;
        _logger = logger;
        BackgroundTask = backgroundTaskRunner;
        ResultsView = CollectionViewSource.GetDefaultView(Results);
        ResultsView.Filter = ShouldDisplayResult;
        BrowseCommand = new RelayCommand(Browse);
        StartCommand = new RelayCommand(Start, CanStart);
        ExportCommand = new RelayCommand(Export, () => Results.Count > 0 && !BackgroundTask.IsRunning);
        MoveValidCommand = new RelayCommand(MoveValid, CanMoveValid);
        _inputDirectory = Directory.Exists(appSettings.LastUsedDir) ? appSettings.LastUsedDir : "";
        BackgroundTask.PropertyChanged += BackgroundTaskOnPropertyChanged;
    }

    public IMainBackgroundTaskRunnerService BackgroundTask { get; }
    public ObservableCollection<BatchIntegrityResult> Results { get; } = new();
    public ICollectionView ResultsView { get; }
    public RelayCommand BrowseCommand { get; }
    public RelayCommand StartCommand { get; }
    public RelayCommand ExportCommand { get; }
    public RelayCommand MoveValidCommand { get; }
    public bool IncludeSubdirectories { get; set; } = true;
    public FileOverviewViewModel? PreviewOverview
    {
        get => _previewOverview;
        private set { _previewOverview = value; NotifyPropertyChanged(); }
    }

    public bool ShowOnlyErrors
    {
        get => _showOnlyErrors;
        set
        {
            _showOnlyErrors = value;
            NotifyPropertyChanged();
            ResultsView.Refresh();
        }
    }

    public string InputDirectory
    {
        get => _inputDirectory;
        set { _inputDirectory = value; NotifyPropertyChanged(); StartCommand.TriggerCanExecuteChanged(); }
    }

    private void Browse()
    {
        var directory = _promptService.PromptSelectDir(LocalizationManager.Instance.Current.Keys.BatchIntegrity_SelectDirectory);
        if (directory != null) InputDirectory = directory;
    }

    private bool CanStart() => Directory.Exists(InputDirectory) && !BackgroundTask.IsRunning;

    private async void Start()
    {
        Results.Clear();
        ClearPreview();
        ExportCommand.TriggerCanExecuteChanged();
        try
        {
            var runnable = _serviceProvider.GetRequiredService<IVerifyDirectoryIntegrityRunnable>()
                .Setup(InputDirectory, IncludeSubdirectories, ShowPreview, ShowCompletedResult);
            await BackgroundTask.RunAsync(runnable);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _logger.LogError(ex, "Batch integrity check failed."); }
        finally
        {
            ExportCommand.TriggerCanExecuteChanged();
            MoveValidCommand.TriggerCanExecuteChanged();
        }
    }

    private void ShowPreview(NxFile file)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            _previewFile?.Dispose();
            _previewFile = file;
            PreviewOverview = new FileOverviewViewModel(file.Overview, _serviceProvider);
        });
    }

    private void ShowCompletedResult(BatchIntegrityResult result)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            Results.Add(result);
            ExportCommand.TriggerCanExecuteChanged();
            MoveValidCommand.TriggerCanExecuteChanged();
        });
    }

    private void ClearPreview()
    {
        _previewFile?.Dispose();
        _previewFile = null;
        PreviewOverview = null;
    }

    private bool CanMoveValid() => !BackgroundTask.IsRunning &&
        Results.Any(result => result.Integrity == NcasIntegrity.Original && File.Exists(result.FilePath));

    private async void MoveValid()
    {
        var destinationRoot = _promptService.PromptSelectDir(
            LocalizationManager.Instance.Current.Keys.BatchIntegrity_SelectMoveDestination);
        if (destinationRoot == null) return;

        var sourceRoot = Path.GetFullPath(InputDirectory);
        var destinationRootFull = Path.GetFullPath(destinationRoot);
        ClearPreview();
        var validFiles = Results.Where(result =>
            result.Integrity == NcasIntegrity.Original && File.Exists(result.FilePath)).ToArray();
        var runnable = new RunnableRelay<int>((reporter, cancellationToken) =>
        {
            var moved = 0;
            for (var index = 0; index < validFiles.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var source = validFiles[index].FilePath;
                reporter.SetText($"{LocalizationManager.Instance.Current.Keys.BatchIntegrity_Moving} {index + 1}/{validFiles.Length}: {Path.GetFileName(source)}");
                var relativePath = Path.GetRelativePath(sourceRoot, source);
                var destination = Path.GetFullPath(Path.Combine(destinationRootFull, relativePath));
                if (!destination.StartsWith(destinationRootFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    continue;
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                if (!File.Exists(destination))
                {
                    File.Move(source, destination);
                    moved++;
                }
                reporter.SetPercentage((double)(index + 1) / validFiles.Length);
            }
            return moved;
        }) { SupportProgress = true, SupportsCancellation = true };

        try
        {
            await BackgroundTask.RunAsync(runnable);
            foreach (var movedResult in Results.Where(result => !File.Exists(result.FilePath)).ToArray())
                Results.Remove(movedResult);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _logger.LogError(ex, "Failed to move verified files."); }
        finally
        {
            ExportCommand.TriggerCanExecuteChanged();
            MoveValidCommand.TriggerCanExecuteChanged();
        }
    }

    private void Export()
    {
        var path = _promptService.PromptSaveFile("integrity-results.csv", "Export integrity results", "CSV files (*.csv)|*.csv");
        if (path == null) return;
        var csv = new StringBuilder("File;FileType;PackageType;Structure;Compression;Integrity;Error\r\n");
        foreach (var result in Results)
            csv.Append(Escape(result.FilePath)).Append(';')
                .Append(Escape(result.FileType)).Append(';')
                .Append(Escape(result.PackageType)).Append(';')
                .Append(Escape(result.Structure)).Append(';')
                .Append(Escape(result.Compression)).Append(';')
                .Append(Escape(result.Integrity.ToString())).Append(';')
                .Append(Escape(result.Error ?? "")).Append("\r\n");
        File.WriteAllText(path, csv.ToString(), new UTF8Encoding(true));
    }

    private static string Escape(string value) => $"\"{value.Replace("\"", "\"\"")}\"";

    private bool ShouldDisplayResult(object item) =>
        !_showOnlyErrors || item is BatchIntegrityResult { Integrity: not NcasIntegrity.Original };

    private void BackgroundTaskOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(IBackgroundTaskRunner.IsRunning)) return;
        StartCommand.TriggerCanExecuteChanged(true);
        ExportCommand.TriggerCanExecuteChanged(true);
        MoveValidCommand.TriggerCanExecuteChanged(true);
    }
}
