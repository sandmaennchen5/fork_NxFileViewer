using System.Text.Json.Serialization;
using Emignatik.NxFileViewer.Utils.MVVM;
using Emignatik.NxFileViewer.Utils.MVVM.Localization;
using Microsoft.Extensions.Logging;
using Emignatik.NxFileViewer.Styling.Theme;

namespace Emignatik.NxFileViewer.Settings;

public class AppSettings : NotifyPropertyChangedBase, IAppSettings
{
    private PackageEngine _packageEngine = DefaultSettings.Get<PackageEngine>("Settings", "PackageEngine");
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PackageEngine PackageEngine
    {
        get => _packageEngine;
        set { _packageEngine = System.Enum.IsDefined(value) ? value : default; NotifyPropertyChanged(); }
    }
    private string _rustPackageExecutablePath = "";
    public string RustPackageExecutablePath
    {
        get => _rustPackageExecutablePath;
        set { _rustPackageExecutablePath = value ?? ""; NotifyPropertyChanged(); }
    }
    private string _kronosPackageDirectory = "";
    public string KronosPackageDirectory
    {
        get => _kronosPackageDirectory;
        set { _kronosPackageDirectory = value ?? ""; NotifyPropertyChanged(); }
    }
    // Legacy fallback preserves the previous combined plugin setting when loading old JSON.
    private bool _enablePlugins = DefaultSettings.Get<bool>("Settings", "EnablePlugins");
    private bool? _enableNszPlugin;
    private bool? _enableNandPlugin;
    private bool _enableMtp = DefaultSettings.Get<bool>("Settings", "EnableMtp");
    private bool _enableNand = DefaultSettings.Get<bool>("Settings", "EnableNand");
    private bool? _nandActive;
    private bool _enablePackageOperations = DefaultSettings.Get<bool>("Settings", "EnablePackageOperations");
    private bool? _packageOperationsActive;
    private bool _enableFirmwareCheck = DefaultSettings.Get<bool>("Settings", "EnableFirmwareCheck");
    private bool? _nszPluginActive, _nandPluginActive, _mtpActive, _firmwareCheckActive;
    public bool EnablePlugins
    {
        get => _enablePlugins;
        set { _enablePlugins = value; NotifyPropertyChanged(); }
    }
    public bool EnableNszPlugin
    {
        get => _enableNszPlugin ?? (EnablePlugins && DefaultSettings.Get<bool>("Settings", "EnableNszPlugin"));
        set { _enableNszPlugin = value; NotifyPropertyChanged(); }
    }
    public bool EnableNandPlugin
    {
        get => _enableNandPlugin ?? (EnablePlugins && DefaultSettings.Get<bool>("Settings", "EnableNandPlugin"));
        set { _enableNandPlugin = value; NotifyPropertyChanged(); }
    }
    public bool EnableNand
    {
        get => _enableNand;
        set { _enableNand = value; NotifyPropertyChanged(); }
    }
    public bool EnableMtp
    {
        get => _enableMtp;
        set { _enableMtp = value; NotifyPropertyChanged(); }
    }
    public bool EnablePackageOperations
    {
        get => _enablePackageOperations;
        set { _enablePackageOperations = value; NotifyPropertyChanged(); }
    }
    [JsonIgnore]
    public bool PackageOperationsActive => _packageOperationsActive ?? EnablePackageOperations;
    public bool EnableFirmwareCheck
    {
        get => _enableFirmwareCheck;
        set { _enableFirmwareCheck = value; NotifyPropertyChanged(); }
    }
    [JsonIgnore]
    public bool NszPluginActive => _nszPluginActive ?? EnableNszPlugin;
    [JsonIgnore]
    public bool NandPluginActive => NandActive && (_nandPluginActive ?? EnableNandPlugin);
    [JsonIgnore]
    public bool NandActive => _nandActive ?? EnableNand;
    [JsonIgnore]
    public bool PluginsActive => NszPluginActive || NandPluginActive;
    [JsonIgnore]
    public bool MtpActive => _mtpActive ?? EnableMtp;
    [JsonIgnore]
    public bool FirmwareCheckActive => _firmwareCheckActive ?? EnableFirmwareCheck;

