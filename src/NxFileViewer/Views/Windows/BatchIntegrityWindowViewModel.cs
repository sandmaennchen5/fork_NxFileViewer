using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
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
using Emignatik.NxFileViewer.Services.FileRenaming;
using Emignatik.NxFileViewer.Services.FileRenaming.Models;

namespace Emignatik.NxFileViewer.Views.Windows;

public sealed class BatchIntegrityWindowViewModel : WindowViewModelBase
{
    private readonly BatchMemoryDiagnostics _memoryDiagnostics;
    public bool MemoryDiagnosticsEnabled => _memoryDiagnostics.IsEnabled;
    public string MemoryDiagnosticMode => _memoryDiagnostics.ActiveMode;
    public string MemoryLogPath => _memoryDiagnostics.LogPath;
    private string _memoryStatus = "";
    public string MemoryStatus { get => _memoryStatus; private set { _memoryStatus = value; NotifyPropertyChanged(); } }
    public RelayCommand MeasureMemoryCommand { get; }
    private void RecordMemory(string stage)
    {
        if (MemoryDiagnosticsEnabled)
            MemoryStatus = _memoryDiagnostics.Capture(stage, Results.Count, _completedPreviews.Count);
    }
    private void OnDiagnosticSettingChanged(object? sender, PropertyChangedEventArgs args)
    {
        NotifyPropertyChanged(nameof(MemoryDiagnosticsEnabled));
        NotifyPropertyChanged(nameof(MemoryDiagnosticMode));
        MeasureMemoryCommand.TriggerCanExecuteChanged(true);
        RecordMemory("diagnostics-setting-changed");
    }
    private void MeasureMemory()
    {
        if (!MemoryDiagnosticsEnabled || BackgroundTask.IsRunning) return;
        RecordMemory("manual-before-gc");
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        RecordMemory("manual-after-gc");
    }
    private readonly IPromptService _promptService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BatchIntegrityWindowViewModel> _logger;
    private string _inputDirectory = "";
    private string[] _sourcePaths = Array.Empty<string>();
    private string[] _explorerSources = Array.Empty<string>();
    public IReadOnlyList<string> SourcePaths => _sourcePaths;
    private string? SourceRoot => _sourcePaths.Length == 1 && Directory.Exists(_sourcePaths[0]) ? _sourcePaths[0]
        : _sourcePaths.Length == 0 && Directory.Exists(InputDirectory) ? InputDirectory : null;
    public void LoadDroppedSources(IEnumerable<string> sources) => LoadSources(sources, _appSettings.VerifyBatchDrops);

    public void LoadExplorerSources(IEnumerable<string> sources, bool verify)
    {
        if (BackgroundTask.IsRunning) return;
        var paths = VerifyBatchSourcesRunnable.Normalize(sources).Where(path => File.Exists(path) || Directory.Exists(path)).ToArray();
        if (paths.Length == 0) return;
        var append = _explorerSources.Length > 0;
        _explorerSources = VerifyBatchSourcesRunnable.Normalize(_explorerSources.Concat(paths));
        InputDirectory = string.Join(" | ", _explorerSources);
        _sourcePaths = _explorerSources.ToArray();
        NotifyPropertyChanged(nameof(SourcePaths));
        StartCommand.TriggerCanExecuteChanged(); ScanAndVerifyCommand.TriggerCanExecuteChanged();
        Start(verify, rescan: true, explorerRequest: true, append: append, sourcesToScan: paths);
    }

