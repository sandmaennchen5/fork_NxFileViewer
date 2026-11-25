using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Emignatik.NxFileViewer.Services.Updates;

public static class PluginStorageCleanup
{
    private static string Full(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    public static IReadOnlyList<string> Downloads(string pluginRoot)
    {
        var root = Full(Path.Combine(pluginRoot, "Downloads"));
        if (!Directory.Exists(root) || !OldVersionCleanup.NoLinks(root)) return Array.Empty<string>();
        return Directory.EnumerateFiles(root).Where(path =>
        {
            if (!Regex.IsMatch(Path.GetFileName(path), @"\A[0-9a-fA-F]{64}\.download\z") ||
                !OldVersionCleanup.NoLinks(path)) return false;
            try { using var probe = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None); return true; }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }).ToArray();
    }

    public static void DeleteDownloads(string pluginRoot, IReadOnlyList<string> confirmedPaths)
    {
        foreach (var path in confirmedPaths)
        {
            var full = Full(path);
            if (!Downloads(pluginRoot).Contains(full, StringComparer.OrdinalIgnoreCase))
                throw new IOException("Download is in use or no longer safe to remove: " + path);
            File.Delete(full);
        }
    }

    // Legacy sessions have no process marker. Be conservative when any possible
    // Kronos console/interpreter is running, including in another viewer instance.
    private static bool PossibleKronosProcess()
    {
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    if (new[] { "cmd", "python", "pythonw", "py", "NSCB" }
                        .Contains(process.ProcessName, StringComparer.OrdinalIgnoreCase)) return true;
                }
                catch (InvalidOperationException) { }
                catch (System.ComponentModel.Win32Exception) { return true; }
            }
        }
        return false;
    }

    public static IReadOnlyList<string> Sessions(string root, Func<bool>? processRunning = null)
    {
        root = Full(root);
        if (!Directory.Exists(root) || !OldVersionCleanup.NoLinks(root) ||
            (processRunning ?? PossibleKronosProcess)()) return Array.Empty<string>();
        var cutoff = DateTime.UtcNow.AddDays(-1);
        return Directory.EnumerateDirectories(root).Where(path =>
            Guid.TryParseExact(Path.GetFileName(path), "N", out _) &&
            Directory.GetCreationTimeUtc(path) < cutoff && Directory.GetLastWriteTimeUtc(path) < cutoff &&
            OldVersionCleanup.SafeTree(path)).ToArray();
    }

    public static void DeleteSessions(string root, IReadOnlyList<string> confirmedPaths, Func<bool>? processRunning = null)
    {
        root = Full(root);
        foreach (var path in confirmedPaths)
        {
            var full = Full(path);
            if (!string.Equals(Path.GetDirectoryName(full), root, StringComparison.OrdinalIgnoreCase) ||
                !Sessions(root, processRunning).Contains(full, StringComparer.OrdinalIgnoreCase))
                throw new IOException("Session is active, recent, contains keys or is no longer safe to remove: " + path);
            Directory.Delete(full, recursive: true);
        }
    }
}