    // Capture once after loading settings; later edits take effect on the next launch.
    public void InitializeFeatures()
    {
        _nszPluginActive ??= EnableNszPlugin;
        _nandPluginActive ??= EnableNandPlugin;
        _nandActive ??= EnableNand;
        _mtpActive ??= EnableMtp;
        _firmwareCheckActive ??= EnableFirmwareCheck;
        _packageOperationsActive ??= EnablePackageOperations;
    }
    private int _batchHistoryLimit = DefaultSettings.Get<int>("Settings", "BatchHistoryLimit");
    public int BatchHistoryLimit
    {
        get => _batchHistoryLimit;
        set { _batchHistoryLimit = System.Math.Clamp(value, 1, 100); NotifyPropertyChanged(); }
    }
    private bool _verifyBatchDrops = DefaultSettings.Get<bool>("Settings", "VerifyBatchDrops");
    public bool VerifyBatchDrops
    {
        get => _verifyBatchDrops;
        set { _verifyBatchDrops = value; NotifyPropertyChanged(); }
    }
    private bool _verifyFilesOpenedInProgram = DefaultSettings.Get<bool>("Settings", "VerifyFilesOpenedInProgram");
    public bool VerifyFilesOpenedInProgram
    {
        get => _verifyFilesOpenedInProgram;
        set { _verifyFilesOpenedInProgram = value; NotifyPropertyChanged(); }
    }
    private bool _verifyFilesOpenedFromWindows = DefaultSettings.Get<bool>("Settings", "VerifyFilesOpenedFromWindows");
    public bool VerifyFilesOpenedFromWindows
    {
        get => _verifyFilesOpenedFromWindows;
        set { _verifyFilesOpenedFromWindows = value; NotifyPropertyChanged(); }
    }
    private string _batchTableLayout = "";
    private bool _batchSummaryPreservePackageOrder = DefaultSettings.Get<bool>("Settings", "BatchSummaryPreservePackageOrder");
    public bool BatchSummaryPreservePackageOrder
    {
        get => _batchSummaryPreservePackageOrder;
        set { _batchSummaryPreservePackageOrder = value; NotifyPropertyChanged(); }
    }
    private bool _batchSeparatePackages = DefaultSettings.Get<bool>("Settings", "BatchSeparatePackages");
    public bool BatchSeparatePackages
    {
        get => _batchSeparatePackages;
        set { _batchSeparatePackages = value; NotifyPropertyChanged(); }
    }
    private bool _batchExportSeparatePackages = DefaultSettings.Get<bool>("Settings", "BatchExportSeparatePackages");
    public bool BatchExportSeparatePackages
    {
        get => _batchExportSeparatePackages;
        set { _batchExportSeparatePackages = value; NotifyPropertyChanged(); }
    }
    private bool _batchExportVisibleColumnsOnly = DefaultSettings.Get<bool>("Settings", "BatchExportVisibleColumnsOnly");
    public bool BatchExportVisibleColumnsOnly
    {
        get => _batchExportVisibleColumnsOnly;
        set { _batchExportVisibleColumnsOnly = value; NotifyPropertyChanged(); }
    }
    private bool _batchExportExcel = DefaultSettings.Get<bool>("Settings", "BatchExportExcel");
    public bool BatchExportExcel
    {
        get => _batchExportExcel;
        set { _batchExportExcel = value; NotifyPropertyChanged(); }
    }
    public string BatchTableLayout
    {
        get => _batchTableLayout;
        set { _batchTableLayout = value ?? ""; NotifyPropertyChanged(); }
    }
    private string _nandExecutablePath = "";
    private string _nandBisKeysPath = "";
    public string NandExecutablePath
    {
        get => _nandExecutablePath;
        set { _nandExecutablePath = value; NotifyPropertyChanged(); }
    }
    public string NandBisKeysPath
    {
        get => _nandBisKeysPath;
        set { _nandBisKeysPath = value; NotifyPropertyChanged(); }
    }
    private int _logFileRetentionCount = DefaultSettings.Get<int>("Settings", "LogFileRetentionCount");
    public int LogFileRetentionCount
    {
        get => _logFileRetentionCount;
        set { _logFileRetentionCount = System.Math.Clamp(value, 1, 100); NotifyPropertyChanged(); }
    }
    private SdBatchSourceMode _sdBatchSourceMode = DefaultSettings.Get<SdBatchSourceMode>("Settings", "SdBatchSourceMode");
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SdBatchSourceMode SdBatchSourceMode
    {
        get => _sdBatchSourceMode;
        set { _sdBatchSourceMode = System.Enum.IsDefined(value) ? value : default; NotifyPropertyChanged(); }
    }
    private string _sdBatchCustomPaths = "";
    public string SdBatchCustomPaths
    {
        get => _sdBatchCustomPaths;
        set { _sdBatchCustomPaths = value ?? ""; NotifyPropertyChanged(); }
    }
    private bool _sdBatchSelectedAreasOnly = DefaultSettings.Get<bool>("Settings", "SdBatchSelectedAreasOnly");
    public bool SdBatchSelectedAreasOnly
    {
        get => _sdBatchSelectedAreasOnly;
        set { _sdBatchSelectedAreasOnly = value; NotifyPropertyChanged(); }
    }
    private bool _sdBatchSwitch = DefaultSettings.Get<bool>("Settings", "SdBatchSwitch");
    public bool SdBatchSwitch
    {
        get => _sdBatchSwitch;
        set { _sdBatchSwitch = value; NotifyPropertyChanged(); }
    }
    private bool _sdBatchNintendo = DefaultSettings.Get<bool>("Settings", "SdBatchNintendo");
    public bool SdBatchNintendo
    {
        get => _sdBatchNintendo;
        set { _sdBatchNintendo = value; NotifyPropertyChanged(); }
    }
    private bool _sdBatchEmuMmc = DefaultSettings.Get<bool>("Settings", "SdBatchEmuMmc");
    public bool SdBatchEmuMmc
    {
        get => _sdBatchEmuMmc;
        set { _sdBatchEmuMmc = value; NotifyPropertyChanged(); }
    }
    private bool _sdBatchHbmenu = DefaultSettings.Get<bool>("Settings", "SdBatchHbmenu");
    public bool SdBatchHbmenu
    {
        get => _sdBatchHbmenu;
        set { _sdBatchHbmenu = value; NotifyPropertyChanged(); }
    }
    private bool _sdBatchTools = DefaultSettings.Get<bool>("Settings", "SdBatchTools");
    public bool SdBatchTools
    {
        get => _sdBatchTools;
        set { _sdBatchTools = value; NotifyPropertyChanged(); }
    }
    private bool _sdBatchDumps = DefaultSettings.Get<bool>("Settings", "SdBatchDumps");
    public bool SdBatchDumps
    {
        get => _sdBatchDumps;
        set { _sdBatchDumps = value; NotifyPropertyChanged(); }
    }
    private bool _sdBatchNxdtRwPoc = DefaultSettings.Get<bool>("Settings", "SdBatchNxdtRwPoc");
    public bool SdBatchNxdtRwPoc
    {
        get => _sdBatchNxdtRwPoc;
        set { _sdBatchNxdtRwPoc = value; NotifyPropertyChanged(); }
    }
    private bool _sdBatchDbiRootSaves = DefaultSettings.Get<bool>("Settings", "SdBatchDbiRootSaves");
    public bool SdBatchDbiRootSaves
    {
        get => _sdBatchDbiRootSaves;
        set { _sdBatchDbiRootSaves = value; NotifyPropertyChanged(); }
    }
    private bool _saveBatchHistoryLogos = DefaultSettings.Get<bool>("Settings", "SaveBatchHistoryLogos");
    public bool SaveBatchHistoryLogos
    {
        get => _saveBatchHistoryLogos;
        set { _saveBatchHistoryLogos = value; NotifyPropertyChanged(); }
    }
    private bool _enableBatchHistory = DefaultSettings.Get<bool>("Settings", "EnableBatchHistory");
    public bool EnableBatchHistory
    {
        get => _enableBatchHistory;
        set { _enableBatchHistory = value; NotifyPropertyChanged(); }
    }
    private bool _includeViewerPrereleases = DefaultSettings.Get<bool>("Settings", "IncludeViewerPrereleases");
    public bool IncludeViewerPrereleases
    {
        get => _includeViewerPrereleases;
        set { _includeViewerPrereleases = value; NotifyPropertyChanged(); }
    }
    private bool _checkViewerUpdatesOnStartup = DefaultSettings.Get<bool>("Settings", "CheckViewerUpdatesOnStartup");
    public bool CheckViewerUpdatesOnStartup
    {
        get => _checkViewerUpdatesOnStartup;
        set { _checkViewerUpdatesOnStartup = value; NotifyPropertyChanged(); }
    }
    private string _nszExecutablePath = "";
    private bool _nszCheckUpdates = DefaultSettings.Get<bool>("Settings", "NszCheckUpdates");
    private int _nszCompressionLevel = DefaultSettings.Get<int>("Settings", "NszCompressionLevel");
    private NszCompressionEngine _nszCompressionEngine = DefaultSettings.Get<NszCompressionEngine>("Settings", "NszCompressionEngine");
    public NszCompressionEngine NszCompressionEngine
    {
        get => _nszCompressionEngine;
        set { _nszCompressionEngine = System.Enum.IsDefined(value) ? value : NszCompressionEngine.Nsz; NotifyPropertyChanged(); }
    }
    public string NszExecutablePath
    {
        get => _nszExecutablePath;
        set { _nszExecutablePath = value; NotifyPropertyChanged(); }
    }
    public bool NszCheckUpdates
    {
        get => _nszCheckUpdates;
        set { _nszCheckUpdates = value; NotifyPropertyChanged(); }
    }
    public int NszCompressionLevel
    {
        get => _nszCompressionLevel;
        set { _nszCompressionLevel = System.Math.Clamp(value, 1, 22); NotifyPropertyChanged(); }
    }
    private NszCompressionMode _nszCompressionMode = DefaultSettings.Get<NszCompressionMode>("Settings", "NszCompressionMode");
    private int _nszBlockSizeExponent = DefaultSettings.Get<int>("Settings", "NszBlockSizeExponent");
    public NszCompressionMode NszCompressionMode
    {
        get => _nszCompressionMode;
        set { _nszCompressionMode = System.Enum.IsDefined(value) ? value : NszCompressionMode.Auto; NotifyPropertyChanged(); }
    }
    public int NszBlockSizeExponent
    {
        get => _nszBlockSizeExponent;
        set { _nszBlockSizeExponent = System.Math.Clamp(value, 14, 32); NotifyPropertyChanged(); }
    }
    private string _appLanguage = DefaultSettings.Get<string>("Settings", "AppLanguage");
    private string _lastRenamePath = "";
    private string _targetDirectory = "";
    public string TargetDirectory
    {
        get => _targetDirectory;
        set { _targetDirectory = value; NotifyPropertyChanged(); }
    }
    private string _lastOpenedFile = "";
    private string _prodKeysFilePath = "";
    private string _titleKeysFilePath = "";
    private PluginLogLevel _nszLogLevel = DefaultSettings.Get<PluginLogLevel>("Settings", "NszLogLevel");

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PluginLogLevel NszLogLevel
    {
        get => _nszLogLevel;
        set { _nszLogLevel = value; NotifyPropertyChanged(); }
    }

