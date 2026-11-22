using System;
using System.IO;
using System.Linq;
using Emignatik.NxFileViewer.Services.Updates;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services.Updates;

public sealed class PluginStorageCleanupTest : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "plugin-cleanup-" + Guid.NewGuid().ToString("N"));
    private string SessionsRoot => Path.Combine(_root, "KronosGuiSessions");

    private string Session(bool old = true)
    {
        var path = Path.Combine(SessionsRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, "output.txt"), "generated result");
        if (old)
        {
            Directory.SetCreationTimeUtc(path, DateTime.UtcNow.AddDays(-2));
            Directory.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(-2));
        }
        return path;
    }

    [Fact]
    public void DownloadsOnlyRemovesConfirmedCacheFilesAndPreservesInstallationAndUnknownFiles()
    {
        var downloads = Path.Combine(_root, "Downloads");
        Directory.CreateDirectory(downloads);
        var first = Path.Combine(downloads, new string('a', 64) + ".download");
        var locked = Path.Combine(downloads, new string('b', 64) + ".download");
        var unknown = Path.Combine(downloads, "notes.txt");
        var installed = Path.Combine(_root, "plugin.exe");
        foreach (var path in new[] { first, locked, unknown, installed }) File.WriteAllText(path, "keep unless confirmed");
        using (var handle = File.Open(locked, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.Equal(first, Assert.Single(PluginStorageCleanup.Downloads(_root)));
            PluginStorageCleanup.DeleteDownloads(_root, new[] { first });
            Assert.False(File.Exists(first));
            Assert.True(File.Exists(locked));
        }
        Assert.Throws<IOException>(() => PluginStorageCleanup.DeleteDownloads(_root, new[] { installed }));
        Assert.True(File.Exists(installed));
        Assert.True(File.Exists(unknown));
    }

    [Fact]
    public void SessionsProtectRecentLockedAndKeyBearingWorkspaces()
    {
        var old = Session();
        var recent = Session(false);
        var locked = Session();
        var withKeys = Session();
        File.WriteAllText(Path.Combine(withKeys, "prod.keys"), "private");
        Directory.SetLastWriteTimeUtc(withKeys, DateTime.UtcNow.AddDays(-2));
        using (var handle = File.Open(Path.Combine(locked, "output.txt"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.Equal(old, Assert.Single(PluginStorageCleanup.Sessions(SessionsRoot, () => false)));
            Assert.Empty(PluginStorageCleanup.Sessions(SessionsRoot, () => true));
            PluginStorageCleanup.DeleteSessions(SessionsRoot, new[] { old }, () => false);
        }
        Assert.False(Directory.Exists(old));
        foreach (var path in new[] { recent, locked, withKeys }) Assert.True(Directory.Exists(path));
        Assert.Throws<IOException>(() => PluginStorageCleanup.DeleteSessions(SessionsRoot, new[] { SessionsRoot }, () => false));
    }

    [Fact]
    public void SessionSafetyIsRecheckedAfterConfirmation()
    {
        var path = Session();
        Assert.Single(PluginStorageCleanup.Sessions(SessionsRoot, () => false));
        Assert.Throws<IOException>(() => PluginStorageCleanup.DeleteSessions(SessionsRoot, new[] { path }, () => true));
        using (var handle = File.Open(Path.Combine(path, "output.txt"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            Assert.Throws<IOException>(() => PluginStorageCleanup.DeleteSessions(SessionsRoot, new[] { path }, () => false));
        Assert.True(Directory.Exists(path));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
