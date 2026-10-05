using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Emignatik.NxFileViewer.FileLoading.QuickFileInfoLoading;
using Emignatik.NxFileViewer.Models;
using Emignatik.NxFileViewer.Services.FileOpening;
using Emignatik.NxFileViewer.Services.FileRenaming;
using Emignatik.NxFileViewer.Services.FileRenaming.Models;
using Emignatik.NxFileViewer.Services.FileRenaming.Models.PatternParts;
using Emignatik.NxFileViewer.Services.OnlineServices;
using Emignatik.NxFileViewer.Settings;
using LibHac.Ns;
using LibHac.Tools.Ncm;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services.FileRenaming;

public class OfflineRenamingTest
{
    [Theory]
    [InlineData(PatternKeyword.OnlineTitleName)]
    [InlineData(PatternKeyword.OnlineAppTitleName)]
    public async Task SimulationUsesLocalTitleWhenServerReturns503(PatternKeyword keyword)
    {
        var metadata = new byte[0x60];
        metadata[0xC] = 0x80; // Application CNMT, no content entries.
        metadata[0xE] = 0x10;
        using var cnmtStream = new MemoryStream(metadata);
        var nacpBytes = new byte[0x4000];
        Encoding.UTF8.GetBytes("Until Then").CopyTo(nacpBytes, 0);
        nacpBytes[0x302C] = 1; // American English supported.
        var content = new Content(new Cnmt(cnmtStream))
        {
            NacpData = new NacpData(MemoryMarshal.Read<ApplicationControlProperty>(nacpBytes))
        };
        using var client = new HttpClient(new UnavailableHandler());
        var online = new CachedOnlineTitleInfoService(new OnlineTitleInfoService(new AppSettings(), httpClient: client));
        var renamer = new FileRenamerService(new PackageLoader(content), online, new UnusedFileOpeningService());
        var settings = new NamingSettings();
        settings.ApplicationPattern.Add(new DynamicTextPatternPart(keyword, StringOperator.Untouched));
        var result = await renamer.RenameFileAsync(Path.Combine(Path.GetTempPath(), "old.nsz"), false, settings, true, null, CancellationToken.None);
        Assert.Null(result.Exception);
        Assert.True(result.IsSimulation);
        Assert.Equal("Until Then", result.NewFileName);
    }

    private sealed class PackageLoader(Content content) : IPackageInfoLoader
    {
        public PackageInfo GetPackageInfo(string path) => new() { Contents = [content], AccuratePackageType = AccuratePackageType.NSZ };
    }
    private sealed class UnavailableHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
    }
    private sealed class UnusedFileOpeningService : IFileOpeningService
    {
        public event OpenedFileChangedHandler OpenedFileChanged { add { } remove { } }
        public NxFile? OpenedFile => throw new InvalidOperationException("Simulation must not access opened files.");
        public Task SafeOpenFile(string path) => throw new InvalidOperationException();
        public void SafeClose() => throw new InvalidOperationException();
    }
}
