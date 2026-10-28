using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Emignatik.NxFileViewer.Services;
using Emignatik.NxFileViewer.Services.FileAssociations;
using Emignatik.NxFileViewer.Services.Updates;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services.Updates;

public sealed class RepositoryMirrorsTest
{
    private const string GitLab="gitlab:https://gitlab.components.example/team/app";
    private const string Forgejo="forgejo:https://forge.components.example/team/app";
    private const string Name="NxFileViewerFork.Explorer_1.0.0.7_x64_setup.zip";
    private static readonly string Hash=new('a',64);
    private static readonly byte[] Firmware=JsonSerializer.SerializeToUtf8Bytes(new { name="test", file_count=1,
        files=new Dictionary<string,object> { ["one.nca"]=new { size=1, sha256=new string('a',64) } } });
    private static string Blob(byte[] bytes) {
        using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
        hash.AppendData(Encoding.ASCII.GetBytes("blob "+bytes.Length+"\0")); hash.AppendData(bytes);
        return Convert.ToHexString(hash.GetHashAndReset());
    }
    private sealed class Handler(Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> handle):HttpMessageHandler {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)=>handle(request,token);
    }
    private static HttpResponseMessage Json(object value)=>new(HttpStatusCode.OK) { Content=new StringContent(JsonSerializer.Serialize(value)) };
    private static HttpResponseMessage Bytes(byte[] bytes)=>new(HttpStatusCode.OK) { Content=new ByteArrayContent(bytes) };
    private static GitHubContentLocation Location(params string[] sources)=>new("owner/legacy","master","fw/hashes") { Sources=sources };

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task NativeFirmwareListingAndRawDownloadsAreVerified(bool gitlab)
    {
        var source=gitlab?GitLab:Forgejo; var raw=false;
        using var client=new HttpClient(new Handler((request,_)=> {
            var path=request.RequestUri!.AbsolutePath;
            if (path.Contains("/repository/tree") || path.Contains("/contents/")) {
                if (gitlab) return Task.FromResult(Json(new[] { new { type="blob",name="hashes.json",id=Blob(Firmware) } }));
                return Task.FromResult(Json(new[] { new { type="file",name="hashes.json",sha=Blob(Firmware),download_url="https://forge.components.example/raw/master/fw/hashes/hashes.json" } }));
            }
            raw=true;
            if (gitlab) Assert.Contains("/repository/files/fw%2Fhashes%2Fhashes.json/raw",request.RequestUri.AbsoluteUri,StringComparison.OrdinalIgnoreCase);
            return Task.FromResult(Bytes(Firmware));
        }));
        var result=await new RepositoryMirrors(client).FirmwareManifestsAsync(Location(source),TestContext.Current.CancellationToken);
        Assert.True(raw); Assert.Single(result); Assert.Equal(Encoding.UTF8.GetString(Firmware),result["hashes.json"]);
    }

    [Fact]
    public async Task CorruptPartialSnapshotIsDiscardedBeforeFallback()
    {
        using var client=new HttpClient(new Handler((request,_)=> {
            var uri=request.RequestUri!;
            if (uri.Host=="gitlab.components.example") return Task.FromResult(uri.AbsolutePath.Contains("/tree")
                ?Json(new[] { new { type="blob",name="bad.json",id=Blob(Firmware) } }):Bytes(Encoding.UTF8.GetBytes("corrupt")));
            return Task.FromResult(uri.AbsolutePath.Contains("/contents/")
                ?Json(new[] { new { type="file",name="good.json",sha=Blob(Firmware),download_url="https://forge.components.example/raw/good.json" } }):Bytes(Firmware));
        }));
        var result=await new RepositoryMirrors(client).FirmwareManifestsAsync(Location(GitLab,Forgejo),TestContext.Current.CancellationToken);
        Assert.Single(result); Assert.True(result.ContainsKey("good.json")); Assert.False(result.ContainsKey("bad.json"));
    }

    [Fact]
    public async Task FirmwareRejectsCrossHostRawLinksBeforeDownload()
    {
        using var client=new HttpClient(new Handler((request,_)=> {
            Assert.Equal("forge.components.example",request.RequestUri!.Host);
            return Task.FromResult(Json(new[] { new { type="file",name="hashes.json",sha=Blob(Firmware),download_url="https://untrusted.example/file.json" } }));
        }));
        await Assert.ThrowsAsync<InvalidDataException>(()=>new RepositoryMirrors(client).FirmwareManifestsAsync(Location(Forgejo),TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GitLabPaginationFollowsNextHeaderEvenWithShortPages()
    {
        int pages=0;
        using var client=new HttpClient(new Handler((request,_)=> {
            var response=Json(new[] {new { type="blob",name="page"+(++pages)+".json",id=Blob(Firmware) }});
            response.Headers.Add("X-Next-Page",pages==1?"2":"");
            return Task.FromResult(response);
        }));
        var files=await new RepositoryMirrors(client).FirmwareListingAsync(Location(GitLab),TestContext.Current.CancellationToken);
        Assert.Equal(2,pages); Assert.Equal(2,files.Count);
    }

    private static object Release(bool gitlab,string origin,string name=Name,string version="v4.2.0",bool draft=false)=>new {
        tag_name=version,draft,prerelease=version.Contains('-'),assets=gitlab?(object)new { links=new[] {
            new {name=ViewerUpdateMetadata.ReleaseManifestName,direct_asset_url=origin+"/files/index.json",url="https://wrong.example/index.json"},
            new {name,direct_asset_url=origin+"/files/"+name,url="https://wrong.example/package.zip"}
        }}:new[] {
            new {name=ViewerUpdateMetadata.ReleaseManifestName,browser_download_url=origin+"/attachments/index"},
            new {name,browser_download_url=origin+"/attachments/package"}
        }
    };
    private static ViewerReleaseManifest Descriptor(string? hash=null,string arch="x64",string name=Name)=>new(1,ViewerPackageLayout.ApplicationId,"4.2.0",Array.Empty<ViewerReleasePackage>()) {
        ExplorerPackages=new[] {new ViewerReleasePackage(arch,false,name) { Sha256=hash??Hash }}
    };

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task NativeExplorerUsesDescriptorHashAndActualAttachmentUrl(bool gitlab)
    {
        var origin=gitlab?"https://gitlab.components.example":"https://forge.components.example";
        using var client=new HttpClient(new Handler((request,_)=>Task.FromResult(request.RequestUri!.AbsolutePath.EndsWith("/releases")
            ?Json(new[] {Release(gitlab,origin)}):Json(Descriptor()))));
        var release=await new ExplorerPackageService(client,new[] {gitlab?GitLab:Forgejo}).CheckAsync("x64",false,TestContext.Current.CancellationToken);
        Assert.Equal(new Version(1,0,0,7),release!.Version); Assert.Equal("sha256:"+Hash,release.Digest);
        Assert.Equal(gitlab?"/files/"+Name:"/attachments/package",release.DownloadUrl.AbsolutePath);
    }

    [Fact]
    public async Task ExplorerSelectsHighestMsixVersionAcrossMirrors()
    {
        var newer=Name.Replace("1.0.0.7","1.0.0.9");
        using var client=new HttpClient(new Handler((request,_)=> {
            var uri=request.RequestUri!; bool gitlab=uri.Host=="gitlab.components.example";
            var name=gitlab?Name:newer;
            return Task.FromResult(uri.AbsolutePath.EndsWith("/releases")?Json(new[] {Release(gitlab,uri.GetLeftPart(UriPartial.Authority),name)}):Json(Descriptor(name:name)));
        }));
        var release=await new ExplorerPackageService(client,new[] {GitLab,Forgejo}).CheckAsync("x64",false,TestContext.Current.CancellationToken);
        Assert.Equal(new Version(1,0,0,9),release!.Version); Assert.Equal(Forgejo,release.SourceSpec);
    }

    [Fact]
    public async Task WrongDescriptorVersionDoesNotBecomeAnExplorerOffer()
    {
        using var client=new HttpClient(new Handler((request,_)=>Task.FromResult(request.RequestUri!.AbsolutePath.EndsWith("/releases")
            ?Json(new[] {Release(true,"https://gitlab.components.example")}):Json(Descriptor() with { Version="9.0.0" }))));
        await Assert.ThrowsAsync<InvalidDataException>(()=>new ExplorerPackageService(client,new[] {GitLab}).CheckAsync("x64",false,TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PreviewReleaseIsSkippedWithoutFetchingDescriptor()
    {
        using var client=new HttpClient(new Handler((request,_)=> {
            Assert.EndsWith("/releases",request.RequestUri!.AbsolutePath);
            return Task.FromResult(Json(new[] {Release(true,"https://gitlab.components.example",version:"v4.2.0-beta1")}));
        }));
        Assert.Null(await new ExplorerPackageService(client,new[] {GitLab}).CheckAsync("x64",false,TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UserCancellationStopsAllMirrors()
    {
        using var cancel=CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        using var client=new HttpClient(new Handler(async (_,token)=> {cancel.Cancel();await Task.Delay(Timeout.Infinite,token);return Json(new {}); }));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>new ExplorerPackageService(client,new[] {GitLab,Forgejo}).CheckAsync("x64",false,cancel.Token));
    }

    [Fact]
    public void DescriptorRejectsMissingHashesAndDuplicateArchitectures()
    {
        Assert.Throws<InvalidDataException>(()=>ViewerUpdateMetadata.ReadRelease(JsonSerializer.SerializeToUtf8Bytes(Descriptor() with {
            ExplorerPackages=new[] { new ViewerReleasePackage("x64",false,Name) }
        }),"v4.2.0"));
        var package=Descriptor().ExplorerPackages[0];
        Assert.Throws<InvalidDataException>(()=>ViewerUpdateMetadata.ReadRelease(JsonSerializer.SerializeToUtf8Bytes(Descriptor() with {
            ExplorerPackages=new[] {package,package}
        }),"v4.2.0"));
    }
    [Fact]
    public async Task AutomaticFirmwareChecksReuseCachedDirectoryMetadata()
    {
        int requests=0;
        using var client=new HttpClient(new Handler((_,_)=> {
            requests++;
            return Task.FromResult(Json(new[] {new {type="file",name="hashes.json",sha=Blob(Firmware)}}));
        }));
        var location=Location("github:cache-owner/firmware");
        await new RepositoryMirrors(client,automatic:true).FirmwareListingAsync(location,TestContext.Current.CancellationToken);
        await new RepositoryMirrors(client,automatic:true).FirmwareListingAsync(location,TestContext.Current.CancellationToken);
        Assert.Equal(1,requests);
    }

    [Fact]
    public async Task RateLimitIsRememberedWithoutBlockingAnotherForge()
    {
        const string limited="gitlab:https://gitlab.limit-components.example/team/app";
        int limitedRequests=0;
        using var client=new HttpClient(new Handler((request,_)=> {
            var uri=request.RequestUri!;
            if(uri.Host=="gitlab.limit-components.example") {
                limitedRequests++;
                var response=new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                response.Headers.RetryAfter=new(TimeSpan.FromHours(1));
                return Task.FromResult(response);
            }
            return Task.FromResult(uri.AbsolutePath.EndsWith("/releases")
                ?Json(new[] {Release(false,"https://forge.components.example")}):Json(Descriptor()));
        }));
        for(int i=0;i<2;i++) {
            var release=await new ExplorerPackageService(client,new[] {limited,Forgejo}).CheckAsync("x64",false,TestContext.Current.CancellationToken);
            Assert.NotNull(release);
        }
        Assert.Equal(1,limitedRequests);
    }

    [Fact]
    public async Task ExplorerArm64UsesItsOwnManifestPackage()
    {
        var name=Name.Replace("_x64_","_arm64_");
        using var client=new HttpClient(new Handler((request,_)=>Task.FromResult(request.RequestUri!.AbsolutePath.EndsWith("/releases")
            ?Json(new[] {Release(false,"https://forge.components.example",name)}):Json(Descriptor(arch:"arm64",name:name)))));
        var release=await new ExplorerPackageService(client,new[] {Forgejo}).CheckAsync("arm64",false,TestContext.Current.CancellationToken);
        Assert.Equal("arm64",release!.Architecture); Assert.Equal(name,release.AssetName);
    }

    [Fact]
    public async Task CrossOriginExplorerDescriptorIsRejectedWithoutFetchingIt()
    {
        int requests=0;
        using var client=new HttpClient(new Handler((request,_)=> {
            requests++; Assert.Equal("gitlab.components.example",request.RequestUri!.Host);
            return Task.FromResult(Json(new[] {Release(true,"https://untrusted.example")}));
        }));
        await Assert.ThrowsAsync<InvalidDataException>(()=>new ExplorerPackageService(client,new[] {GitLab}).CheckAsync("x64",false,TestContext.Current.CancellationToken));
        Assert.Equal(1,requests);
    }

    [Fact]
    public async Task AppUpdaterAlsoContinuesAfterMalformedSourceMetadata()
    {
        var root=Path.Combine(Path.GetTempPath(),"AppMirrorFallback-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try {
            var sources=new[] {"manifest:https://bad.component-index.example/index.json","manifest:https://good.component-index.example/index.json"};
            File.WriteAllText(Path.Combine(root,ViewerUpdateMetadata.StateName),JsonSerializer.Serialize(
                new ViewerUpdateState(1,ViewerPackageLayout.ApplicationId,sources,Array.Empty<string>())));
            using var client=new HttpClient(new Handler((request,_)=>Task.FromResult(request.RequestUri!.Host.StartsWith("bad.",StringComparison.Ordinal)
                ?Json(new {SchemaVersion=77}):Json(new ViewerReleaseManifest(1,ViewerPackageLayout.ApplicationId,"4.2.0",new[] {
                    new ViewerReleasePackage("x64",false,"App.zip") {Sha256=Hash}
                })))));
            var release=await new ViewerUpdateService(client,root).CheckAsync(new Version(4,1,0),"x64",TestContext.Current.CancellationToken);
            Assert.Equal("4.2.0",release!.Tag);
        }
        finally {Directory.Delete(root,true);}
    }
}