    public void LoadSources(IEnumerable<string> sources, bool verify)
    {
        if (BackgroundTask.IsRunning) return;
        var paths = VerifyBatchSourcesRunnable.Normalize(sources).Where(path => File.Exists(path) || Directory.Exists(path)).ToArray();
        if (paths.Length == 0) return;
        InputDirectory = string.Join(" | ", paths);
        _sourcePaths = paths;
        NotifyPropertyChanged(nameof(SourcePaths));
        StartCommand.TriggerCanExecuteChanged(); ScanAndVerifyCommand.TriggerCanExecuteChanged();
        Start(verify, rescan: true);
    }
    private readonly BatchHistoryStore _historyStore = new(BatchHistoryStore.DefaultPath);
    private readonly string _temporaryHistoryRoot = Path.Combine(Path.GetTempPath(), "NxFileViewer-preview-" + Guid.NewGuid().ToString("N"));
    private BatchHistoryStore? _temporaryHistoryStore;
    private BatchHistoryStore OverviewStore => IsHistoryEnabled ? _historyStore
        : _temporaryHistoryStore ??= new BatchHistoryStore(Path.Combine(_temporaryHistoryRoot, "history.json"));
    private ActiveBatchLogoStore? _activeLogos;
    private ActiveBatchLogoStore ActiveLogos => _activeLogos ??= new ActiveBatchLogoStore(_historyStore.EntriesDirectory, _logger);
    private byte[]? ReadStoredIcon(BatchIconReference icon)
    {
        var bytes = _activeLogos?.ReadIcon(icon) ?? _historyStore.ReadIcon(icon) ?? _temporaryHistoryStore?.ReadIcon(icon);
        _logger.LogTrace("Stored logo lookup: scan={ScanId}; key={Key}; found={Found}; bytes={Bytes}",
            icon.EntryId, icon.Key, bytes != null, bytes?.Length ?? 0);
        return bytes;
    }
    private BatchTitleSummary SnapshotTitle(TitleInfo title) => new(title.Language.ToString(), title.AppName, title.Publisher)
    {
        Icon = _session == null ? null : IsHistoryEnabled && _appSettings.SaveBatchHistoryLogos
            ? _historyStore.StageIcon(_session.Id, title.Icon) : ActiveLogos.StageIcon(_session.Id, title.Icon)
    };
    private void DeleteActiveLogos()
    {
        var active = _activeLogos;
        _logger.LogTrace("Releasing active logo cache {ArchivePath}", active?.ArchivePath);
        _activeLogos = null;
        try { active?.Dispose(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { _logger.LogWarning(ex, "Could not remove temporary active-scan logos."); }
    }
    private readonly Dictionary<string, string> _fingerprints = new(StringComparer.OrdinalIgnoreCase);
    private BatchHistoryEntry? _session;
    private DateTime _lastHistorySave;
    private readonly IAppSettings _appSettings;
    public string ColumnLayout
    {
        get => _appSettings.BatchTableLayout;
        set
        {
            if (_appSettings.BatchTableLayout == value) return;
            _appSettings.BatchTableLayout = value;
            _serviceProvider.GetService<IAppSettingsManager>()?.SaveSafe();
        }
    }
    public int HistoryLimit => _appSettings.BatchHistoryLimit;
    public bool IsHistoryEnabled => _appSettings.EnableBatchHistory;
    private static readonly BatchHistoryEntry EmptyHistory = new(Guid.Empty, DateTime.MinValue, "", false, false, false, false, "",
        Array.Empty<BatchIntegrityResult>(), new Dictionary<string, string>());
    public ObservableCollection<BatchHistoryEntry> History { get; } = new();
    private bool _refreshingHistory;
    private BatchHistoryEntry? _selectedHistory;
    public BatchHistoryEntry? SelectedHistory
    {
        get => _selectedHistory;
        set
        {
            if (ReferenceEquals(_selectedHistory, value)) return;
            _selectedHistory = value;
            NotifyPropertyChanged();
            ResumeHistoryCommand?.TriggerCanExecuteChanged();
            if (!_refreshingHistory && value != null && !BackgroundTask.IsRunning)
            {
                if (value.Id != Guid.Empty) RestoreHistory();
                else
                {
                    SelectedResult = null;
                    DeleteTemporaryHistory();
                    ClearPreview();
                    _completedPreviews.Clear();
                    Results.Clear();
                    _fingerprints.Clear();
                    _session = null;
                    VerifyAllCommand?.TriggerCanExecuteChanged();
                    ExportCommand?.TriggerCanExecuteChanged();
                    MoveValidCommand?.TriggerCanExecuteChanged();
                    CheckNamingCommand?.TriggerCanExecuteChanged();
                    RenameAllCommand?.TriggerCanExecuteChanged();
                }
            }
        }
    }
    public RelayCommand ResumeHistoryCommand { get; }
    private bool _groupFiles;
    private bool _showOnlyErrors;
    private NxFile? _previewFile;
    private bool _closed;
    // Only the in-flight file has a live overview. Completed rows contain detached snapshots.
    private readonly Dictionary<string, FileOverviewViewModel> _completedPreviews = new(StringComparer.OrdinalIgnoreCase);
    private FileOverviewViewModel? _previewOverview;

    public BatchIntegrityWindowViewModel(IPromptService promptService, IServiceProvider serviceProvider,
        IMainBackgroundTaskRunnerService backgroundTaskRunner, ILogger<BatchIntegrityWindowViewModel> logger,
        IAppSettings appSettings)
    {
        _appSettings = appSettings;
        _promptService = promptService;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _memoryDiagnostics = new BatchMemoryDiagnostics(logger);
        BackgroundTask = backgroundTaskRunner;
        MeasureMemoryCommand = new RelayCommand(MeasureMemory, () => MemoryDiagnosticsEnabled && !BackgroundTask.IsRunning);
        RecordMemory("window-opened");
        ResumeHistoryCommand = new RelayCommand(ResumeHistory, () => IsHistoryEnabled && !BackgroundTask.IsRunning && SelectedHistory is { Id: var id } && id != Guid.Empty);
        ResultsView = CollectionViewSource.GetDefaultView(_displayResults);
        Results.CollectionChanged += OnResultsChanged;
        ResultsView.Filter = ShouldDisplayResult;
        BrowseCommand = new RelayCommand(Browse);
        BrowseSphairaCommand = new RelayCommand(async () =>
        {
            var path = await Services.Mtp.SphairaMtpActions.SelectAndImport(_serviceProvider, BackgroundTask, batch: true);
            if (path != null) InputDirectory = Directory.Exists(path) || PackageZip.IsArchive(path) ? path : Path.GetDirectoryName(path)!;
        }, () => _serviceProvider.GetRequiredService<IAppSettings>().MtpActive && !BackgroundTask.IsRunning);
        BrowseFirmwareZipCommand = new RelayCommand(() =>
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "ZIP / 7z (*.zip;*.7z)|*.zip;*.7z", Title = LocalizationManager.Instance.Current.Keys.BatchIntegrity_Title };
            if (dialog.ShowDialog() == true) InputDirectory = dialog.FileName;
        }, () => _serviceProvider.GetRequiredService<IAppSettings>().FirmwareCheckActive && !BackgroundTask.IsRunning);
        StartCommand = new RelayCommand(() => Start(false), CanStart);
        ScanAndVerifyCommand = new RelayCommand(() => Start(true, rescan: true), CanStart);
        VerifyAllCommand = new RelayCommand(() => Start(true), () => Results.Count > 0 && !BackgroundTask.IsRunning);
        CheckNamingCommand = new RelayCommand(() => ProcessNaming(false), CanProcessNaming);
        RenameAllCommand = new RelayCommand(() => ProcessNaming(true), CanProcessNaming);
        PropertyChangedEventManager.AddHandler(appSettings.RenamingOptions, OnNamingSettingsChanged, string.Empty);
        ExportCommand = new RelayCommand(Export, () => Results.Count > 0 && !BackgroundTask.IsRunning);
        MoveValidCommand = new RelayCommand(MoveValid, CanMoveValid);
        CompressValidCommand = new RelayCommand(() => ConvertValid(NszOperation.Compress), () => CanConvertValid(NszOperation.Compress));
        DecompressValidCommand = new RelayCommand(() => ConvertValid(NszOperation.Decompress), () => CanConvertValid(NszOperation.Decompress));
        _inputDirectory = Directory.Exists(appSettings.LastUsedDir) ? appSettings.LastUsedDir : "";
        BackgroundTask.PropertyChanged += BackgroundTaskOnPropertyChanged;
        PropertyChangedEventManager.AddHandler(appSettings, OnPackageDisplaySettingChanged, nameof(IAppSettings.BatchSeparatePackages));
        PropertyChangedEventManager.AddHandler(appSettings, OnPackageDisplaySettingChanged, nameof(IAppSettings.BatchSummaryPreservePackageOrder));
        PropertyChangedEventManager.AddHandler(appSettings, OnHistorySettingChanged, nameof(IAppSettings.EnableBatchHistory));
        PropertyChangedEventManager.AddHandler(appSettings, OnHistorySettingChanged, nameof(IAppSettings.BatchHistoryLimit));
        PropertyChangedEventManager.AddHandler(appSettings, OnDiagnosticSettingChanged, nameof(IAppSettings.LogLevel));
        ReloadHistory();
    }

    public IMainBackgroundTaskRunnerService BackgroundTask { get; }
    public ObservableCollection<BatchIntegrityResult> Results { get; } = new();
    private readonly BatchRowCollection _displayResults = new();
    private readonly Dictionary<string, IReadOnlyList<BatchIntegrityResult>> _rowsByPath = new(StringComparer.OrdinalIgnoreCase);

    private BatchIntegrityResult? FindPresentationResult(string path, int packageIndex) =>
        _rowsByPath.TryGetValue(path, out var rows)
            ? rows.FirstOrDefault(row => row.PackageIndex == packageIndex) ?? rows.FirstOrDefault() : null;

    private void OnPackageDisplaySettingChanged(object? sender, PropertyChangedEventArgs args)
    {
        var selected = SelectedResult;
        var displayRows = new List<BatchIntegrityResult>();
        _rowsByPath.Clear();
        foreach (var result in Results)
        {
            var rows = BatchPackageRows.Create(result, _appSettings.BatchSeparatePackages, _appSettings.BatchSummaryPreservePackageOrder);
            _rowsByPath[result.FilePath] = rows;
            displayRows.AddRange(rows);
        }
        // Rebuild in one notification; WPF cannot process row changes during DeferRefresh.
        _displayResults.ReplaceAll(displayRows);
        if (selected != null) SelectedResult = FindPresentationResult(selected.FilePath, selected.PackageIndex);
    }

