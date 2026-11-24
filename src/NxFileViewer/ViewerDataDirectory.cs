using System;
using System.IO;

namespace NxFileViewer.Avalonia;

internal static class ViewerDataDirectory
{
    internal static bool IsAppImage => OperatingSystem.IsLinux() &&
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APPIMAGE"));

    internal static string Root => IsAppImage
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify),
            "NxFileViewerFork")
        : AppContext.BaseDirectory;
}
