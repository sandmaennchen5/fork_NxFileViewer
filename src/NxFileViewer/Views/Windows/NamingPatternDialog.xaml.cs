using System;
using System.Windows;
using Emignatik.NxFileViewer.Localization;
using Emignatik.NxFileViewer.Services.FileRenaming;
using Emignatik.NxFileViewer.Services.FileRenaming.Exceptions;

namespace Emignatik.NxFileViewer.Views.Windows;

public partial class NamingPatternDialog : Window
{
    private readonly PatternType _type;
    public string Pattern => PatternInput.Text;
    public NamingPatternDialog() : this(PatternType.Application, "") { }
    public NamingPatternDialog(PatternType type,string pattern)
    {
        InitializeComponent();
        _type=type;
        var keys=LocalizationManager.Instance.Current.Keys;
        PatternLabel.Text=type switch { PatternType.Application=>keys.RenamingTool_ApplicationPattern,PatternType.Patch=>keys.RenamingTool_PatchPattern,PatternType.Addon=>keys.RenamingTool_AddonPattern,PatternType.Super=>keys.RenamingTool_SuperPattern,_=>keys.RenamingTool_MultiPattern };
        Title=keys.Naming_Manual+" – "+PatternLabel.Text;
        AllowedVariables.Text=keys.Naming_AllowedVariables+Environment.NewLine+NamingPresets.AvailableVariables(type);
        if(type==PatternType.Multi)PatternHelp.Text=keys.RenamingTool_ToolTip_MultiPattern;
        PatternInput.Text=pattern;
        MaxWidth=SystemParameters.WorkArea.Width; MaxHeight=SystemParameters.WorkArea.Height;
        Loaded+=(_,_)=>{PatternInput.Focus();PatternInput.SelectAll();};
    }
    private void Accept(object sender,RoutedEventArgs e)
    {
        try { NamingPresets.ValidatePattern(Pattern,_type);DialogResult=true; }
        catch(Exception ex) {ValidationError.Text=ex.Message;PatternInput.Focus();}
    }
}
