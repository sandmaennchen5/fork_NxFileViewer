using System.IO;
using System.Text.Json;
using Emignatik.NxFileViewer.Services.FileRenaming;
using Emignatik.NxFileViewer.Services.FileRenaming.Exceptions;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services.FileRenaming;

public sealed class NamingPresetsTest
{
    private static NamingPresetCatalog Read(NamingPresetCatalog catalog)=>NamingPresets.Read(JsonSerializer.SerializeToUtf8Bytes(catalog));
    [Fact]
    public void EmbeddedDefaultSuppliesNewSettingsWithoutOverwritingSavedPatterns()
    {
        var settings=new Emignatik.NxFileViewer.Settings.AppSettings();
        var preset=NamingPresets.Current;
        Assert.Equal(preset.Application.Default.Pattern,settings.RenamingOptions.ApplicationPattern);
        Assert.Equal(preset.Patch.Default.Pattern,settings.RenamingOptions.PatchPattern);
        Assert.Equal(preset.Addon.Default.Pattern,settings.RenamingOptions.AddonPattern);
        settings.RenamingOptions.ApplicationPattern="my-custom-{TitleId}.{Ext:L}";
        var restored=JsonSerializer.Deserialize<Emignatik.NxFileViewer.Settings.AppSettings>(JsonSerializer.Serialize(settings))!;
        Assert.Equal(settings.RenamingOptions.ApplicationPattern,restored.RenamingOptions.ApplicationPattern);
    }
    [Fact]
    public void MissingSuperSettingGetsIndependentDefaultAndSavedPatternsRoundTrip()
    {
        var old = JsonSerializer.Deserialize<Emignatik.NxFileViewer.Settings.AppSettings>(
            """{"RenamingOptions":{"ApplicationPattern":"old-game-{TitleId}","PatchPattern":"old-update-{TitleId}","AddonPattern":"old-dlc-{TitleId}"}}""")!;
        Assert.Equal("old-game-{TitleId}", old.RenamingOptions.ApplicationPattern);
        Assert.Equal("old-update-{TitleId}", old.RenamingOptions.PatchPattern);
        Assert.Equal("old-dlc-{TitleId}", old.RenamingOptions.AddonPattern);
        Assert.Equal(NamingPresets.Current.Combined.Default.Pattern, old.RenamingOptions.SuperPattern);
        old.RenamingOptions.SuperPattern = "custom-super-{AppId}-{GameCount}.{Ext:L}";
        var restored = JsonSerializer.Deserialize<Emignatik.NxFileViewer.Settings.AppSettings>(JsonSerializer.Serialize(old))!;
        Assert.Equal(old.RenamingOptions.SuperPattern, restored.RenamingOptions.SuperPattern);
        Assert.Equal("old-game-{TitleId}", restored.RenamingOptions.ApplicationPattern);
        Assert.NotEqual(restored.RenamingOptions.SuperPattern, new Emignatik.NxFileViewer.Settings.AppSettings().RenamingOptions.SuperPattern);
    }
    [Fact]
    public void DefaultCanBeChangedInCatalogAndLabelsFallBackToEnglish()
    {
        var original=NamingPresets.Current;
        var catalog=Read(original with{Application=original.Application with{DefaultPreset="offline"}});
        Assert.Equal("offline",catalog.Application.Default.Id);
        Assert.Equal(catalog.Application.Default.Names["de"],catalog.Application.Default.Name("de-DE"));
        Assert.Equal(catalog.Application.Default.Names["en"],catalog.Application.Default.Name("ja-JP"));
    }
    [Theory]
    [InlineData("{Unknown}.{Ext}")]
    [InlineData("{Title:LATER}.{Ext}")]
    [InlineData("{Title")]
    [InlineData("Title}")]
    [InlineData("")]
    public void BrokenPresetPatternsAreRejected(string pattern)
    {
        var catalog=NamingPresets.Current;
        Assert.Throws<InvalidDataException>(()=>Read(catalog with{Application=catalog.Application with{Presets=new[]{catalog.Application.Default with{Pattern=pattern}}}}));
    }
    [Fact]
    public void DlcCannotUseLocalTitleButOtherVariablesAreAvailable()
    {
        Assert.Throws<KeywordNotAllowedException>(()=>NamingPresets.ValidatePattern("{Title}.{Ext}",PatternType.Addon));
        var variables=NamingPresets.AvailableVariables(PatternType.Addon);
        Assert.DoesNotContain("{Title}",variables);
        Assert.Contains("{WTitle}",variables);Assert.Contains("{TitleId}",variables);Assert.Contains("{VerNum}",variables);
        Assert.Contains("{Title}",NamingPresets.AvailableVariables(PatternType.Application));
    }
    [Fact]
    public void DuplicateReservedIdsAndMissingDefaultAreRejected()
    {
        var catalog=NamingPresets.Current;
        Assert.Throws<InvalidDataException>(()=>Read(catalog with{Application=catalog.Application with{DefaultPreset="missing"}}));
        Assert.Throws<InvalidDataException>(()=>Read(catalog with{Application=catalog.Application with{Presets=new[]{catalog.Application.Default,catalog.Application.Default}}}));
        Assert.Throws<InvalidDataException>(()=>Read(catalog with{Application=catalog.Application with{Presets=new[]{catalog.Application.Default with{Id="manual"}}}}));
    }
    [Fact]
    public void MissingMultiPatternGetsDefaultWithoutChangingSavedSuperPattern()
    {
        var old=JsonSerializer.Deserialize<Emignatik.NxFileViewer.Settings.AppSettings>(
            """{"RenamingOptions":{"ApplicationPattern":"single-{TitleId}","SuperPattern":"super-{AppId}"}}""")!;
        Assert.Equal("single-{TitleId}",old.RenamingOptions.ApplicationPattern);
        Assert.Equal("super-{AppId}",old.RenamingOptions.SuperPattern);
        Assert.Equal(NamingPresets.Current.MultiPackage.Default.Pattern,old.RenamingOptions.MultiPattern);
        old.RenamingOptions.MultiPattern="Sammlung-{GameCount}-{DlcCount}.{Ext}";
        var restored=JsonSerializer.Deserialize<Emignatik.NxFileViewer.Settings.AppSettings>(JsonSerializer.Serialize(old))!;
        Assert.Equal(old.RenamingOptions.MultiPattern,restored.RenamingOptions.MultiPattern);
        Assert.Equal("super-{AppId}",restored.RenamingOptions.SuperPattern);
    }

}
