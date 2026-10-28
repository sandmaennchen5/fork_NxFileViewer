using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Emignatik.NxFileViewer.FileLoading.QuickFileInfoLoading;
using Emignatik.NxFileViewer.Models;
using Emignatik.NxFileViewer.Services.FileOpening;
using Emignatik.NxFileViewer.Services.FileRenaming;
using Emignatik.NxFileViewer.Services.FileRenaming.Exceptions;
using Emignatik.NxFileViewer.Services.FileRenaming.Models;
using Emignatik.NxFileViewer.Services.OnlineServices;
using LibHac.Ncm;
using LibHac.Ns;
using LibHac.Tools.Ncm;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services.FileRenaming;

public class SuperPackageRenamingTest
{
    private const ulong AppId = 0x0100F43008C44000;
    private static Content Make(ContentMetaType type, ulong id, uint version, ulong appId = AppId)
    {
        var bytes = new byte[0x80];
        BinaryPrimitives.WriteUInt64LittleEndian(bytes, id);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), version);
        bytes[0xC] = (byte)type;
        bytes[0xE] = type == ContentMetaType.Patch ? (byte)0x18 : (byte)0x10;
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(0x20),
            type == ContentMetaType.Application ? id + 0x800 : appId);
        using var stream = new MemoryStream(bytes);
        var content = new Content(new Cnmt(stream));
        var nacp = new byte[0x4000];
        Encoding.UTF8.GetBytes("Pokemon Legends Z-A").CopyTo(nacp, 0);
        nacp[0x302C] = 1;
        content.NacpData = new NacpData(MemoryMarshal.Read<ApplicationControlProperty>(nacp));
        return content;
    }

    private static Task<RenamingResult> Preview(params Content[] contents) => Preview(AccuratePackageType.NSZ, contents);
    private static Task<RenamingResult> Preview(AccuratePackageType type, params Content[] contents)
    {
        var preset = NamingPresets.Current;
        var parser = new NamingPatternsParser();
        var settings = new NamingSettings {
            ApplicationPattern = parser.ParseApplicationPattern("GAME-{TitleId}.{Ext:L}"),
            PatchPattern = parser.ParsePatchPattern("UPDATE-{TitleId}.{Ext:L}"),
            AddonPattern = parser.ParseAddonPattern("DLC-{TitleId}.{Ext:L}"),
            SuperPattern = parser.ParseSuperPattern(preset.Combined.Default.Pattern),
            MultiPattern = parser.ParseMultiPattern(preset.MultiPackage.Default.Pattern)
        };
        var service = new FileRenamerService(new Loader(type, contents), new Offline(), new UnusedOpening());
        return service.RenameFileAsync(Path.Combine(Path.GetTempPath(), "super-original.nsz"),
            false, settings, true, null, CancellationToken.None);
    }

    [Fact]
    public async Task ProducesRequestedSuperNspNameAndIgnoresHigherDlcVersion()
    {
        var result = await Preview(
            Make(ContentMetaType.AddOnContent, AppId + 0x1002, 999999),
            Make(ContentMetaType.Patch, AppId + 0x800, 393216),
            Make(ContentMetaType.Application, AppId, 0));
        Assert.Null(result.Exception);
        Assert.Equal("Pokemon Legends Z-A [0100F43008C44000] [v393216] (1G+1U+1D).nsz", result.NewFileName);
        Assert.True(result.IsSimulation);
    }

    [Fact]
    public async Task CountsAllContentsAndUsesHighestUpdateRegardlessOfOrder()
    {
        var result = await Preview(Make(ContentMetaType.Patch, AppId + 0x800, 65536),
            Make(ContentMetaType.AddOnContent, AppId + 0x1001, 0),
            Make(ContentMetaType.Application, AppId, 0),
            Make(ContentMetaType.Patch, AppId + 0x800, 393216),
            Make(ContentMetaType.AddOnContent, AppId + 0x1002, 0));
        Assert.Null(result.Exception);
        Assert.Equal("Pokemon Legends Z-A [0100F43008C44000] [v393216] (1G+2U+2D).nsz", result.NewFileName);
    }

    [Theory]
    [InlineData(AccuratePackageType.NSP, "nsp")]
    [InlineData(AccuratePackageType.XCI, "xci")]
    [InlineData(AccuratePackageType.XCZ, "xcz")]
    public async Task KeepsActualPackageExtension(AccuratePackageType type, string extension)
    {
        var result = await Preview(type, Make(ContentMetaType.Application, AppId, 0),
            Make(ContentMetaType.Patch, AppId + 0x800, 393216));
        Assert.Null(result.Exception);
        Assert.Equal("Pokemon Legends Z-A [0100F43008C44000] [v393216] (1G+1U+0D)." + extension, result.NewFileName);
    }

    [Fact]
    public async Task SupportsUpdateAndDlcWithoutBaseGame()
    {
        var result = await Preview(Make(ContentMetaType.Patch, AppId + 0x800, 393216),
            Make(ContentMetaType.AddOnContent, AppId + 0x1002, 0));
        Assert.Null(result.Exception);
        Assert.Equal("Pokemon Legends Z-A [0100F43008C44000] [v393216] (0G+1U+1D).nsz", result.NewFileName);
    }

    [Fact]
    public async Task SingleContentUsesItsOwnPatternInsteadOfSuperPattern()
    {
        var result = await Preview(Make(ContentMetaType.Application, AppId, 0));
        Assert.Null(result.Exception);
        Assert.Equal("GAME-0100F43008C44000.nsz", result.NewFileName);
    }

    [Fact]
    public async Task NamesDifferentGamesWithGenericMultiPattern()
    {
        var result = await Preview(Make(ContentMetaType.Application, AppId, 0),
            Make(ContentMetaType.Application, AppId + 0x2000, 0));
        Assert.Null(result.Exception);
        Assert.Equal("MultiPaket (2G+0U+0D).nsz", result.NewFileName);
    }

    [Theory]
    [InlineData(ContentMetaType.Patch, 0x0100F43008C44800UL, "UPDATE-0100F43008C44800.nsz")]
    [InlineData(ContentMetaType.AddOnContent, 0x0100F43008C45002UL, "DLC-0100F43008C45002.nsz")]
    public async Task StandaloneUpdateAndDlcUseTheirOwnPatterns(ContentMetaType type, ulong id, string expected)
    {
        var result = await Preview(Make(type, id, 393216));
        Assert.Null(result.Exception);
        Assert.Equal(expected, result.NewFileName);
    }
    [Fact]
    public async Task NamesRequestedTwoGamesThreeUpdatesFiveDlcBundle()
    {
        const ulong other = AppId + 0x2000;
        var contents = new[] {
            Make(ContentMetaType.Application, AppId, 0),
            Make(ContentMetaType.Application, other, 0),
            Make(ContentMetaType.Patch, AppId + 0x800, 65536),
            Make(ContentMetaType.Patch, AppId + 0x800, 393216),
            Make(ContentMetaType.Patch, other + 0x800, 131072, other),
            Make(ContentMetaType.AddOnContent, AppId + 0x1001, 0),
            Make(ContentMetaType.AddOnContent, AppId + 0x1002, 0),
            Make(ContentMetaType.AddOnContent, other + 0x1001, 0, other),
            Make(ContentMetaType.AddOnContent, other + 0x1002, 0, other),
            Make(ContentMetaType.AddOnContent, other + 0x1003, 0, other)
        };
        var result = await Preview(contents);
        Assert.Null(result.Exception);
        Assert.Equal("MultiPaket (2G+3U+5D).nsz", result.NewFileName);
        var reversed = await Preview(AccuratePackageType.XCZ, contents.Reverse().ToArray());
        Assert.Null(reversed.Exception);
        Assert.Equal("MultiPaket (2G+3U+5D).xcz", reversed.NewFileName);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SupportsDlcCollectionsWithOrWithoutMixedApplicationIds(bool mixed)
    {
        var other = mixed ? AppId + 0x2000 : AppId;
        var result = await Preview(Make(ContentMetaType.AddOnContent, AppId + 0x1001, 0),
            Make(ContentMetaType.AddOnContent, other + 0x1002, 0, other));
        Assert.Null(result.Exception);
        Assert.Equal("MultiPaket (0G+0U+2D).nsz", result.NewFileName);
    }

    [Theory]
    [InlineData("{TitleId}")]
    [InlineData("{AppId}")]
    [InlineData("{WAppTitle}")]
    [InlineData("{Title}")]
    [InlineData("{VerNum}")]
    [InlineData("{PackageVerNum}")]
    public void MultiPatternRejectsAmbiguousSingleTitleMetadata(string pattern)
    {
        Assert.Throws<KeywordNotAllowedException>(() => new NamingPatternsParser().ParseMultiPattern(pattern));
    }
    private sealed class Loader(AccuratePackageType type, Content[] contents) : IPackageInfoLoader
    {
        public PackageInfo GetPackageInfo(string path) => new() { AccuratePackageType = type, Contents = contents.ToList() };
    }
    private sealed class Offline : ICachedOnlineTitleInfoService
    {
        public bool IsEnabled { get; set; }
        public void ClearCache() { }
        public Task<IOnlineTitleInfo?> GetTitleInfoAsync(string titleId) => Task.FromResult<IOnlineTitleInfo?>(null);
    }
    private sealed class UnusedOpening : IFileOpeningService
    {
        public event OpenedFileChangedHandler OpenedFileChanged { add { } remove { } }
        public NxFile? OpenedFile => throw new InvalidOperationException();
        public Task SafeOpenFile(string path) => throw new InvalidOperationException();
        public void SafeClose() => throw new InvalidOperationException();
    }
}