    private void OnResultsChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        if (args.Action is NotifyCollectionChangedAction.Reset or NotifyCollectionChangedAction.Move)
        {
            OnPackageDisplaySettingChanged(sender, new PropertyChangedEventArgs(null));
            return;
        }
        var selected = SelectedResult;
        var restoreSelection = false;
        if (args.OldItems != null)
            foreach (BatchIntegrityResult result in args.OldItems)
            {
                restoreSelection |= selected?.FilePath.Equals(result.FilePath, StringComparison.OrdinalIgnoreCase) == true;
                if (!_rowsByPath.Remove(result.FilePath, out var rows)) continue;
                foreach (var row in rows) _displayResults.Remove(row);
            }
        if (args.NewItems != null)
        {
            var insertAt = args.NewStartingIndex == Results.Count - args.NewItems.Count ? _displayResults.Count :
                Results.Take(args.NewStartingIndex).Sum(result => _rowsByPath[result.FilePath].Count);
            foreach (BatchIntegrityResult result in args.NewItems)
            {
                var rows = BatchPackageRows.Create(result, _appSettings.BatchSeparatePackages, _appSettings.BatchSummaryPreservePackageOrder);
                _rowsByPath[result.FilePath] = rows;
                foreach (var row in rows) _displayResults.Insert(insertAt++, row);
            }
        }
        if (restoreSelection && selected != null) SelectedResult = FindPresentationResult(selected.FilePath, selected.PackageIndex);
    }

    public IReadOnlyList<BatchIntegrityResult> GetExportRows()
    {
        var visibleRows = ResultsView.Cast<BatchIntegrityResult>().ToArray();
        var containers = Results.ToDictionary(result => result.FilePath, StringComparer.OrdinalIgnoreCase);
        var rows = (_appSettings.BatchExportSeparatePackages
            ? visibleRows.SelectMany(result => BatchPackageRows.Create(result, true, _appSettings.BatchSummaryPreservePackageOrder))
            : visibleRows.Select(result => containers[result.FilePath]).DistinctBy(result => result.FilePath, StringComparer.OrdinalIgnoreCase).Select(result => BatchPackageSummaryOrder.Apply(result, _appSettings.BatchSummaryPreservePackageOrder))).ToList();
        var view = new ListCollectionView(rows);
        using (view.DeferRefresh())
        {
            foreach (var sort in ResultsView.SortDescriptions) view.SortDescriptions.Add(sort);
            if (GroupFiles) view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(BatchIntegrityResult.SourceGroupKey)) { StringComparison = StringComparison.OrdinalIgnoreCase });
        }
        return view.Cast<BatchIntegrityResult>().ToArray();
    }
    public ICollectionView ResultsView { get; }
    public bool GroupFiles
    {
        get => _groupFiles;
        set
        {
            if (_groupFiles == value) return;
            _groupFiles = value;
            using (ResultsView.DeferRefresh())
            {
                ResultsView.GroupDescriptions.Clear();
                if (value) ResultsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(BatchIntegrityResult.SourceGroupKey)) { StringComparison = StringComparison.OrdinalIgnoreCase });
            }
            NotifyPropertyChanged();
        }
    }
    public RelayCommand BrowseSphairaCommand { get; }
    public RelayCommand BrowseFirmwareZipCommand { get; }
    private BatchIntegrityResult? _selectedResult;
    public BatchIntegrityResult? SelectedResult
    {
        get => _selectedResult;
        set
        {
            if (value is { PackageIndex: 0, HasMultiplePackages: true, IsFirmware: false, IsNand: false } && Results.Any(result => ReferenceEquals(result, value))) value = FindPresentationResult(value.FilePath, 0) ?? value;
            if (ReferenceEquals(_selectedResult, value)) return;
            _selectedResult = value;
            StoredOverview = value is { IsFirmware: false, IsNand: false } ? new StoredFileOverviewViewModel(value, _serviceProvider, ReadStoredIcon) : null;
            NotifyPropertyChanged(nameof(StoredOverview));
            NotifyPropertyChanged(nameof(StoredPackages));
            NotifyPropertyChanged(nameof(HasStoredPackages));
            SelectedStoredPackage = value?.Packages.FirstOrDefault();
            SelectedDetailsTabIndex = value?.IsNand == true && _serviceProvider.GetRequiredService<IAppSettings>().NandActive ? 2 : value?.IsFirmware == true && _serviceProvider.GetRequiredService<IAppSettings>().FirmwareCheckActive ? 1 : 0;
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
    public RelayCommand ScanAndVerifyCommand { get; }
    public RelayCommand VerifyAllCommand { get; }
    public bool IncludeArchives { get; set; } = true;
    public SdBatchSourceMode SdSourceMode
    {
        get => _appSettings.SdBatchSourceMode;
        set { _appSettings.SdBatchSourceMode = value; NotifyPropertyChanged(); }
    }
    public RelayCommand CheckNamingCommand { get; }
    public RelayCommand RenameAllCommand { get; }
    public RelayCommand ExportCommand { get; }
    public RelayCommand MoveValidCommand { get; }
    public RelayCommand CompressValidCommand { get; }
    public RelayCommand DecompressValidCommand { get; }
    public bool IncludeSubdirectories { get; set; } = true;
    public StoredFileOverviewViewModel? StoredOverview { get; private set; }
    public bool ShowStoredOverview => !_closed && PreviewOverview == null && SelectedResult is { IsFirmware: false, IsNand: false };
    public IReadOnlyList<KeyValuePair<string, string>> StoredOverviewFields => ShowStoredOverview
        ? (HasStoredPackages ? BatchStoredOverview.BuildFile(SelectedResult!, LocalizationManager.Instance.Current.Keys)
            : BatchStoredOverview.Build(SelectedResult!, LocalizationManager.Instance.Current.Keys)) : Array.Empty<KeyValuePair<string, string>>();
    public IReadOnlyList<BatchPackageSummary> StoredPackages => SelectedResult?.Packages ?? Array.Empty<BatchPackageSummary>();
    public bool HasStoredPackages => StoredPackages.Count > 0;
    private BatchPackageSummary? _selectedStoredPackage;
    public BatchPackageSummary? SelectedStoredPackage
    {
        get => _selectedStoredPackage;
        set { _selectedStoredPackage = value; NotifyPropertyChanged(); NotifyPropertyChanged(nameof(StoredPackageFields)); }
    }
    public IReadOnlyList<KeyValuePair<string, string>> StoredPackageFields => SelectedStoredPackage is { } package
        ? BatchStoredOverview.BuildPackage(package, LocalizationManager.Instance.Current.Keys) : Array.Empty<KeyValuePair<string, string>>();
    public FileOverviewViewModel? PreviewOverview
    {
        get => _previewOverview;
        private set { if (value != null && SelectedResult is { PackageIndex: > 0 } selected) value.SelectedCnmtContainer = value.CnmtContainers.ElementAtOrDefault(selected.PackageIndex - 1) ?? value.SelectedCnmtContainer; _previewOverview = value; NotifyPropertyChanged(); NotifyPropertyChanged(nameof(ShowStoredOverview)); NotifyPropertyChanged(nameof(StoredOverviewFields)); }
    }

    private string _searchText = "";
    private string _fileTypeFilter = "";
    private string _integrityFilter = "";
    private string _namingFilter = "";
    public string NamingFilter { get => _namingFilter; set { _namingFilter = value ?? ""; NotifyPropertyChanged(); ResultsView.Refresh(); } }
    public IReadOnlyList<BatchNamingFilterOption> NamingFilters { get; } = new[] { "", "Matches", "Differs", "Unchecked", "Error" }.Select(value => new BatchNamingFilterOption(value)).ToArray();
    public string SearchText { get => _searchText; set { _searchText = value ?? ""; NotifyPropertyChanged(); ResultsView.Refresh(); } }
    public string FileTypeFilter { get => _fileTypeFilter; set { _fileTypeFilter = value ?? ""; NotifyPropertyChanged(); ResultsView.Refresh(); } }
    public string IntegrityFilter { get => _integrityFilter; set { _integrityFilter = value ?? ""; NotifyPropertyChanged(); ResultsView.Refresh(); } }
    public IReadOnlyList<BatchFilterOption> FileTypeFilters => new[] { "", "Games", "NSP", "NSZ", "XCI", "XCZ", "NRO", "NAX0", "NAND", "ZIP", "7Z", "Folder" }.Where(value => value != "NAND" || _serviceProvider.GetRequiredService<IAppSettings>().NandActive).Select(value => new BatchFilterOption(value)).ToArray();
    public IReadOnlyList<BatchFilterOption> IntegrityFilters { get; } = new[] { new BatchFilterOption("") }.Concat(Enum.GetValues<NcasIntegrity>().Select(value => new BatchFilterOption(value.ToString()))).ToArray();
    public RelayCommand ResetFiltersCommand => new(() => { SearchText = ""; FileTypeFilter = ""; IntegrityFilter = ""; NamingFilter = ""; ShowOnlyErrors = false; });
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
        set { _inputDirectory = value; _sourcePaths = Array.Empty<string>(); NotifyPropertyChanged(); NotifyPropertyChanged(nameof(SourcePaths)); StartCommand.TriggerCanExecuteChanged(); ScanAndVerifyCommand.TriggerCanExecuteChanged(); }
    }

    private void Browse()
    {
        var directory = _promptService.PromptSelectDir(LocalizationManager.Instance.Current.Keys.BatchIntegrity_SelectDirectory);
        if (directory != null) InputDirectory = directory;
    }

    private bool CanStart() => !BackgroundTask.IsRunning && (_sourcePaths.Length > 0
        ? _sourcePaths.Any(path => Directory.Exists(path) || File.Exists(path))
        : Directory.Exists(InputDirectory) || (File.Exists(InputDirectory) && PackageZip.IsArchive(InputDirectory)));

    private async void Start(bool verifyIntegrity, bool rescan = false, bool resume = false,
        bool explorerRequest = false, bool append = false, string[]? sourcesToScan = null)
    {
        if (!resume && !append && (!verifyIntegrity || rescan)) DeleteTemporaryHistory();
        if (!explorerRequest) _explorerSources = Array.Empty<string>();
        RecordMemory("scan-start");
        var paths = verifyIntegrity && !rescan && !resume ? Results.Select(r => r.FilePath).ToArray() : null;
        if (!resume)
        {
            if (!append) _fingerprints.Clear();
            _session = new BatchHistoryEntry((append || (verifyIntegrity && !rescan)) && _session != null ? _session.Id : Guid.NewGuid(), DateTime.UtcNow, InputDirectory, IncludeSubdirectories, IncludeArchives,
                verifyIntegrity, _appSettings.IgnoreMissingDeltaFragments, "Running", Array.Empty<BatchIntegrityResult>(), new()) { SourcePaths = _sourcePaths.ToArray() };
        }

        var checkpoint = _session;
        var state = "Completed";
        if (!append && !resume && (!verifyIntegrity || rescan))
        {
            Results.Clear();
            _completedPreviews.Clear();
        }
        SelectedResult = null;
        ClearPreview();
        ExportCommand.TriggerCanExecuteChanged();
        try
        {
            Func<string, bool>? skip = resume ? path => checkpoint?.CanSkip(path, _appSettings.IgnoreMissingDeltaFragments) == true : null;
            var scanSources = sourcesToScan ?? _sourcePaths;
            IRunnable<IReadOnlyList<BatchIntegrityResult>> runnable = paths == null && scanSources.Length > 0
                ? new VerifyBatchSourcesRunnable(scanSources, () => _serviceProvider.GetRequiredService<IVerifyDirectoryIntegrityRunnable>(),
                    IncludeSubdirectories, IncludeArchives, verifyIntegrity, ShowPreview, ShowCompletedResult, skip)
                : _serviceProvider.GetRequiredService<IVerifyDirectoryIntegrityRunnable>()
                    .Setup(InputDirectory, IncludeSubdirectories, ShowPreview, ShowCompletedResult, IncludeArchives, verifyIntegrity, paths, skip);
            await BackgroundTask.RunAsync(runnable);
        }
        catch (OperationCanceledException) { state = "Interrupted"; }
        catch (Exception ex) { state = "Interrupted"; _logger.LogError(ex, "Batch integrity check failed."); }
        finally
        {
            _completedPreviews.Clear();
            ClearPreview();
            SaveHistory(state, true);
            RecordMemory("scan-" + state.ToLowerInvariant());
            if (_closed) { ClearPreview(); DeleteTemporaryHistory(); }
            VerifyAllCommand.TriggerCanExecuteChanged();
            ExportCommand.TriggerCanExecuteChanged();
            MoveValidCommand.TriggerCanExecuteChanged();
            CheckNamingCommand.TriggerCanExecuteChanged();
            RenameAllCommand.TriggerCanExecuteChanged();
        }
    }

    private void OnHistorySettingChanged(object? sender, PropertyChangedEventArgs args)
    {
        NotifyPropertyChanged(nameof(IsHistoryEnabled));
        NotifyPropertyChanged(nameof(HistoryLimit));
        if (!IsHistoryEnabled) { History.Clear(); SelectedHistory = null; _session = null; _fingerprints.Clear(); }
        else ReloadHistory();
    }
    private void ReloadHistory()
    {
        if (!IsHistoryEnabled) return;
        try
        {
            _refreshingHistory = true;
            var selectedId = SelectedHistory?.Id;
            History.Clear();
            History.Add(EmptyHistory);
            foreach (var entry in _historyStore.Load(HistoryLimit)) History.Add(entry);
            SelectedHistory = History.FirstOrDefault(e => e.Id == selectedId) ?? EmptyHistory;
            NotifyPropertyChanged(nameof(SelectedHistory));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException)
        { _logger.LogWarning(ex, "Could not load batch history."); }
        finally { _refreshingHistory = false; }
    }
    private void SaveHistory(string state, bool force = false)
    {
        if (_session == null || (!force && DateTime.UtcNow - _lastHistorySave < TimeSpan.FromSeconds(1))) return;
        try
        {
            _session = _session with { UpdatedUtc = DateTime.UtcNow, State = state, IgnoreMissingDeltaFragments = _appSettings.IgnoreMissingDeltaFragments, Results = Results.ToArray(),
                Fingerprints = new Dictionary<string, string>(_fingerprints, StringComparer.OrdinalIgnoreCase) };
            try
            {
                if (!_closed)
                {
                    var references = _session.Results.SelectMany(result => result.Titles
                        .Concat(result.Packages.SelectMany(package => package.Titles)))
                        .Select(title => title.Icon).OfType<BatchIconReference>().Distinct().ToArray();
                    if (IsHistoryEnabled && _appSettings.SaveBatchHistoryLogos)
                    {
                        // Also supports enabling logo persistence after this scan was read.
                        foreach (var icon in references)
                            if (_activeLogos?.ReadIcon(icon) is { } bytes) _historyStore.StageIcon(icon.EntryId, bytes);
                    }
                    else
                    {
                        // Preserve current-session logos before removing them from a saved ZIP.
                        foreach (var icon in references)
                            if (_activeLogos?.Contains(icon) != true && _historyStore.ReadIcon(icon) is { } bytes)
                                ActiveLogos.StageIcon(icon.EntryId, bytes);
                    }
                    if (_activeLogos?.Flush() == true)
                        _logger.LogWarning("The temporary active-scan logo ZIP was unreadable and has been rebuilt. Previously cached logos may be unavailable; scan metadata is retained.");
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                _logger.LogWarning(ex, "Unable to update temporary scan logos; continuing to save metadata.");
            }
            OverviewStore.Save(_session, IsHistoryEnabled ? HistoryLimit : 1,
                includeLogos: IsHistoryEnabled && _appSettings.SaveBatchHistoryLogos);
            _logger.LogTrace("Saved history {ScanId}; results={Count}; persistentLogos={IncludeLogos}", _session.Id, Results.Count, IsHistoryEnabled && _appSettings.SaveBatchHistoryLogos);
            _lastHistorySave = DateTime.UtcNow;
            if (IsHistoryEnabled) ReloadHistory();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException)
        { _logger.LogWarning(ex, "Could not save batch history."); }
    }
    private void ResumeHistory()
    {
        if (SelectedHistory == null || SelectedHistory.Id == Guid.Empty || !(SelectedHistory.SourcePaths.Length > 0 ? SelectedHistory.SourcePaths : new[] { SelectedHistory.Source })
            .Any(path => Directory.Exists(path) || File.Exists(path))) return;
        if (RestoreHistory(true)) Start(_session!.VerifyIntegrity, resume: true);
    }
    private bool RestoreHistory(bool prepareResume = false)
    {
        if (!IsHistoryEnabled || BackgroundTask.IsRunning || SelectedHistory == null || SelectedHistory.Id == Guid.Empty) return false;
        var saved = SelectedHistory;
        _logger.LogTrace("Restoring history {ScanId}; results={Count}; resume={Resume}", saved.Id, saved.Results.Length, prepareResume);
        DeleteTemporaryHistory();
        _explorerSources = Array.Empty<string>();

        InputDirectory = saved.Source;
        _sourcePaths = saved.SourcePaths.ToArray();
        NotifyPropertyChanged(nameof(SourcePaths));
        StartCommand.TriggerCanExecuteChanged(); ScanAndVerifyCommand.TriggerCanExecuteChanged();
        IncludeArchives = saved.IncludeArchives; IncludeSubdirectories = saved.IncludeSubdirectories;
        NotifyPropertyChanged(nameof(IncludeArchives)); NotifyPropertyChanged(nameof(IncludeSubdirectories));
        SelectedResult = null;
        ClearPreview(); _completedPreviews.Clear(); Results.Clear(); _fingerprints.Clear();
        foreach (var result in saved.Results)
            Results.Add(!prepareResume || saved.CanSkip(result.FilePath, _appSettings.IgnoreMissingDeltaFragments) ? result :
                result with { Integrity = NcasIntegrity.Unchecked });
        foreach (var pair in saved.Fingerprints) _fingerprints[pair.Key] = pair.Value;
        _session = saved;
        SelectedResult = ResultsView.Cast<BatchIntegrityResult>().FirstOrDefault();
        ExportCommand.TriggerCanExecuteChanged(); VerifyAllCommand.TriggerCanExecuteChanged();
        MoveValidCommand.TriggerCanExecuteChanged();
        RecordMemory("history-loaded");
        return true;
    }
    private void ShowPreview(NxFile file)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            _previewFile?.Dispose();
            _previewFile = file;
            _logger.LogTrace("Creating live overview for {FilePath}", file.FilePath);
            if (_closed) return;
            // Display the current file until its detached snapshot is published.
            _completedPreviews.Clear();
            _completedPreviews[file.FilePath] = new FileOverviewViewModel(file.Overview, _serviceProvider);
            SelectedResult = Results.FirstOrDefault(result => result.FilePath.Equals(file.FilePath, StringComparison.OrdinalIgnoreCase));
            SelectedDetailsTabIndex = 0;
            PreviewOverview = _completedPreviews[file.FilePath];
        });
    }

    private void ShowCompletedResult(BatchIntegrityResult result)
    {
        _logger.LogTrace("Publishing detached metadata snapshot for {FilePath}", result.FilePath);
        Application.Current.Dispatcher.Invoke(() =>
        {
            // The cached model observes integrity changes from the verifier.
            // Publish it before adding a row, which can trigger selection bindings.
            if (!result.IsFirmware && !result.IsNand && _completedPreviews.TryGetValue(result.FilePath, out var completed) &&
                SelectedResult?.FilePath.Equals(result.FilePath, StringComparison.OrdinalIgnoreCase) == true)
                PreviewOverview = completed;
            if (!result.IsFirmware && !result.IsNand && _completedPreviews.TryGetValue(result.FilePath, out var overview))
            {
                var packages = overview.CnmtContainers;
                string Join(Func<CnmtContainerViewModel, string?> selector) => string.Join(" / ",
                    packages.Select(selector).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct());
                result = result with
                {
                    Title = Join(package => package.Titles.FirstOrDefault()?.AppName),
                    TitleId = Join(package => package.TitleId),
                    Packages = packages.Select(package => new BatchPackageSummary(
                        package.Titles.FirstOrDefault()?.AppName ?? "—", package.TitleId,
                        BatchContentMetadata.Types(new[] { Enum.Parse<LibHac.Ncm.ContentMetaType>(package.Type) }), package.TitleVersion ?? "—")
                        {
                            Titles = package.Titles.Select(title => SnapshotTitle(title.Title)).ToArray(),
                            SystemVersion = package.MinimumSystemVersion ?? "", BaseTitleId = package.BaseTitleId,
                            MasterKey = package.MasterKey, MinimumApplicationVersion = package.MinimumApplicationVersion ?? "",
                            SecurityDetails = package.SecurityDetails, PatchLevel = package.PatchLevel,
                            Distribution = package.Distribution,
                            BuildId = package.BuildID, IsDemo = package.DemoStatus, Permission = package.SecurityAssessment,
                            FileSystemPermissions = package.FileSystemPermissions, AcidSignatureValidity = package.AcidSignatureValidity,
                            Publisher = package.Titles.FirstOrDefault()?.Publisher ?? "", DisplayVersion = package.DisplayVersion ?? "",
                            Languages = string.Join(", ", package.Titles.Select(title => title.Language.ToString()).Distinct())
                        }).ToArray(),
                    BaseTitleId = Join(package => package.BaseTitleId),
                    MinimumApplicationVersion = Join(package => package.MinimumApplicationVersion),
                    IsDemo = packages.Any(p => p.DemoStatus == true) ? true : packages.Count > 0 && packages.All(p => p.DemoStatus == false) ? false : null,
                    FileSystemPermissions = Join(package => package.FileSystemPermissions),
                    AcidSignatureValidity = Join(package => package.AcidSignatureValidity),
                    Publisher = Join(package => package.Titles.FirstOrDefault()?.Publisher),
                    Version = Join(package => package.TitleVersion),
                    DisplayVersion = Join(package => package.DisplayVersion),
                    SystemVersion = Join(package => package.MinimumSystemVersion),
                    MasterKey = Join(package => package.MasterKey),
                    BuildId = Join(package => package.BuildID),
                    Distribution = Join(package => package.Distribution),
                    Languages = string.Join(", ", packages.SelectMany(package => package.Titles).Select(title => title.Language.ToString()).Distinct()),
                    FileSize = result.FileSize,
                    CompressionRatio = result.CompressionRatio,
                    EstimatedUncompressedSize = _previewFile?.Overview.EstimatedUncompressedSize,
                    InstalledTitlesWithoutMeta = overview.InstalledTitleFallbacks.ToArray()
                };
                if (overview.IsHomebrew)
                    result = result with { Title = overview.HomebrewTitle?.AppName ?? "", Publisher = overview.HomebrewTitle?.Publisher ?? "",
                        DisplayVersion = overview.HomebrewVersion, BuildId = overview.HomebrewBuildId, Languages = overview.HomebrewLanguages, Distribution = "Homebrew",
                        Titles = _previewFile?.RootItem is Models.TreeItems.Impl.NroFileItem nro ? nro.Titles.Select(SnapshotTitle).ToArray() : result.Titles };
                else if (result.FileType == "NAX0")
                {
                    result = result with { Distribution = "Filesystem" };
                    if (packages.Count == 0)
                        result = result with { Title = string.Join(" / ", overview.InstalledTitleFallbacks.Select(t => t.Title)),
                            TitleId = string.Join(" / ", overview.InstalledTitleFallbacks.Select(t => t.TitleId)),
                            Publisher = string.Join(" / ", overview.InstalledTitleFallbacks.Select(t => t.Publisher)),
                            DisplayVersion = string.Join(" / ", overview.InstalledTitleFallbacks.Select(t => t.DisplayVersion)) };
                }
            }
            var existing = Results.FirstOrDefault(r => r.FilePath.Equals(result.FilePath, StringComparison.OrdinalIgnoreCase));
            if (existing != null) Results[Results.IndexOf(existing)] = result;
            else Results.Add(result);
            var fingerprint = result.SourceFingerprint;
            if (fingerprint != null) _fingerprints[result.FilePath] = fingerprint;
            else _fingerprints.Remove(result.FilePath);
            // Only compact metadata and compressed icon references survive completion.
            _completedPreviews.Clear();
            ClearPreview();
            SaveHistory("Running");
            SelectedResult = result;
            if (Results.Count % 100 == 0) RecordMemory("scan-progress");
            ExportCommand.TriggerCanExecuteChanged();
            MoveValidCommand.TriggerCanExecuteChanged();
            CheckNamingCommand.TriggerCanExecuteChanged();
            RenameAllCommand.TriggerCanExecuteChanged();
        });
    }

    private void ClearPreview()
    {
        _logger.LogTrace("Releasing live batch preview: {FilePath}; cachedOverviews={Count}", _previewFile?.FilePath, _completedPreviews.Count);
        _previewFile?.Dispose();
        _previewFile = null;
        PreviewOverview = null;
    }

    public void ClosePreview()
    {
        RecordMemory("window-closing");
        PropertyChangedEventManager.RemoveHandler(_appSettings, OnDiagnosticSettingChanged, nameof(IAppSettings.LogLevel));
        _closed = true;
        DeleteActiveLogos();
        _completedPreviews.Clear();
        BackgroundTask.PropertyChanged -= BackgroundTaskOnPropertyChanged;
        PropertyChangedEventManager.RemoveHandler(_appSettings, OnPackageDisplaySettingChanged, nameof(IAppSettings.BatchSeparatePackages));
        PropertyChangedEventManager.RemoveHandler(_appSettings, OnPackageDisplaySettingChanged, nameof(IAppSettings.BatchSummaryPreservePackageOrder));
        // The checking file remains in use until the verifier task ends.
        if (!BackgroundTask.IsRunning) { ClearPreview(); DeleteTemporaryHistory(); }
    }

    private void DeleteTemporaryHistory()
    {
        DeleteActiveLogos();
        try { if (Directory.Exists(_temporaryHistoryRoot)) Directory.Delete(_temporaryHistoryRoot, true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { _logger.LogWarning(ex, "Could not remove temporary overview cache."); }
    }
    private void UpdateSelectedPreview()
    {
        // Selection is served entirely by StoredOverview, including in the current scan.
        // Opening a source remains an explicit action (OpenInSingle / VerifySelected).
        _logger.LogTrace("Selecting stored overview for {FilePath}; source file will not be reopened", SelectedResult?.FilePath);
        PreviewOverview = null;
    }
    public bool NszPluginActive => _serviceProvider.GetRequiredService<IAppSettings>().NszPluginActive;
    public bool CanActOnFile(BatchIntegrityResult file, NszOperation? operation = null) => (operation == null || _serviceProvider.GetRequiredService<IAppSettings>().NszPluginActive) && !BackgroundTask.IsRunning &&
        !file.IsFirmware && !file.IsNand && file.FileType != "NAX0" && !file.HasMissingKeys && !Services.Mtp.MtpDirectSource.IsDirect(file.FilePath) && File.Exists(file.FilePath) &&
        (operation == null || PackageConversionService.Supports(file.FilePath, operation.Value));
    public bool CanCheckNaming(BatchIntegrityResult file) => !BackgroundTask.IsRunning &&
        !file.IsFirmware && !file.IsNand && !file.HasMissingKeys &&
        !Services.Mtp.MtpDirectSource.IsDirect(file.FilePath) && File.Exists(file.FilePath) && Path.GetExtension(file.FilePath).ToLowerInvariant() is ".nsp" or ".nsz" or ".xci" or ".xcz";
    private bool CanProcessNaming() => Results.Any(CanCheckNaming);
    public void CheckSelectedNaming(BatchIntegrityResult file) => ProcessNaming(false, file);
    public void RenameSelected(BatchIntegrityResult file) => ProcessNaming(true, file);

    private void OnNamingSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        foreach (var result in Results.ToArray())
            if (result.NamingMatches != null || result.NamingError != null)
                Results[Results.IndexOf(result)] = result with { NamingMatches = null, ProposedPath = null, NamingError = null };
    }

    private async void ProcessNaming(bool rename, BatchIntegrityResult? selected = null)
    {
        if (BackgroundTask.IsRunning) return;
        if (selected != null) selected = Results.FirstOrDefault(result => result.FilePath.Equals(selected.FilePath, StringComparison.OrdinalIgnoreCase)) ?? selected;
        var files = (selected == null ? Results.AsEnumerable() : new[] { selected }).Where(CanCheckNaming).ToArray();
        if (files.Length == 0) return;
        RenamingResult? selectedOutcome = null;
        try
        {
            var options = _appSettings.RenamingOptions;
            var parser = _serviceProvider.GetRequiredService<INamingPatternsParser>();
            var settings = new NamingSettings
            {
                ApplicationPattern = parser.ParseApplicationPattern(options.ApplicationPattern),
                PatchPattern = parser.ParsePatchPattern(options.PatchPattern),
                AddonPattern = parser.ParseAddonPattern(options.AddonPattern),
                TargetDirectory = options.TargetDirectory,
                InvalidFileNameCharsReplacement = options.InvalidFileNameCharsReplacement,
                ReplaceWhiteSpaceChars = options.ReplaceWhiteSpaceChars,
                WhiteSpaceCharsReplacement = options.WhiteSpaceCharsReplacement
            };
            var autoClose = options.AutoCloseOpenedFile;
            var service = _serviceProvider.GetRequiredService<IFileRenamerService>();
            var runnable = new RunnableRelay((progress, token) =>
            {
                for (var i = 0; i < files.Length; i++)
                {
                    token.ThrowIfCancellationRequested();
                    var original = files[i];
                    progress.SetText(original.FileName);
                    var outcome = service.RenameFileAsync(original.FilePath, autoClose, settings, true, _logger, token).GetAwaiter().GetResult();
                    token.ThrowIfCancellationRequested();
                    if (rename && outcome.Exception == null && outcome.IsRenamed)
                    {
                        if (selected != null)
                        {
                            var confirmed = Application.Current.Dispatcher.Invoke(() =>
                                ThemedDialog.Confirm(LocalizationManager.Instance.Current.Keys.RenamingTool_Button_Rename,
                                    NamingOutcomeMessage(outcome), allowNo: false) == MessageBoxResult.Yes);
                            if (!confirmed) throw new OperationCanceledException();
                            token.ThrowIfCancellationRequested();
                        }
                        outcome = service.RenameFileAsync(original.FilePath, autoClose, settings, false, _logger, token).GetAwaiter().GetResult();
                    }
                    if (selected != null) selectedOutcome = outcome;
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        var index = Results.IndexOf(original);
                        if (index < 0) return;
                        var moved = rename && outcome.Exception == null && outcome.IsRenamed;
                        var path = moved ? outcome.NewFilePath! : original.FilePath;
                        var updated = original with
                        {
                            FilePath = path,
                            NamingMatches = outcome.Exception != null ? null : rename || !outcome.IsRenamed,
                            ProposedPath = outcome.NewFilePath,
                            NamingError = outcome.Exception?.Message,
                            SourceFingerprint = moved ? BatchHistoryStore.Fingerprint(path) : original.SourceFingerprint
                        };
                        if (moved)
                        {
                            if (_completedPreviews.Remove(original.FilePath, out var preview)) _completedPreviews[path] = preview;
                            _fingerprints.Remove(original.FilePath);
                            if (updated.SourceFingerprint != null) _fingerprints[path] = updated.SourceFingerprint;
                        }
                        var wasSelected = ReferenceEquals(SelectedResult, original);
                        Results[index] = updated;
                        if (wasSelected) SelectedResult = updated;
                    });
                    progress.SetPercentage((i + 1d) / files.Length);
                }
            }) { SupportsCancellation = true, SupportProgress = true };
            await BackgroundTask.RunAsync(runnable);
            if (selectedOutcome != null)
                ThemedDialog.Notice(NamingOutcomeMessage(selectedOutcome),
                    LocalizationManager.Instance.Current.Keys.BatchNaming_Check,
                    selectedOutcome.Exception == null ? MessageBoxImage.Information : MessageBoxImage.Error);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Batch naming operation failed.");
            if (selected != null)
                ThemedDialog.Notice(ex.Message, LocalizationManager.Instance.Current.Keys.BatchNaming_Error, MessageBoxImage.Error);
            foreach (var file in files)
            {
                var index = Results.IndexOf(file);
                if (index >= 0) Results[index] = file with { NamingMatches = null, NamingError = ex.Message };
            }
        }
        finally { SaveHistory(_session?.State ?? "Completed", force: true); }
    }

    private static string NamingOutcomeMessage(RenamingResult outcome)
    {
        var keys = LocalizationManager.Instance.Current.Keys;
        var status = outcome.Exception != null ? keys.BatchNaming_Error
            : outcome.IsSimulation ? (outcome.IsRenamed ? keys.BatchNaming_Differs : keys.BatchNaming_Matches)
            : outcome.Status;
        return $"{status}\n\n{keys.RenamingTool_OldName}:\n{outcome.OldFilePath}\n\n{keys.RenamingTool_NewName}:\n{outcome.NewFilePath ?? outcome.OldFilePath}"
            + (outcome.Exception == null ? "" : $"\n\n{outcome.Exception.Message}");
    }

    public bool CanOpenLocation(BatchIntegrityResult file)
    {
        var path = PackageZip.ArchivePath(file.FilePath);
        return File.Exists(path) || Directory.Exists(path);
    }
    public void OpenLocation(BatchIntegrityResult file)
    {
        if (!CanOpenLocation(file)) return;
        _serviceProvider.GetRequiredService<Services.FileLocationOpening.IFileLocationOpenerService>()
            .OpenFileLocationSafe(PackageZip.ArchivePath(file.FilePath));
    }

    public bool CanOpenInSingle(BatchIntegrityResult file) => (!file.IsNand || _serviceProvider.GetRequiredService<IAppSettings>().NandActive) && !BackgroundTask.IsRunning &&
        (File.Exists(PackageZip.ArchivePath(file.FilePath)) || SdCardSource.FindContents(file.FilePath) != null);
    public async void OpenInSingle(BatchIntegrityResult file)
    {
        if (!CanOpenInSingle(file)) return;
        ClearPreview();
        await _serviceProvider.GetRequiredService<Emignatik.NxFileViewer.Services.FileOpening.IFileOpeningService>().SafeOpenFile(file.FilePath);
    }
    public void ConvertSelected(BatchIntegrityResult file, NszOperation operation) => ConvertValid(operation, file);
    public void MoveSelected(BatchIntegrityResult file) => MoveFiles(file);
    public bool CanOpenTitle(BatchIntegrityResult file) => !BackgroundTask.IsRunning &&
        ulong.TryParse(file.TitleId.Split('/')[0].Trim(), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out _);
    public void OpenSelectedTitle(BatchIntegrityResult file)
    {
        if (!CanOpenTitle(file)) return;
        try { _serviceProvider.GetRequiredService<Emignatik.NxFileViewer.Services.OnlineServices.IOnlineTitlePageOpenerService>()
            .OpenTitlePage(file.TitleId.Split('/')[0].Trim()); }
        catch (Exception ex) { _logger.LogError(ex, "Failed to open title website."); }
    }
    public async void VerifySelected(BatchIntegrityResult file)
    {
        if (file.IsNand || BackgroundTask.IsRunning || (!Directory.Exists(file.FilePath) && !File.Exists(PackageZip.ArchivePath(file.FilePath)))) return;
        try
        {
            var runnable = _serviceProvider.GetRequiredService<IVerifyDirectoryIntegrityRunnable>()
                .Setup(InputDirectory, IncludeSubdirectories, ShowPreview, ShowCompletedResult, true, true, new[] { file.FilePath });
            await BackgroundTask.RunAsync(runnable);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _logger.LogError(ex, "Failed to verify selected file."); }
        finally
        {
            _completedPreviews.Clear();
            ClearPreview();
            SaveHistory(_session?.State ?? "Completed", true);
            if (_closed) DeleteTemporaryHistory();
        }
    }
    private bool CanMoveValid() => !BackgroundTask.IsRunning &&
        Results.Any(result => !result.IsFirmware && !result.IsNand && result.FileType != "NAX0" && result.Integrity == NcasIntegrity.Original && !Services.Mtp.MtpDirectSource.IsDirect(result.FilePath) && File.Exists(result.FilePath));

    private bool CanConvertValid(NszOperation operation) => _serviceProvider.GetRequiredService<IAppSettings>().NszPluginActive && !BackgroundTask.IsRunning && Results.Any(result =>
        !result.IsFirmware && !result.IsNand && result.Integrity == NcasIntegrity.Original && !Services.Mtp.MtpDirectSource.IsDirect(result.FilePath) && File.Exists(result.FilePath) &&
        PackageConversionService.Supports(result.FilePath, operation));

    private async void ConvertValid(NszOperation operation, BatchIntegrityResult? selected = null)
    {
        if (selected == null ? !CanConvertValid(operation) : !CanActOnFile(selected, operation)) return;
        var paths = (selected == null ? Results.AsEnumerable() : new[] { selected }).Where(result => !result.IsFirmware && !result.IsNand && (selected != null || result.Integrity == NcasIntegrity.Original) &&
            !Services.Mtp.MtpDirectSource.IsDirect(result.FilePath) && File.Exists(result.FilePath) && PackageConversionService.Supports(result.FilePath, operation))
            .Select(result => result.FilePath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        ClearPreview();
        var attempts = await _serviceProvider.GetRequiredService<NszActions>().ConvertFilesAsync(paths, operation, selected == null ? SourceRoot : null);
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

    private void MoveValid() => MoveFiles(null);
    private async void MoveFiles(BatchIntegrityResult? selected)
    {
        if (BackgroundTask.IsRunning || (selected != null && !CanActOnFile(selected))) return;
        var destinationRoot = _promptService.PromptSelectDir(
            LocalizationManager.Instance.Current.Keys.BatchIntegrity_SelectMoveDestination);
        if (destinationRoot == null) return;

        var sourceRoot = selected == null ? SourceRoot : Path.GetDirectoryName(Path.GetFullPath(selected.FilePath));
        var destinationRootFull = Path.GetFullPath(destinationRoot);
        var opening = _serviceProvider.GetRequiredService<Emignatik.NxFileViewer.Services.FileOpening.IFileOpeningService>();
        if (opening.OpenedFile != null && (selected == null
            ? Results.Any(file => file.FilePath.Equals(opening.OpenedFile.FilePath, StringComparison.OrdinalIgnoreCase))
            : selected.FilePath.Equals(opening.OpenedFile.FilePath, StringComparison.OrdinalIgnoreCase))) opening.SafeClose();
        ClearPreview();
        var validFiles = (selected == null ? Results.AsEnumerable() : new[] { selected }).Where(result =>
            !result.IsFirmware && !result.IsNand && result.FileType != "NAX0" && (selected != null || result.Integrity == NcasIntegrity.Original) && !Services.Mtp.MtpDirectSource.IsDirect(result.FilePath) && File.Exists(result.FilePath)).ToArray();
        var runnable = new RunnableRelay<int>((reporter, cancellationToken) =>
        {
            var moved = 0;
            for (var index = 0; index < validFiles.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var source = validFiles[index].FilePath;
                reporter.SetText($"{LocalizationManager.Instance.Current.Keys.BatchIntegrity_Moving} {index + 1}/{validFiles.Length}: {Path.GetFileName(source)}");
                var relativePath = sourceRoot == null ? Path.GetFileName(source) : Path.GetRelativePath(sourceRoot, source);
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
            foreach (var movedResult in Results.Where(result => !result.IsFirmware && !result.IsNand && !!Services.Mtp.MtpDirectSource.IsDirect(result.FilePath) && File.Exists(result.FilePath)).ToArray())
                Results.Remove(movedResult);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _logger.LogError(ex, "Failed to move verified files."); }
        finally
        {
            ExportCommand.TriggerCanExecuteChanged();
            MoveValidCommand.TriggerCanExecuteChanged();
            CheckNamingCommand.TriggerCanExecuteChanged();
            RenameAllCommand.TriggerCanExecuteChanged();
        }
    }

    private void Export()
    {
        var keys = LocalizationManager.Instance.Current.Keys;
        var excel = _appSettings.BatchExportExcel;
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            FileName = "integrity-results",
            InitialDirectory = _appSettings.LastUsedDir,
            DefaultExt = excel ? ".xlsx" : ".csv",
            Title = keys.BatchExport_Title,
            Filter = "CSV (*.csv)|*.csv|Excel (*.xlsx)|*.xlsx",
            FilterIndex = excel ? 2 : 1,
            AddExtension = true
        };
        if (dialog.ShowDialog() != true) return;
        var path = dialog.FileName;
        var columns = BatchResultExport.SelectColumns(_appSettings.BatchExportVisibleColumnsOnly, ColumnLayout);
        if (columns.Count == 0)
        {
            ThemedDialog.Notice(keys.BatchExport_NoColumns, keys.BatchExport_Title, MessageBoxImage.Information);
            return;
        }
        string? temporary = null;
        try
        {
            temporary = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path))!, "." + Guid.NewGuid().ToString("N") + ".tmp");
            using (var stream = File.Create(temporary))
            {
                if (Path.GetExtension(path).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
                    BatchResultExport.WriteExcel(stream, GetExportRows(), columns);
                else if (Path.GetExtension(path).Equals(".csv", StringComparison.OrdinalIgnoreCase))
                    BatchResultExport.WriteCsv(stream, GetExportRows(), columns);
                else throw new InvalidOperationException(keys.BatchExport_InvalidExtension);
            }
            File.Move(temporary, path, overwrite: true);
            _appSettings.LastUsedDir = Path.GetDirectoryName(path)!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Batch export failed.");
            ThemedDialog.Notice(ex.Message, keys.BatchExport_Title, MessageBoxImage.Error);
        }
        finally
        {
            if (temporary != null)
                try { File.Delete(temporary); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    private bool ShouldDisplayResult(object item) =>
        item is BatchIntegrityResult result && BatchResultFilter.Matches(result, SearchText, FileTypeFilter, IntegrityFilter, ShowOnlyErrors, NamingFilter);

    private void BackgroundTaskOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(IBackgroundTaskRunner.IsRunning)) return;
        MeasureMemoryCommand.TriggerCanExecuteChanged(true);
        ResumeHistoryCommand.TriggerCanExecuteChanged(true);
        StartCommand.TriggerCanExecuteChanged(true);
        ScanAndVerifyCommand.TriggerCanExecuteChanged(true);
        VerifyAllCommand.TriggerCanExecuteChanged(true);
        CheckNamingCommand.TriggerCanExecuteChanged(true);
        RenameAllCommand.TriggerCanExecuteChanged(true);
        ExportCommand.TriggerCanExecuteChanged(true);
        MoveValidCommand.TriggerCanExecuteChanged(true);
        CompressValidCommand.TriggerCanExecuteChanged(true);
        DecompressValidCommand.TriggerCanExecuteChanged(true);
    }
}

public sealed record BatchFilterOption(string Value)
{
    public string Label => Value switch
    {
        "" => LocalizationManager.Instance.Current.Keys.BatchTable_All,
        "Games" => LocalizationManager.Instance.Current.Keys.BatchTable_Games,
        _ => Value
    };
}

public sealed record BatchNamingFilterOption(string Value)
{
    public string Label => Value switch
    {
        "Matches" => LocalizationManager.Instance.Current.Keys.BatchNaming_Matches,
        "Differs" => LocalizationManager.Instance.Current.Keys.BatchNaming_Differs,
        "Error" => LocalizationManager.Instance.Current.Keys.BatchNaming_Error,
        "Unchecked" => LocalizationManager.Instance.Current.Keys.BatchNaming_Unchecked,
        _ => LocalizationManager.Instance.Current.Keys.BatchTable_All
    };
}
