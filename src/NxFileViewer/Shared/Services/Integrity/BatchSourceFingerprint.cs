using System;
using System.IO;
using System.Linq;
using Emignatik.NxFileViewer.FileLoading;

namespace Emignatik.NxFileViewer.Services.Integrity;

public static class BatchSourceFingerprint
{
    public static string? Read(string path)
    {
        try
        {
            var archivePath = PackageZip.ArchivePath(path);
            if (archivePath != path) path = archivePath.TrimEnd();
            if (Services.Mtp.MtpDirectSource.IsDirect(path)) return null;
            if (File.Exists(path))
            {
                var parts = Services.Nand.NandDetection.SplitFiles(path);
                if (parts.Count > 1) return string.Join("|", parts.Select(part => Describe(part, Path.GetFileName(part))));
                return Describe(path);
            }
            if (Directory.Exists(path)) return string.Join("|", Directory.GetFiles(path, "*", SearchOption.AllDirectories)
                .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
                .Select(file => Describe(file, Path.GetRelativePath(path, file))));
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        return null;
    }

    private static string Describe(string path, string? name = null)
    {
        var file = new FileInfo(path);
        return (name == null ? "" : name + ":") + $"{file.Length}:{file.LastWriteTimeUtc.Ticks}";
    }
}
