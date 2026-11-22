using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Emignatik.NxFileViewer.Services.FileRenaming;
using Emignatik.NxFileViewer.Services.FileRenaming.Exceptions;
using Emignatik.NxFileViewer.Localization.Keys;

namespace NxFileViewer.Avalonia;

internal sealed class NamingPatternWindow : Window
{
    internal static Task<string?> Edit(Window owner, string section, string label, string pattern, ILocalizationKeys text)
    {
        var type = section switch { "Application" => PatternType.Application, "Patch" => PatternType.Patch, "Addon" => PatternType.Addon, "Combined" => PatternType.Super, _ => PatternType.Multi };
        var window = new NamingPatternWindow { Title = text.Naming_Manual + " – " + label, Width = 820, Height = 650, MinWidth = 360, MinHeight = 300, WindowStartupLocation = WindowStartupLocation.CenterOwner, RequestedThemeVariant = owner.ActualThemeVariant };
        var grid = new Grid { Margin = new Thickness(20), RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*,Auto,Auto") };
        var input = new TextBox { Text = pattern, MinHeight = 60, MaxLength = 2048, TextWrapping = TextWrapping.Wrap, FontFamily = new FontFamily("Consolas"), VerticalContentAlignment = VerticalAlignment.Center };
        var error = new TextBlock { Foreground = Brushes.IndianRed, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
        void Add(Control control, int row) { Grid.SetRow(control, row); grid.Children.Add(control); }
        Add(new TextBlock { Text = label, FontWeight = FontWeight.Bold, Margin = new Thickness(0,0,0,8) }, 0);
        Add(input, 1);
        Add(new TextBlock { Text = text.Naming_AllowedVariables + Environment.NewLine + NamingPresets.AvailableVariables(type), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,12,0,12) }, 2);
        Add(new ScrollViewer { Content = new TextBlock { Text = type == PatternType.Multi ? text.RenamingTool_ToolTip_MultiPattern : text.RenamingTool_ToolTip_Patterns, TextWrapping = TextWrapping.Wrap } }, 3);
        Add(error, 4);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8, Margin = new Thickness(0,16,0,0) };
        var accept = new Button { Content = "OK", MinWidth = 90, IsDefault = true };
        var cancel = new Button { Content = text.Nsz_Cancel, MinWidth = 90, IsCancel = true };
        accept.Click += (_, _) => { try { NamingPresets.ValidatePattern(input.Text ?? "", type); window.Close(input.Text ?? ""); } catch (Exception ex) { error.Text = ex.Message; input.Focus(); } };
        cancel.Click += (_, _) => window.Close(null);
        actions.Children.Add(accept); actions.Children.Add(cancel); Add(actions, 5);
        window.Content = grid; WpfTheme.Apply(window);
        window.Opened += (_, _) => { input.Focus(); input.SelectAll(); };
        return window.ShowDialog<string?>(owner);
    }
}
