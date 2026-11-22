using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;

namespace NxFileViewer.Avalonia;

internal sealed class App : Application
{
    [STAThread]
    public static void Main(string[] args) =>
        AppBuilder.Configure<App>().UsePlatformDetect().StartWithClassicDesktopLifetime(args);

    public override void Initialize() { Styles.Add(new FluentTheme()); Styles.Add(new global::Avalonia.Markup.Xaml.Styling.StyleInclude(new Uri("avares://NxFileViewerFork.Avalonia/")) { Source = new Uri("avares://Avalonia.Controls.DataGrid/Themes/Fluent.xaml") }); }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow();
            desktop.MainWindow = window;
            if (desktop.Args is { Length: > 0 } args)
                window.Opened += async (_, _) => await window.OpenFileAsync(args[0], fromWindows: true);
        }
        base.OnFrameworkInitializationCompleted();
    }
}
