using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using NxFileViewer.Avalonia.Updates;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services;
public sealed class AvaloniaUpdaterTest : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(),"avalonia-updater-test-"+Guid.NewGuid().ToString("N"));
    public AvaloniaUpdaterTest() => Directory.CreateDirectory(_root);
    private static byte[] Elf(bool arm=false) { var bytes=new byte[32];bytes[0]=0x7f;bytes[1]=(byte)'E';bytes[2]=(byte)'L';bytes[3]=(byte)'F';bytes[4]=2;bytes[5]=1;bytes[18]=(byte)(arm?183:62);return bytes; }
    private static MemoryStream Zip(params (string Name,byte[] Bytes)[] files)
    {
        var memory=new MemoryStream();
        using(var zip=new ZipArchive(memory,ZipArchiveMode.Create,true))foreach(var file in files){using var output=zip.CreateEntry(file.Name).Open();output.Write(file.Bytes);}
        memory.Position=0;return memory;
    }
    private static AvaloniaRelease Release(bool generic=false)=>new("4.1.1-beta6","fixture.zip","https://github.com/owner/repo/releases/download/4.1.1-beta6/fixture.zip",null,generic);
    [Theory]
    [InlineData("win-x64",false,"x64",false)]
    [InlineData("win-x86",true,"x86",true)]
    [InlineData("linux-arm64",true,"linux-arm64",true)]
    [InlineData("osx-x64",false,"osx-x64",false)]
    public void PicksExactPlatformArchitectureAndRuntime(string rid,bool contained,string suffix,bool self)
    {
        var name=$"NxFileViewerFork_Avalonia_v4.1.1-beta6{(self?"_self-contained":"")}_{suffix}.zip";
        using var json=JsonDocument.Parse(JsonSerializer.Serialize(new{tag_name="4.1.1-beta6",assets=new[]{new{name="NxFileViewerFork_v4.1.1-beta6_x64.zip"},new{name},new{name="NxFileViewerFork.LogViewer_Avalonia_v4.1.1-beta6_x64.zip"}}}));
        Assert.Equal(name,AvaloniaAppUpdater.SelectRelease(json.RootElement,"owner/repo",rid,contained)?.AssetName);
    }
    [Theory]
    [InlineData("4.1.1-beta9","4.1.1-beta10",true)]
    [InlineData("4.1.1-beta10","4.1.1-beta9",false)]
    [InlineData("4.1.1-beta10+old","4.1.1-beta10+new",false)]
    public void ComparesNumericBetaVersions(string installed,string latest,bool expected) => Assert.Equal(expected,AvaloniaAppUpdater.IsNewer(installed,latest));
    [Fact]
    public void X64SwitchFindsSameVersionWithMatchingRuntimeAndRejectsDowngrades()
    {
        using var json = JsonDocument.Parse("""
            [
              {"tag_name":"4.1.3","assets":[{"name":"NxFileViewerFork_v4.1.3_win-x86-without-net.zip"}]},
              {"tag_name":"4.1.2","assets":[{"name":"NxFileViewerFork_v4.1.2_win-x64-without-net.zip"}]},
              {"tag_name":"4.1.1","assets":[{"name":"NxFileViewerFork_v4.1.1_win-x64-with-net.zip"}]}
            ]
            """);
        var releases = json.RootElement.EnumerateArray().ToArray();
        Assert.Equal("4.1.2", AvaloniaAppUpdater.SelectX64SwitchRelease(releases, "owner/repo", "4.1.2", false)?.Version);
        Assert.Null(AvaloniaAppUpdater.SelectX64SwitchRelease(releases, "owner/repo", "4.1.3", false));
        Assert.Null(AvaloniaAppUpdater.SelectX64SwitchRelease(releases, "owner/repo", "4.1.2", true));
    }

    [Fact]
    public void GenericWpfPackagesAreNotOfferedWithoutAvaloniaMarker()
    {
        var name="NxFileViewerFork_v4.1.1-beta6_x64.zip";
        using var plain=JsonDocument.Parse(JsonSerializer.Serialize(new{tag_name="4.1.1-beta6",assets=new[]{new{name}}}));
        Assert.Null(AvaloniaAppUpdater.SelectRelease(plain.RootElement,"owner/repo","win-x64",false));
        using var marked=JsonDocument.Parse(JsonSerializer.Serialize(new{tag_name="4.1.1-beta6",assets=new[]{new{name},new{name=name+".avalonia.json"}}}));
        Assert.True(AvaloniaAppUpdater.SelectRelease(marked.RootElement,"owner/repo","win-x64",false)!.RequiresManifest);
    }
    [Theory]
    [InlineData("win-x64", false, "NxFileViewerFork_v4.1.2-beta1_win-x64-without-net.zip")]
    [InlineData("linux-x64", true, "NxFileViewerFork_v4.1.2-beta1_linux-x64-with-net.zip")]
    [InlineData("osx-x64", true, "NxFileViewerFork_v4.1.2-beta1_osx-x64-with-net.zip")]
    public void SelectsNewZipNamesAndIgnoresNativeWrapperArchives(string rid, bool selfContained, string name)
    {
        var wrapper = name.Replace(".zip", rid.StartsWith("linux-") ? "-appimage.zip" : "-dmg.zip");
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            tag_name = "4.1.2-beta1", assets = new[] { new { name = wrapper }, new { name } }
        }));
        var result = Assert.IsType<AvaloniaRelease>(AvaloniaAppUpdater.SelectRelease(json.RootElement, "owner/repo", rid, selfContained));
        Assert.Equal(name, result.AssetName);
        Assert.True(result.RequiresManifest);
    }
    [Fact]
    public void RejectsTraversalUserSettingsAndWrongArchitecture()
    {
        foreach(var name in new[]{"../outside","../symbols.pdb","Plugins/symbols.pdb","prod.keys","NxFileViewer.settings.json","Plugins/tool"})
        {
            using var zip=Zip(("NxFileViewerFork.Avalonia",Elf()),(name,new byte[]{1}));
            Assert.Throws<InvalidDataException>(()=>AvaloniaAppUpdater.Extract(zip,Path.Combine(_root,Guid.NewGuid().ToString("N")),Release(),"linux-x64",false));
        }
        using var wrong=Zip(("NxFileViewerFork.Avalonia",Elf(true)));
        Assert.Throws<InvalidDataException>(()=>AvaloniaAppUpdater.Extract(wrong,Path.Combine(_root,"wrong"),Release(),"linux-x64",false));
        using var missing=Zip(("NxFileViewerFork",Elf()));
        Assert.Throws<InvalidDataException>(()=>AvaloniaAppUpdater.Extract(missing,Path.Combine(_root,"missing"),Release(true),"linux-x64",false));
    }
    [Fact]
    public void IgnoresPublishedDebugSymbolsWithoutStagingThem()
    {
        using var zip = Zip(("NxFileViewerFork.Avalonia", Elf()), ("libHarfBuzzSharp.pdb", new byte[] { 1 }));
        var destination = Path.Combine(_root, "with-symbols");
        AvaloniaAppUpdater.Extract(zip, destination, Release(), "linux-x64", false);
        Assert.Equal("NxFileViewerFork.Avalonia", Path.GetFileName(Assert.Single(Directory.GetFiles(destination))));
    }
    [Fact]
    public void StagesLinuxShortcutIconFromUpdateZip()
    {
        byte[] icon = [137, 80, 78, 71];
        using var zip = Zip(("NxFileViewerFork.Avalonia", Elf()), ("nxfv-icon.png", icon));
        var destination = Path.Combine(_root, "with-icon");
        AvaloniaAppUpdater.Extract(zip, destination, Release(), "linux-x64", false);
        Assert.Equal(icon, File.ReadAllBytes(Path.Combine(destination, "nxfv-icon.png")));
    }

    [Fact]
    public void ValidatesManifestAndSupportsFutureExecutableName()
    {
        var package=new AvaloniaPackage(1,"Avalonia","4.1.1-beta6","linux-x64",false,"NxFileViewerFork");
        using var zip=Zip(("NxFileViewerFork",Elf()),(AvaloniaAppUpdater.ManifestName,JsonSerializer.SerializeToUtf8Bytes(package)));
        Assert.Equal("NxFileViewerFork",AvaloniaAppUpdater.Extract(zip,Path.Combine(_root,"future"),Release(true),"linux-x64",false).Executable);
        using var wrong=Zip(("NxFileViewerFork",Elf()),(AvaloniaAppUpdater.ManifestName,JsonSerializer.SerializeToUtf8Bytes(package with{Framework="WPF"})));
        Assert.Throws<InvalidDataException>(()=>AvaloniaAppUpdater.Extract(wrong,Path.Combine(_root,"wpf"),Release(true),"linux-x64",false));
        using var renamed = Zip(("NxFileViewer", Elf()), (AvaloniaAppUpdater.ManifestName, JsonSerializer.SerializeToUtf8Bytes(package with { Executable = "NxFileViewer" })));
        Assert.Equal("NxFileViewer", AvaloniaAppUpdater.Extract(renamed, Path.Combine(_root, "renamed"), Release(true), "linux-x64", false).Executable);
    }
    [Fact]
    public void LegacyPackageManifestIsExtractedWithNeutralName()
    {
        var package = new AvaloniaPackage(1, "Avalonia", "4.1.1-beta6", "linux-x64", false, "NxFileViewerFork");
        using var zip = Zip(("NxFileViewerFork", Elf()), ("nxfileviewer-avalonia.json", JsonSerializer.SerializeToUtf8Bytes(package)));
        var destination = Path.Combine(_root, "legacy-package");
        AvaloniaAppUpdater.Extract(zip, destination, Release(true), "linux-x64", false);
        Assert.True(File.Exists(Path.Combine(destination, AvaloniaAppUpdater.ManifestName)));
        Assert.False(File.Exists(Path.Combine(destination, "nxfileviewer-avalonia.json")));
    }
    [Fact]
    public void FailedRestartRollsBackAndKeepsUserFiles()
    {
        var stage=Path.Combine(_root,"stage");var payload=Path.Combine(stage,"payload");var install=Path.Combine(_root,"installed");Directory.CreateDirectory(payload);Directory.CreateDirectory(install);
        const string exe="NxFileViewerFork.Avalonia";
        File.WriteAllText(Path.Combine(payload,exe),"new");File.WriteAllText(Path.Combine(install,exe),"old");File.WriteAllText(Path.Combine(install,"prod.keys"),"private");
        var job=new UpdateJob(1,1,exe,exe,new[]{new UpdateFile(exe,AvaloniaAppUpdater.Hash(Path.Combine(payload,exe)))});
        Assert.Throws<IOException>(()=>AvaloniaAppUpdater.Apply(stage,install,job,_=>throw new IOException("restart failure")));
        Assert.Equal("old",File.ReadAllText(Path.Combine(install,exe)));Assert.Equal("private",File.ReadAllText(Path.Combine(install,"prod.keys")));
        File.WriteAllText(Path.Combine(payload,exe),"tampered");
        Assert.Throws<InvalidDataException>(()=>AvaloniaAppUpdater.Apply(stage,install,job,_=>throw new Exception("must not launch")));
    }
    [Fact]
    public void SuccessfulApplyPreservesSettingsAndStartsFromInstallDirectory()
    {
        var stage=Path.Combine(_root,"success-stage");var payload=Path.Combine(stage,"payload");var install=Path.Combine(_root,"success-install");Directory.CreateDirectory(payload);Directory.CreateDirectory(install);
        const string exe="NxFileViewerFork.Avalonia";
        File.WriteAllText(Path.Combine(payload,exe),"new");File.WriteAllText(Path.Combine(install,exe),"old");File.WriteAllText(Path.Combine(install,"NxFileViewer.settings.json"),"keep");
        var job=new UpdateJob(1,1,exe,exe,new[]{new UpdateFile(exe,AvaloniaAppUpdater.Hash(Path.Combine(payload,exe)))});
        var launched=false;
        AvaloniaAppUpdater.Apply(stage,install,job,start=>{Assert.Equal(install,start.WorkingDirectory);Assert.Equal(Path.Combine(install,exe),start.FileName);launched=true;});
        Assert.True(launched);Assert.Equal("new",File.ReadAllText(Path.Combine(install,exe)));Assert.Equal("keep",File.ReadAllText(Path.Combine(install,"NxFileViewer.settings.json")));
    }
    [Theory]
    [InlineData("NxFileViewerFork.exe")]
    [InlineData("NxFileViewer.exe")]
    public void ManualRollbackRestoresPreviousFilesAndCleanupKeepsUnrelatedData(string newName)
    {
        const string previousName = "NxFileViewerFork.exe";
        var stage = Path.Combine(_root, "Updates", "avalonia-" + Guid.NewGuid().ToString("N"));
        var payload = Directory.CreateDirectory(Path.Combine(stage, "payload")).FullName;
        var active = Path.Combine(_root, newName);
        var previous = Path.Combine(_root, previousName);
        var settings = Path.Combine(_root, "NxFileViewer.settings.json");
        File.WriteAllText(previous, "previous");
        File.WriteAllText(settings, "settings");
        File.WriteAllText(Path.Combine(payload, newName), "installed");
        File.WriteAllText(Path.Combine(payload, "LICENSE.txt"), "added");
        var files = Directory.GetFiles(payload).Select(path => new UpdateFile(Path.GetFileName(path), AvaloniaAppUpdater.Hash(path))).ToArray();
        AvaloniaAppUpdater.Apply(stage, _root, new UpdateJob(1, 1, newName, previousName, files), _ => { });
        Assert.NotNull(AvaloniaAppUpdater.FindRollback(active));

        var rollbackJob = AvaloniaAppUpdater.PrepareRollback(active);
        var job = JsonSerializer.Deserialize<UpdateJob>(File.ReadAllText(rollbackJob))!;
        File.WriteAllText(active, "changed");
        Assert.Throws<InvalidDataException>(() =>
            AvaloniaAppUpdater.Apply(Path.GetDirectoryName(rollbackJob)!, _root, job, _ => throw new InvalidOperationException()));
        File.WriteAllText(active, "installed");
        AvaloniaAppUpdater.Apply(Path.GetDirectoryName(rollbackJob)!, _root, job, start => Assert.Equal(previous, start.FileName));
        Assert.Equal("previous", File.ReadAllText(previous));
        Assert.False(File.Exists(Path.Combine(_root, "LICENSE.txt")));
        if (newName != previousName) Assert.False(File.Exists(active));

        var unrelated = Directory.CreateDirectory(Path.Combine(_root, "Updates", "unrelated")).FullName;
        AvaloniaAppUpdater.CleanCompletedStages(_root, AvaloniaAppUpdater.CompletedStages(_root));
        Assert.Empty(AvaloniaAppUpdater.CompletedStages(_root));
        Assert.True(Directory.Exists(unrelated));
        Assert.Equal("settings", File.ReadAllText(settings));
        Assert.Equal("previous", File.ReadAllText(previous));
    }

    [Fact]
    public void RecognizesOlderRenamedExecutableOnlyWhenItsHashMatchesTheUpdateJob()
    {
        const string previousName = "NxFileViewerFork.exe";
        const string installedName = "NxFileViewer.exe";
        var previous = Path.Combine(_root, previousName);
        var installed = Path.Combine(_root, installedName);
        File.WriteAllText(previous, "previous");
        File.WriteAllText(installed, "installed");
        var stage = Path.Combine(_root, "Updates", "avalonia-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(stage, "backup"));
        File.WriteAllText(Path.Combine(stage, "ready"), "ready");
        var update = new UpdateJob(1, 1, installedName, previousName,
            [new(installedName, AvaloniaAppUpdater.Hash(installed))], AvaloniaAppUpdater.Hash(previous));
        File.WriteAllText(Path.Combine(stage, "job.json"), JsonSerializer.Serialize(update));
        var rollbackJob = AvaloniaAppUpdater.PrepareRollback(installed);
        var rollbackStage = Path.GetDirectoryName(rollbackJob)!;
        Assert.Equal("previous", File.ReadAllText(Path.Combine(rollbackStage, "payload", previousName)));
        File.WriteAllText(previous, "modified");
        Assert.Null(AvaloniaAppUpdater.FindRollback(installed));
        File.WriteAllText(previous, "previous");
        var job = JsonSerializer.Deserialize<UpdateJob>(File.ReadAllText(rollbackJob))!;
        AvaloniaAppUpdater.Apply(rollbackStage, _root, job, start => Assert.Equal(previous, start.FileName));
        Assert.False(File.Exists(installed));
    }

    private sealed class FixtureHandler(byte[] bytes) : System.Net.Http.HttpMessageHandler
    {
        protected override Task<System.Net.Http.HttpResponseMessage> SendAsync(System.Net.Http.HttpRequestMessage request,System.Threading.CancellationToken token) =>
            Task.FromResult(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK){Content=new System.Net.Http.ByteArrayContent(bytes)});
    }
    [Fact]
    public async Task RefusesChecksumMismatchBeforeExtractingAndCleansStage()
    {
        using var zip=Zip(("NxFileViewerFork.Avalonia",Elf()));
        using var client=new System.Net.Http.HttpClient(new FixtureHandler(zip.ToArray()));
        await Assert.ThrowsAsync<InvalidDataException>(()=>AvaloniaAppUpdater.DownloadAsync(client,Release() with{Digest="sha256:"+new string('0',64)},_root,"linux-x64",false,TestContext.Current.CancellationToken));
        Assert.Empty(Directory.GetDirectories(Path.Combine(_root,"Updates")));
    }
    private sealed class IndexHandler(byte[] zip, bool wrongFramework, bool combined = false) : System.Net.Http.HttpMessageHandler
    {
        protected override Task<System.Net.Http.HttpResponseMessage> SendAsync(System.Net.Http.HttpRequestMessage request, System.Threading.CancellationToken token)
        {
            var entries = new[] { new { AssetName = "fixture.zip", Framework = wrongFramework ? "WPF" : "Avalonia", Sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(zip)) } };
            var bytes = request.RequestUri!.AbsolutePath.EndsWith("nxfileviewer-update.json")
                ? combined
                    ? JsonSerializer.SerializeToUtf8Bytes(new { SchemaVersion = 1, ApplicationId = "NxFileViewerFork", AvaloniaPackages = entries })
                    : JsonSerializer.SerializeToUtf8Bytes(new { SchemaVersion = 1, ApplicationId = "NxFileViewerFork" })
                : request.RequestUri.AbsolutePath.EndsWith("nxfileviewer-packages.json") && !combined
                    ? JsonSerializer.SerializeToUtf8Bytes(new { SchemaVersion = 1, Packages = entries })
                    : zip;
            return Task.FromResult(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new System.Net.Http.ByteArrayContent(bytes) });
        }
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ReadsCombinedManifestOrFallsBackToLegacyIndex(bool combined)
    {
        using var zip = Zip(("NxFileViewerFork.Avalonia", Elf()));
        using var client = new System.Net.Http.HttpClient(new IndexHandler(zip.ToArray(), false, combined));
        var release = Release() with { HasReleaseManifest = true };
        var stage = await AvaloniaAppUpdater.DownloadAsync(client, release, _root, "linux-x64", false, TestContext.Current.CancellationToken);
        Assert.True(File.Exists(Path.Combine(stage, "payload", "NxFileViewerFork.Avalonia")));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReadsCentralIndexAndRejectsOtherFramework(bool wrongFramework)
    {
        using var zip = Zip(("NxFileViewerFork.Avalonia", Elf()));
        using var client = new System.Net.Http.HttpClient(new IndexHandler(zip.ToArray(), wrongFramework));
        if (wrongFramework)
            await Assert.ThrowsAsync<InvalidDataException>(() => AvaloniaAppUpdater.DownloadAsync(client, Release(), _root, "linux-x64", false, TestContext.Current.CancellationToken));
        else
        {
            var stage = await AvaloniaAppUpdater.DownloadAsync(client, Release(), _root, "linux-x64", false, TestContext.Current.CancellationToken);
            Assert.True(File.Exists(Path.Combine(stage, "payload", "NxFileViewerFork.Avalonia")));
        }
    }
    [Fact]
    public void ValidatesArm32LinuxExecutableWithoutAcceptingItForArm64()
    {
        var bytes = Elf();
        bytes[4] = 1;
        bytes[18] = 40;
        var executable = Path.Combine(_root, "NxFileViewerFork");
        File.WriteAllBytes(executable, bytes);

        AvaloniaAppUpdater.ValidateNativeExecutable(executable, "linux-arm");
        Assert.Throws<InvalidDataException>(() => AvaloniaAppUpdater.ValidateNativeExecutable(executable, "linux-arm64"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SelectsAndStagesMatchingAppImage(bool zipped)
    {
        const string directAsset = "v4.1.1-beta6_linux-x64-with-net.AppImage";
        var asset = zipped ? "NxFileViewerFork_" + directAsset + ".zip" : directAsset;
        const string localName = "Mein Viewer.AppImage";
        var bytes = Elf();
        bytes[8] = (byte)'A';
        bytes[9] = (byte)'I';
        bytes[10] = 2;
        var download = bytes;
        if (zipped)
        {
            using var archiveBytes = new MemoryStream();
            using (var zip = new ZipArchive(archiveBytes, ZipArchiveMode.Create, leaveOpen: true))
            using (var entry = zip.CreateEntry("NxFileViewerFork.AppImage").Open())
                entry.Write(bytes);
            download = archiveBytes.ToArray();
        }
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(download));
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            tag_name = "v4.1.1-beta6",
            assets = new[] { "NxFileViewerFork.LogViewer_" + directAsset, asset.Replace("x64", "arm64"), asset, directAsset }.Distinct()
                .Select(name => new { name, digest = "sha256:" + hash })
        }));
        var release = AvaloniaAppUpdater.SelectRelease(json.RootElement, "owner/repo", "linux-x64", true, appImage: true);
        Assert.Equal(asset, release?.AssetName);
        Assert.Null(AvaloniaAppUpdater.SelectRelease(json.RootElement, "owner/repo", "linux-x64", false, appImage: true));
        Assert.Null(AvaloniaAppUpdater.SelectRelease(json.RootElement, "owner/repo", "linux-x64", true));
        using var client = new System.Net.Http.HttpClient(new FixtureHandler(download));
        var stage = await AvaloniaAppUpdater.DownloadAsync(client, release!, _root, "linux-x64", true, TestContext.Current.CancellationToken, localName);
        Assert.Equal(bytes, File.ReadAllBytes(Path.Combine(stage, "payload", localName)));
        Assert.Throws<InvalidDataException>(() => AvaloniaAppUpdater.ValidateAppImage(Path.Combine(stage, "payload", localName), "linux-arm64"));
    }

    [Fact]
    public void RestoresAppImageAtTheSamePathWhenRestartFails()
    {
        const string name = "Mein Viewer.AppImage";
        var installed = Path.Combine(_root, name);
        var bytes = Elf(System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture == System.Runtime.InteropServices.Architecture.Arm64);
        bytes[8] = (byte)'A';
        bytes[9] = (byte)'I';
        bytes[10] = 2;
        File.WriteAllBytes(installed, bytes);
        var original = AvaloniaAppUpdater.Hash(installed);
        var data = Directory.CreateDirectory(Path.Combine(_root, "NxFileViewerFork-Daten"));
        var settings = Path.Combine(data.FullName, "settings.json");
        File.WriteAllText(settings, "{}");
        var stage = Path.Combine(_root, "Updates", "update-" + Guid.NewGuid().ToString("N"));
        var payload = Directory.CreateDirectory(Path.Combine(stage, "payload"));
        var replacement = Path.Combine(payload.FullName, name);
        bytes[^1] = 1;
        File.WriteAllBytes(replacement, bytes);
        var updated = AvaloniaAppUpdater.Hash(replacement);
        var job = new UpdateJob(1, 1, name, name, [new UpdateFile(name, updated)], original);

        Assert.Throws<IOException>(() => AvaloniaAppUpdater.Apply(stage, _root, job, start =>
        {
            Assert.Equal(installed, start.FileName);
            Assert.Equal(updated, AvaloniaAppUpdater.Hash(installed));
            throw new IOException("Restart failed");
        }));
        Assert.Equal(original, AvaloniaAppUpdater.Hash(installed));
        Assert.Equal("{}", File.ReadAllText(settings));
    }

    [Theory]
    [InlineData(true, true, ".dmg")]
    [InlineData(true, false, ".dmg")]
    [InlineData(false, true, ".app.zip")]
    public void SelectsMacBundleUpdateWhetherTheReleaseHasZipOrDmg(bool dmg, bool zip, string extension)
    {
        const string asset = "NxFileViewerFork_v4.1.1-beta6_osx-arm64-with-net";
        var names = new[] { asset.Replace("arm64", "x64") + ".dmg", asset + ".zip", asset.Replace("Fork", "Fork.LogViewer") + ".dmg" }.ToList();
        if (dmg) names.Add(asset + ".dmg");
        if (zip) names.Add(asset + ".app.zip");
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(new { tag_name = "v4.1.1-beta6", assets = names.Select(name => new { name }) }));
        Assert.Equal(asset + extension, AvaloniaAppUpdater.SelectRelease(json.RootElement, "owner/repo", "osx-arm64", true, macBundle: true)?.AssetName);
        Assert.Null(AvaloniaAppUpdater.SelectRelease(json.RootElement, "owner/repo", "osx-arm64", false, macBundle: true));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ReplacesMacBundleAtItsCurrentPathAndRestoresItOnFailure(bool restartFails)
    {
        const string rid = "osx-arm64";
        const string executableName = "NxFileViewerFork";
        const string bundleRoot = "NxFileViewerFork Avalonia.app/Contents/";
        const string plist = "<plist><dict><key>CFBundleIdentifier</key><string>io.github.nxfileviewer.nxfileviewerfork.avalonia</string><key>CFBundleExecutable</key><string>NxFileViewerFork</string></dict></plist>";
        var executable = new byte[32];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(executable, 0xfeedfacf);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(executable.AsSpan(4), 0x100000c);
        var bundle = Path.Combine(_root, "Mein Viewer.app");
        using (var oldZip = CreateBundleZip())
            AvaloniaAppUpdater.ExtractMacBundle(oldZip, bundle, rid, TestContext.Current.CancellationToken);
        var previous = Path.Combine(bundle, "Contents", "MacOS", executableName);
        var settings = Path.Combine(Path.GetDirectoryName(previous)!, "NxFileViewerFork.settings.json");
        File.WriteAllText(settings, "{}");
        Assert.Equal(bundle, AvaloniaAppUpdater.GetMacBundle(previous));
        var stage = Path.Combine(_root, "Updates", "update-" + Guid.NewGuid().ToString("N"));
        executable[^1] = 1;
        using (var newZip = CreateBundleZip())
            AvaloniaAppUpdater.ExtractMacBundle(newZip, Path.Combine(stage, "bundle"), rid, TestContext.Current.CancellationToken);
        var jobPath = AvaloniaAppUpdater.CreateMacBundleJob(stage, bundle, previous, rid);
        var job = JsonSerializer.Deserialize<UpdateJob>(File.ReadAllText(jobPath))!;

        void Install() => AvaloniaAppUpdater.ApplyMacBundle(stage, _root, job, rid, start =>
        {
            Assert.Equal(previous, start.FileName);
            Assert.Equal(executable, File.ReadAllBytes(previous));
            Assert.Equal("{}", File.ReadAllText(settings));
            if (restartFails) throw new IOException("Restart failed.");
        });
        if (restartFails) Assert.Throws<IOException>(Install);
        else Install();
        Assert.Equal(restartFails ? 0 : 1, File.ReadAllBytes(previous)[^1]);
        Assert.Equal("{}", File.ReadAllText(settings));
        Assert.Equal(!restartFails, Directory.Exists(Path.Combine(stage, "previous.app")));

        MemoryStream CreateBundleZip() => Zip((bundleRoot + "Info.plist", System.Text.Encoding.UTF8.GetBytes(plist)),
            (bundleRoot + "MacOS/" + executableName, executable));
    }

    [Fact]
    public void RejectsTraversalInMacBundleZip()
    {
        using var zip = Zip(("Viewer.app/Contents/../../../outside", new byte[] { 1 }));
        Assert.Throws<InvalidDataException>(() => AvaloniaAppUpdater.ExtractMacBundle(zip, Path.Combine(_root, "bundle"), "osx-arm64", TestContext.Current.CancellationToken));
    }

    public void Dispose()=>Directory.Delete(_root,true);
}
