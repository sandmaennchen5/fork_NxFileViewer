using System;
using System.IO;
using System.Text.Json;
using System.Security.Cryptography;
using Emignatik.NxFileViewer.Settings;
using Emignatik.NxFileViewer.Services;
using Emignatik.NxFileViewer.Services.PackageOperations;
using Xunit;
namespace Emignatik.NxFileViewer.Test.Services;
public sealed class PackageVersionStoreTest
{
    private static string Install(string root, PackageComponent component)
    {
        var directory = Path.Combine(root, component + "-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        string path;
        if (component == PackageComponent.Kronos)
        {
            path = directory; Directory.CreateDirectory(Path.Combine(path, "runtime")); Directory.CreateDirectory(Path.Combine(path, "ztools"));
            File.WriteAllText(Path.Combine(path, "runtime", "python.exe"), "runtime"); File.WriteAllText(Path.Combine(path, "ztools", "squirrel.py"), "script");
        }
        else { path = Path.Combine(directory, component == PackageComponent.Rust ? "nscb_rust.exe" : "nscb-desktop.exe"); File.WriteAllText(path, "exe"); }
        var repository = component switch { PackageComponent.Rust => ExternalSources.Current.RustPackageRepository, PackageComponent.Kronos => ExternalSources.Current.KronosPackageRepository, _ => ExternalSources.Current.RustDesktopRepository };
        File.WriteAllText(Path.Combine(directory, "installation.json"), JsonSerializer.Serialize(new { Repository = repository, Version = "1.0",
            Sha256 = "sha256:" + Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("exe"))), Commit = new string('a',40) }));
        return path;
    }
    [Theory]
    [InlineData(PackageComponent.Rust)]
    [InlineData(PackageComponent.Kronos)]
    [InlineData(PackageComponent.Desktop)]
    public void RollbackIsPersistentAndCleanupProtectsActiveAndCustomInstallations(PackageComponent component)
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var settings = new AppSettings(); var store = new PackageVersionStore(settings, root);
            var first = Install(root, component); store.Select(component, first); store.Synchronize();
            var second = Install(root, component); store.Select(component, second); store.Synchronize();
            Assert.True(store.CanRollback(component)); store.Rollback(component); Assert.Equal(first, store.Selected(component));
            Assert.Equal("1.0", PackageVersionStore.VersionLabel(component, store.Selected(component)));
            // Kronos ships an empty title.keys placeholder; it must not hide every old version.
            if (component == PackageComponent.Kronos) File.WriteAllText(Path.Combine(second, "ztools", "title.keys"), "");
            var old = Assert.Single(store.OldVersions(component));
            Assert.Equal(component == PackageComponent.Kronos ? second : Path.GetDirectoryName(second), old);
            Assert.Throws<IOException>(() => store.Remove(component, new[] { component == PackageComponent.Kronos ? first : Path.GetDirectoryName(first)! }));
            store.Remove(component, new[] { old }); Assert.False(store.CanRollback(component));
            Assert.True(component == PackageComponent.Kronos ? Directory.Exists(first) : File.Exists(first));
            var custom = Path.Combine(root, "custom.exe"); File.WriteAllText(custom, "manual"); store.Select(component, custom);
            Assert.False(store.CanRollback(component)); Assert.Equal(custom, store.Selected(component));
        } finally { Directory.Delete(root, true); }
    }
    [Fact]
    public void CleanupProtectsKeysAndRollbackRejectsTamperedExecutable()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var settings = new AppSettings(); var store = new PackageVersionStore(settings, root);
            var first = Install(root, PackageComponent.Rust); store.Select(PackageComponent.Rust, first); store.Synchronize();
            var second = Install(root, PackageComponent.Rust); store.Select(PackageComponent.Rust, second); store.Synchronize();
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(first)!, "prod.keys"), "fixture keys");
            Assert.Empty(store.OldVersions(PackageComponent.Rust));
            File.WriteAllText(first, "modified");
            Assert.Throws<InvalidDataException>(() => store.Rollback(PackageComponent.Rust));
            Assert.Equal(second, store.Selected(PackageComponent.Rust));
        } finally { Directory.Delete(root, true); }
    }
}