    private PluginLogLevel _nandLogLevel = DefaultSettings.Get<PluginLogLevel>("Settings", "NandLogLevel");

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PluginLogLevel NandLogLevel
    {
        get => _nandLogLevel;
        set { _nandLogLevel = value; NotifyPropertyChanged(); }
    }

    private PluginLogLevel _mtpLogLevel = DefaultSettings.Get<PluginLogLevel>("Settings", "MtpLogLevel");

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PluginLogLevel MtpLogLevel
    {
        get => _mtpLogLevel;
        set { _mtpLogLevel = value; NotifyPropertyChanged(); }
    }

    private PluginLogLevel _packageLogLevel = DefaultSettings.Get<PluginLogLevel>("Settings", "PackageLogLevel");

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PluginLogLevel PackageLogLevel
    {
        get => _packageLogLevel;
        set { _packageLogLevel = value; NotifyPropertyChanged(); }
    }

    private LogLevel _logLevel = DefaultSettings.Get<LogLevel>("Settings", "LogLevel");
    private string _keysDownloadHost = DefaultSettings.Get<string>("Settings", "KeysDownloadHost");
    public string KeysDownloadHost
    {
        get => _keysDownloadHost;
        set { _keysDownloadHost = value; NotifyPropertyChanged(); }
    }
    private string _prodKeysDownloadUrl = DefaultSettings.Get<string>("Settings", "ProdKeysDownloadUrl");
    private string _titleKeysDownloadUrl = DefaultSettings.Get<string>("Settings", "TitleKeysDownloadUrl");
    private bool _alwaysReloadKeysBeforeOpen = DefaultSettings.Get<bool>("Settings", "AlwaysReloadKeysBeforeOpen");

