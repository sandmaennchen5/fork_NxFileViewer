using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using Emignatik.NxFileViewer.Settings;

namespace NxFileViewer.Avalonia;

internal sealed record ViewerPreferences(string Language = "", string Theme = "", string? ProdKeysPath = null,
    string? NszGuiPath = null, string? NandGuiPath = null, string? RustDesktopPath = null, string? KronosGuiPath = null,
    string? RustBackendPath = null)
{
    public System.Collections.Generic.Dictionary<string, string>? NamingPatterns { get; init; }
    public string? TitleDbRepository { get; init; }
    public System.Collections.Generic.Dictionary<string, string>? PluginLogLevels { get; init; }
    public System.Collections.Generic.Dictionary<string, int>? OutputModes { get; init; }
    public string? CustomOutputFolder { get; init; }
    public string InvalidFileNameCharsReplacement { get; init; } = "꞉";
    public bool ReplaceWhiteSpaceChars { get; init; }
    public string WhiteSpaceCharsReplacement { get; init; } = "_";
    public bool InjectTicketKeys { get; init; } = true;
    public bool PackageOperationsActive { get; init; } = DefaultSettings.Get<bool>("Settings", "EnablePackageOperations");
    public bool NszActive { get; init; } = true;
    public bool ExportVisibleColumnsOnly { get; init; }
    public string LogMinimumLevel { get; init; } = "INFO";
    public int LogFileRetention { get; init; } = DefaultSettings.Get<int>("Settings", "LogFileRetentionCount");
    public bool CheckUpdatesOnStart { get; init; } = DefaultSettings.Get<bool>("Settings", "CheckViewerUpdatesOnStartup");
    public bool IncludePreReleases { get; init; } = true;
    public int CompressionEngine { get; init; }
    public string? NszCliPath { get; init; }
    public int CompressionLevel { get; init; } = 18;
    public int CompressionMode { get; init; }
    public int CompressionBlockSize { get; init; } = 20;
    public string? TitleKeysPath { get; init; }
    public bool BatchHistoryEnabled { get; init; } = true;
    public int BatchHistoryLimit { get; init; } = 5;
    public bool AutoVerifyBatchDrop { get; init; }
    public bool NszDirectStart { get; init; } = true;
    public bool NandDirectStart { get; init; } = true;
    public bool RustDirectStart { get; init; } = true;
    public bool KronosDirectStart { get; init; } = true;
    public bool VerifyOnOpen { get; init; }
    public bool NandActive { get; init; } = true;
    public bool FirmwareActive { get; init; } = true;
    public SavedWindowPlacement? WindowPlacement { get; init; }
    public AppSettings SharedSettings { get; init; } = new();
    private static string FilePath => Path.Combine(AppContext.BaseDirectory, "NxFileViewerFork.Avalonia.settings.json");

    internal static ViewerPreferences Load()
    {
        var sharedPath = Path.Combine(AppContext.BaseDirectory, "NxFileViewerFork.settings.json");
        var shared = File.Exists(sharedPath)
            ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(sharedPath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new()
            : new AppSettings();
        if (File.Exists(sharedPath)) { using var saved = JsonDocument.Parse(File.ReadAllText(sharedPath)); shared.UsageStatisticsChoiceConfirmed = SystemReportChoice.IsUsageStatisticsChoiceConfirmed(saved.RootElement); }
        if (File.Exists(FilePath))
        {
            var existing = JsonSerializer.Deserialize<ViewerPreferences>(File.ReadAllText(FilePath)) ?? new();
            using var document = JsonDocument.Parse(File.ReadAllText(FilePath));
            if (document.RootElement.TryGetProperty(nameof(SharedSettings), out var savedShared))
                existing.SharedSettings.UsageStatisticsChoiceConfirmed = SystemReportChoice.IsUsageStatisticsChoiceConfirmed(savedShared);

            return document.RootElement.TryGetProperty(nameof(SharedSettings), out _) || !File.Exists(sharedPath) ? existing : FromShared(shared) with { NszGuiPath = existing.NszGuiPath };
        }
        return FromShared(shared);
    }

    internal static ViewerPreferences FromShared(AppSettings settings) => new(
        settings.AppLanguage == "Auto" ? "" : settings.AppLanguage,
        settings.Theme == Emignatik.NxFileViewer.Styling.Theme.AppTheme.System ? "" : settings.Theme.ToString(),
        settings.ProdKeysFilePath, null, settings.NandExecutablePath, settings.RustDesktopExecutablePath,
        settings.KronosPackageDirectory, settings.RustPackageExecutablePath)
    {
        SharedSettings = settings,
        TitleKeysPath = settings.TitleKeysFilePath,
        NszCliPath = settings.NszExecutablePath,
        NszActive = settings.EnableNszPlugin,
        NandActive = settings.EnableNand,
        FirmwareActive = settings.EnableFirmwareCheck,
        PackageOperationsActive = settings.EnablePackageOperations,
        InjectTicketKeys = settings.InjectTicketKeys,
        BatchHistoryEnabled = settings.EnableBatchHistory,
        BatchHistoryLimit = settings.BatchHistoryLimit,
        AutoVerifyBatchDrop = settings.VerifyBatchDrops,
        VerifyOnOpen = settings.VerifyFilesOpenedInProgram,
        ExportVisibleColumnsOnly = settings.BatchExportVisibleColumnsOnly,
        LogFileRetention = settings.LogFileRetentionCount,
        CheckUpdatesOnStart = settings.CheckViewerUpdatesOnStartup,
        IncludePreReleases = true,
        CompressionEngine = settings.NszCompressionEngine switch { NszCompressionEngine.Rust => 0, NszCompressionEngine.Nsz => 1, NszCompressionEngine.RustWithNszBackup => 2, _ => 3 },
        CompressionLevel = settings.NszCompressionLevel,
        CompressionMode = (int)settings.NszCompressionMode,
        CompressionBlockSize = settings.NszBlockSizeExponent,
        TitleDbRepository = settings.TitleDbRepository,
        NszDirectStart = settings.EnableNszGui,
        NandDirectStart = settings.EnableNandGui,
        RustDirectStart = settings.EnableRustDesktopGui,
        KronosDirectStart = settings.EnableKronosGui,
        InvalidFileNameCharsReplacement = settings.RenamingOptions.InvalidFileNameCharsReplacement,
        ReplaceWhiteSpaceChars = settings.RenamingOptions.ReplaceWhiteSpaceChars,
        WhiteSpaceCharsReplacement = settings.RenamingOptions.WhiteSpaceCharsReplacement,
        NamingPatterns = new() { ["Application"] = settings.RenamingOptions.ApplicationPattern,
            ["Patch"] = settings.RenamingOptions.PatchPattern, ["Addon"] = settings.RenamingOptions.AddonPattern,
            ["Combined"] = settings.RenamingOptions.SuperPattern, ["MultiPackage"] = settings.RenamingOptions.MultiPattern },
        PluginLogLevels = new() { ["NSZ"] = LogTag(settings.NszLogLevel), ["NAND"] = LogTag(settings.NandLogLevel), ["MTP"] = LogTag(settings.MtpLogLevel), ["PACKAGE"] = LogTag(settings.PackageLogLevel) },
        OutputModes = new() { ["Merge"] = (int)settings.MergeOutputLocation, ["Split"] = (int)settings.SplitOutputLocation,
            ["Compress"] = (int)settings.CompressionOutputLocation, ["Decompress"] = (int)settings.DecompressionOutputLocation },
        CustomOutputFolder = settings.CustomOutputDirectory,
        LogMinimumLevel = settings.LogLevel switch { Microsoft.Extensions.Logging.LogLevel.Trace => "TRACE", Microsoft.Extensions.Logging.LogLevel.Debug => "DEBUG", Microsoft.Extensions.Logging.LogLevel.Critical => "CRITICAL", Microsoft.Extensions.Logging.LogLevel.Warning => "WARN", Microsoft.Extensions.Logging.LogLevel.Error => "ERROR", Microsoft.Extensions.Logging.LogLevel.None => "OFF", _ => "INFO" }
    };

    private static string LogTag(PluginLogLevel level) => level switch { PluginLogLevel.Trace => "TRACE", PluginLogLevel.Debug => "DEBUG", PluginLogLevel.Information => "INFO", PluginLogLevel.Warning => "WARN", PluginLogLevel.Error => "ERROR", PluginLogLevel.Critical => "CRITICAL", PluginLogLevel.None => "OFF", _ => "" };

    private void SynchronizeSharedSettings()
    {
        var settings = SharedSettings;
        settings.AppLanguage = string.IsNullOrEmpty(Language) ? "Auto" : Language;
        settings.Theme = Enum.TryParse<Emignatik.NxFileViewer.Styling.Theme.AppTheme>(Theme, out var theme) ? theme : Emignatik.NxFileViewer.Styling.Theme.AppTheme.System;
        settings.ProdKeysFilePath = ProdKeysPath ?? ""; settings.TitleKeysFilePath = TitleKeysPath ?? "";
        settings.NandExecutablePath = NandGuiPath ?? ""; settings.NszExecutablePath = NszCliPath ?? "";
        settings.RustDesktopExecutablePath = RustDesktopPath ?? ""; settings.RustPackageExecutablePath = RustBackendPath ?? "";
        settings.EnableNszPlugin = NszActive; settings.EnableNand = NandActive; settings.EnableFirmwareCheck = FirmwareActive; settings.EnablePackageOperations = PackageOperationsActive;
        settings.InjectTicketKeys = InjectTicketKeys; settings.EnableBatchHistory = BatchHistoryEnabled; settings.BatchHistoryLimit = BatchHistoryLimit;
        settings.VerifyBatchDrops = AutoVerifyBatchDrop; settings.VerifyFilesOpenedInProgram = VerifyOnOpen; settings.BatchExportVisibleColumnsOnly = ExportVisibleColumnsOnly;
        settings.CheckViewerUpdatesOnStartup = CheckUpdatesOnStart; settings.IncludeViewerPrereleases = IncludePreReleases; settings.LogFileRetentionCount = LogFileRetention;
        settings.NszCompressionEngine = CompressionEngine switch { 1 => NszCompressionEngine.Nsz, 2 => NszCompressionEngine.RustWithNszBackup, 3 => NszCompressionEngine.NszWithRustBackup, _ => NszCompressionEngine.Rust };
        settings.NszCompressionMode = (NszCompressionMode)CompressionMode; settings.NszCompressionLevel = CompressionLevel; settings.NszBlockSizeExponent = CompressionBlockSize;
        settings.EnableNszGui = NszDirectStart; settings.EnableNandGui = NandDirectStart; settings.EnableRustDesktopGui = RustDirectStart; settings.EnableKronosGui = KronosDirectStart;
        settings.TitleDbRepository = TitleDbRepository ?? settings.TitleDbRepository; settings.CustomOutputDirectory = CustomOutputFolder ?? "";
        if (OutputModes != null) { settings.MergeOutputLocation = (OutputLocation)OutputModes.GetValueOrDefault("Merge", 1); settings.SplitOutputLocation = (OutputLocation)OutputModes.GetValueOrDefault("Split", 1); settings.CompressionOutputLocation = (OutputLocation)OutputModes.GetValueOrDefault("Compress", 1); settings.DecompressionOutputLocation = (OutputLocation)OutputModes.GetValueOrDefault("Decompress", 1); }
        var rename = settings.RenamingOptions;
        rename.InvalidFileNameCharsReplacement = InvalidFileNameCharsReplacement; rename.ReplaceWhiteSpaceChars = ReplaceWhiteSpaceChars; rename.WhiteSpaceCharsReplacement = WhiteSpaceCharsReplacement;
        if (NamingPatterns != null) { rename.ApplicationPattern = NamingPatterns.GetValueOrDefault("Application", rename.ApplicationPattern); rename.PatchPattern = NamingPatterns.GetValueOrDefault("Patch", rename.PatchPattern); rename.AddonPattern = NamingPatterns.GetValueOrDefault("Addon", rename.AddonPattern); rename.SuperPattern = NamingPatterns.GetValueOrDefault("Combined", rename.SuperPattern); rename.MultiPattern = NamingPatterns.GetValueOrDefault("MultiPackage", rename.MultiPattern); }
        if (PluginLogLevels != null)
        {
            PluginLogLevel Level(string key) => PluginLogLevels.GetValueOrDefault(key) switch { "TRACE" => PluginLogLevel.Trace, "DEBUG" => PluginLogLevel.Debug, "INFO" => PluginLogLevel.Information, "WARN" => PluginLogLevel.Warning, "ERROR" => PluginLogLevel.Error, "CRITICAL" => PluginLogLevel.Critical, "OFF" => PluginLogLevel.None, _ => PluginLogLevel.Inherit };
            settings.NszLogLevel = Level("NSZ"); settings.NandLogLevel = Level("NAND"); settings.MtpLogLevel = Level("MTP"); settings.PackageLogLevel = Level("PACKAGE");
        }
        settings.LogLevel = LogMinimumLevel switch { "TRACE" => Microsoft.Extensions.Logging.LogLevel.Trace, "DEBUG" => Microsoft.Extensions.Logging.LogLevel.Debug, "WARN" => Microsoft.Extensions.Logging.LogLevel.Warning, "ERROR" => Microsoft.Extensions.Logging.LogLevel.Error, "CRITICAL" => Microsoft.Extensions.Logging.LogLevel.Critical, "OFF" => Microsoft.Extensions.Logging.LogLevel.None, _ => Microsoft.Extensions.Logging.LogLevel.Information };
    }

    internal void Save()
    {
        SynchronizeSharedSettings();
        var temporary = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, FilePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}

internal sealed record SavedWindowPlacement(int Left, int Top, double Width, double Height, bool IsMaximized);
