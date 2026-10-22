using System.IO;
using Emignatik.NxFileViewer.FileLoading;

namespace Emignatik.NxFileViewer.Services.Integrity;

public static class BatchSourceGrouping
{
    public static string Key(string path)
    {
        if (PackageZip.IsMember(path)) return Path.GetFullPath(PackageZip.ArchivePath(path));
        if (PackageZip.IsArchive(path)) return Path.GetFullPath(path);
        return Path.GetDirectoryName(Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) ?? path;
    }
}