    private string _titlePageUrl = Emignatik.NxFileViewer.Services.ExternalSources.Current.TitlePageUrl;
    private string _titleInfoApiUrl = Emignatik.NxFileViewer.Services.ExternalSources.Current.TitleInfoApiUrl;
    private TitleInfoProvider _titleInfoProvider = DefaultSettings.Get<TitleInfoProvider>("Settings", "TitleInfoProvider");
    private string _titleDbRegion = DefaultSettings.Get<string>("Settings", "TitleDbRegion");
    private string _nLibApiUrl = Emignatik.NxFileViewer.Services.ExternalSources.Current.NLibApiUrl;
    private string _lastUsedDir = "";
    private bool _allowNczBlocklessCompressionOpening = DefaultSettings.Get<bool>("Settings", "OpenBlocklessCompressionNCZ");
    private bool _acceptMissingDeltaFragments = DefaultSettings.Get<bool>("Settings", "IgnoreMissingDeltaFragments");
    private bool _injectTicketKeys = DefaultSettings.Get<bool>("Settings", "InjectTicketKeys");
    private bool _saveTicketKeys = DefaultSettings.Get<bool>("Settings", "SaveTicketKeys");
    public bool SaveTicketKeys { get => _saveTicketKeys; set { _saveTicketKeys = value; NotifyPropertyChanged(); } }
    private AppTheme _theme = DefaultSettings.Get<AppTheme>("Settings", "Theme");
    private bool _rememberWindowPlacement = DefaultSettings.Get<bool>("Settings", "RememberWindowPlacement");

