using System;
using System.IO;
using Emignatik.NxFileViewer.Services.PackageOperations;
using Xunit;
namespace Emignatik.NxFileViewer.Test.Services;
public sealed class PackageEnginePathsTest
{
    [Fact]
    public void RebaseOnlySameManagedInstallation()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var name = "Rust-" + Guid.NewGuid().ToString("N");
            var relative = Path.Combine("Plugins", "PackageEngines", name, "nscb_rust.exe");
            var old = Path.Combine(root, "old", relative);
            var app = Path.Combine(root, "new");
            var current = Path.Combine(app, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(current)!);
            File.WriteAllText(current, "");
            Assert.Equal(current, PackageEnginePaths.ResolveRust(old, app));
            var other = old.Replace(name, "Rust-" + Guid.NewGuid().ToString("N"));
            Assert.Equal(other, PackageEnginePaths.ResolveRust(other, app));
            var custom = Path.Combine(root, "custom", "nscb_rust.exe");
            Assert.Equal(custom, PackageEnginePaths.ResolveRust(custom, app));
            Directory.CreateDirectory(Path.GetDirectoryName(old)!);
            File.WriteAllText(old, "");
            Assert.Equal(old, PackageEnginePaths.ResolveRust(old, app));
            Assert.Equal("", PackageEnginePaths.ResolveRust("", app));
            var desktopName = "Desktop-" + Guid.NewGuid().ToString("N");
            var desktopRelative = Path.Combine("Plugins", "PackageEngines", desktopName, "nscb-desktop.exe");
            var desktopNew = Path.Combine(app, desktopRelative);
            Directory.CreateDirectory(Path.GetDirectoryName(desktopNew)!);
            File.WriteAllText(desktopNew, "");
            Assert.Equal(desktopNew, PackageEnginePaths.ResolveDesktop(Path.Combine(root, "old", desktopRelative), app));
            var kronosRelative = Path.Combine("Plugins", "PackageEngines", "Kronos-" + Guid.NewGuid().ToString("N"));
            var kronosNew = Path.Combine(app, kronosRelative);
            Directory.CreateDirectory(Path.Combine(kronosNew, "runtime"));
            Directory.CreateDirectory(Path.Combine(kronosNew, "ztools"));
            File.WriteAllText(Path.Combine(kronosNew, "runtime", "python.exe"), "");
            File.WriteAllText(Path.Combine(kronosNew, "ztools", "squirrel.py"), "");
            Assert.Equal(kronosNew, PackageEnginePaths.ResolveKronos(Path.Combine(root, "old", kronosRelative), app));
            var missingVersion = Path.Combine(root, "Plugins", "PackageEngines", "Kronos-" + Guid.NewGuid().ToString("N"));
            Assert.Equal(missingVersion, PackageEnginePaths.ResolveKronos(missingVersion, app));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}