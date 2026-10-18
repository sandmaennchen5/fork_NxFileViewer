using System;
using System.Collections.Generic;
using System.IO;

namespace Emignatik.NxFileViewer.Services;

public static class ApplicationDataPaths
{
    private static readonly HashSet<string> Migrated = new(StringComparer.OrdinalIgnoreCase);
    public static string TitleDb(string root) { MigrateLegacy(root); return Path.Combine(root, "Plugins", "TitleDB"); }
    public static string UpdateCache(string root) { MigrateLegacy(root); return Path.Combine(root, "Plugins", "CacheUpdates"); }
    public static string FirmwareHashes(string root) { MigrateLegacy(root); return Path.Combine(root, "Plugins", "fwhashes"); }
    public static void MigrateLegacy(string root)
    {
        root = Path.GetFullPath(root);
        lock (Migrated)
        {
            if (Migrated.Contains(root)) return;
            foreach (var pair in new[] { ("Cache/TitleDB", "Plugins/TitleDB"), ("Cache/Updates", "Plugins/CacheUpdates"), ("fw/hashes", "Plugins/fwhashes") })
            {
                try { CopyMissing(Path.Combine(root, pair.Item1), Path.Combine(root, pair.Item2)); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { continue; }
            }
            Migrated.Add(root);
        }
    }
    private static void CopyMissing(string source, string destination)
    {
        if (!Directory.Exists(source)) return;
        Updates.ViewerPackageLayout.NoLinks(source); Updates.ViewerPackageLayout.NoLinks(destination);
        Directory.CreateDirectory(destination);
        foreach (var entry in Directory.EnumerateFileSystemEntries(source))
        {
            if ((File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0) continue;
            var target = Path.Combine(destination, Path.GetFileName(entry));
            Updates.ViewerPackageLayout.NoLinks(target);
            if (Directory.Exists(entry)) CopyMissing(entry, target);
            else if (!File.Exists(target)) File.Copy(entry, target, false);
        }
    }
}