    public string AppLanguage
    {
        get => _appLanguage;
        set
        {
            _appLanguage = value;
            NotifyPropertyChanged();
        }
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AppTheme Theme
    {
        get => _theme;
        set { _theme = value; NotifyPropertyChanged(); }
    }

    public bool RememberWindowPlacement
    {
        get => _rememberWindowPlacement;
        set { _rememberWindowPlacement = value; NotifyPropertyChanged(); }
    }

    [JsonIgnore]
    IWindowPlacementSettings IAppSettings.MainWindowPlacement => MainWindowPlacement;

    public WindowPlacementSettings MainWindowPlacement { get; set; } = new();

    public string LastUsedDir
    {
        get => _lastUsedDir;
        set
        {
            _lastUsedDir = value;
            NotifyPropertyChanged();
        }
    }

    public string LastRenamePath
    {
        get => _lastRenamePath;
        set
        {
            _lastRenamePath = value;
            NotifyPropertyChanged();
        }
    }

    public string LastOpenedFile
    {
        get => _lastOpenedFile;
        set
        {
            _lastOpenedFile = value;
            NotifyPropertyChanged();
        }
    }

    public string ProdKeysFilePath
    {
        get => _prodKeysFilePath;
        set
        {
            _prodKeysFilePath = value;
            NotifyPropertyChanged();
        }
    }

    public bool OpenBlocklessCompressionNCZ
    {
        get => _allowNczBlocklessCompressionOpening;
        set
        {
            _allowNczBlocklessCompressionOpening = value;
            NotifyPropertyChanged();
        }
    }

    public bool IgnoreMissingDeltaFragments
    {
        get => _acceptMissingDeltaFragments;
        set
        {
            _acceptMissingDeltaFragments = value;
            NotifyPropertyChanged();
        }
    }

    public string TitleKeysFilePath
    {
        get => _titleKeysFilePath;
        set
        {
            _titleKeysFilePath = value;
            NotifyPropertyChanged();
        }
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public LogLevel LogLevel
    {
        get => _logLevel;
        set
        {
            _logLevel = value;
            NotifyPropertyChanged();
        }
    }

    public string ProdKeysDownloadUrl
    {
        get => _prodKeysDownloadUrl;
        set
        {
            _prodKeysDownloadUrl = value;
            NotifyPropertyChanged();
        }
    }

    public string TitleKeysDownloadUrl
    {
        get => _titleKeysDownloadUrl;
        set
        {
            _titleKeysDownloadUrl = value;
            NotifyPropertyChanged();
        }
    }

    public bool AlwaysReloadKeysBeforeOpen
    {
        get => _alwaysReloadKeysBeforeOpen;
        set
        {
            _alwaysReloadKeysBeforeOpen = value;
            NotifyPropertyChanged();
        }
    }

    public bool InjectTicketKeys
    {
        get => _injectTicketKeys;
        set
        {
            _injectTicketKeys = value;
            NotifyPropertyChanged();
        }
    }

    public string TitlePageUrl
    {
        get => _titlePageUrl;
        set
        {
            _titlePageUrl = value;
            NotifyPropertyChanged();
        }
    }

    public string TitleInfoApiUrl
    {
        get => _titleInfoApiUrl;
        set
        {
            _titleInfoApiUrl = value;
            NotifyPropertyChanged();
        }
    }

    [JsonIgnore]
    IRenamingOptions IAppSettings.RenamingOptions => RenamingOptions;

    public TitleInfoProvider TitleInfoProvider
    {
        get => _titleInfoProvider;
        set { _titleInfoProvider = value; NotifyPropertyChanged(); }
    }

    public string TitleDbRegion
    {
        get => _titleDbRegion;
        set { _titleDbRegion = value; NotifyPropertyChanged(); }
    }

    public string NLibApiUrl
    {
        get => _nLibApiUrl;
        set { _nLibApiUrl = value; NotifyPropertyChanged(); }
    }

    public RenamingOptions RenamingOptions { get; set; } = new();

    [JsonIgnore]
    public int ProgressBufferSize { get; } = 4 * 1024 * 1024;

}

public class WindowPlacementSettings : NotifyPropertyChangedBase, IWindowPlacementSettings
{
    private int _left;
    private int _top;
    private int _width;
    private int _height;
    private bool _isMaximized;

    public int Left { get => _left; set { _left = value; NotifyPropertyChanged(); } }
    public int Top { get => _top; set { _top = value; NotifyPropertyChanged(); } }
    public int Width { get => _width; set { _width = value; NotifyPropertyChanged(); } }
    public int Height { get => _height; set { _height = value; NotifyPropertyChanged(); } }
    public bool IsMaximized { get => _isMaximized; set { _isMaximized = value; NotifyPropertyChanged(); } }

    [JsonIgnore]
    public bool IsDefined => _width > 0 && _height > 0;
}

public class RenamingOptions : NotifyPropertyChangedBase, IRenamingOptions
{
    private string? _fileFilters = DefaultSettings.Get<string?>("RenamingOptions", "FileFilters");
    private bool _includeSubdirectories = DefaultSettings.Get<bool>("RenamingOptions", "IncludeSubdirectories");
    private string _applicationPattern = Emignatik.NxFileViewer.Services.FileRenaming.NamingPresets.Current.Application.Default.Pattern;
    private string _patchPattern = Emignatik.NxFileViewer.Services.FileRenaming.NamingPresets.Current.Patch.Default.Pattern;
    private string _addonPattern = Emignatik.NxFileViewer.Services.FileRenaming.NamingPresets.Current.Addon.Default.Pattern;
    private string _multiPattern = Emignatik.NxFileViewer.Services.FileRenaming.NamingPresets.Current.MultiPackage.Default.Pattern;
    private string _superPattern = Emignatik.NxFileViewer.Services.FileRenaming.NamingPresets.Current.Combined.Default.Pattern;
    private bool _isSimulation = DefaultSettings.Get<bool>("RenamingOptions", "IsSimulation");
    private string _invalidFileNameCharsReplacement = Emignatik.NxFileViewer.Services.FileRenaming.NamingPresets.Current.Defaults.InvalidFileNameCharsReplacement;
    private bool _replaceWhiteSpaceChars = Emignatik.NxFileViewer.Services.FileRenaming.NamingPresets.Current.Defaults.ReplaceWhiteSpaceChars;
    private string _whiteSpaceCharsReplacement = Emignatik.NxFileViewer.Services.FileRenaming.NamingPresets.Current.Defaults.WhiteSpaceCharsReplacement;
    private string _lastRenamePath = "";
    private string _targetDirectory = "";
    public string TargetDirectory
    {
        get => _targetDirectory;
        set { _targetDirectory = value; NotifyPropertyChanged(); }
    }
    private bool _autoCloseOpenedFile = DefaultSettings.Get<bool>("RenamingOptions", "AutoCloseOpenedFile");

    public bool AutoCloseOpenedFile
    {
        get => _autoCloseOpenedFile;
        set
        {
            _autoCloseOpenedFile = value;
            NotifyPropertyChanged();
        }
    }

    public string LastRenamePath
    {
        get => _lastRenamePath;
        set
        {
            _lastRenamePath = value;
            NotifyPropertyChanged();
        }
    }

    public string? FileFilters
    {
        get => _fileFilters;
        set
        {
            _fileFilters = value;
            NotifyPropertyChanged();
        }
    }

    public bool IncludeSubdirectories
    {
        get => _includeSubdirectories;
        set
        {
            _includeSubdirectories = value;
            NotifyPropertyChanged();
        }
    }

    public string ApplicationPattern
    {
        get => _applicationPattern;
        set
        {
            _applicationPattern = value;
            NotifyPropertyChanged();
        }
    }

    public string PatchPattern
    {
        get => _patchPattern;
        set
        {
            _patchPattern = value;
            NotifyPropertyChanged();
        }
    }

    public string AddonPattern
    {
        get => _addonPattern;
        set
        {
            _addonPattern = value;
            NotifyPropertyChanged();
        }
    }

    public string MultiPattern
    {
        get => _multiPattern;
        set { _multiPattern = value; NotifyPropertyChanged(); }
    }
    public string SuperPattern
    {
        get => _superPattern;
        set { _superPattern = value; NotifyPropertyChanged(); }
    }

    public bool IsSimulation
    {
        get => _isSimulation;
        set
        {
            _isSimulation = value;
            NotifyPropertyChanged();
        }
    }

    public string InvalidFileNameCharsReplacement
    {
        get => _invalidFileNameCharsReplacement;
        set
        {
            _invalidFileNameCharsReplacement = value;
            NotifyPropertyChanged();
        }
    }

    public bool ReplaceWhiteSpaceChars
    {
        get => _replaceWhiteSpaceChars;
        set
        {
            _replaceWhiteSpaceChars = value;
            NotifyPropertyChanged();
        }
    }

    public string WhiteSpaceCharsReplacement
    {
        get => _whiteSpaceCharsReplacement;
        set
        {
            _whiteSpaceCharsReplacement = value;
            NotifyPropertyChanged();
        }
    }

}
