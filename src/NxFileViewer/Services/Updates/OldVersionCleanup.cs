using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Emignatik.NxFileViewer.Services.Updates;

public static class OldVersionCleanup
{
    private static string Full(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    private static bool Inside(string path, string directory) => Full(path).StartsWith(Full(directory) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static bool NoLinks(string path)
    {
        for (var item = Full(path); !string.IsNullOrEmpty(item); item = Path.GetDirectoryName(item))
            if ((File.Exists(item) || Directory.Exists(item)) && (File.GetAttributes(item) & FileAttributes.ReparsePoint) != 0) return false;
        return true;
    }

    private static bool SafeTree(string directory)
    {
        if (!NoLinks(directory)) return false;
        foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
        {
            var attributes = File.GetAttributes(entry);
            if ((attributes & FileAttributes.ReparsePoint) != 0) return false;
            if ((attributes & FileAttributes.Directory) != 0) { if (!SafeTree(entry)) return false; }
            else
            {
                if (Path.GetExtension(entry).Equals(".keys", StringComparison.OrdinalIgnoreCase)) return false;
                try { using var probe = File.Open(entry, FileMode.Open, FileAccess.ReadWrite, FileShare.None); }
                catch (IOException) { return false; }
                catch (UnauthorizedAccessException) { return false; }
            }
        }
        return true;
    }

    public static IReadOnlyList<string> PluginVersions(string root, params string?[] protectedExecutables)
    {
        root = Full(root);
        if (!Directory.Exists(root) || !NoLinks(root) || protectedExecutables.Length == 0 ||
            string.IsNullOrWhiteSpace(protectedExecutables[0]) || !File.Exists(protectedExecutables[0])) return Array.Empty<string>();
        return Directory.EnumerateDirectories(root).Where(path =>
        {
            var name = Path.GetFileName(path);
            return name.StartsWith("version-", StringComparison.Ordinal) && Guid.TryParseExact(name[8..], "N", out _) &&
                !protectedExecutables.Where(value => !string.IsNullOrWhiteSpace(value)).Any(value => Inside(value!, path)) && SafeTree(path);
        }).OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static void DeletePluginVersion(string root, string directory, params string?[] protectedExecutables)
    {
        root = Full(root); directory = Full(directory);
        if (!string.Equals(Path.GetDirectoryName(directory), root, StringComparison.OrdinalIgnoreCase) ||
            !PluginVersions(root, protectedExecutables).Contains(directory, StringComparer.OrdinalIgnoreCase))
            throw new IOException("The plugin version is active, protected, or no longer safe to remove: " + directory);
        Directory.Delete(directory, recursive: true);
    }

    public static IReadOnlyList<string> ViewerUpdateDirectories(string applicationDirectory)
    {
        var root = Full(Path.Combine(applicationDirectory, "Updates"));
        if (!Directory.Exists(root) || !NoLinks(root)) return Array.Empty<string>();
        string target;
        try { target = Full(ViewerPackageLayout.InstalledExecutable(applicationDirectory)); }
        catch (Exception ex) when (ex is IOException or JsonException or ArgumentException) { return Array.Empty<string>(); }
        if (!File.Exists(target) || !NoLinks(target)) return Array.Empty<string>();
        var backups = new List<string>();
        foreach (var stage in Directory.EnumerateDirectories(root))
        {
            if (!Guid.TryParseExact(Path.GetFileName(stage), "N", out _) || !NoLinks(stage)) continue;
            var completed = Path.Combine(stage, "completed");
            var plan = Path.Combine(stage, "plan.json");
            if (!File.Exists(completed) || !File.Exists(plan) ||
                !SafeTree(stage) ||
                File.Exists(Path.Combine(stage, "error.txt")) || File.Exists(Path.Combine(stage, "new.exe")) ||
                File.GetLastWriteTimeUtc(completed) > DateTime.UtcNow.AddMinutes(-1)) continue;
            if (File.Exists(Path.Combine(stage, "journal.json")))
            {
                try
                {
                    var journal = ViewerRollback.ReadJournal(stage, applicationDirectory);
                    using var packagePlan = JsonDocument.Parse(File.ReadAllText(plan));
                    var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "completed", "install.ps1", "plan.json", "ready", "journal.json", "release.zip" };
                    var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "payload", "backup" };
                    void Allow(string relative)
                    {
                        allowed.Add(relative);
                        for (var parent = Path.GetDirectoryName(relative.Replace('/', Path.DirectorySeparatorChar)); !string.IsNullOrEmpty(parent); parent = Path.GetDirectoryName(parent))
                            directories.Add(parent.Replace('\\', '/'));
                    }
                    foreach (var file in packagePlan.RootElement.GetProperty("Files").EnumerateArray())
                        if (!file.TryGetProperty("Remove", out var remove) || !remove.GetBoolean()) Allow("payload/" + ViewerPackageLayout.Relative(file.GetProperty("Path").GetString()!));
                    if (packagePlan.RootElement.TryGetProperty("Directories", out var listedDirectories))
                        foreach (var value in listedDirectories.EnumerateArray())
                        {
                            var relative = "payload/" + ViewerPackageLayout.Relative(value.GetString()!);
                            directories.Add(relative); Allow(relative + "/.directory-marker"); allowed.Remove(relative + "/.directory-marker");
                        }
                    foreach (var file in journal.Entries.Where(e => e.Existed)) Allow("backup/" + ViewerPackageLayout.Relative(file.Path));
                    string RelativeToStage(string entry) => Path.GetRelativePath(stage, entry).Replace('\\', '/');
                    if (Directory.EnumerateFiles(stage, "*", SearchOption.AllDirectories).Any(file => !allowed.Contains(RelativeToStage(file))) ||
                        Directory.EnumerateDirectories(stage, "*", SearchOption.AllDirectories).Any(directory => !directories.Contains(RelativeToStage(directory)))) continue;
                    backups.Add(stage);
                }
                catch (Exception ex) when (ex is IOException or JsonException or ArgumentException or KeyNotFoundException or InvalidOperationException or UnauthorizedAccessException) { }
                continue;
            }
            var knownFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "completed", "install.ps1", "plan.json", "ready", "previous.exe", "release.zip" };
            if (Directory.EnumerateDirectories(stage).Any() || Directory.EnumerateFiles(stage).Any(file => !knownFiles.Contains(Path.GetFileName(file)))) continue;
            try
            {
                using var json = JsonDocument.Parse(File.ReadAllText(plan));
                if (json.RootElement.ValueKind == JsonValueKind.Object && json.RootElement.TryGetProperty("Target", out var value) && value.ValueKind == JsonValueKind.String &&
                    string.Equals(Full(value.GetString()!), target, StringComparison.OrdinalIgnoreCase)) backups.Add(stage);
            }
            catch (JsonException) { }
            catch (ArgumentException) { }
        }
        return backups.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static void DeleteViewerUpdates(string applicationDirectory, IReadOnlyList<string> confirmedPaths)
    {
        foreach (var path in confirmedPaths)
        {
            // Recheck completion and exact paths immediately before each deletion.
            if (!ViewerUpdateDirectories(applicationDirectory).Contains(Full(path), StringComparer.OrdinalIgnoreCase))
                throw new IOException("The update directory is no longer safe to remove: " + path);
            Directory.Delete(Full(path), recursive: true);
        }
    }
}