using System;
using System.IO;
using Emignatik.NxFileViewer.Services;
using Emignatik.NxFileViewer.Services.Nsz;
using Emignatik.NxFileViewer.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services;

public sealed class PluginGuiLauncherTest
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void LaunchUsesExecutableDirectoryAndGuiArguments(bool nand)
    {
        var path = Path.Combine(Path.GetTempPath(), "plugin with spaces", "plugin.exe");
        var info = PluginGuiLauncher.CreateStartInfo(path, nand);
        Assert.Equal(path, info.FileName);
        Assert.Equal(Path.GetDirectoryName(path), info.WorkingDirectory);
        Assert.True(info.UseShellExecute);
        Assert.False(info.RedirectStandardOutput);
        Assert.Equal(nand ? new[] { "--gui" } : Array.Empty<string>(), info.ArgumentList);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ActiveKeysAreIsolatedAndGuiStillStartsWithoutCliKeyArguments(bool nand)
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var source = Path.Combine(root, "active keys.txt");
            File.WriteAllText(source, "fixture");
            var session = Path.Combine(root, "session");
            var info = PluginGuiLauncher.CreateKeyedStartInfo(Path.Combine(root, "plugin.exe"), nand, source, session);
            var copied = nand ? Path.Combine(session, "keys.dat") : Path.Combine(session, ".switch", "prod.keys");
            Assert.Equal("fixture", File.ReadAllText(copied));
            Assert.Equal(nand ? new[] { "--gui" } : Array.Empty<string>(), info.ArgumentList);
            if (nand) Assert.Equal(session, info.WorkingDirectory);
            else
            {
                Assert.False(info.UseShellExecute);
                Assert.Equal(session, info.Environment["USERPROFILE"]);
                File.WriteAllText(Path.Combine(root, "prod.keys"), "existing");
                Assert.Throws<IOException>(() => PluginGuiLauncher.CreateKeyedStartInfo(info.FileName, false, source, Path.Combine(root, "other")));
                Assert.Equal("existing", File.ReadAllText(Path.Combine(root, "prod.keys")));
            }
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void KronosReceivesSelectedKeysAndCleanupPreservesProducedFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var installation = Path.Combine(root, "installation");
        var workspace = Path.Combine(root, "session");
        Directory.CreateDirectory(Path.Combine(installation, "ztools", "lib"));
        Directory.CreateDirectory(Path.Combine(installation, "ztools", "web"));
        Directory.CreateDirectory(Path.Combine(installation, "runtime"));
        try
        {
            File.WriteAllText(Path.Combine(installation, "ztools", "keys.txt"), "plugin original keys");
            File.WriteAllText(Path.Combine(installation, "ztools", "prod.keys"), "must not copy");
            File.WriteAllText(Path.Combine(installation, "ztools", "squirrel.py"), "fixture");
            File.WriteAllText(Path.Combine(installation, "ztools", "lib", "Interface.py"), "fixture");
            File.WriteAllText(Path.Combine(installation, "ztools", "web", "main.html"), "fixture");
            File.WriteAllText(Path.Combine(installation, "NSCB.bat"), "@echo off");
            File.WriteAllText(Path.Combine(installation, "NSCB.exe"), "fixture exe");
            File.WriteAllText(Path.Combine(installation, "runtime", "python.exe"), "fixture runtime");
            Directory.CreateDirectory(Path.Combine(installation, "zconfig"));
            File.WriteAllText(Path.Combine(installation, "zconfig", "NSCB_options.cmd"), "set \"pycommand=py -3\"");
            var selected = Path.Combine(root, "selected.keys");
            File.WriteAllText(selected, "selected fixture keys");
            var info = PluginGuiLauncher.CreateKronosStartInfo(installation, selected, workspace);
            Assert.Equal("cmd.exe", Path.GetFileName(info.FileName), ignoreCase: true);
            Assert.Equal(new[] { "/d", "/c", "NSCB.bat" }, info.ArgumentList);
            Assert.False(info.UseShellExecute);
            Assert.False(info.CreateNoWindow);
            Assert.True(File.Exists(Path.Combine(workspace, "NSCB.bat")));
            Assert.Contains(Path.Combine(installation, "runtime", "python.exe"),
                File.ReadAllText(Path.Combine(workspace, "zconfig", "NSCB_options.cmd")));
            Assert.Equal("set \"pycommand=py -3\"", File.ReadAllText(Path.Combine(installation, "zconfig", "NSCB_options.cmd")));
            Assert.Equal(workspace, info.WorkingDirectory);
            Assert.Equal("selected fixture keys", File.ReadAllText(Path.Combine(workspace, "ztools", "keys.txt")));
            Assert.False(File.Exists(Path.Combine(workspace, "ztools", "prod.keys")));
            Assert.Equal("plugin original keys", File.ReadAllText(Path.Combine(installation, "ztools", "keys.txt")));
            var output = Path.Combine(workspace, "NSCB_output", "result.nsp");
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            File.WriteAllText(output, "produced package");
            PluginGuiLauncher.CleanupKronosKeys(workspace);
            Assert.False(File.Exists(Path.Combine(workspace, "ztools", "keys.txt")));
            Assert.Equal("produced package", File.ReadAllText(output));
            Assert.Equal("selected fixture keys", File.ReadAllText(selected));
        }
        finally { Directory.Delete(root, true); }
    }
    [Fact]
    public void DesktopUsesPrivateTemporaryBackendAndSelectedKeysWithoutChangingGlobalEnvironment()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var backend = Path.Combine(root, "backend.exe");
            var keys = Path.Combine(root, "keys.txt");
            File.WriteAllText(backend, "fixture backend");
            File.WriteAllText(keys, "fixture keys");
            var originalTemp = Environment.GetEnvironmentVariable("TEMP");
            var session = Path.Combine(root, "session");
            var info = PluginGuiLauncher.CreateDesktopStartInfo(Path.Combine(root, "desktop.exe"), backend, keys, session);
            Assert.False(info.UseShellExecute);
            Assert.Empty(info.ArgumentList);
            Assert.Equal(session, info.Environment["TEMP"]);
            Assert.Equal(session, info.Environment["TMP"]);
            Assert.Equal("fixture backend", File.ReadAllText(Path.Combine(session, "nscb-desktop-tools", "nscb_rust.exe")));
            Assert.Equal("fixture keys", File.ReadAllText(Path.Combine(session, "nscb-desktop-tools", "prod.keys")));
            Assert.Equal(originalTemp, Environment.GetEnvironmentVariable("TEMP"));
            Assert.Equal("fixture keys", File.ReadAllText(keys));
        }
        finally { Directory.Delete(root, true); }
    }
    [Fact]
    public void NszGuiRequiresAnExistingGuiBuild()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var settings = new AppSettings();
            var manager = new NszPluginManager(settings, NullLogger<NszPluginManager>.Instance, root);
            Assert.Empty(manager.GuiExecutablePath);
            settings.NszExecutablePath = Path.Combine(root, "nsz-cli-windows-x64.exe");
            File.WriteAllText(settings.NszExecutablePath, "fixture");
            Assert.Empty(manager.GuiExecutablePath);
            settings.NszExecutablePath = Path.Combine(root, "nsz-gui-windows-x64.exe");
            Assert.Empty(manager.GuiExecutablePath);
            File.WriteAllText(settings.NszExecutablePath, "fixture");
            Assert.Equal(settings.NszExecutablePath, manager.GuiExecutablePath);
        }
        finally { Directory.Delete(root, true); }
    }
    [Fact]
    public void KronosAcceptsExeOnlyAndPrefersBatchWhenBothLaunchersExist()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try {
            Assert.Null(PluginGuiLauncher.KronosLauncherPath(root));
            File.WriteAllText(Path.Combine(root, "NSCB.exe"), "fixture");
            Assert.Equal(Path.Combine(root, "NSCB.exe"), PluginGuiLauncher.KronosLauncherPath(root));
            File.WriteAllText(Path.Combine(root, "NSCB.bat"), "fixture");
            Assert.Equal(Path.Combine(root, "NSCB.bat"), PluginGuiLauncher.KronosLauncherPath(root));
        } finally { Directory.Delete(root, true); }
    }
}
