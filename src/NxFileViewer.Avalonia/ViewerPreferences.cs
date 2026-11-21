using System;
using System.IO;
using System.Text.Json;

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
    public bool PackageOperationsActive { get; init; } = true;
    public bool NszActive { get; init; } = true;
    public bool ExportVisibleColumnsOnly { get; init; }
    public string LogMinimumLevel { get; init; } = "INFO";
    public int LogFileRetention { get; init; } = 14;
    public bool CheckUpdatesOnStart { get; init; }
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
    private static string FilePath => Path.Combine(AppContext.BaseDirectory, "NxFileViewerFork.Avalonia.settings.json");

    internal static ViewerPreferences Load() =>
        File.Exists(FilePath)
            ? JsonSerializer.Deserialize<ViewerPreferences>(File.ReadAllText(FilePath)) ?? new ViewerPreferences()
            : new ViewerPreferences();

    internal void Save()
    {
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
