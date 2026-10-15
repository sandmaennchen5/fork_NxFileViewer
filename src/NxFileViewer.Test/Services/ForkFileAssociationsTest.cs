using System;
using System.Linq;
using System.IO;
using System.Reflection.PortableExecutable;
using Emignatik.NxFileViewer.Services.FileAssociations;
using Xunit;
namespace Emignatik.NxFileViewer.Test.Services;
public sealed class ForkFileAssociationsTest {
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
}
