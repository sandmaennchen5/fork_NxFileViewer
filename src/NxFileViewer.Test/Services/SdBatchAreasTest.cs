using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Emignatik.NxFileViewer.FileLoading;
using Emignatik.NxFileViewer.Settings;
using Xunit;
namespace Emignatik.NxFileViewer.Test.Services;
public sealed class SdBatchAreasTest : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "SdAreas-" + Guid.NewGuid().ToString("N"));
    public SdBatchAreasTest() {
        foreach (var folder in new[] { "switch", "JKSV", "Nintendo", "emuMMC", "tools", "other" }) {
            Directory.CreateDirectory(Path.Combine(_root, folder, "nested"));
            File.WriteAllText(Path.Combine(_root, folder, "nested", "file.nsp"), "test");
        }
        File.WriteAllText(Path.Combine(_root, "hbmenu.nro"), "test");
        File.WriteAllText(Path.Combine(_root, "other.nro"), "test");
    }
    [Fact]
    public void SelectedRootAreasExcludeOtherFoldersAndPreserveExplicitSubfolderScanning() {
        var areas = new SdBatchAreas(true, true, false, false, true, false);
        Assert.True(areas.AppliesTo(_root));
        var files = areas.EnumerateFiles(_root, SearchOption.AllDirectories).ToArray();
        Assert.Equal(2, files.Length);
        Assert.Contains(Path.Combine(_root, "hbmenu.nro"), files);
        Assert.Contains(Path.Combine(_root, "switch", "nested", "file.nsp"), files);
        Assert.False(areas.IncludesPath(_root, Path.Combine(_root, "switch-other", "file.nsp")));
        Assert.False(areas.IncludesPath(_root, Path.Combine(_root, "Nintendo", "Contents")));
        Assert.Single(areas.EnumerateFiles(Path.Combine(_root, "JKSV"), SearchOption.AllDirectories));
        Assert.Single(areas.EnumerateFiles(_root, SearchOption.TopDirectoryOnly));
        Assert.Empty((areas with { Switch = false, Hbmenu = false }).EnumerateFiles(_root, SearchOption.AllDirectories));
    }
    [Fact]
    public void DisabledFilterPreservesOrdinaryScanAndDefaultsSurviveOldSettings() {
        var settings = JsonSerializer.Deserialize<AppSettings>("{}")!;
        Assert.False(settings.SdBatchSelectedAreasOnly);
        Assert.Equal(8, SdBatchAreas.From(settings).EnumerateFiles(_root, SearchOption.AllDirectories).Count());
        Assert.Equal(new SdBatchAreas(false, true, true, true, true, true), SdBatchAreas.From(settings));
        settings.SdBatchSelectedAreasOnly = true; settings.SdBatchNintendo = false;
        var saved = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(settings))!;
        Assert.Equal(SdBatchAreas.From(settings), SdBatchAreas.From(saved));
    }
    [Fact]
    public void CustomPathsSupportNestedFoldersAndIndividualFilesWithoutAdjacentFolders() {
        var areas = new SdBatchAreas(true, false, false, false, false, false) {
            CustomPaths = "/other/nested\r\n/other.nro\n/OTHER/nested\n/missing"
        };
        var files = areas.EnumerateFiles(_root, SearchOption.AllDirectories).ToArray();
        Assert.Equal(2, files.Length);
        Assert.Contains(Path.Combine(_root, "other.nro"), files);
        Assert.True(areas.AllowsTraversal("other", true));
        Assert.False(areas.AllowsTraversal("other", false));
        Assert.False(areas.AllowsTraversal("other/nested-other", true));
        Assert.False(areas.IncludesRelative("other/ignored.nsp"));
        var settings = new AppSettings { SdBatchSelectedAreasOnly = true, SdBatchCustomPaths = areas.CustomPaths };
        Assert.Equal(areas.CustomPaths, SdBatchAreas.From(JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(settings))!).CustomPaths);
    }
    [Theory]
    [InlineData("../outside")]
    [InlineData("folder/../outside")]
    [InlineData("C:/games")]
    [InlineData("//server/games")]
    [InlineData("folder/*.nsp")]
    [InlineData("folder/.. /outside")]
    public void RejectsCustomPathsOutsideSdRoot(string custom) {
        var areas = new SdBatchAreas(true, false, false, false, false, false) { CustomPaths = custom };
        Assert.Throws<InvalidDataException>(() => areas.EnumerateFiles(_root, SearchOption.AllDirectories).ToArray());
    }
    [Fact]
    public void SourceChoiceCanBypassDetectionOrForceSdFilteringWithoutMarkers() {
        var areas = new SdBatchAreas(true, false, false, false, true, false);
        Assert.Single(areas.EnumerateFiles(_root, SearchOption.AllDirectories));
        Assert.Equal(8, (areas with { SourceMode = SdBatchSourceMode.Folder }).EnumerateFiles(_root, SearchOption.AllDirectories).Count());
        var plain = Path.Combine(_root, "plain");
        Directory.CreateDirectory(Path.Combine(plain, "games")); Directory.CreateDirectory(Path.Combine(plain, "other"));
        File.WriteAllText(Path.Combine(plain, "games", "one.nsp"), "test"); File.WriteAllText(Path.Combine(plain, "other", "two.nsp"), "test");
        var forced = areas with { Hbmenu = false, SourceMode = SdBatchSourceMode.SwitchSd, CustomPaths = "/games" };
        Assert.Single(forced.EnumerateFiles(plain, SearchOption.AllDirectories));
        Assert.Equal(2, (forced with { SourceMode = SdBatchSourceMode.Auto }).EnumerateFiles(plain, SearchOption.AllDirectories).Count());
        var settings = new AppSettings { SdBatchSourceMode = SdBatchSourceMode.SwitchSd };
        Assert.Equal(SdBatchSourceMode.SwitchSd, JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(settings))!.SdBatchSourceMode);
    }
    [Theory]
    [InlineData("SdBatchDbiRootSaves", "DBIsaves")]
    [InlineData("SdBatchDumps", "dumps")]
    [InlineData("SdBatchNxdtRwPoc", "nxdt_rw_poc")]
    public void ToolPresetsAreIndependentSavedAndDoNotIncludeAdjacentFolders(string property, string path) {
        var settings = new AppSettings { SdBatchSelectedAreasOnly = true, SdBatchSourceMode = SdBatchSourceMode.SwitchSd,
            SdBatchSwitch = false, SdBatchNintendo = false, SdBatchEmuMmc = false, SdBatchHbmenu = false, SdBatchTools = false };
        typeof(AppSettings).GetProperty(property)!.SetValue(settings, true);
        var directory = Path.Combine(_root, path.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(directory); var file = Path.Combine(directory, "backup.zip"); File.WriteAllText(file, "test");
        var saved = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(settings))!;
        var areas = SdBatchAreas.From(saved);
        Assert.Single(areas.EnumerateFiles(_root, SearchOption.AllDirectories));
        Assert.True(areas.IncludesRelative(path + "/backup.zip"));
        Assert.False(areas.IncludesRelative(path + "-other/backup.zip"));
        Assert.False(areas.IncludesRelative("switch/other/backup.zip"));
        Assert.True(areas.AllowsTraversal(path.Split('/')[0], true));
        if (path != "DBIsaves") Assert.False(areas.IncludesRelative("DBIsaves/backup.zip"));
    }
    public void Dispose() => Directory.Delete(_root, true);
}
