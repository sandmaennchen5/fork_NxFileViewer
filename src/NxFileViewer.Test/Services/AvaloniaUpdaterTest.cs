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
    public void GenericWpfPackagesAreNotOfferedWithoutAvaloniaMarker()
    {
        var name="NxFileViewerFork_v4.1.1-beta6_x64.zip";
        using var plain=JsonDocument.Parse(JsonSerializer.Serialize(new{tag_name="4.1.1-beta6",assets=new[]{new{name}}}));
        Assert.Null(AvaloniaAppUpdater.SelectRelease(plain.RootElement,"owner/repo","win-x64",false));
        using var marked=JsonDocument.Parse(JsonSerializer.Serialize(new{tag_name="4.1.1-beta6",assets=new[]{new{name},new{name=name+".avalonia.json"}}}));
        Assert.True(AvaloniaAppUpdater.SelectRelease(marked.RootElement,"owner/repo","win-x64",false)!.RequiresManifest);
    }
    [Fact]
    public void RejectsTraversalUserSettingsAndWrongArchitecture()
    {
        foreach(var name in new[]{"../outside","prod.keys","NxFileViewerFork.Avalonia.settings.json","Plugins/tool"})
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
    public void ValidatesManifestAndSupportsFutureExecutableName()
    {
        var package=new AvaloniaPackage(1,"Avalonia","4.1.1-beta6","linux-x64",false,"NxFileViewerFork");
        using var zip=Zip(("NxFileViewerFork",Elf()),(AvaloniaAppUpdater.ManifestName,JsonSerializer.SerializeToUtf8Bytes(package)));
        Assert.Equal("NxFileViewerFork",AvaloniaAppUpdater.Extract(zip,Path.Combine(_root,"future"),Release(true),"linux-x64",false).Executable);
        using var wrong=Zip(("NxFileViewerFork",Elf()),(AvaloniaAppUpdater.ManifestName,JsonSerializer.SerializeToUtf8Bytes(package with{Framework="WPF"})));
        Assert.Throws<InvalidDataException>(()=>AvaloniaAppUpdater.Extract(wrong,Path.Combine(_root,"wpf"),Release(true),"linux-x64",false));
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
        File.WriteAllText(Path.Combine(payload,exe),"new");File.WriteAllText(Path.Combine(install,exe),"old");File.WriteAllText(Path.Combine(install,"NxFileViewerFork.Avalonia.settings.json"),"keep");
        var job=new UpdateJob(1,1,exe,exe,new[]{new UpdateFile(exe,AvaloniaAppUpdater.Hash(Path.Combine(payload,exe)))});
        var launched=false;
        AvaloniaAppUpdater.Apply(stage,install,job,start=>{Assert.Equal(install,start.WorkingDirectory);Assert.Equal(Path.Combine(install,exe),start.FileName);launched=true;});
        Assert.True(launched);Assert.Equal("new",File.ReadAllText(Path.Combine(install,exe)));Assert.Equal("keep",File.ReadAllText(Path.Combine(install,"NxFileViewerFork.Avalonia.settings.json")));
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
    private sealed class IndexHandler(byte[] zip, bool wrongFramework) : System.Net.Http.HttpMessageHandler
    {
        protected override Task<System.Net.Http.HttpResponseMessage> SendAsync(System.Net.Http.HttpRequestMessage request, System.Threading.CancellationToken token)
        {
            var bytes = request.RequestUri!.AbsolutePath.EndsWith("nxfileviewer-packages.json")
                ? JsonSerializer.SerializeToUtf8Bytes(new { SchemaVersion = 1, Packages = new[] { new { AssetName = "fixture.zip", Framework = wrongFramework ? "WPF" : "Avalonia", Sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(zip)) } } })
                : zip;
            return Task.FromResult(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new System.Net.Http.ByteArrayContent(bytes) });
        }
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
    public void Dispose()=>Directory.Delete(_root,true);
}
