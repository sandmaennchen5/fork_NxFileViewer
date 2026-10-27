using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using Emignatik.NxFileViewer.Services.FileRenaming;
using Emignatik.NxFileViewer.Services.FileRenaming.Exceptions;
using Emignatik.NxFileViewer.Views.CustomControls;
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
    private static void Sta(Action action)
    {
        Exception? error=null;
        var thread=new Thread(()=>{try{action();}catch(Exception ex){error=ex;}});
        thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();
        if(error!=null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
    }
    private static ComboBox Choices(NamingPatternSelector control)=>(ComboBox)control.FindName("Choices");
    [Fact]
    public void ExistingManualPatternDoesNotOpenDialogAndCancelRestoresSelection()=>Sta(()=>{
        var control=new NamingPatternSelector();int prompts=0;
        control.PromptPattern=(_,_)=>{prompts++;return null;};
        control.Pattern="my-{TitleId}.{Ext}";
        Assert.Equal(0,prompts);Assert.Null(((NamingPatternSelector.Choice)Choices(control).SelectedItem).Pattern);
        control.Pattern=NamingPresets.Current.Application.Default.Pattern;
        Choices(control).SelectedIndex=Choices(control).Items.Count-1;
        Assert.Equal(1,prompts);Assert.Equal(NamingPresets.Current.Application.Default.Pattern,control.Pattern);
        Assert.NotNull(((NamingPatternSelector.Choice)Choices(control).SelectedItem).Pattern);
    });
    [Fact]
    public void SuperSelectorEditsOnlySuperPatternAndUsesSuperDialog()=>Sta(()=>{
        var options=new Emignatik.NxFileViewer.Settings.AppSettings().RenamingOptions;
        var application=options.ApplicationPattern;var patch=options.PatchPattern;var addon=options.AddonPattern;
        var control=new NamingPatternSelector { Kind=PatternType.Super };
        BindingOperations.SetBinding(control,NamingPatternSelector.PatternProperty,
            new Binding(nameof(options.SuperPattern)){Source=options,Mode=BindingMode.TwoWay,UpdateSourceTrigger=UpdateSourceTrigger.PropertyChanged});
        var offline=NamingPresets.Current.Combined.Presets.Single(p=>p.Id=="offline").Pattern;
        Choices(control).SelectedItem=Choices(control).Items.Cast<NamingPatternSelector.Choice>().First(c=>c.Pattern==offline);
        Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.DataBind);
        Assert.Equal(offline,options.SuperPattern);
        control.PromptPattern=(kind,pattern)=>{Assert.Equal(PatternType.Super,kind);Assert.Equal(offline,pattern);return "SUPER-{AppId}.{Ext}";};
        Choices(control).SelectedIndex=Choices(control).Items.Count-1;
        Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.DataBind);
        Assert.Equal("SUPER-{AppId}.{Ext}",options.SuperPattern);
        Assert.Equal(application,options.ApplicationPattern);
        Assert.Equal(patch,options.PatchPattern);
        Assert.Equal(addon,options.AddonPattern);
        Assert.Contains("{GameCount}",NamingPresets.AvailableVariables(PatternType.Super));
    });
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

    [Fact]
    public void MultiSelectorIsIndependentAndOffersOnlyCountVariables()=>Sta(()=>{
        var options=new Emignatik.NxFileViewer.Settings.AppSettings().RenamingOptions;
        var saved=options.SuperPattern;var single=options.ApplicationPattern;
        var control=new NamingPatternSelector { Kind=PatternType.Multi };
        BindingOperations.SetBinding(control,NamingPatternSelector.PatternProperty,
            new Binding(nameof(options.MultiPattern)){Source=options,Mode=BindingMode.TwoWay,UpdateSourceTrigger=UpdateSourceTrigger.PropertyChanged});
        control.PromptPattern=(kind,_)=>{Assert.Equal(PatternType.Multi,kind);return "Sammlung-{GameCount}G-{DlcCount}D.{Ext}";};
        Choices(control).SelectedIndex=Choices(control).Items.Count-1;
        Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.DataBind);
        Assert.Equal("Sammlung-{GameCount}G-{DlcCount}D.{Ext}",options.MultiPattern);
        Assert.Equal(saved,options.SuperPattern);Assert.Equal(single,options.ApplicationPattern);
        Assert.Contains("{GameCount}",NamingPresets.AvailableVariables(PatternType.Multi));
        Assert.DoesNotContain("{AppId}",NamingPresets.AvailableVariables(PatternType.Multi));
        Assert.DoesNotContain("{PackageVerNum}",NamingPresets.AvailableVariables(PatternType.Multi));
    });
    [Fact]
    public void SectionsHaveIndependentDefaultsAndSelections()=>Sta(()=>{
        var catalog=NamingPresets.Current;
        var changed=Read(catalog with{Patch=catalog.Patch with{DefaultPreset="offline"}});
        Assert.Equal("offline",changed.Patch.Default.Id);
        Assert.Equal(catalog.Application.Default.Id,changed.Application.Default.Id);
        foreach(var kind in Enum.GetValues<PatternType>()){
            var control=new NamingPatternSelector{Kind=kind};
            var expected=catalog.Section(kind).Presets.Select(p=>p.Pattern).ToArray();
            Assert.Equal(expected,Choices(control).Items.Cast<NamingPatternSelector.Choice>().Where(c=>c.Pattern!=null).Select(c=>c.Pattern).ToArray());
        }
        Assert.Throws<InvalidDataException>(()=>Read(catalog with{Combined=null!}));
    });
    private sealed class Draft:INotifyPropertyChanged
    {
        private string _pattern=NamingPresets.Current.Application.Default.Pattern;
        public string Pattern{get=>_pattern;set{_pattern=value;PropertyChanged?.Invoke(this,new(nameof(Pattern)));}}
        public event PropertyChangedEventHandler? PropertyChanged;
    }
    [Fact]
    public void PresetAndManualEditsKeepTwoWayBindingAndDraftIsolation()=>Sta(()=>{
        var saved=NamingPresets.Current.Application.Default.Pattern;var draft=new Draft();var control=new NamingPatternSelector();
        BindingOperations.SetBinding(control,NamingPatternSelector.PatternProperty,new Binding(nameof(Draft.Pattern)){
            Source=draft,Mode=BindingMode.TwoWay,UpdateSourceTrigger=UpdateSourceTrigger.PropertyChanged});
        var offline=NamingPresets.Current.Application.Presets.Single(p=>p.Id=="offline").Pattern;
        Choices(control).SelectedItem=Choices(control).Items.Cast<NamingPatternSelector.Choice>().First(c=>c.Pattern==offline);
        Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.DataBind);
        Assert.Equal(offline,draft.Pattern);Assert.NotEqual(saved,draft.Pattern);
        control.PromptPattern=(_,_)=>"custom-{TitleId:U}.{Ext:L}";
        Choices(control).SelectedIndex=Choices(control).Items.Count-1;
        Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.DataBind);
        Assert.Equal("custom-{TitleId:U}.{Ext:L}",draft.Pattern);
        Assert.True(BindingOperations.IsDataBound(control,NamingPatternSelector.PatternProperty));
        draft.Pattern=saved;
        Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.DataBind);
        Assert.Equal(saved,control.Pattern);Assert.NotNull(((NamingPatternSelector.Choice)Choices(control).SelectedItem).Pattern);
    });
    [Fact]
    public void DlcSelectorUsesDlcPresetAndShowsOwnVariables()=>Sta(()=>{
        var control=new NamingPatternSelector{Kind=PatternType.Addon,Pattern=NamingPresets.Current.Addon.Default.Pattern};
        var offline=NamingPresets.Current.Addon.Presets.Single(p=>p.Id=="offline").Pattern;
        Choices(control).SelectedItem=Choices(control).Items.Cast<NamingPatternSelector.Choice>().First(c=>c.Pattern==offline);
        Assert.Equal(offline,control.Pattern);
        control.PromptPattern=(kind,pattern)=>{Assert.Equal(PatternType.Addon,kind);Assert.Equal(offline,pattern);return "DLC-{TitleId}.{Ext}";};
        Choices(control).SelectedIndex=Choices(control).Items.Count-1;
        Assert.Equal("DLC-{TitleId}.{Ext}",control.Pattern);
    });
}
