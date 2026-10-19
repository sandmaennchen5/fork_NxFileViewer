using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Emignatik.NxFileViewer.Services;
using Emignatik.NxFileViewer.Services.Updates;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services.Updates;

public sealed class ViewerPackageLayoutTest : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ViewerPackage-" + Guid.NewGuid().ToString("N"));
    public ViewerPackageLayoutTest() => Directory.CreateDirectory(_root);
    private string RootFile(string relative) => Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar));
    private void Put(string relative, string text)
    { var path = RootFile(relative); Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, text); }
    private string Stage() { var path = RootFile("Updates/" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(path); return path; }
    private string Package(string main = "NxFileViewerFork.exe", params (string Name, string Text)[] extra)
    {
        var path = RootFile(Guid.NewGuid().ToString("N") + ".zip");
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        void Write(string name, string text) { using var writer = new StreamWriter(zip.CreateEntry(name).Open()); writer.Write(text); }
        Write(main, "new-main");
        Write(ViewerPackageLayout.ManifestName, JsonSerializer.Serialize(new ViewerPackageManifest(1, ViewerPackageLayout.ApplicationId, main)));
        foreach (var item in extra) Write(item.Name, item.Text);
        return path;
    }
    [Fact]
    public void FlatPackageUsesExplicitMainAndRetainsExtraExecutableFoldersAndEmptyDirectory()
    {
        var stage = Stage();
        var prepared = ViewerPackageLayout.Extract(Package("NxFileViewerFork.exe", ("Tools/helper.exe", "helper"), ("Data/a.json", "data"), ("Assets/Empty/", "")), stage, "NxFileViewer_v4_x64.zip", CancellationToken.None);
        Assert.Equal("NxFileViewerFork.exe", prepared.EntryPoint);
        Assert.Equal(4, prepared.Files.Length);
        Assert.Equal("new-main", File.ReadAllText(prepared.Executable));
        Assert.Equal("data", File.ReadAllText(Path.Combine(stage, "payload", "Data", "a.json")));
        Assert.Contains("Assets/Empty", prepared.Directories);
        Assert.True(Directory.Exists(Path.Combine(stage, "payload", "Assets", "Empty")));
    }
    [Theory]
    [InlineData("../outside")][InlineData("..\\outside")][InlineData("/outside")][InlineData("C:/outside")]
    [InlineData("Data/file:stream")][InlineData("Data/CON.txt")][InlineData("Data/file.")][InlineData("Updates/other.txt")]
    [InlineData("prod.keys")][InlineData("NxFileViewer.settings.json")][InlineData("Data/../../NxFileViewer.exe")]
    public void RejectsUnsafeOrProtectedPackagePathsBeforeWritingPayload(string name)
    {
        var stage = Stage();
        Assert.Throws<InvalidDataException>(() => ViewerPackageLayout.Extract(Package("NxFileViewerFork.exe", (name, "bad")), stage, "package.zip", CancellationToken.None));
        Assert.False(Directory.Exists(Path.Combine(stage, "payload")));
    }
    [Theory]
    [InlineData("nxfileviewerfork.exe")][InlineData("Data")]
    public void RejectsCaseDuplicateAndFileDirectoryConflicts(string conflict)
    {
        var stage = Stage();
        Assert.Throws<InvalidDataException>(() => ViewerPackageLayout.Extract(Package("NxFileViewerFork.exe", ("Data/a.txt", "data"), (conflict, "bad")), stage, "package.zip", CancellationToken.None));
    }
    [Fact]
    public void RejectsForeignManifestAndRetainsLegacySingleExeCompatibility()
    {
        var archive = Package();
        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Update))
        {
            zip.GetEntry(ViewerPackageLayout.ManifestName)!.Delete();
            using var writer = new StreamWriter(zip.CreateEntry(ViewerPackageLayout.ManifestName).Open());
            writer.Write(JsonSerializer.Serialize(new ViewerPackageManifest(1, "OtherApp", "NxFileViewerFork.exe")));
        }
        Assert.Throws<InvalidDataException>(() => ViewerPackageLayout.Extract(archive, Stage(), "package.zip", CancellationToken.None));
        var legacy = RootFile("legacy.zip");
        using (var zip = ZipFile.Open(legacy, ZipArchiveMode.Create))
        using (var writer = new StreamWriter(zip.CreateEntry("NxFileViewer_v3_x64/NxFileViewer.exe").Open())) writer.Write("legacy");
        var prepared = ViewerPackageLayout.Extract(legacy, Stage(), "NxFileViewer_v3_x64.zip", CancellationToken.None);
        Assert.Equal("NxFileViewer.exe", prepared.EntryPoint); Assert.Equal("legacy", File.ReadAllText(prepared.Executable));
    }
    private void Original()
    {
        Put("NxFileViewer.exe", "old-main");
        Put(ViewerPackageLayout.ManifestName, JsonSerializer.Serialize(new ViewerPackageManifest(1, ViewerPackageLayout.ApplicationId, "NxFileViewer.exe")));
        Put("Data/shared.txt", "old-data"); Put("Data/user.txt", "user-data"); Put("prod.keys", "private");
    }
    private async Task<int> Install(PreparedViewerUpdate prepared, string previous = "NxFileViewer.exe")
    {
        var script = Path.Combine(prepared.Directory, "install.ps1");
        using (var input = typeof(ViewerUpdateService).Assembly.GetManifestResourceStream("ViewerUpdate.InstallUpdate.ps1")!)
        using (var output = File.Create(script)) input.CopyTo(output);
        var plan = Path.Combine(prepared.Directory, "plan.json");
        File.WriteAllText(plan, JsonSerializer.Serialize(new {
            SchemaVersion=2, ApplicationId=ViewerPackageLayout.ApplicationId, TargetRoot=_root, Target=RootFile(prepared.EntryPoint), PreviousTarget=RootFile(previous),
            ProcessId=999999999, OriginalExecutableHash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(RootFile(previous)))),
            Files=prepared.Files, Directories=prepared.Directories, RemoveDirectories=prepared.RemoveDirectories
        }));
        var start = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32/WindowsPowerShell/v1.0/powershell.exe")) {
            UseShellExecute=false, CreateNoWindow=true, RedirectStandardOutput=true, RedirectStandardError=true
        };
        foreach (var value in new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", script, "-PlanPath", plan, "-NoRestart", "-LockTimeoutSeconds", "1" }) start.ArgumentList.Add(value);
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        try { await process.WaitForExitAsync(timeout.Token); } catch { process.Kill(true); throw; }
        await Task.WhenAll(stdout, stderr);
        if (process.ExitCode != 0 && !File.Exists(Path.Combine(prepared.Directory, "error.txt"))) throw new Exception(await stderr);
        return process.ExitCode;
    }
    private static string Error(PreparedViewerUpdate prepared) => File.Exists(Path.Combine(prepared.Directory, "error.txt")) ? File.ReadAllText(Path.Combine(prepared.Directory, "error.txt")) : "";
    [Fact]
    public async Task MultiFileInstallRenameAndManualRollbackRestoreEntirePreviousPackage()
    {
        Original();
        var prepared = ViewerPackageLayout.Extract(Package("NxFileViewerFork.exe", ("Data/shared.txt", "new-data"), ("Data/new.txt", "new-file"), ("Assets/Empty/", "")), Stage(), "package.zip", CancellationToken.None);
        Assert.True(await Install(prepared) == 0, Error(prepared));
        Assert.False(File.Exists(RootFile("NxFileViewer.exe"))); Assert.Equal("new-main", File.ReadAllText(RootFile("NxFileViewerFork.exe")));
        Assert.Equal("new-data", File.ReadAllText(RootFile("Data/shared.txt"))); Assert.Equal("new-file", File.ReadAllText(RootFile("Data/new.txt")));
        Assert.True(Directory.Exists(RootFile("Assets/Empty")));
        Assert.Equal("user-data", File.ReadAllText(RootFile("Data/user.txt"))); Assert.Equal("private", File.ReadAllText(RootFile("prod.keys")));
        Assert.NotNull(ViewerRollback.FindBackup(_root));
        var rollback = ViewerRollback.Prepare(_root);
        Assert.True(await Install(rollback, "NxFileViewerFork.exe") == 0, Error(rollback));
        Assert.Equal("old-main", File.ReadAllText(RootFile("NxFileViewer.exe"))); Assert.False(File.Exists(RootFile("NxFileViewerFork.exe")));
        Assert.Equal("old-data", File.ReadAllText(RootFile("Data/shared.txt"))); Assert.False(File.Exists(RootFile("Data/new.txt")));
        Assert.False(Directory.Exists(RootFile("Assets"))); Assert.Equal("user-data", File.ReadAllText(RootFile("Data/user.txt")));
        Assert.Equal("NxFileViewer.exe", ViewerPackageLayout.ReadManifest(File.ReadAllBytes(RootFile(ViewerPackageLayout.ManifestName))).Executable);
    }
    [Fact]
    public async Task LockedCompanionCausesWholePackageRecoveryAndRetainsUserFiles()
    {
        Original();
        var prepared = ViewerPackageLayout.Extract(Package("NxFileViewerFork.exe", ("Data/shared.txt", "new-data")), Stage(), "package.zip", CancellationToken.None);
        using (var locked = File.Open(RootFile("Data/shared.txt"), FileMode.Open, FileAccess.Read, FileShare.Read))
            Assert.Equal(1, await Install(prepared));
        Assert.Equal("old-main", File.ReadAllText(RootFile("NxFileViewer.exe"))); Assert.False(File.Exists(RootFile("NxFileViewerFork.exe")));
        Assert.Equal("old-data", File.ReadAllText(RootFile("Data/shared.txt"))); Assert.Equal("private", File.ReadAllText(RootFile("prod.keys")));
        Assert.True(File.Exists(Path.Combine(prepared.Directory, "ready")));
        var journal=JsonSerializer.Deserialize<ViewerUpdateJournal>(File.ReadAllText(Path.Combine(prepared.Directory, "journal.json")))!;
        Assert.Contains(journal.Entries, entry => entry.Path == "Data/shared.txt" && entry.Existed);
        Assert.False(Error(prepared).Contains("Rollback:"), Error(prepared));
        Assert.False(File.Exists(Path.Combine(prepared.Directory, "completed")));
    }
    [Fact]
    public async Task TamperedPreparedCompanionCannotStartInstallation()
    {
        Original();
        var prepared = ViewerPackageLayout.Extract(Package("NxFileViewerFork.exe", ("Data/shared.txt", "new-data")), Stage(), "package.zip", CancellationToken.None);
        File.WriteAllText(Path.Combine(prepared.Directory, "payload", "Data", "shared.txt"), "tampered");
        Assert.Equal(1, await Install(prepared));
        Assert.False(File.Exists(Path.Combine(prepared.Directory, "ready")));
        Assert.Equal("old-main", File.ReadAllText(RootFile("NxFileViewer.exe"))); Assert.Equal("old-data", File.ReadAllText(RootFile("Data/shared.txt")));
    }
    [Fact]
    public async Task CleanupRecognizesSuccessfulPackageBackupButRefusesForeignFiles()
    {
        Original();
        var prepared = ViewerPackageLayout.Extract(Package("NxFileViewerFork.exe", ("Data/shared.txt", "new-data"), ("Empty/", "")), Stage(), "package.zip", CancellationToken.None);
        Assert.True(await Install(prepared) == 0, Error(prepared));
        File.SetLastWriteTimeUtc(Path.Combine(prepared.Directory, "completed"), DateTime.UtcNow.AddMinutes(-3));
        Assert.Contains(prepared.Directory, OldVersionCleanup.ViewerUpdateDirectories(_root));
        File.WriteAllText(Path.Combine(prepared.Directory, "backup", "user.txt"), "keep");
        Assert.DoesNotContain(prepared.Directory, OldVersionCleanup.ViewerUpdateDirectories(_root));
        File.Delete(Path.Combine(prepared.Directory, "backup", "user.txt"));
        OldVersionCleanup.DeleteViewerUpdates(_root, new[] { prepared.Directory });
        Assert.False(Directory.Exists(prepared.Directory)); Assert.True(File.Exists(RootFile("NxFileViewerFork.exe")));
    }
    [Fact]
    public void TransitionMovesOldLocationsWithoutReplacingNewerDataAndLeavesConflicts()
    {
        Put("Cache/TitleDB/DE.de.json", "old-title"); Put("Cache/Updates/cache.json", "old-cache"); Put("fw/hashes/reference.json", "old-hash");
        Put("Plugins/TitleDB/DE.de.json", "newer-title");
        Put("NxFileViewer.settings.json", "settings");
        global::NxFileViewer.Transition.LegacyDataMigration.Move(_root);
        Assert.Equal("settings", File.ReadAllText(RootFile("NxFileViewerFork.settings.json")));
        Assert.False(File.Exists(RootFile("NxFileViewer.settings.json")));
        Assert.Equal("newer-title", File.ReadAllText(RootFile("Plugins/TitleDB/DE.de.json")));
        Assert.Equal("old-cache", File.ReadAllText(RootFile("Plugins/CacheUpdates/cache.json")));
        Assert.Equal("old-hash", File.ReadAllText(RootFile("Plugins/fwhashes/reference.json")));
        Assert.False(File.Exists(RootFile("fw/hashes/reference.json")));
        Assert.Equal("old-title", File.ReadAllText(RootFile("Cache/TitleDB/DE.de.json")));
        global::NxFileViewer.Transition.LegacyDataMigration.Move(_root);
        Assert.Equal(RootFile("Plugins/fwhashes"), ApplicationDataPaths.FirmwareHashes(_root));
    }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}