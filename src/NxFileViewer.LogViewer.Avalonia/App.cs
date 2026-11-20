using System;
using System.Xml.Linq;
using Avalonia.Media;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;

namespace NxFileViewer.LogViewer;

internal sealed class App : Application
{
    [STAThread]
    public static void Main(string[] args) =>
        AppBuilder.Configure<App>().UsePlatformDetect().StartWithClassicDesktopLifetime(args);

    public override void Initialize()
    {
        RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Light;
        Styles.Add(new FluentTheme());
        using var stream = typeof(App).Assembly.GetManifestResourceStream("LogViewer.Palette")
            ?? throw new InvalidOperationException("The shared LogViewer palette is missing.");
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
        var palette = XElement.Load(stream);
        foreach (var brush in palette.Elements(palette.Name.Namespace + "SolidColorBrush"))
            Resources[(string)brush.Attribute(xaml + "Key")!] = SolidColorBrush.Parse((string)brush.Attribute("Color")!);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow();
            desktop.MainWindow = window;
            if (desktop.Args is { Length: > 0 } args)
                window.Opened += async (_, _) => await window.OpenPathAsync(args[0]);
        }
        base.OnFrameworkInitializationCompleted();
    }
}
