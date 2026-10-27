using System;
using System.IO;
using System.Security.Cryptography;
using Emignatik.NxFileViewer.Services.FileAssociations;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services;

public sealed class ExplorerVersionCleanupTest : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "NxExplorerCleanup-" + Guid.NewGuid().ToString("N"));
    private string Version(string architecture, string content)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var directory = Path.Combine(_root, architecture, Convert.ToHexString(SHA256.HashData(bytes)));
        Directory.CreateDirectory(directory);
        var library = Path.Combine(directory, "NxFileViewer.Explorer.dll");
        File.WriteAllBytes(library, bytes); return library;
    }
    [Fact]
    public void UpdateRemovesUnregisteredVersionsAcrossArchitecturesAndKeepsActiveDll()
    {
        var active = Version("x64", "current"); var old = Version("x64", "old"); var arm = Version("arm64", "old-arm");
        var removed = ExplorerVersionCleanup.RemoveUnused(_root, new[] { active.ToLowerInvariant() });
        Assert.Equal(2, removed.Count); Assert.True(File.Exists(active));
        Assert.False(Directory.Exists(Path.GetDirectoryName(old))); Assert.False(Directory.Exists(Path.GetDirectoryName(arm)));
    }
    [Fact]
    public void RemovalDeletesAllManagedVersionsAndEmptyParentsWhenNoRegistrationRemains()
    {
        Version("x64", "one"); Version("x86", "two");
        Assert.Equal(2, ExplorerVersionCleanup.RemoveUnused(_root, Array.Empty<string>()).Count);
        Assert.False(Directory.Exists(_root));
    }
    [Fact]
    public void LockedVersionIsRetriedAfterReleaseAndDoesNotPreventOtherCleanup()
    {
        var locked = Version("x64", "locked"); var other = Version("x64", "other");
        using (var handle = new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Assert.Single(ExplorerVersionCleanup.RemoveUnused(_root, Array.Empty<string>()));
            Assert.True(File.Exists(locked)); Assert.False(File.Exists(other));
        }
        Assert.Single(ExplorerVersionCleanup.RemoveUnused(_root, Array.Empty<string>()));
        Assert.False(Directory.Exists(_root));
    }
    [Fact]
    public void ForeignFilesModifiedPayloadsAndUnknownDirectoriesArePreserved()
    {
        var extra = Version("x64", "extra"); File.WriteAllText(Path.Combine(Path.GetDirectoryName(extra)!, "user.keys"), "keep");
        var modified = Version("x64", "original"); File.WriteAllText(modified, "changed");
        var unknown = Path.Combine(_root, "x64", "my-files"); Directory.CreateDirectory(unknown); File.WriteAllText(Path.Combine(unknown, "NxFileViewer.Explorer.dll"), "keep");
        Assert.Empty(ExplorerVersionCleanup.RemoveUnused(_root, Array.Empty<string>()));
        Assert.True(File.Exists(extra)); Assert.Equal("changed", File.ReadAllText(modified)); Assert.True(Directory.Exists(unknown));
    }
    [Fact]
    public void InvalidRegistrationFailsClosed()
    {
        var file = Version("x64", "keep");
        Assert.Empty(ExplorerVersionCleanup.RemoveUnused(_root, new[] { "\0" }));
        Assert.True(File.Exists(file));
    }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}