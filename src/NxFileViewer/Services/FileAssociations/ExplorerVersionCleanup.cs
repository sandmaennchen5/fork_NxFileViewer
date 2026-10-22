using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Emignatik.NxFileViewer.Services.FileAssociations;

public static class ExplorerVersionCleanup
{
    public static IReadOnlyList<string> RemoveUnused(string root, IEnumerable<string> registeredLibraries)
    {
        var removed = new List<string>();
        try
        {
            root = Path.GetFullPath(root);
            var active = registeredLibraries.Select(Path.GetFullPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (!Directory.Exists(root) || !NoLinks(root)) return removed;
            foreach (var architecture in new[] { "x64", "x86", "arm64" })
            {
                var architectureRoot = Path.Combine(root, architecture);
                if (!Directory.Exists(architectureRoot) || !NoLinks(architectureRoot)) continue;
                foreach (var directory in Directory.EnumerateDirectories(architectureRoot))
                {
                    try
                    {
                        var hash = Path.GetFileName(directory);
                        if (!Regex.IsMatch(hash, "^[0-9a-fA-F]{64}$") || !NoLinks(directory)) continue;
                        var library = Path.Combine(directory, "NxFileViewer.Explorer.dll");
                        if (active.Contains(library) || !File.Exists(library) || !NoLinks(library)) continue;
                        var entries = Directory.GetFileSystemEntries(directory);
                        if (entries.Length != 1 || !entries[0].Equals(library, StringComparison.OrdinalIgnoreCase)) continue;
                        // Refuse loaded/locked versions. Allow deletion through our own verification handle only.
                        using (var probe = new FileStream(library, FileMode.Open, FileAccess.ReadWrite, FileShare.Delete))
                        {
                            if (!Convert.ToHexString(SHA256.HashData(probe)).Equals(hash, StringComparison.OrdinalIgnoreCase)) continue;
                            File.Delete(library);
                        }
                        Directory.Delete(directory); // Never recursively delete unexpected contents.
                        removed.Add(directory);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
                }
                try { if (Directory.GetFileSystemEntries(architectureRoot).Length == 0) Directory.Delete(architectureRoot); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
            }
            try { if (Directory.GetFileSystemEntries(root).Length == 0) Directory.Delete(root); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or System.Security.SecurityException) { }
        return removed;
    }
    private static bool NoLinks(string path)
    {
        for (var item = Path.GetFullPath(path); !string.IsNullOrEmpty(item); item = Path.GetDirectoryName(item))
            if ((File.Exists(item) || Directory.Exists(item)) && (File.GetAttributes(item) & FileAttributes.ReparsePoint) != 0) return false;
        return true;
    }
}