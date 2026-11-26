using System;
using System.IO;
using NxFileViewer.Avalonia;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services;

public sealed class ViewerDataDirectoryTest : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("nx-linux-data-");

    [Fact]
    public void AppImageUsesItsOwnDirectoryEvenBeforeTheDataFolderExists()
    {
        var imageDirectory = _root.CreateSubdirectory("USB drive").FullName;
        var image = Path.Combine(imageDirectory, "NxFileViewer Fork.AppImage");
        var mountedProgram = _root.CreateSubdirectory("mounted-app").FullName;

        Assert.Equal(Path.Combine(imageDirectory, "NxFileViewerFork-Daten"),
            ViewerDataDirectory.ResolveRoot(true, image, mountedProgram));
    }

    [Fact]
    public void LinuxExecutableUsesItsOwnDirectoryEvenBeforeTheDataFolderExists()
    {
        var program = _root.CreateSubdirectory("program").FullName;
        Assert.Equal(Path.Combine(program, "NxFileViewerFork-Daten"),
            ViewerDataDirectory.ResolveRoot(true, null, program));
    }

    [Fact]
    public void OtherPlatformsKeepTheProgramDirectory()
    {
        Assert.Equal(_root.FullName, ViewerDataDirectory.ResolveRoot(false, null, _root.FullName));
    }

    [Fact]
    public void WindowsUsesNewFolderUnlessAnExistingInstallationStillUsesTheExecutableDirectory()
    {
        var program = _root.CreateSubdirectory("windows").FullName;
        var destination = Path.Combine(program, ViewerDataDirectory.DataFolderName);
        Assert.Equal(destination, ViewerDataDirectory.ResolveRoot(false, null, program, isWindows: true));
        File.WriteAllText(Path.Combine(program, ViewerPreferences.FileName), "{}");
        Assert.Equal(program, ViewerDataDirectory.ResolveRoot(false, null, program, isWindows: true));

        Assert.Equal((1, 0), ViewerDataMigration.Copy(program, destination));
        Assert.Equal(destination, ViewerDataDirectory.ResolveRoot(false, null, program, isWindows: true));
        Assert.True(File.Exists(Path.Combine(program, ViewerPreferences.FileName)));
        Assert.True(ViewerDataMigration.WasCopied(program, destination));
    }

    [Fact]
    public void MigrationCopiesMissingDataWithoutReplacingCurrentSettings()
    {
        var legacy = _root.CreateSubdirectory("legacy");
        var current = _root.CreateSubdirectory("current");
        File.WriteAllText(Path.Combine(legacy.FullName, ViewerPreferences.FileName), "old");
        File.WriteAllText(Path.Combine(current.FullName, ViewerPreferences.FileName), "new");
        File.WriteAllText(Path.Combine(legacy.CreateSubdirectory("History").FullName, "scan.zip"), "history");
        File.WriteAllText(Path.Combine(legacy.FullName, "NxFileViewerFork.exe"), "program");

        Assert.Equal((1, 1), ViewerDataMigration.Copy(legacy.FullName, current.FullName));
        Assert.Equal("new", File.ReadAllText(Path.Combine(current.FullName, ViewerPreferences.FileName)));
        Assert.Equal("history", File.ReadAllText(Path.Combine(current.FullName, "History", "scan.zip")));
        Assert.False(File.Exists(Path.Combine(current.FullName, "NxFileViewerFork.exe")));
        Assert.True(ViewerDataMigration.WasCopied(legacy.FullName, current.FullName));
    }


    [Fact]
    public void MacBundleMigrationKeepsTheSourceAndDoesNotRepeat()
    {
        var bundle = _root.CreateSubdirectory("NxFileViewerFork.app/Contents/MacOS");
        var destination = _root.CreateSubdirectory("Library/Application Support/NxFileViewer Fork");
        var oldSettings = Path.Combine(bundle.FullName, ViewerPreferences.FileName);
        var newSettings = Path.Combine(destination.FullName, ViewerPreferences.FileName);
        File.WriteAllText(oldSettings, "legacy");
        File.WriteAllText(newSettings, "existing");
        var history = bundle.CreateSubdirectory("History");
        File.WriteAllText(Path.Combine(history.FullName, "scan-123.zip"), "scan");
        File.WriteAllText(Path.Combine(bundle.CreateSubdirectory("Logs").FullName, "NxFileViewer-test.log"), "log");

        ViewerDataDirectory.MigrateMacBundleData(bundle.FullName, destination.FullName);

        Assert.Equal("existing", File.ReadAllText(newSettings));
        Assert.Equal("legacy", File.ReadAllText(oldSettings));
        Assert.Equal("scan", File.ReadAllText(Path.Combine(destination.FullName, "History", "scan-123.zip")));
        Assert.Equal("log", File.ReadAllText(Path.Combine(destination.FullName, "Logs", "NxFileViewer-test.log")));
        File.WriteAllText(Path.Combine(history.FullName, "scan-456.zip"), "new legacy data");
        ViewerDataDirectory.MigrateMacBundleData(bundle.FullName, destination.FullName);
        Assert.False(File.Exists(Path.Combine(destination.FullName, "History", "scan-456.zip")));
    }

    public void Dispose() => _root.Delete(recursive: true);
}
