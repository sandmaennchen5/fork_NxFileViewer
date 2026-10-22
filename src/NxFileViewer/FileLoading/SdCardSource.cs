using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace Emignatik.NxFileViewer.FileLoading;

public static class SdCardSource
{
    // NAX0 paths are relative to Nintendo/Contents, not to the selected folder.
    public static string? FindContents(string path)
        => FindAllContents(path).FirstOrDefault()?.ContentsPath;

    public static IReadOnlyList<SdContentSource> FindAllContents(string path)
    {
        var full = Path.GetFullPath(path);
        if (!Directory.Exists(full)) return [];
        var sources = new List<SdContentSource>();
        foreach (var candidate in new[] { Path.Combine(full, "Nintendo", "Contents"), Path.Combine(full, "Contents"), full })
            Add(candidate);
        var emuRoot = Path.GetFileName(full).Equals("emuMMC", StringComparison.OrdinalIgnoreCase) ? full : Path.Combine(full, "emuMMC");
        for (var i = 1; i <= 3; i++) Add(Path.Combine(emuRoot, "RAW" + i, "Nintendo", "Contents"));
        for (var i = 0; i <= 99; i++) Add(Path.Combine(emuRoot, "SD" + i.ToString("D2"), "Nintendo", "Contents"));
        return sources;

        void Add(string contents)
        {
            if (!Directory.Exists(Path.Combine(contents, "registered")) || sources.Any(s => s.ContentsPath.Equals(contents, StringComparison.OrdinalIgnoreCase))) return;
            var nintendo = Directory.GetParent(contents);
            var slot = nintendo?.Parent;
            var label = slot?.Parent?.Name.Equals("emuMMC", StringComparison.OrdinalIgnoreCase) == true
                ? $"emuMMC {slot.Name} — {contents}" : $"Nintendo — {contents}";
            sources.Add(new SdContentSource(contents, label));
        }
    }

    public static IReadOnlyList<SdContentSource> FindRelatedContents(string contents)
    {
        var parent = Directory.GetParent(contents);
        if (parent?.Name.Equals("Nintendo", StringComparison.OrdinalIgnoreCase) != true) return FindAllContents(contents);
        var root = parent.Parent;
        if (root?.Parent?.Name.Equals("emuMMC", StringComparison.OrdinalIgnoreCase) == true) root = root.Parent.Parent;
        return FindAllContents(root?.FullName ?? contents);
    }
    public static bool IsNax0(string path)
    {
        if (Directory.Exists(path)) path = Path.Combine(path, "00");
        if (!File.Exists(path)) return false;
        using var stream = File.OpenRead(path);
        if (stream.Length < 0x24) return false;
        stream.Position = 0x20;
        Span<byte> magic = stackalloc byte[4];
        return stream.Read(magic) == 4 && magic.SequenceEqual("NAX0"u8);
    }
    public static string? ContentsForFile(string path)
    {
        var directory = new DirectoryInfo(Directory.Exists(path) ? path : Path.GetDirectoryName(Path.GetFullPath(path))!);
        while (directory != null)
        {
            if (directory.Name.Equals("Contents", StringComparison.OrdinalIgnoreCase) && Directory.Exists(Path.Combine(directory.FullName, "registered")))
                return directory.FullName;
            directory = directory.Parent;
        }
        return null;
    }
}

public sealed record SdContentSource(string ContentsPath, string DisplayName);
