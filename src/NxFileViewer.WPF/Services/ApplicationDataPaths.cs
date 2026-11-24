using System.IO;

namespace Emignatik.NxFileViewer.Services;

public static class ApplicationDataPaths
{
    public static string TitleDb(string root) => Path.Combine(root, "Plugins", "TitleDB");
    public static string UpdateCache(string root) => Path.Combine(root, "Plugins", "CacheUpdates");
    public static string FirmwareHashes(string root) => Path.Combine(root, "Plugins", "fwhashes");
}