using System;
using System.IO;
using Emignatik.NxFileViewer.Utils;

namespace Emignatik.NxFileViewer.Services.PackageOperations;

public static class PackageEnginePaths
{
    // Rebase only the exact managed installation, never an arbitrary plugin version.
    public static string ResolveRust(string configuredPath, string? appDirectory = null) =>
        Resolve(configuredPath, "Rust-", "nscb_rust.exe", appDirectory);
    public static string ResolveDesktop(string configuredPath, string? appDirectory = null) =>
        Resolve(configuredPath, "Desktop-", "nscb-desktop.exe", appDirectory);
    public static string ResolveKronos(string configuredDirectory, string? appDirectory = null)
    {
        static bool Complete(string directory) => File.Exists(Path.Combine(directory, "runtime", "python.exe")) &&
            File.Exists(Path.Combine(directory, "ztools", "squirrel.py"));
        if (string.IsNullOrWhiteSpace(configuredDirectory) || Complete(configuredDirectory)) return configuredDirectory;
        var normalized = configuredDirectory.Replace('\\', '/').TrimEnd('/');
        const string marker = "/Plugins/PackageEngines/Kronos-";
        var index = normalized.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0 || !Guid.TryParseExact(normalized[(index + marker.Length)..], "N", out _)) return configuredDirectory;
        var candidate = Path.Combine(appDirectory ?? PathHelper.CurrentAppDir, "Plugins", "PackageEngines",
            "Kronos-" + normalized[(index + marker.Length)..]);
        return Complete(candidate) ? candidate : configuredDirectory;
    }
    private static string Resolve(string configuredPath, string prefix, string filename, string? appDirectory)
    {
        if (string.IsNullOrWhiteSpace(configuredPath) || File.Exists(configuredPath))
            return configuredPath;
        var normalized = configuredPath.Replace('\\', '/');
        const string marker = "/Plugins/PackageEngines/";
        var index = normalized.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0) return configuredPath;
        var parts = normalized[(index + marker.Length)..].Split('/');
        if (parts.Length != 2 || !parts[0].StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
            !Guid.TryParseExact(parts[0][prefix.Length..], "N", out _) ||
            !parts[1].Equals(filename, StringComparison.OrdinalIgnoreCase))
            return configuredPath;
        var candidate = Path.Combine(appDirectory ?? PathHelper.CurrentAppDir,
            "Plugins", "PackageEngines", parts[0], parts[1]);
        return File.Exists(candidate) ? candidate : configuredPath;
    }
}