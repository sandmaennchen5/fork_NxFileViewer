using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia;
namespace NxFileViewer.Avalonia;
public partial class MainWindow
{
    private IReadOnlyDictionary<string, string> _namingPatterns = new Dictionary<string, string>();
    private readonly Dictionary<string, TextBox> _patternEditors = new();
    private bool _verifyOnOpen;
    private TextBox _invalidReplacementEditor = null!;
    private TextBox _whiteSpaceReplacementEditor = null!;
    private CheckBox _replaceWhiteSpaceEditor = null!;
    private void LoadNamingSettings(ViewerPreferences preferences)
    {
        _namingPatterns = preferences.NamingPatterns ?? new Dictionary<string, string>();
        _patternEditors.Clear(); NamingSettingsHost.Children.Clear();
        using var stream = typeof(MainWindow).Assembly.GetManifestResourceStream("Viewer.NamingPresets.json")!;
        using var catalog = JsonDocument.Parse(stream);
        var language = _text.CultureName.Split('-')[0];
        foreach (var section in new[] { "Application", "Patch", "Addon", "Combined", "MultiPackage" })
        {
            var label = section switch { "Application" => _text.RenamingTool_ApplicationPattern, "Patch" => _text.RenamingTool_PatchPattern, "Addon" => _text.RenamingTool_AddonPattern, "Combined" => _text.RenamingTool_SuperPattern, _ => _text.RenamingTool_MultiPattern };
            var choices = catalog.RootElement.GetProperty(section).GetProperty("Presets").EnumerateArray()
                .Select(preset => { var names = preset.GetProperty("Names"); return new RenamePresetChoiceRow(names.TryGetProperty(language, out var name) ? name.GetString()! : names.GetProperty("en").GetString()!, preset.GetProperty("Pattern").GetString()!); }).ToArray();
            var selected = _namingPatterns.TryGetValue(section, out var pattern) ? pattern : choices.FirstOrDefault()?.Pattern ?? "";
            var title = new TextBlock { Text = label, FontWeight = global::Avalonia.Media.FontWeight.Bold };
            title.Classes.Add("overview-label"); NamingSettingsHost.Children.Add(title);
            var tip = section switch { "Application" => _text.RenamingTool_ToolTip_BasePattern, "Patch" => _text.RenamingTool_ToolTip_PatchPattern, "Addon" => _text.RenamingTool_ToolTip_AddonPattern, "Combined" => _text.RenamingTool_ToolTip_SuperPattern, _ => _text.RenamingTool_ToolTip_MultiPattern };
            ToolTip.SetTip(title, tip);
            var editor = new TextBox { Text = selected, FontFamily = new global::Avalonia.Media.FontFamily("Consolas") }; ToolTip.SetTip(editor, tip);
            var combo = new ComboBox { ItemsSource = choices, HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch };
            ToolTip.SetTip(combo, tip);
            combo.SelectedItem = choices.FirstOrDefault(choice => choice.Pattern == selected);
            combo.SelectionChanged += (_, _) => { if (combo.SelectedItem is RenamePresetChoiceRow choice) editor.Text = choice.Pattern; };
            var selector = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 4 };
            selector.Children.Add(combo);
            var help = new Button { Content = "?", Padding = new Thickness(6, 0), MinWidth = 24 };
            ToolTip.SetTip(help, tip); Grid.SetColumn(help, 1); selector.Children.Add(help);
            help.Click += async (_, _) => {
                var edited = await NamingPatternWindow.Edit(this, section, label, editor.Text ?? "", _text);
                if (edited != null) editor.Text = edited;
            };
            NamingSettingsHost.Children.Add(selector); NamingSettingsHost.Children.Add(editor); _patternEditors[section] = editor;
            if (section is "Combined" or "MultiPackage") NamingSettingsHost.Children.Add(new TextBlock { Text = tip, TextWrapping = global::Avalonia.Media.TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 6) });
        }
        NamingSettingsHost.Children.Add(new TextBlock { Text = _text.RenamingTool_Miscellaneous, FontWeight = global::Avalonia.Media.FontWeight.Bold });
        NamingSettingsHost.Children.Add(new TextBlock { Text = _text.RenamingTool_InvalidWindowsCharReplacement });
        _invalidReplacementEditor = new TextBox { Text = preferences.InvalidFileNameCharsReplacement, Width = 160, HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Left };
        NamingSettingsHost.Children.Add(_invalidReplacementEditor);
        _replaceWhiteSpaceEditor = new CheckBox { Content = _text.RenamingTool_ReplaceWhiteSpaceChars, IsChecked = preferences.ReplaceWhiteSpaceChars };
        NamingSettingsHost.Children.Add(_replaceWhiteSpaceEditor);
        NamingSettingsHost.Children.Add(new TextBlock { Text = _text.RenamingTool_ReplaceWhiteSpaceCharsWith });
        _whiteSpaceReplacementEditor = new TextBox { Text = preferences.WhiteSpaceCharsReplacement, Width = 160, HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Left };
        NamingSettingsHost.Children.Add(_whiteSpaceReplacementEditor);
        _whiteSpaceReplacementEditor.IsEnabled = _replaceWhiteSpaceEditor.IsChecked == true;
        _replaceWhiteSpaceEditor.IsCheckedChanged += (_, _) => _whiteSpaceReplacementEditor.IsEnabled = _replaceWhiteSpaceEditor.IsChecked == true;
    }
}
