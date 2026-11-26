using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
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
    public bool IncludePreReleases { get; init; }
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
    public SavedRenameInputs? RenameInputs { get; init; }
    public string? LastBatchPath { get; init; }
    public string? LastOpenedPath { get; init; }
    public SavedBatchLayout? BatchLayout { get; init; }
    public AppSettings SharedSettings { get; init; } = new();
    [JsonIgnore]
    internal bool RequiresFormatMigration { get; init; }
    private const string AppPreferencesKey = "AvaloniaPreferences";
    internal const string FileName = "NxFileViewerFork.settings.json";
    private static string FilePath => Path.Combine(ViewerDataDirectory.Root, FileName);
    private static readonly JsonSerializerOptions ReadOptions = new() { PropertyNameCaseInsensitive = true };

    internal static ViewerPreferences Load()
    {
        var root = ViewerDataDirectory.Root;
        var sharedPath = File.Exists(FilePath) ? FilePath : Path.Combine(AppContext.BaseDirectory, FileName);
        using var sharedDocument = File.Exists(sharedPath) ? JsonDocument.Parse(File.ReadAllText(sharedPath)) : null;
        if (sharedDocument?.RootElement is { ValueKind: JsonValueKind.Object } sharedRoot &&
            sharedRoot.TryGetProperty(AppPreferencesKey, out var appPreferences))
        {
            var shared = ReadSharedSettings(sharedRoot);
            return ReadPreferences(appPreferences, shared);
        }

        foreach (var name in new[] { "NxFileViewer.settings.json", "NxFileViewerFork.Avalonia.settings.json" })
        {
            var legacyPath = Path.Combine(root, name);
            if (!File.Exists(legacyPath)) continue;

            using var legacyDocument = JsonDocument.Parse(File.ReadAllText(legacyPath));
            var legacy = legacyDocument.RootElement;
            if (legacy.TryGetProperty(nameof(SharedSettings), out var legacyShared))
            {
                return ReadPreferences(legacy, ReadSharedSettings(legacyShared)) with { RequiresFormatMigration = true };
            }

            if (legacy.TryGetProperty(nameof(AppSettings.AppLanguage), out _) ||
                legacy.TryGetProperty(nameof(AppSettings.LastOpenedFile), out _))
            {
                return FromShared(ReadSharedSettings(sharedDocument?.RootElement ?? legacy)) with { RequiresFormatMigration = true };
            }

            var preferences = legacy.Deserialize<ViewerPreferences>() ?? new();
            return sharedDocument == null
                ? preferences with { RequiresFormatMigration = true }
                : FromShared(ReadSharedSettings(sharedDocument.RootElement)) with
                {
                    NszGuiPath = preferences.NszGuiPath, RequiresFormatMigration = true
                };
        }

        return sharedDocument == null ? FromShared(new AppSettings()) :
            FromShared(ReadSharedSettings(sharedDocument.RootElement)) with { RequiresFormatMigration = true };
    }

    private static ViewerPreferences ReadPreferences(JsonElement saved, AppSettings shared)
    {
        var preferences = saved.Deserialize<ViewerPreferences>() ?? FromShared(shared);
        return preferences with
        {
            SharedSettings = shared,
            IncludePreReleases = saved.ValueKind == JsonValueKind.Object &&
                saved.TryGetProperty(nameof(IncludePreReleases), out _)
                ? preferences.IncludePreReleases : shared.IncludeViewerPrereleases
        };
    }

    private static AppSettings ReadSharedSettings(JsonElement saved)
    {
        var settings = saved.Deserialize<AppSettings>(ReadOptions) ?? new();
        settings.UsageStatisticsChoiceConfirmed = SystemReportChoice.IsUsageStatisticsChoiceConfirmed(saved);
        return settings;
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
        IncludePreReleases = settings.IncludeViewerPrereleases,
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
        if (LastOpenedPath != null) settings.LastOpenedFile = LastOpenedPath;
        if (LastBatchPath != null)
            settings.LastUsedDir = File.Exists(LastBatchPath) ? Path.GetDirectoryName(LastBatchPath) ?? "" : LastBatchPath;
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
        Directory.CreateDirectory(ViewerDataDirectory.Root);
        var temporary = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var settings = JsonSerializer.SerializeToNode(SharedSettings)!.AsObject();
            var preferences = JsonSerializer.SerializeToNode(this)!.AsObject();
            preferences.Remove(nameof(SharedSettings));
            settings[AppPreferencesKey] = preferences;
            File.WriteAllText(temporary, settings.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, FilePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}

internal sealed record SavedWindowPlacement(int Left, int Top, double Width, double Height, bool IsMaximized);
internal sealed record SavedRenameInputs(string Source, string Destination, string Filter, bool Recursive, bool AutoClose, bool CompactPaths);
