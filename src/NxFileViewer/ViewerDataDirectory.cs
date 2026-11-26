using System;
using System.IO;
using System.Linq;
using NxFileViewer.Avalonia.Updates;

namespace NxFileViewer.Avalonia;

internal static class ViewerDataDirectory
{
    internal const string DataFolderName = "NxFileViewerFork-Daten";

    private static string? AppImagePath => OperatingSystem.IsLinux() ? Environment.GetEnvironmentVariable("APPIMAGE") : null;
    private static readonly Lazy<string> DefaultRoot = new(() => AvaloniaAppUpdater.GetRunningMacBundle() != null
        ? GetMacBundleDataRoot()
        : ResolveRoot(OperatingSystem.IsLinux(), AppImagePath, AppContext.BaseDirectory, OperatingSystem.IsWindows()));

    internal static bool IsAppImage => !string.IsNullOrEmpty(AppImagePath);
    internal static string Root => AppContext.GetData("NxFileViewer.DataDirectory") as string ?? DefaultRoot.Value;
    internal static string NewRoot => GetNewRoot(AppImagePath, AppContext.BaseDirectory);

    internal static string GetMacBundleDataRoot()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!Path.IsPathFullyQualified(home)) throw new IOException("The macOS user folder could not be located.");
        return Path.Combine(home, "Library", "Application Support", "NxFileViewer Fork");
    }

    internal static void InitializeMacBundleData()
    {
        if (AppContext.GetData("NxFileViewer.DataDirectory") is string || AvaloniaAppUpdater.GetRunningMacBundle() == null) return;
        MigrateMacBundleData(Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory), GetMacBundleDataRoot());
    }

    internal static void MigrateMacBundleData(string source, string destination)
    {
        if (ViewerDataMigration.HasData(source) && !ViewerDataMigration.WasCopied(source, destination))
            ViewerDataMigration.Copy(source, destination);
        Directory.CreateDirectory(destination);
    }

    internal static string ResolveRoot(bool isLinux, string? appImagePath, string programDirectory, bool isWindows = false)
    {
        if (!isLinux && !isWindows) return programDirectory;
        var destination = GetNewRoot(isLinux ? appImagePath : null, programDirectory);
        return !Directory.Exists(destination) && ViewerDataMigration.HasData(Path.GetDirectoryName(destination)!)
            ? Path.GetDirectoryName(destination)!
            : destination;
    }

    private static string GetNewRoot(string? appImagePath, string programDirectory)
    {
        var directory = appImagePath != null && Path.IsPathFullyQualified(appImagePath)
            ? Path.GetDirectoryName(appImagePath) ?? programDirectory
            : programDirectory;
        return Path.Combine(directory, DataFolderName);
    }

    internal static string? MigrationSource()
    {
        if ((!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux()) ||
            AppContext.GetData("NxFileViewer.DataDirectory") is string) return null;

        var program = Path.GetDirectoryName(NewRoot)!;
        if (!PathsEqual(Root, NewRoot) && ViewerDataMigration.HasData(Root)) return Root;
        if (!OperatingSystem.IsLinux()) return null;

        var home = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolderOption.DoNotVerify);
        return new[] { program, Path.Combine(home, DataFolderName), Path.Combine(home, "NxFileViewerFork") }
            .FirstOrDefault(source => !PathsEqual(source, NewRoot) && ViewerDataMigration.HasData(source) &&
                !ViewerDataMigration.WasCopied(source, NewRoot));
    }

    private static bool PathsEqual(string left, string right) => string.Equals(Path.TrimEndingDirectorySeparator(left),
        Path.TrimEndingDirectorySeparator(right), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
