using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
namespace NxFileViewer.Avalonia;
public partial class MainWindow
{
    private readonly Dictionary<string, ComboBox> _pluginLogEditors = new();
    private readonly Dictionary<string, ComboBox> _outputEditors = new();
    private TextBox _customOutputEditor = null!;
    private void LoadPluginGeneralSettings(ViewerPreferences preferences)
    {
        PluginGeneralHost.Children.Clear(); _pluginLogEditors.Clear(); _outputEditors.Clear();
        var logs = new StackPanel { Spacing = 4, Margin = new global::Avalonia.Thickness(8) }; logs.Children.Add(new TextBlock { Text = _text.PluginLog_Title, FontWeight = global::Avalonia.Media.FontWeight.Bold });
        logs.Children.Add(new TextBlock { Text = _text.PluginLog_Tip, TextWrapping = global::Avalonia.Media.TextWrapping.Wrap });
        foreach (var (key, label) in new[] { ("NSZ", "nicoboss/nsz"), ("NAND", "NxNandManager"), ("MTP", "MTP"), ("PACKAGE", "NSC_BUILDER / nscb_rust") })
        {
            logs.Children.Add(new TextBlock { Text = label }); var choice = new ComboBox { HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch };
            foreach (var level in new[] { "", "TRACE", "DEBUG", "INFO", "WARN", "ERROR", "CRITICAL", "OFF" }) choice.Items.Add(new ComboBoxItem { Tag = level, Content = level == "" ? _text.PluginLog_Inherit : level == "OFF" ? _text.PluginLog_Off : level });
            SelectChoice(choice, preferences.PluginLogLevels?.GetValueOrDefault(key) ?? ""); logs.Children.Add(choice); _pluginLogEditors[key] = choice;
        }
        var logBorder = new Border { Child = logs }; logBorder.Classes.Add("overview-group"); PluginGeneralHost.Children.Add(logBorder);
        var outputs = new StackPanel { Spacing = 4, Margin = new global::Avalonia.Thickness(8) }; outputs.Children.Add(new TextBlock { Text = _text.Output_Defaults, FontWeight = global::Avalonia.Media.FontWeight.Bold });
        foreach (var (key, label) in new[] { ("Merge", _text.Output_Merge), ("Split", _text.Output_Split), ("Compress", _text.Output_Compression), ("Decompress", _text.Output_Decompression) })
        {
            outputs.Children.Add(new TextBlock { Text = label }); var choice = new ComboBox { ItemsSource = new[] { _text.Output_NextToSource, _text.Output_ActionSubdirectory, _text.Output_Custom }, SelectedIndex = preferences.OutputModes?.GetValueOrDefault(key, 1) ?? 1, HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch };
            outputs.Children.Add(choice); _outputEditors[key] = choice;
        }
        outputs.Children.Add(new TextBlock { Text = _text.Output_Custom }); _customOutputEditor = new TextBox { Text = preferences.CustomOutputFolder }; outputs.Children.Add(_customOutputEditor);
        var browse = new Button { Content = _text.Output_Browse }; browse.Click += BrowseCustomOutput; outputs.Children.Add(browse);
        var outputBorder = new Border { Child = outputs }; outputBorder.Classes.Add("overview-group"); PluginGeneralHost.Children.Add(outputBorder);
    }
    private async void BrowseCustomOutput(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = _text.Output_Custom });
        if (folders.FirstOrDefault()?.TryGetLocalPath() is { } path) _customOutputEditor.Text = path;
    }
}
