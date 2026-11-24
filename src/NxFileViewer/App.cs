using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;

namespace NxFileViewer.Avalonia;

internal sealed class App : Application
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (args is ["--apply-avalonia-update", var job])
        {
            Environment.ExitCode = Updates.AvaloniaAppUpdater.RunWorker(job);
            return;
        }
        AppBuilder.Configure<App>().UsePlatformDetect().StartWithClassicDesktopLifetime(args);
    }

    public override void Initialize() { Styles.Add(new FluentTheme()); Styles.Add(new global::Avalonia.Markup.Xaml.Styling.StyleInclude(new Uri("avares://NxFileViewerFork/")) { Source = new Uri("avares://Avalonia.Controls.DataGrid/Themes/Fluent.xaml") }); }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow();
            desktop.MainWindow = window;
            if (desktop.Args is ["--avalonia-update-ready", var ready])
            {
                var full = System.IO.Path.GetFullPath(ready);
                var updates = System.IO.Path.Combine(AppContext.BaseDirectory,"Updates") + System.IO.Path.DirectorySeparatorChar;
                if (full.StartsWith(updates, OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal) &&
                    System.IO.Path.GetFileName(full)=="ready")
                    window.Opened += (_, _) => System.IO.File.WriteAllText(full,"ready");
            }
            else if (OperatingSystem.IsWindows() && desktop.Args is ["--explorer-request", ..])
                window.Opened += async (_, _) => await window.OpenExplorerRequestAsync(desktop.Args);
            else if (desktop.Args is { Length: > 0 } args)
                window.Opened += async (_, _) => await window.OpenFileAsync(args[0], fromWindows: true);
        }
        base.OnFrameworkInitializationCompleted();
    }
}
