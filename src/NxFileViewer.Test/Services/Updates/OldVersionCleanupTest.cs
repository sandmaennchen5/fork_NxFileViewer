using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Emignatik.NxFileViewer.Services.Nand;
using Emignatik.NxFileViewer.Services.Nsz;
using Emignatik.NxFileViewer.Services.Updates;
using Emignatik.NxFileViewer.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services.Updates;

public sealed class OldVersionCleanupTest : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "old-version-test-" + Guid.NewGuid().ToString("N"));
    private string Version(string executable = "plugin.exe")
    {
        var directory = Path.Combine(_root, "version-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var file = Path.Combine(directory, executable);
        File.WriteAllText(file, "test executable");
        return file;
    }
    private void State(string current, string previous) => File.WriteAllText(Path.Combine(_root, "active.json"), JsonSerializer.Serialize(new
    {
        Executable = Path.GetRelativePath(_root, current), Version = "2", Digest = "asset-hash", ExecutableDigest = "exe-hash",
        PreviousExecutable = Path.GetRelativePath(_root, previous), PreviousVersion = "1", SourceAssetName = "plugin.zip"
    }));

    [Fact]
    public void OnlyOldManagedVersionsAreOfferedAndCustomPathsAndKeysAreProtected()
    {
        var current = Version();
        var old = Version();
        var custom = Version("custom.exe");
        var withKeys = Version();
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(withKeys)!, "prod.keys"), "keep keys");
        Directory.CreateDirectory(Path.Combine(_root, "Downloads"));
        File.WriteAllText(Path.Combine(_root, "Downloads", "cached.zip"), "keep cache");
        var candidates = OldVersionCleanup.PluginVersions(_root, current, custom);
        Assert.Equal(Path.GetDirectoryName(old), Assert.Single(candidates));
        OldVersionCleanup.DeletePluginVersion(_root, candidates[0], current, custom);
        Assert.False(File.Exists(old));
        Assert.True(File.Exists(current));
        Assert.True(File.Exists(custom));
        Assert.True(File.Exists(withKeys));
        Assert.True(File.Exists(Path.Combine(_root, "Downloads", "cached.zip")));
    }

    [Fact]
    public void MissingCurrentExecutableAndArbitraryPathsCannotBeCleaned()
    {
        var current = Version();
        var old = Version();
        Assert.Throws<IOException>(() => OldVersionCleanup.DeletePluginVersion(_root, _root, current));
        Assert.Throws<IOException>(() => OldVersionCleanup.DeletePluginVersion(_root, Path.GetTempPath(), current));
        Assert.Throws<IOException>(() => OldVersionCleanup.DeletePluginVersion(_root, Path.GetDirectoryName(current)!, current));
        File.Delete(current);
        Assert.Empty(OldVersionCleanup.PluginVersions(_root, current));
        Assert.Throws<IOException>(() => OldVersionCleanup.DeletePluginVersion(_root, Path.GetDirectoryName(old)!, current));
        Assert.True(File.Exists(old));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ManagersRemoveOldVersionAndClearRollbackWithoutChangingCurrentState(bool nand)
    {
        var current = Version();
        var previous = Version();
        State(current, previous);
        var settings = new AppSettings();
        if (nand)
        {
            var manager = new NandPluginManager(settings, _root);
            Assert.True(manager.CanRollback);
            manager.RemoveOldVersions(manager.OldVersionDirectories());
            Assert.False(manager.CanRollback);
            Assert.Equal(current, manager.ExecutablePath);
        }
        else
        {
            var manager = new NszPluginManager(settings, NullLogger<NszPluginManager>.Instance, _root);
            Assert.True(manager.CanRollback);
            manager.RemoveOldVersions(manager.OldVersionDirectories());
            Assert.False(manager.CanRollback);
            Assert.Equal(current, manager.ExecutablePath);
        }
        Assert.True(File.Exists(current));
        Assert.False(File.Exists(previous));
        using var state = JsonDocument.Parse(File.ReadAllText(Path.Combine(_root, "active.json")));
        var metadata = PluginVersionStorage.Metadata(Path.GetDirectoryName(current)!)!;
        Assert.Equal("2", metadata.Version);
        Assert.Equal("exe-hash", metadata.ExecutableDigest);
        Assert.False(state.RootElement.TryGetProperty("Previous", out _));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ManagersRecheckActiveVersionAfterRollback(bool nand)
    {
        var current = Version();
        var previous = Version();
        State(current, previous);
        var settings = new AppSettings();
        if (nand)
        {
            var manager = new NandPluginManager(settings, _root);
            var confirmed = manager.OldVersionDirectories();
            manager.Rollback();
            Assert.Throws<IOException>(() => manager.RemoveOldVersions(confirmed));
        }
        else
        {
            var manager = new NszPluginManager(settings, NullLogger<NszPluginManager>.Instance, _root);
            var confirmed = manager.OldVersionDirectories();
            manager.Rollback();
            Assert.Throws<IOException>(() => manager.RemoveOldVersions(confirmed));
        }
        Assert.True(File.Exists(current));
        Assert.True(File.Exists(previous));
    }

    [Fact]
    public void ManagersDoNotDeleteCustomExecutablesOrVersionsWithoutValidState()
    {
        var current = Version();
        var previous = Version();
        var settings = new AppSettings { NszExecutablePath = previous, NandExecutablePath = previous };
        var nsz = new NszPluginManager(settings, NullLogger<NszPluginManager>.Instance, _root);
        var nand = new NandPluginManager(settings, _root);
        Assert.Empty(nsz.OldVersionDirectories());
        Assert.Empty(nand.OldVersionDirectories());
        State(current, previous);
        Assert.Empty(nsz.OldVersionDirectories());
        Assert.Empty(nand.OldVersionDirectories());
        settings.EnablePlugins = false;
        Assert.Throws<InvalidOperationException>(() => nsz.RemoveOldVersions(new[] { Path.GetDirectoryName(previous)! }));
        Assert.Throws<InvalidOperationException>(() => nand.RemoveOldVersions(new[] { Path.GetDirectoryName(previous)! }));
        Assert.True(File.Exists(current));
        Assert.True(File.Exists(previous));
    }

    [Fact]
    public void ViewerRollbackUsesNewestBackupAndRetainsCurrentVersion()
    {
        var older = Path.GetDirectoryName(ViewerBackup())!;
        var latest = Path.GetDirectoryName(ViewerBackup())!;
        File.SetLastWriteTimeUtc(Path.Combine(older, "completed"), DateTime.UtcNow.AddMinutes(-10));
        File.SetLastWriteTimeUtc(Path.Combine(latest, "completed"), DateTime.UtcNow.AddMinutes(-3));
        File.WriteAllText(Path.Combine(latest, "previous.exe"), "previous version");
        Assert.Equal(Path.Combine(latest, "previous.exe"), ViewerRollback.FindBackup(_root));
        var prepared = ViewerRollback.Prepare(_root);
        Assert.Equal("previous version", File.ReadAllText(prepared.Executable));
        Assert.True(File.Exists(Path.Combine(_root, "NxFileViewer.exe")));
        Assert.True(File.Exists(Path.Combine(latest, "previous.exe")));
        OldVersionCleanup.DeleteViewerUpdates(_root, OldVersionCleanup.ViewerUpdateDirectories(_root));
        Assert.Null(ViewerRollback.FindBackup(_root));
        Assert.Throws<IOException>(() => ViewerRollback.Prepare(_root));
    }
    private string ViewerBackup(bool completed = true, bool recent = false, bool failed = false, bool pending = false, string? target = null)
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "NxFileViewer.exe"), "current");
        var stage = Path.Combine(_root, "Updates", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        var backup = Path.Combine(stage, "previous.exe");
        File.WriteAllText(backup, "previous");
        File.WriteAllText(Path.Combine(stage, "plan.json"), JsonSerializer.Serialize(new { Target = target ?? Path.Combine(_root, "NxFileViewer.exe") }));
        File.WriteAllText(Path.Combine(stage, "install.ps1"), "helper");
        File.WriteAllText(Path.Combine(stage, "ready"), "ready");
        if (completed)
        {
            var marker = Path.Combine(stage, "completed");
            File.WriteAllText(marker, "installed");
            if (!recent) File.SetLastWriteTimeUtc(marker, DateTime.UtcNow.AddMinutes(-5));
        }
        if (failed) File.WriteAllText(Path.Combine(stage, "error.txt"), "failed");
        if (pending) File.WriteAllText(Path.Combine(stage, "new.exe"), "pending");
        return backup;
    }

    [Fact]
    public void ViewerRemovesWholeSuccessfulUpdateDirectoryAndPreservesUnrelatedFiles()
    {
        var old = ViewerBackup();
        var recent = ViewerBackup(recent: true);
        var failed = ViewerBackup(failed: true);
        var pending = ViewerBackup(pending: true);
        var incomplete = ViewerBackup(completed: false);
        var foreign = ViewerBackup(target: Path.Combine(_root, "other.exe"));
        File.WriteAllText(Path.Combine(_root, "prod.keys"), "keep keys");
        var withUserFile = ViewerBackup();
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(withUserFile)!, "user.txt"), "keep user file");
        var paths = OldVersionCleanup.ViewerUpdateDirectories(_root);
        Assert.Equal(Path.GetDirectoryName(old), Assert.Single(paths));
        OldVersionCleanup.DeleteViewerUpdates(_root, paths);
        Assert.False(Directory.Exists(Path.GetDirectoryName(old)));
        Assert.All(new[] { recent, failed, pending, incomplete, foreign, withUserFile }, path => Assert.True(File.Exists(path)));
        Assert.Equal("current", File.ReadAllText(Path.Combine(_root, "NxFileViewer.exe")));
        Assert.True(File.Exists(Path.Combine(_root, "prod.keys")));
        Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(withUserFile)!, "user.txt")));
    }

    [Fact]
    public void ViewerRechecksBackupAndRejectsArbitraryOrNowFailedPaths()
    {
        var old = ViewerBackup();
        var paths = OldVersionCleanup.ViewerUpdateDirectories(_root);
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(old)!, "error.txt"), "failure after listing");
        Assert.Throws<IOException>(() => OldVersionCleanup.DeleteViewerUpdates(_root, paths));
        Assert.Throws<IOException>(() => OldVersionCleanup.DeleteViewerUpdates(_root, new[] { Path.Combine(_root, "NxFileViewer.exe") }));
        Assert.True(File.Exists(old));
        File.Delete(Path.Combine(_root, "NxFileViewer.exe"));
        Assert.Empty(OldVersionCleanup.ViewerUpdateDirectories(_root));
    }

    [Fact]
    public void LockedOldPluginFilesAreNotOfferedOrPartiallyDeleted()
    {
        var current = Version();
        var old = Version();
        using (var locked = File.Open(old, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Assert.Empty(OldVersionCleanup.PluginVersions(_root, current));
            Assert.Throws<IOException>(() => OldVersionCleanup.DeletePluginVersion(_root, Path.GetDirectoryName(old)!, current));
        }
        Assert.True(File.Exists(old));
        Assert.Single(OldVersionCleanup.PluginVersions(_root, current));
    }

    [Fact]
    public void PluginDirectoryJunctionsAreNeverTraversedOrRemoved()
    {
        var current = Version();
        var outside = Path.Combine(_root, "user-data");
        Directory.CreateDirectory(outside);
        var keep = Path.Combine(outside, "keep.txt");
        File.WriteAllText(keep, "keep");
        var link = Path.Combine(_root, "version-" + Guid.NewGuid().ToString("N"));
        var start = new System.Diagnostics.ProcessStartInfo("cmd.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { "/d", "/c", "mklink", "/J", link, outside }) start.ArgumentList.Add(argument);
        using var process = System.Diagnostics.Process.Start(start)!;
        Assert.True(process.WaitForExit(10000));
        Assert.Equal(0, process.ExitCode);
        try
        {
            Assert.Empty(OldVersionCleanup.PluginVersions(_root, current));
            Assert.Throws<IOException>(() => OldVersionCleanup.DeletePluginVersion(_root, link, current));
            Assert.Equal("keep", File.ReadAllText(keep));
        }
        finally { Directory.Delete(link); }
    }

    [Fact]
    public void InvalidViewerPlansAreIgnored()
    {
        var old = ViewerBackup();
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(old)!, "plan.json"), "[]");
        Assert.Empty(OldVersionCleanup.ViewerUpdateDirectories(_root));
        Assert.True(File.Exists(old));
    }
    [Fact]
    public void ViewerAlsoRemovesCompletedFoldersWhoseBackupWasAlreadyDeleted()
    {
        var old = ViewerBackup();
        var stage = Path.GetDirectoryName(old)!;
        File.Delete(old);
        Assert.True(File.Exists(Path.Combine(stage, "completed")));
        Assert.True(File.Exists(Path.Combine(stage, "install.ps1")));
        Assert.True(File.Exists(Path.Combine(stage, "plan.json")));
        Assert.True(File.Exists(Path.Combine(stage, "ready")));
        var candidates = OldVersionCleanup.ViewerUpdateDirectories(_root);
        Assert.Equal(stage, Assert.Single(candidates));
        OldVersionCleanup.DeleteViewerUpdates(_root, candidates);
        Assert.False(Directory.Exists(stage));
        Assert.True(File.Exists(Path.Combine(_root, "NxFileViewer.exe")));
    }

    [Fact]
    public void ViewerRechecksNewFilesAndLocksBeforeDeletingDirectory()
    {
        var old = ViewerBackup();
        var stage = Path.GetDirectoryName(old)!;
        var candidates = OldVersionCleanup.ViewerUpdateDirectories(_root);
        var keys = Path.Combine(stage, "prod.keys");
        File.WriteAllText(keys, "keep keys");
        Assert.Throws<IOException>(() => OldVersionCleanup.DeleteViewerUpdates(_root, candidates));
        Assert.True(File.Exists(old));
        File.Delete(keys);
        using (var locked = File.Open(Path.Combine(stage, "install.ps1"), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Assert.Empty(OldVersionCleanup.ViewerUpdateDirectories(_root));
            Assert.Throws<IOException>(() => OldVersionCleanup.DeleteViewerUpdates(_root, candidates));
        }
        Assert.True(File.Exists(old));
        Assert.Single(OldVersionCleanup.ViewerUpdateDirectories(_root));
    }
    public void Dispose()
    {
        // This fixture creates only ordinary files within its unique temporary directory.
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}