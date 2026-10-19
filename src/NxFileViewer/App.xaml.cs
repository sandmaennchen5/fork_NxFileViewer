using Emignatik.NxFileViewer.Services.Updates;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Windows;
using Emignatik.NxFileViewer.Commands;
using Emignatik.NxFileViewer.FileLoading;
using Emignatik.NxFileViewer.FileLoading.QuickFileInfoLoading;
using Emignatik.NxFileViewer.Localization;
using Emignatik.NxFileViewer.Logging;
using Emignatik.NxFileViewer.Services.BackgroundTask;
using Emignatik.NxFileViewer.Services.BackgroundTask.RunnableImpl;
using Emignatik.NxFileViewer.Services.FileLocationOpening;
using Emignatik.NxFileViewer.Services.FileOpening;
using Emignatik.NxFileViewer.Services.FileAssociations;
using Emignatik.NxFileViewer.Services.FileRenaming;
using Emignatik.NxFileViewer.Services.GlobalEvents;
using Emignatik.NxFileViewer.Services.Integrity;
using Emignatik.NxFileViewer.Services.Nsz;
using Emignatik.NxFileViewer.Services.KeysManagement;
using Emignatik.NxFileViewer.Services.OnlineServices;
using Emignatik.NxFileViewer.Services.Prompting;
using Emignatik.NxFileViewer.Services.Selection;
using Emignatik.NxFileViewer.Services.WindowPlacement;
using Emignatik.NxFileViewer.Settings;
using Emignatik.NxFileViewer.Styling.Theme;
using Emignatik.NxFileViewer.Tools;
using Emignatik.NxFileViewer.Views.TreeItems;
using Emignatik.NxFileViewer.Views.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Emignatik.NxFileViewer;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application, IAppEvents
{
    private static readonly ILogger _logger;
    private ExplorerInstance? _explorerInstance;
    private readonly Queue<ExplorerLaunchRequest> _explorerRequests = new();
    private DispatcherTimer? _explorerTimer;
    private bool _explorerReady;
    private bool _handlingExplorerRequest;

    public event Action? AppShuttingDown;

    static App()
    {
        ServiceProvider = new ServiceCollection()
            .AddSingleton<IAppEvents>(_ => (App)Current)
            .AddSingleton<ILoggerFactory, LoggerFactory>()

            .AddSingleton<IKeySetProviderService, KeySetProviderService>()
            .AddSingleton<IFileOpeningService, FileOpeningService>()
            .AddSingleton<Services.Mtp.MtpImportService>()
            .AddSingleton<NszPluginManager>()
            .AddSingleton<INszPlugin, NszCliPlugin>()
            .AddSingleton<IConversionVerifier, ConversionVerifier>()
            .AddSingleton<IXciRepairService, XciRepairService>()
            .AddSingleton<IXciRepairPrompt, XciRepairPrompt>()
            .AddSingleton<PackageConversionService>()
            .AddSingleton<UpdateCenterViewModel>()
            .AddSingleton<ComponentUpdateChecker>()
            .AddSingleton<ViewerUpdateService>()
            .AddSingleton<ViewerUpdateActions>()
            .AddSingleton<NszActions>()
            .AddSingleton<Services.PackageOperations.PackageOperationService>()
            .AddSingleton<Services.PackageOperations.PackageActions>()
            .AddSingleton<ISelectedItemService, SelectedItemService>()
            .AddSingleton<IPromptService, PromptService>()
            .AddSingleton<IPackageInfoLoader, PackageInfoLoader>()
            .AddSingleton<IFileRenamerService, FileRenamerService>()
            .AddSingleton<IFileLocationOpenerService, FileLocationOpenerService>()
            .AddSingleton<OnlineTitleInfoService>()
            .AddSingleton<IOnlineTitleInfoService>(sp => sp.GetRequiredService<OnlineTitleInfoService>())
            .AddSingleton<ITitleDbUpdater>(sp => sp.GetRequiredService<OnlineTitleInfoService>())
            .AddSingleton<ICachedOnlineTitleInfoService, CachedOnlineTitleInfoService>()
            .AddSingleton<IOnlineTitlePageOpenerService, OnlineTitlePageOpenerService>()
            .AddSingleton<MainBackgroundTaskRunnerService>()
            .AddSingleton<IMainBackgroundTaskRunnerService>(provider => provider.GetRequiredService<MainBackgroundTaskRunnerService>())

            .AddSingleton<IOpenFileCommand, OpenFileCommand>()
            .AddSingleton<IExitAppCommand, ExitAppCommand>()
            .AddSingleton<IOpenLastFileCommand, OpenLastFileCommand>()
            .AddSingleton<ICloseFileCommand, CloseFileCommand>()
            .AddSingleton<IShowSettingsWindowCommand, ShowSettingsWindowCommand>()
            .AddSingleton<IVerifyNcasIntegrityCommand, VerifyNcasIntegrityCommand>()
            .AddSingleton<IShowBatchIntegrityWindowCommand, ShowBatchIntegrityWindowCommand>()
            .AddSingleton<IShowItemErrorsWindowCommand, ShowItemErrorsWindowCommand>()
            .AddSingleton<ISaveTitleImageCommand, SaveTitleImageCommand>()
            .AddSingleton<ICopyImageCommand, CopyImageCommand>()
            .AddSingleton<ILoadKeysCommand, LoadKeysCommand>()
            .AddSingleton<IOpenTitleWebPageCommand, OpenTitleWebPageCommand>()
            .AddSingleton<IShowRenameToolWindowCommand, ShowRenameToolWindowCommand>()
            .AddSingleton<IRenameFilesCommand, RenameFilesCommand>()

            .AddSingleton<INamingPatternsParser, NamingPatternsParser>()
            .AddSingleton<IPackageTypeAnalyzer, PackageTypeAnalyzer>()
            .AddSingleton<MainWindowViewModel>()
            .AddSingleton<Emignatik.NxFileViewer.Services.Nand.NandCliPlugin>()
            .AddSingleton<Emignatik.NxFileViewer.Services.Nand.NandPluginManager>()
            .AddSingleton<Emignatik.NxFileViewer.Services.Nand.NandPluginActions>()
            .AddSingleton<NandViewModel>()
            .AddSingleton<RenameToolWindowViewModel>()
            .AddTransient<BatchIntegrityWindowViewModel>()
            .AddSingleton<AppSettings>()
            .AddSingleton<IAppSettings>(provider => provider.GetRequiredService<AppSettings>())
            .AddSingleton<IAppSettingsManager, AppSettingsManager>()
            .AddSingleton<IFileItemLoader, FileItemLoader>()
            .AddSingleton<IFileOverviewLoader, FileOverviewLoader>()
            .AddSingleton<IItemViewModelBuilder, ItemViewModelBuilder>()
            .AddSingleton<IFileLoader, FileLoader>()
            .AddSingleton<IBrushesProvider, BrushesProvider>()
            .AddSingleton<IThemeService, ThemeService>()
            .AddSingleton<IWindowPlacementService, WindowPlacementService>()
            .AddSingleton<ILocalizationFromSettingsSynchronizerService, LocalizationFromSettingsSynchronizerService>()
            .AddSingleton<IShallowCopier, ShallowCopier>()
            .AddSingleton<INcaHashService, NcaHashService>()
            .AddSingleton<INcaItemIntegrityService, NcaItemIntegrityService>()

            .AddTransient<SettingsWindowViewModel>() // Important to let transient so that real actual settings are displayed when settings view is shown
            .AddTransient<ISaveFileRunnable, SaveFileRunnable>()
            .AddTransient<ISaveDirectoryRunnable, SaveDirectoryRunnable>()
            .AddTransient<IVerifyNcasIntegrityRunnable, VerifyNcasIntegrityRunnable>()
            .AddTransient<IVerifyDirectoryIntegrityRunnable, VerifyDirectoryIntegrityRunnable>()
            .AddTransient<IDownloadFileRunnable, DownloadFileRunnable>()
            .AddTransient<ISaveStorageRunnable, SaveStorageRunnable>()
            .AddTransient<IFilesRenamerRunnable, FilesRenamerRunnable>()
            .AddTransient<IFileRenamerRunnable, FileRenamerRunnable>()

            .AddTransient<IStreamToFileHelper, StreamToFileHelper>()
            .AddTransient<IOpenFileLocationCommand, OpenFileLocationCommand>()
            .AddTransient<ISaveDirectoryEntryCommand, SaveDirectoryEntryCommand>()
            .AddTransient<ISavePartitionFileCommand, SavePartitionFileCommand>()
            .AddTransient<ISaveSectionContentCommand, SaveSectionContentCommand>()
            .AddTransient<ISavePlaintextNcaFileCommand, SavePlaintextNcaFileCommand>()
            .AddTransient<IFsSanitizer, FsSanitizer>()
            .AddTransient<IHttpDownloader, HttpDownloader>()
            .AddTransient<IBackgroundTaskRunner, BackgroundTaskRunner>()

            .AddLogging(builder => builder.AddAppLoggerProvider())

            .BuildServiceProvider();

        _logger = ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(MethodBase.GetCurrentMethod()!.DeclaringType!);
    }

    public static IServiceProvider ServiceProvider { get; }
    public static IAppSettings Features => ServiceProvider.GetRequiredService<IAppSettings>();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ExplorerLaunchRequest request;
        try
        {
            request = ExplorerLaunchRequest.Parse(e.Args);
            _explorerInstance = new ExplorerInstance(Environment.ProcessPath
                ?? throw new InvalidOperationException("The current executable path is unavailable."));
            if (!_explorerInstance.IsPrimary)
            {
                _explorerInstance.Forward(request).GetAwaiter().GetResult();
                Shutdown();
                return;
            }
            _explorerRequests.Enqueue(request);
            _explorerInstance.Listen(incoming => Dispatcher.BeginInvoke(new Action(() => QueueExplorerRequest(incoming))),
                ex => _logger.LogWarning(ex, "Explorer request could not be received."));
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "NxFileViewer Fork", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        try { Services.FileAssociations.ForkAssociationLauncher.RefreshInstalled(); }
        catch (Exception ex) { _logger.LogWarning(ex, "Could not refresh the file-association launcher."); }

        // Loads the application settings
        ServiceProvider.GetRequiredService<IAppSettingsManager>().LoadSafe();
        ServiceProvider.GetRequiredService<AppSettings>().InitializeFeatures();
        KeyDownloads.MigrateLegacyHost(ServiceProvider.GetRequiredService<IAppSettings>());
        var titleSettings = ServiceProvider.GetRequiredService<IAppSettings>();
        if (titleSettings.TitlePageUrl == "https://tinfoil.io/Title/{TitleId}")
            titleSettings.TitlePageUrl = "https://tinfoil.media/Title/{TitleId}";
        if (titleSettings.TitleInfoProvider == TitleInfoProvider.Tinfoil && titleSettings.TitleInfoApiUrl == "https://tinfoil.io/api/title/{TitleId}")
            titleSettings.TitleInfoApiUrl = "https://tinfoil.media/api/title/{TitleId}";
        ServiceProvider.GetRequiredService<AppLoggerProvider>().ConfigureRetention();

        // Initialize localization
        ServiceProvider.GetRequiredService<ILocalizationFromSettingsSynchronizerService>().Initialize();
        void UpdateExplorerLanguage()
        {
            try { ExplorerIntegration.UpdateLanguage(LocalizationManager.Instance.Current.Keys.CultureName); }
            catch (Exception ex) { _logger.LogWarning(ex, "Could not update Explorer menu language."); }
        }
        UpdateExplorerLanguage();
        LocalizationManager.Instance.LocalizationChanged += (_, _) => UpdateExplorerLanguage();
        ServiceProvider.GetRequiredService<IThemeService>().Initialize();

        LocalizationStringExtension.FormatException += OnLocalizationStringFormatException;

        var mainWindowViewModel = ServiceProvider.GetRequiredService<MainWindowViewModel>();
        var mainWindow = new MainWindow
        {
            DataContext = mainWindowViewModel
        };
        mainWindowViewModel.Window = mainWindow;
        ServiceProvider.GetRequiredService<IThemeService>().RegisterWindow(mainWindow);
        ServiceProvider.GetRequiredService<IWindowPlacementService>().Track(mainWindow);

        void MainWindowLoaded(object sender, RoutedEventArgs args)
        {
            mainWindow.Loaded -= MainWindowLoaded;
            _ = ServiceProvider.GetRequiredService<ViewerUpdateActions>().CheckAsync(true);
            _explorerReady = true;
            StartExplorerQueue();
        }
        mainWindow.Loaded += MainWindowLoaded;

        Application.Current.MainWindow = mainWindow;
        mainWindow.Show();
    }

    private void QueueExplorerRequest(ExplorerLaunchRequest request)
    {
        _explorerRequests.Enqueue(request);
        StartExplorerQueue();
    }

    private void StartExplorerQueue()
    {
        if (_explorerTimer == null)
        {
            _explorerTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _explorerTimer.Tick += async (_, _) => await ProcessExplorerRequests();
        }
        _explorerTimer.Start();
    }

    private async Task ProcessExplorerRequests()
    {
        if (!_explorerReady || _handlingExplorerRequest ||
            ServiceProvider.GetRequiredService<IMainBackgroundTaskRunnerService>().IsRunning) return;
        if (!_explorerRequests.TryDequeue(out var request)) { _explorerTimer?.Stop(); return; }
        _handlingExplorerRequest = true;
        try
        {
            var window = (MainWindow)MainWindow;
            if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
            window.Activate();
            if (request.Paths.Length == 0) return;
            if (request.Paths.Any(p => !System.IO.File.Exists(p) && !System.IO.Directory.Exists(p)))
                throw new System.IO.FileNotFoundException("An Explorer selection no longer exists.");
            var batch = request.Mode is ExplorerLaunchMode.BatchOpen or ExplorerLaunchMode.BatchVerify ||
                (request.Mode == ExplorerLaunchMode.Automatic && Views.Windows.MainWindow.ShouldUseBatchDrop(request.Paths, false));
            if (batch)
                window.OpenExplorerBatch(request.Paths, request.Mode == ExplorerLaunchMode.BatchVerify ||
                    (request.Mode == ExplorerLaunchMode.Automatic && Features.VerifyFilesOpenedFromWindows));
            else
                await ServiceProvider.GetRequiredService<IFileOpeningService>().SafeOpenFile(request.Paths[0], request.Mode switch {
                    ExplorerLaunchMode.FileOpen => FileOpenOrigin.ExplorerOpen,
                    ExplorerLaunchMode.FileVerify => FileOpenOrigin.ExplorerVerify,
                    _ => FileOpenOrigin.WindowsAssociation
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not process Explorer selection.");
            MessageBox.Show(ex.Message, "NxFileViewer Fork", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { _handlingExplorerRequest = false; }
    }

    private void OnLocalizationStringFormatException(object? sender, FormatExceptionHandlerArgs args)
    {
        var argsFormatted = string.Join(", ", args.FormatArgs.Select(o => $"«{o ?? "NULL"}»"));
        var message = $"Localization key value «{args.KeyValue}» appears to be invalid, at least one of the following values {argsFormatted} couldn't be replaced.";
        Debug.Fail(message);
        _logger.LogError(message);
    }


    protected override void OnExit(ExitEventArgs e)
    {
        base.OnExit(e);
        _explorerTimer?.Stop();
        var isPrimary = _explorerInstance?.IsPrimary == true;
        _explorerInstance?.Dispose();
        if (!isPrimary) return;
        NotifyAppShuttingDown();
        var removed = Emignatik.NxFileViewer.Services.EmptyDirectoryCleanup.Clean(AppContext.BaseDirectory);
        if (removed > 0) _logger.LogInformation("Removed {Count} empty program subdirectories on exit.", removed);
        ServiceProvider.GetRequiredService<IAppLoggerProvider>().Dispose();
    }

    protected virtual void NotifyAppShuttingDown()
    {
        AppShuttingDown?.Invoke();
    }

}
