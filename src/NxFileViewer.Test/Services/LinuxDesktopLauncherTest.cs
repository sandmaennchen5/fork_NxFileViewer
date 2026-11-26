using System;
using System.IO;
using NxFileViewer.Avalonia;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services;

public sealed class LinuxDesktopLauncherTest : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("nx-launcher-");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WritesIndependentLaunchersWithDirectExecutableAndPersistentIcon(bool appImage)
    {
        var program = _root.CreateSubdirectory("Folder 50%");
        var executable = Path.Combine(program.FullName, appImage ? "NxFileViewer.AppImage" : "NxFileViewerFork");
        File.WriteAllText(executable, "");
        var icon = Path.Combine(program.FullName, "nxfv-icon.png");
        if (!appImage) File.WriteAllBytes(icon, [137, 80, 78, 71]);
        var data = _root.CreateSubdirectory("user-data").FullName;
        var menu = Path.Combine(data, "applications");
        var desktop = Path.Combine(_root.FullName, "Desktop");

        var menuLauncher = LinuxDesktopLauncher.Create(executable, data, menu, appImage);
        Assert.False(Directory.Exists(desktop));
        var desktopLauncher = LinuxDesktopLauncher.Create(executable, data, desktop, appImage);
        var contents = File.ReadAllText(menuLauncher);
        Assert.Equal(contents, File.ReadAllText(desktopLauncher));
        Assert.Contains(appImage ? "NxFileViewer.AppImage\" %F" : "NxFileViewerFork\" %F", contents);
        Assert.Contains("Folder 50%%", contents);
        Assert.DoesNotContain(".sh", contents);
        if (appImage)
        {
            Assert.Contains("NxFileViewerFork.AppImage.png", contents);
            Assert.Equal(new byte[] { 137, 80, 78, 71 }, File.ReadAllBytes(Path.Combine(data, "icons", "NxFileViewerFork.AppImage.png"))[..4]);
        }
        else Assert.Contains("nxfv-icon.png", contents);
    }

    public void Dispose() => _root.Delete(recursive: true);
}
