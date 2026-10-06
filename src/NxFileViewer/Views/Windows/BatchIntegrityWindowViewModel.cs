using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
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
using System.Threading;
using System.Threading.Tasks;
using Emignatik.NxFileViewer.FileLoading;
using Emignatik.NxFileViewer.Services.Nsz;

namespace Emignatik.NxFileViewer.Views.Windows;

public sealed class BatchIntegrityWindowViewModel : WindowViewModelBase
{
    private readonly IPromptService _promptService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BatchIntegrityWindowViewModel> _logger;
    private string _inputDirectory = "";
    private bool _showOnlyErrors;
    private NxFile? _previewFile;
    private bool _closed;
    // Overview models contain metadata already read by the checker; retaining them
    // does not require retaining the open NxFile or its decoded NCZ cache.
    private readonly Dictionary<string, FileOverviewViewModel> _completedPreviews = new(StringComparer.OrdinalIgnoreCase);
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
        BrowseFirmwareZipCommand = new RelayCommand(() =>
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "ZIP / 7z (*.zip;*.7z)|*.zip;*.7z", Title = LocalizationManager.Instance.Current.Keys.BatchIntegrity_Title };
            if (dialog.ShowDialog() == true) InputDirectory = dialog.FileName;
        });
        StartCommand = new RelayCommand(Start, CanStart);
        ExportCommand = new RelayCommand(Export, () => Results.Count > 0 && !BackgroundTask.IsRunning);
        MoveValidCommand = new RelayCommand(MoveValid, CanMoveValid);
        CompressValidCommand = new RelayCommand(() => ConvertValid(NszOperation.Compress), () => CanConvertValid(NszOperation.Compress));
        DecompressValidCommand = new RelayCommand(() => ConvertValid(NszOperation.Decompress), () => CanConvertValid(NszOperation.Decompress));
        _inputDirectory = Directory.Exists(appSettings.LastUsedDir) ? appSettings.LastUsedDir : "";
        BackgroundTask.PropertyChanged += BackgroundTaskOnPropertyChanged;
    }

    public IMainBackgroundTaskRunnerService BackgroundTask { get; }
    public ObservableCollection<BatchIntegrityResult> Results { get; } = new();
    public ICollectionView ResultsView { get; }
    public RelayCommand BrowseFirmwareZipCommand { get; }
    private BatchIntegrityResult? _selectedResult;
    public BatchIntegrityResult? SelectedResult
    {
        get => _selectedResult;
        set
        {
            if (ReferenceEquals(_selectedResult, value)) return;
            _selectedResult = value;
            SelectedDetailsTabIndex = value?.IsFirmware == true ? 1 : 0;
            NotifyPropertyChanged();
            UpdateSelectedPreview();
        }
    }
    private int _selectedDetailsTabIndex;
    public int SelectedDetailsTabIndex
    {
        get => _selectedDetailsTabIndex;
        set
        {
            if (_selectedDetailsTabIndex == value) return;
            _selectedDetailsTabIndex = value;
            NotifyPropertyChanged();
        }
    }
    public RelayCommand BrowseCommand { get; }
    public RelayCommand StartCommand { get; }
    public RelayCommand ExportCommand { get; }
    public RelayCommand MoveValidCommand { get; }
    public RelayCommand CompressValidCommand { get; }
    public RelayCommand DecompressValidCommand { get; }
    public bool IncludeSubdirectories { get; set; } = true;
    public FileOverviewViewModel? PreviewOverview
    {
        get => _previewOverview;
        private set { _previewOverview = value; NotifyPropertyChanged(); }
    }

    private string _searchText = "";
    private string _fileTypeFilter = "";
    private string _integrityFilter = "";
    public string SearchText { get => _searchText; set { _searchText = value ?? ""; NotifyPropertyChanged(); ResultsView.Refresh(); } }
    public string FileTypeFilter { get => _fileTypeFilter; set { _fileTypeFilter = value ?? ""; NotifyPropertyChanged(); ResultsView.Refresh(); } }
    public string IntegrityFilter { get => _integrityFilter; set { _integrityFilter = value ?? ""; NotifyPropertyChanged(); ResultsView.Refresh(); } }
    public IReadOnlyList<BatchFilterOption> FileTypeFilters { get; } = new[] { "", "NSP", "NSZ", "XCI", "XCZ", "ZIP", "7Z", "Folder" }.Select(value => new BatchFilterOption(value)).ToArray();
    public IReadOnlyList<BatchFilterOption> IntegrityFilters { get; } = new[] { new BatchFilterOption("") }.Concat(Enum.GetValues<NcasIntegrity>().Select(value => new BatchFilterOption(value.ToString()))).ToArray();
    public RelayCommand ResetFiltersCommand => new(() => { SearchText = ""; FileTypeFilter = ""; IntegrityFilter = ""; ShowOnlyErrors = false; });
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

    private bool CanStart() => (Directory.Exists(InputDirectory) || (File.Exists(InputDirectory) && PackageZip.IsArchive(InputDirectory))) && !BackgroundTask.IsRunning;

    private async void Start()
    {
        Results.Clear();
        _completedPreviews.Clear();
        SelectedResult = null;
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
            if (_closed) ClearPreview();
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
            if (_closed) return;
            // Capture every loaded overview once, even while another result is selected.
            _completedPreviews[file.FilePath] = new FileOverviewViewModel(file.Overview, _serviceProvider);
            if (SelectedResult == null || SelectedResult.FilePath.Equals(file.FilePath, StringComparison.OrdinalIgnoreCase))
                PreviewOverview = _completedPreviews[file.FilePath];
            else UpdateSelectedPreview();
        });
    }

    private void ShowCompletedResult(BatchIntegrityResult result)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            // The cached model observes integrity changes from the verifier.
            // Publish it before adding a row, which can trigger selection bindings.
            if (!result.IsFirmware && _completedPreviews.TryGetValue(result.FilePath, out var completed) &&
                SelectedResult?.FilePath.Equals(result.FilePath, StringComparison.OrdinalIgnoreCase) == true)
                PreviewOverview = completed;
            if (!result.IsFirmware && _completedPreviews.TryGetValue(result.FilePath, out var overview))
            {
                var packages = overview.CnmtContainers;
                string Join(Func<CnmtContainerViewModel, string?> selector) => string.Join(" / ",
                    packages.Select(selector).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct());
                result = result with
                {
                    Title = Join(package => package.Titles.FirstOrDefault()?.AppName),
                    TitleId = Join(package => package.TitleId),
                    Publisher = Join(package => package.Titles.FirstOrDefault()?.Publisher),
                    Version = Join(package => package.TitleVersion),
                    DisplayVersion = Join(package => package.DisplayVersion),
                    SystemVersion = Join(package => package.MinimumSystemVersion),
                    MasterKey = Join(package => package.MasterKey),
                    BuildId = Join(package => package.BuildID),
                    Distribution = Join(package => package.Distribution),
                    Languages = string.Join(", ", packages.SelectMany(package => package.Titles).Select(title => title.Language.ToString()).Distinct()),
                    FileSize = _previewFile?.FilePath == result.FilePath ? _previewFile.Overview.FileSize : null,
                    CompressionRatio = _previewFile?.FilePath == result.FilePath ? _previewFile.Overview.CompressionRatio : null
                };
            }
            Results.Add(result);
            if (result.IsFirmware) SelectedResult = result;
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

    public void ClosePreview()
    {
        _closed = true;
        _completedPreviews.Clear();
        BackgroundTask.PropertyChanged -= BackgroundTaskOnPropertyChanged;
        // The checking file remains in use until the verifier task ends.
        if (!BackgroundTask.IsRunning) ClearPreview();
    }

    private void UpdateSelectedPreview()
    {
        PreviewOverview = null;
        var selected = SelectedResult;
        if (_closed || selected == null || selected.IsFirmware) return;
        // Completed results are snapshots. Selection must never reopen a package,
        // including when its source was moved/deleted after the check.
        if (_completedPreviews.TryGetValue(selected.FilePath, out var completed))
            PreviewOverview = completed;
    }
    private bool CanMoveValid() => !BackgroundTask.IsRunning &&
        Results.Any(result => !result.IsFirmware && result.Integrity == NcasIntegrity.Original && File.Exists(result.FilePath));

    private bool CanConvertValid(NszOperation operation) => !BackgroundTask.IsRunning && Results.Any(result =>
        !result.IsFirmware && result.Integrity == NcasIntegrity.Original && File.Exists(result.FilePath) &&
        PackageConversionService.Supports(result.FilePath, operation));

    private async void ConvertValid(NszOperation operation)
    {
        if (!CanConvertValid(operation)) return;
        var paths = Results.Where(result => !result.IsFirmware && result.Integrity == NcasIntegrity.Original &&
            File.Exists(result.FilePath) && PackageConversionService.Supports(result.FilePath, operation))
            .Select(result => result.FilePath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        ClearPreview();
        var attempts = await _serviceProvider.GetRequiredService<NszActions>().ConvertFilesAsync(paths, operation, InputDirectory);
        foreach (var attempt in attempts)
        {
            var existing = Results.FirstOrDefault(result => result.FilePath == attempt.SourcePath);
            if (existing == null) continue;
            var result = attempt.Result;
            Results[Results.IndexOf(existing)] = existing with
            {
                ConversionStatus = result != null ? LocalizationManager.Instance.Current.Keys.Nsz_Verified +
                    (result.SourceDeleted ? "; " + LocalizationManager.Instance.Current.Keys.Nsz_SourceDeleted :
                    result.SourceDeletionError != null ? "; " + LocalizationManager.Instance.Current.Keys.Nsz_SourceDeleteFailed + ": " + result.SourceDeletionError : "") : attempt.Error,
                ConversionFailed = result == null || result.SourceDeletionError != null,
                ConvertedPath = result?.OutputPath, SourceSize = result?.SourceSize, OutputSize = result?.OutputSize
            };
            if (result != null && !Results.Any(row => row.FilePath.Equals(result.OutputPath, StringComparison.OrdinalIgnoreCase)))
                Results.Add(result.Verification with { ConversionStatus = LocalizationManager.Instance.Current.Keys.Nsz_Verified,
                    SourceSize = result.SourceSize, OutputSize = result.OutputSize });
        }
        ResultsView.Refresh();
    }

    private async void MoveValid()
    {
        var destinationRoot = _promptService.PromptSelectDir(
            LocalizationManager.Instance.Current.Keys.BatchIntegrity_SelectMoveDestination);
        if (destinationRoot == null) return;

        var sourceRoot = Path.GetFullPath(InputDirectory);
        var destinationRootFull = Path.GetFullPath(destinationRoot);
        ClearPreview();
        var validFiles = Results.Where(result =>
            !result.IsFirmware && result.Integrity == NcasIntegrity.Original && File.Exists(result.FilePath)).ToArray();
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
            foreach (var movedResult in Results.Where(result => !result.IsFirmware && !File.Exists(result.FilePath)).ToArray())
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
        var csv = new StringBuilder("File;FileType;PackageType;Structure;Compression;Integrity;Error;FirmwareDetails;Conversion;ConvertedPath;SourceBytes;OutputBytes;Title;TitleId;Publisher;Version;DisplayVersion;Firmware;MasterKey;BuildId;Distribution;Languages;FileBytes;CompressionRatio\r\n");
        foreach (var result in ResultsView.Cast<BatchIntegrityResult>())
            csv.Append(Escape(result.FilePath)).Append(';')
                .Append(Escape(result.FileType)).Append(';')
                .Append(Escape(result.PackageType)).Append(';')
                .Append(Escape(result.Structure)).Append(';')
                .Append(Escape(result.Compression)).Append(';')
                .Append(Escape(result.Integrity.ToString())).Append(';')
                .Append(Escape(result.Error ?? "")).Append(';')
                .Append(Escape(result.FirmwareDetails ?? "")).Append(';')
                .Append(Escape(result.ConversionStatus ?? "")).Append(';')
                .Append(Escape(result.ConvertedPath ?? "")).Append(';')
                .Append(result.SourceSize).Append(';').Append(result.OutputSize).Append(';')
                .Append(string.Join(";", new[] { result.Title, result.TitleId, result.Publisher, result.Version, result.DisplayVersion,
                    result.SystemVersion, result.MasterKey, result.BuildId, result.Distribution, result.Languages }.Select(Escape)))
                .Append(';').Append(result.FileSize).Append(';')
                .Append(result.CompressionRatio?.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append("\r\n");
        File.WriteAllText(path, csv.ToString(), new UTF8Encoding(true));
    }

    private static string Escape(string value) => $"\"{value.Replace("\"", "\"\"")}\"";

    private bool ShouldDisplayResult(object item) =>
        item is BatchIntegrityResult result && BatchResultFilter.Matches(result, SearchText, FileTypeFilter, IntegrityFilter, ShowOnlyErrors);

    private void BackgroundTaskOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(IBackgroundTaskRunner.IsRunning)) return;
        StartCommand.TriggerCanExecuteChanged(true);
        ExportCommand.TriggerCanExecuteChanged(true);
        MoveValidCommand.TriggerCanExecuteChanged(true);
        CompressValidCommand.TriggerCanExecuteChanged(true);
        DecompressValidCommand.TriggerCanExecuteChanged(true);
    }
}

public sealed record BatchFilterOption(string Value)
{
    public string Label => Value.Length == 0 ? LocalizationManager.Instance.Current.Keys.BatchTable_All : Value;
}