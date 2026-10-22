using System;
using System.Linq;
using System.IO;
using System.Reflection.PortableExecutable;
using Emignatik.NxFileViewer.Services.FileAssociations;
using Xunit;
using Microsoft.Win32;
namespace Emignatik.NxFileViewer.Test.Services;
public sealed class ForkFileAssociationsTest {
    [Fact]
    public void RemovalDeletesOnlyTheForkRegistrationAndLeavesMenusAndOtherApps()
    {
        var testKey = @"Software\NxFileViewerFork\Tests\" + Guid.NewGuid().ToString("N");
        try {
            using var user = Registry.CurrentUser.CreateSubKey(testKey);
            foreach (var entry in ForkFileAssociations.Entries(@"C:\Apps\NxFileViewer.exe")) {
                using var key = user.CreateSubKey(entry.Key); key.SetValue(entry.Name, entry.Value);
            }
            using (var other = user.CreateSubKey(@"Software\Classes\.nsp\OpenWithProgids")) other.SetValue("OtherViewer.File", "");
            using (var choice = user.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.nsp\UserChoice")) choice.SetValue("ProgId", "OtherViewer.File");
            const string historyPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.nsp\OpenWithList";
            using (var history = user.CreateSubKey(historyPath)) {
                history.SetValue("a", ForkAssociationLauncher.FileName); history.SetValue("b", "OtherViewer.exe"); history.SetValue("MRUList", "ba");
            }
            using (var menu = user.CreateSubKey(ExplorerIntegration.ConfigurationKey)) menu.SetValue("Executable", @"C:\Apps\NxFileViewer.exe");
            ForkFileAssociations.RemoveRegistration(user);
            ForkFileAssociations.RemoveRegistration(user); // Removing twice is safe.
            Assert.Null(user.OpenSubKey(@"Software\Classes\" + ForkFileAssociations.ProgId));
            Assert.Null(user.OpenSubKey(@"Software\Classes\Applications\" + ForkAssociationLauncher.FileName));
            Assert.Null(user.OpenSubKey(@"Software\NxFileViewerFork\Capabilities"));
            using var apps = user.OpenSubKey(@"Software\RegisteredApplications");
            Assert.Null(apps?.GetValue(ForkFileAssociations.ApplicationName));
            using var openWith = user.OpenSubKey(@"Software\Classes\.nsp\OpenWithProgids");
            Assert.Null(openWith?.GetValue(ForkFileAssociations.ProgId));
            Assert.NotNull(openWith?.GetValue("OtherViewer.File"));
            using var originalChoice = user.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.nsp\UserChoice");
            Assert.Equal("OtherViewer.File", originalChoice?.GetValue("ProgId"));
            using var originalHistory = user.OpenSubKey(historyPath);
            Assert.Null(originalHistory?.GetValue("a"));
            Assert.Equal("OtherViewer.exe", originalHistory?.GetValue("b"));
            Assert.Equal("b", originalHistory?.GetValue("MRUList"));
            using var originalMenu = user.OpenSubKey(ExplorerIntegration.ConfigurationKey);
            Assert.Equal(@"C:\Apps\NxFileViewer.exe", originalMenu?.GetValue("Executable"));
        } finally { Registry.CurrentUser.DeleteSubKeyTree(testKey, false); }
    }
    [Fact]
    public void RegistrationUsesForkIdentityAndQuotesExecutableAndFileArgument() {
        var executable = @"C:\Apps with spaces\NxFileViewer.exe";
        var entries = ForkFileAssociations.Entries(executable);
        var command = Assert.Single(entries, e => e.Key == @"Software\Classes\" + ForkFileAssociations.ProgId + @"\shell\open\command");
        Assert.Equal("\"" + ForkAssociationLauncher.PathFor(executable) + "\" \"%1\"", command.Value);
        Assert.Contains(ForkFileAssociations.ProgId, command.Key);
        var extensions = entries.Where(e => e.Key.EndsWith(@"\FileAssociations")).ToArray();
        Assert.Equal(new[] { ".nsp", ".nsz", ".xci", ".xcz", ".nro" }, extensions.Select(e => e.Name));
        Assert.All(extensions, e => Assert.Equal(ForkFileAssociations.ProgId, e.Value));
        Assert.Equal(5, entries.Count(e => e.Key.EndsWith(@"\OpenWithProgids")));
        Assert.Contains(entries, e => e.Key == @"Software\Classes\Applications\NxFileViewerFork.Open.exe" && e.Name == "FriendlyAppName" && e.Value == "NxFileViewer Fork");
        Assert.Equal(5, entries.Count(e => e.Key.EndsWith(@"\SupportedTypes")));
        Assert.Equal(command.Value, Assert.Single(entries, e => e.Key == @"Software\Classes\Applications\NxFileViewerFork.Open.exe\shell\open\command").Value);
        Assert.DoesNotContain(entries, e => e.Key.Contains("UserChoice") || e.Key.Contains(@"Applications\NxFileViewer.exe"));
        Assert.DoesNotContain(entries, e => e.Key.EndsWith(".nsp") && e.Name.Length == 0);
    }
    [Fact]
    public void RelocationKeepsIdentityAndUpdatesExecutablePath() {
        var oldEntries = ForkFileAssociations.Entries(@"C:\Old\NxFileViewer.exe");
        var newEntries = ForkFileAssociations.Entries(@"D:\New\NxFileViewer.exe");
        Assert.Equal(oldEntries.Select(e => (e.Key, e.Name)), newEntries.Select(e => (e.Key, e.Name)));
        Assert.Contains(newEntries, e => e.Value == @"""D:\New\NxFileViewerFork.Open.exe"" ""%1""");
        Assert.Throws<ArgumentException>(() => ForkFileAssociations.Entries(@"C:\dotnet.exe"));
    }
    [Fact]
    public void EmbeddedNativeLauncherIsSmallAndRefreshReplacesOldBytesOnlyWhenChanged() {
        var root = Path.Combine(Path.GetTempPath(), "Fork launcher test " + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try {
            var executable = Path.Combine(root, "NxFileViewer.exe"); File.WriteAllBytes(executable, new byte[] { 1 });
            var launcher = ForkAssociationLauncher.Ensure(executable);
            var payload = ForkAssociationLauncher.Payload(); Assert.InRange(payload.Length, 1024, 50 * 1024);
            using (var pe = new PEReader(new MemoryStream(payload))) { Assert.Null(pe.PEHeaders.CorHeader); Assert.Equal(Machine.I386, pe.PEHeaders.CoffHeader.Machine); }
            Assert.Equal(payload, File.ReadAllBytes(launcher));
            var time = File.GetLastWriteTimeUtc(launcher); ForkAssociationLauncher.Ensure(executable); Assert.Equal(time, File.GetLastWriteTimeUtc(launcher));
            File.WriteAllBytes(launcher, new byte[] { 2, 3 }); ForkAssociationLauncher.Ensure(executable);
            Assert.Equal(payload, File.ReadAllBytes(launcher)); Assert.Equal(new byte[] { 1 }, File.ReadAllBytes(executable));
            Assert.Empty(Directory.GetFiles(root, "*.tmp"));
        } finally { Directory.Delete(root, true); }
    }
    [Fact]
    public void RenamedExecutableKeepsStableLauncherAndWritesItsSiblingReference()
    {
        var root=Path.Combine(Path.GetTempPath(),"Fork rename "+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try {
            var old=Path.Combine(root,"NxFileViewer.exe"); File.WriteAllText(old,"old");
            var launcher=ForkAssociationLauncher.Ensure(old);
            var renamed=Path.Combine(root,"NxFileViewerFork.exe"); File.WriteAllText(renamed,"new");
            Assert.Equal(launcher,ForkAssociationLauncher.Ensure(renamed));
            Assert.Equal("NxFileViewerFork.exe",File.ReadAllText(Path.Combine(root,"NxFileViewerFork.Open.target")));
            Assert.Contains(ForkFileAssociations.Entries(renamed),entry=>entry.Name=="ApplicationIcon" && entry.Value.Contains("NxFileViewerFork.exe"));
            Assert.NotEmpty(ExplorerIntegration.ClassicEntries(renamed,Path.Combine(root,"Explorer.dll")));
        } finally { Directory.Delete(root,true); }
    }
}
