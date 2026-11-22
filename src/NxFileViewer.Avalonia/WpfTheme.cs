using System;
using System.Collections.Generic;
using System.Xml.Linq;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace NxFileViewer.Avalonia;

internal static class WpfTheme
{
    internal static void Apply(Window window)
    {
        if (!window.Styles.OfType<global::Avalonia.Markup.Xaml.Styling.StyleInclude>().Any(style => style.Source?.AbsolutePath.EndsWith("/WorkspaceStyles.axaml", StringComparison.Ordinal) == true))
            window.Styles.Add(new global::Avalonia.Markup.Xaml.Styling.StyleInclude(new Uri("avares://NxFileViewerFork.Avalonia/")) { Source = new Uri("avares://NxFileViewerFork.Avalonia/WorkspaceStyles.axaml") });
        var theme = window.ActualThemeVariant == ThemeVariant.Dark ? "Dark" : "Light";
        using var stream = typeof(WpfTheme).Assembly.GetManifestResourceStream("Viewer.Theme." + theme)
            ?? throw new InvalidOperationException("The shared WPF theme is missing.");
        var root = XElement.Load(stream);
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
        var colors = new Dictionary<string, Color>();
        foreach (var element in root.Elements())
        {
            var key = (string?)element.Attribute(xaml + "Key");
            if (key == null) continue;
            if (element.Name.LocalName == "Color")
            {
                var color = Color.Parse(element.Value.Trim());
                colors[key] = color;
                window.Resources[key] = color;
            }
            else if (element.Name.LocalName == "SolidColorBrush")
            {
                var value = (string)element.Attribute("Color")!;
                var color = value.StartsWith("{StaticResource ", StringComparison.Ordinal)
                    ? colors[value[16..^1]] : Color.Parse(value);
                window.Resources[key] = new SolidColorBrush(color);
            }
        }
        foreach (var suffix in new[] { "Selected", "SelectedHovered", "SelectedUnfocused", "SelectedHoveredUnfocused" })
        {
            window.Resources["DataGridRow" + suffix + "BackgroundBrush"] = window.Resources["Item.Selected.Background"];
            window.Resources["DataGridRow" + suffix + "BackgroundOpacity"] = 1d;
        }
    }
}
