using System;
using System.IO;
using Emignatik.NxFileViewer.Services.Updates;

namespace NxFileViewer.Transition;

public static class LegacyDataMigration
{
    public static void Move(string root)
    {
        var oldSettings = Path.Combine(root, "NxFileViewer.settings.json");
        var newSettings = Path.Combine(root, "NxFileViewerFork.settings.json");
        ViewerPackageLayout.NoLinks(oldSettings); ViewerPackageLayout.NoLinks(newSettings);
        if (File.Exists(oldSettings) && !File.Exists(newSettings)) File.Move(oldSettings, newSettings);
        foreach (var pair in new[] { ("Cache/TitleDB", "Plugins/TitleDB"), ("Cache/Updates", "Plugins/CacheUpdates"), ("fw/hashes", "Plugins/fwhashes") })
            MoveMissing(Path.Combine(root, pair.Item1), Path.Combine(root, pair.Item2));
    }
    private static void MoveMissing(string source, string destination)
    {
        if (!Directory.Exists(source)) return;
        ViewerPackageLayout.NoLinks(source); ViewerPackageLayout.NoLinks(destination);
        Directory.CreateDirectory(destination);
        foreach (var entry in Directory.EnumerateFileSystemEntries(source))
        {
            ViewerPackageLayout.NoLinks(entry);
            var target = Path.Combine(destination, Path.GetFileName(entry));
            ViewerPackageLayout.NoLinks(target);
            if (Directory.Exists(entry)) MoveMissing(entry, target);
            else if (!File.Exists(target)) File.Move(entry, target);
            // Conflicting files stay at their original path; never discard user data.
        }
        if (Directory.GetFileSystemEntries(source).Length == 0) Directory.Delete(source);
    }
}