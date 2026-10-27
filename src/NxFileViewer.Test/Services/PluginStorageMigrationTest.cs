using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Emignatik.NxFileViewer.Services;
using Emignatik.NxFileViewer.Services.Nsz;
using Emignatik.NxFileViewer.Services.Nand;
using Emignatik.NxFileViewer.Services.PackageOperations;
using Emignatik.NxFileViewer.Services.Updates;
using Emignatik.NxFileViewer.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services;

public sealed class PluginStorageMigrationTest
{
    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    private static string Install(string root, string prefix, string name, string value)
    {
        var directory = Path.Combine(root, prefix + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, name); File.WriteAllText(path, value); return path;
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LegacyPluginsMigrateCurrentAndPreviousAndKeepDigestsAcrossRollback(bool nand)
    {
        var root = Path.Combine(Path.GetTempPath(), "plugin-migrate-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var previous = Install(root, "version-", nand ? "NxNandManager.exe" : "nsz-cli-windows-x64.exe", "first");
            var current = Install(root, "version-", Path.GetFileName(previous), "second");
            File.WriteAllText(Path.Combine(root, "active.json"), JsonSerializer.Serialize(new {
                Executable = Path.GetRelativePath(root, current), Version = "2",
                PreviousExecutable = Path.GetRelativePath(root, previous), PreviousVersion = "1",
                Digest = "sha256:source", ExecutableDigest = Hash("second"),
                SourceAssetName = "asset.zip", SourceDigest = "sha256:source"
            }));
            var settings = new AppSettings();
            var nsz = new NszPluginManager(settings, NullLogger<NszPluginManager>.Instance, root);
            var manager = new NandPluginManager(settings, root);
            Assert.Equal("2", nand ? manager.InstalledVersion : nsz.InstalledVersion);
            using var pointer = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "active.json")));
            Assert.Equal(1, pointer.RootElement.GetProperty("SchemaVersion").GetInt32());
            Assert.False(pointer.RootElement.TryGetProperty("Version", out _));
            Assert.False(pointer.RootElement.TryGetProperty("Executable", out _));
            Assert.True(File.Exists(Path.Combine(root, "active.json.legacy")));
            var metadataPath = Path.Combine(Path.GetDirectoryName(current)!, "installation.json");
            var originalMetadata = File.ReadAllText(metadataPath);
            Assert.Equal("1", PluginVersionStorage.Metadata(Path.GetDirectoryName(previous)!)!.Version);
            if (nand) manager.Rollback(); else nsz.Rollback();
            Assert.Equal("1", nand ? manager.InstalledVersion : nsz.InstalledVersion);
            Assert.Equal(originalMetadata, File.ReadAllText(metadataPath));
            if (nand) manager.Rollback(); else nsz.Rollback();
            Assert.Equal("2", nand ? manager.InstalledVersion : nsz.InstalledVersion);
            File.WriteAllText(previous, "changed");
            Assert.Throws<InvalidDataException>(() => { if (nand) manager.Rollback(); else nsz.Rollback(); });
        }
        finally { Directory.Delete(root, true); }
    }
    [Fact]
    public void PackageLegacyPointerMigratesAndRemainsPortableWithoutChangingVersionDirectories()
    {
        var root = Path.Combine(Path.GetTempPath(), "package-migrate-" + Guid.NewGuid().ToString("N"));
        var moved = root + "-moved";
        Directory.CreateDirectory(root);
        try
        {
            var first = Install(root, "Rust-", "nscb_rust.exe", "first");
            var second = Install(root, "Rust-", "nscb_rust.exe", "second");
            PluginVersionStorage.AtomicWrite(Path.Combine(Path.GetDirectoryName(first)!, "installation.json"),
                new PluginInstallation(ExternalSources.Current.RustPackageRepository, "1", Sha256: "sha256:" + Hash("first")));
            PluginVersionStorage.AtomicWrite(Path.Combine(Path.GetDirectoryName(second)!, "installation.json"),
                new PluginInstallation(ExternalSources.Current.RustPackageRepository, "2", Sha256: "sha256:" + Hash("second")));
            File.WriteAllText(Path.Combine(root, "Rust-active.json"), JsonSerializer.Serialize(new {
                Current = Path.GetRelativePath(root, second), Previous = Path.GetRelativePath(root, first)
            }));
            var settings = new AppSettings();
            var store = new PackageVersionStore(settings, root); store.Synchronize();
            Assert.Equal(second, settings.RustPackageExecutablePath);
            Assert.True(File.Exists(Path.Combine(root, "Rust", "active.json")));
            Assert.True(File.Exists(Path.Combine(root, "Rust-active.json")));
            store.Rollback(PackageComponent.Rust);
            Assert.Equal(first, settings.RustPackageExecutablePath);
            Directory.Move(root, moved);
            var movedSettings = new AppSettings();
            var movedStore = new PackageVersionStore(movedSettings, moved); movedStore.Synchronize();
            Assert.Equal(Path.GetFullPath(Path.GetRelativePath(root, first), moved), movedSettings.RustPackageExecutablePath);
            Assert.True(movedStore.CanRollback(PackageComponent.Rust));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); if (Directory.Exists(moved)) Directory.Delete(moved, true); }
    }
    [Fact]
    public void SharedPointerRejectsPathsOutsideTheManagedRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var storage = new PluginVersionStorage(root, Path.Combine(root, "active.json"));
            Assert.Throws<InvalidDataException>(() => storage.Write(Path.Combine(Path.GetTempPath(), "outside.exe"), null));
            Assert.False(File.Exists(Path.Combine(root, "active.json")));
        }
        finally { Directory.Delete(root, true); }
    }
}