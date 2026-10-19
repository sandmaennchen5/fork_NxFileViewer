using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Emignatik.NxFileViewer.Settings;
namespace Emignatik.NxFileViewer.FileLoading;

public sealed record SdBatchAreas(bool Enabled, bool Switch, bool Nintendo, bool EmuMmc, bool Hbmenu, bool Tools)
{
    public bool Dumps { get; init; }
    public bool NxdtRwPoc { get; init; }
    public bool DbiRootSaves { get; init; }
    public SdBatchSourceMode SourceMode { get; init; }
    public string CustomPaths { get; init; } = "";
    public static SdBatchAreas From(IAppSettings settings) => new(settings.SdBatchSelectedAreasOnly, settings.SdBatchSwitch,
        settings.SdBatchNintendo, settings.SdBatchEmuMmc, settings.SdBatchHbmenu, settings.SdBatchTools) { CustomPaths = settings.SdBatchCustomPaths, SourceMode = settings.SdBatchSourceMode, Dumps = settings.SdBatchDumps, NxdtRwPoc = settings.SdBatchNxdtRwPoc, DbiRootSaves = settings.SdBatchDbiRootSaves };
    public static bool IsSdRoot(IEnumerable<string> names) => names.Any(name =>
        name.Equals("Nintendo", StringComparison.OrdinalIgnoreCase) || name.Equals("emuMMC", StringComparison.OrdinalIgnoreCase)
        || name.Equals("hbmenu.nro", StringComparison.OrdinalIgnoreCase));
    private IEnumerable<string> Paths() {
        if (Switch) yield return "switch";
        if (Nintendo) yield return "Nintendo";
        if (EmuMmc) yield return "emuMMC";
        if (Hbmenu) yield return "hbmenu.nro";
        if (Tools) yield return "tools";
        if (DbiRootSaves) yield return "DBIsaves";
        if (Dumps) yield return "dumps";
        if (NxdtRwPoc) yield return "nxdt_rw_poc";

        foreach (var line in CustomPaths.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)) {
            var path = line.Trim().Replace('\\', '/').Trim('/');
            if (path.Length == 0) continue;
            // Only paths relative to the SD root, never drive/UNC paths or parent traversal.
            if (line.Trim().StartsWith("//") || line.Trim().StartsWith(@"\\") || path.Split('/').Any(part =>
                part.Length == 0 || part is "." or ".." || part.EndsWith('.') || part.EndsWith(' ') || part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
                throw new InvalidDataException(Localization.LocalizationManager.Instance.Current.Keys.SdBatch_InvalidPath + " " + line);
            yield return path;
        }
    }
    public bool IncludesRelative(string relative) {
        relative = relative.Replace('\\', '/').Trim('/');
        return Paths().Any(path => relative.Equals(path, StringComparison.OrdinalIgnoreCase) || relative.StartsWith(path + "/", StringComparison.OrdinalIgnoreCase));
    }
    public bool AllowsTraversal(string relative, bool directory) {
        relative = relative.Replace('\\', '/').Trim('/');
        return IncludesRelative(relative) || directory && Paths().Any(path => path.StartsWith(relative + "/", StringComparison.OrdinalIgnoreCase));
    }
    public bool AppliesToNames(IEnumerable<string> names) => Enabled && (SourceMode == SdBatchSourceMode.SwitchSd
        || SourceMode == SdBatchSourceMode.Auto && IsSdRoot(names));
    public bool AppliesTo(string root) => Directory.Exists(root) && AppliesToNames(Directory.EnumerateFileSystemEntries(root).Select(Path.GetFileName)!);
    public bool IncludesPath(string root, string path) {
        var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
        return IncludesRelative(relative);
    }
    public IEnumerable<string> EnumerateFiles(string root, SearchOption option) {
        if (!AppliesTo(root)) return Directory.EnumerateFiles(root, "*", option);
        var roots = Paths().Select(path => Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar)))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return roots.SelectMany(path => Directory.Exists(path) ? Directory.EnumerateFiles(path, "*", option)
            : File.Exists(path) ? new[] { path } : Array.Empty<string>()).Distinct(StringComparer.OrdinalIgnoreCase);
    }
}
