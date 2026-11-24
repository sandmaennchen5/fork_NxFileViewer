using System;
using System.IO;
using System.Text.Json;
using Emignatik.NxFileViewer.Services;
using Emignatik.NxFileViewer.Services.PackageOperations;
using Emignatik.NxFileViewer.Services.Updates;

namespace NxFileViewer.Avalonia;

public partial class MainWindow
{
    private static string? ActivePluginExecutable(string folder)
    {
        var root = Path.Combine(ViewerDataDirectory.Root, "Plugins", folder);
        var state = Path.Combine(root, "active.json");
        if (!File.Exists(state)) return null;
        using var json = JsonDocument.Parse(File.ReadAllText(state));
        if (json.RootElement.TryGetProperty("SchemaVersion", out _))
            return new PluginVersionStorage(root, state).Read()?.Current;
        if (!json.RootElement.TryGetProperty("Executable", out var executable)) return null;
        var path = Path.GetFullPath(executable.GetString()!, root);
        return path.StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ? path : null;
    }

    private ViewerPreferences AdoptExistingPlugins(ViewerPreferences preferences)
    {
        try
        {
            var wpfSettings = Path.Combine(AppContext.BaseDirectory, ViewerPreferences.FileName);
            using var settings = File.Exists(wpfSettings) ? JsonDocument.Parse(File.ReadAllText(wpfSettings)) : null;
            string? Value(string name) => settings != null && settings.RootElement.TryGetProperty(name, out var value) &&
                value.ValueKind == JsonValueKind.String ? value.GetString() : null;
            var managedNsz = ActivePluginExecutable("NSZ");
            var nsz = OperatingSystem.IsWindows() && string.IsNullOrWhiteSpace(preferences.NszGuiPath)
                ? managedNsz ?? Value("NszExecutablePath") : preferences.NszGuiPath;
            var nand = string.IsNullOrWhiteSpace(preferences.NandGuiPath) ? ActivePluginExecutable("NxNandManager") ?? Value("NandExecutablePath") : preferences.NandGuiPath;
            var rust = string.IsNullOrWhiteSpace(preferences.RustBackendPath) ? Value("RustPackageExecutablePath") : preferences.RustBackendPath;
            var desktop = string.IsNullOrWhiteSpace(preferences.RustDesktopPath) ? Value("RustDesktopExecutablePath") : preferences.RustDesktopPath;
            var kronos = preferences.KronosGuiPath;
            var kronosDirectory = Value("KronosPackageDirectory");
            if (string.IsNullOrWhiteSpace(kronos) && !string.IsNullOrWhiteSpace(kronosDirectory))
                kronos = PluginGuiLauncher.KronosLauncherPath(PackageEnginePaths.ResolveKronos(kronosDirectory));
            return preferences with
            {
                NszGuiPath = nsz,
                NszCliPath = !string.IsNullOrWhiteSpace(preferences.NszCliPath)
                    ? preferences.NszCliPath : managedNsz ?? Value("NszExecutablePath"),
                NandGuiPath = nand,
                RustBackendPath = rust == null ? null : PackageEnginePaths.ResolveRust(rust),
                RustDesktopPath = desktop == null ? null : PackageEnginePaths.ResolveDesktop(desktop),
                KronosGuiPath = kronos
            };
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException or ArgumentException)
        {
            SettingsStatus.Text = ex.Message;
            return preferences;
        }
    }
}
