using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Emignatik.NxFileViewer.Localization;
using Emignatik.NxFileViewer.Services.FileRenaming;
using Emignatik.NxFileViewer.Services.FileRenaming.Exceptions;
using Emignatik.NxFileViewer.Styling.Theme;
using Emignatik.NxFileViewer.Views.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace Emignatik.NxFileViewer.Views.CustomControls;

public partial class NamingPatternSelector : UserControl
{
    public sealed record Choice(string Name,string? Pattern);
    public static readonly DependencyProperty PatternProperty=DependencyProperty.Register(nameof(Pattern),typeof(string),typeof(NamingPatternSelector),
        new FrameworkPropertyMetadata("",FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,Changed));
    public static readonly DependencyProperty KindProperty=DependencyProperty.Register(nameof(Kind),typeof(PatternType),typeof(NamingPatternSelector),
        new PropertyMetadata(PatternType.Application,Changed));
    public string Pattern {get=>(string)GetValue(PatternProperty);set=>SetValue(PatternProperty,value);}
    public PatternType Kind {get=>(PatternType)GetValue(KindProperty);set=>SetValue(KindProperty,value);}
    private bool _syncing;
    // A replaceable prompt also keeps selection/cancel behavior testable without opening modal windows.
    public Func<PatternType,string,string?> PromptPattern {get;set;}
    public NamingPatternSelector()
    {
        InitializeComponent();
        PromptPattern=Prompt;
        Loaded+=(_,_)=>{LocalizationManager.Instance.PropertyChanged+=LanguageChanged;Synchronize();};
        Unloaded+=(_,_)=>LocalizationManager.Instance.PropertyChanged-=LanguageChanged;
        Synchronize();
    }
    private static void Changed(DependencyObject sender,DependencyPropertyChangedEventArgs e)=>((NamingPatternSelector)sender).Synchronize();
    private void LanguageChanged(object? sender,PropertyChangedEventArgs e)=>Synchronize();
    private void Synchronize()
    {
        if(Choices==null)return;
        _syncing=true;
        try {
            var manager=LocalizationManager.Instance;
            var choices=NamingPresets.Current.Section(Kind).Presets.Select(p=>new Choice(p.Name(manager.Current.CultureName),p.Pattern))
                .Append(new Choice(manager.Current.Keys.Naming_Manual,null)).ToArray();
            Choices.ItemsSource=choices;
            Choices.SelectedItem=choices.FirstOrDefault(choice=>choice.Pattern==Pattern)??choices[^1];
            EditButton.Visibility=((Choice)Choices.SelectedItem).Pattern==null?Visibility.Visible:Visibility.Collapsed;
        }
        finally {_syncing=false;}
    }
    private void Choose(object sender,SelectionChangedEventArgs e)
    {
        if(_syncing||Choices.SelectedItem is not Choice choice)return;
        if(choice.Pattern==null)Edit(sender,e);
        else {SetCurrentValue(PatternProperty,choice.Pattern);Synchronize();}
    }
    private void Edit(object sender,RoutedEventArgs e)
    {
        var result=PromptPattern(Kind,Pattern??"");
        if(result!=null) {NamingPresets.ValidatePattern(result,Kind);SetCurrentValue(PatternProperty,result);}
        Synchronize();
    }
    private string? Prompt(PatternType kind,string pattern)
    {
        var dialog=new NamingPatternDialog(kind,pattern){Owner=Window.GetWindow(this)};
        App.ServiceProvider.GetRequiredService<IThemeService>().RegisterWindow(dialog);
        return dialog.ShowDialog()==true?dialog.Pattern:null;
    }
    private void ShowVariables(object sender,RoutedEventArgs e)
    {
        var keys=LocalizationManager.Instance.Current.Keys;
        ThemedDialog.Notice(keys.Naming_AllowedVariables+Environment.NewLine+NamingPresets.AvailableVariables(Kind)+Environment.NewLine+Environment.NewLine+
            (Kind==PatternType.Multi?keys.RenamingTool_ToolTip_MultiPattern:keys.RenamingTool_ToolTip_Patterns),keys.Naming_Variables,MessageBoxImage.Information);
    }
}
