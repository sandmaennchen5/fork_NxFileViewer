using System;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Emignatik.NxFileViewer.Services.FileAssociations;

namespace NxFileViewer.Avalonia;

internal sealed class App(ExplorerInstance? instance, ExplorerLaunchRequest? request, string? readyFile, string? startupError) : Application
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (args is ["--apply-avalonia-update", var job])
        {
            Environment.ExitCode = Updates.AvaloniaAppUpdater.RunWorker(job);
            return;
        }
        var readyFile = args is ["--avalonia-update-ready", var ready] ? ready : null;
        ExplorerLaunchRequest? request = null;
        string? startupError = null;
        if (readyFile == null)
        {
            try { request = ExplorerLaunchRequest.Parse(args); }
            catch (Exception error) when (error is IOException or ArgumentException or UnauthorizedAccessException or NotSupportedException)
            { startupError = error.Message; }
        }
        if (OperatingSystem.IsWindows())
        {
            using var instance = new ExplorerInstance(Environment.ProcessPath ?? throw new InvalidOperationException("Executable path unavailable."));
            if (!instance.IsPrimary)
            {
                try
                {
                    if (request == null) throw new InvalidOperationException(startupError ?? "Another instance is using this installation.");
                    instance.Forward(request).GetAwaiter().GetResult();
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException or OperationCanceledException)
                {
                    Console.Error.WriteLine(error.Message);
                    Environment.ExitCode = 1;
                }
                return;
            }
            AppBuilder.Configure(() => new App(instance, request, readyFile, startupError)).UsePlatformDetect().StartWithClassicDesktopLifetime(args);
            Emignatik.NxFileViewer.Services.EmptyDirectoryCleanup.Clean(ViewerDataDirectory.Root);
            return;
        }
        AppBuilder.Configure(() => new App(null, request, readyFile, startupError)).UsePlatformDetect().StartWithClassicDesktopLifetime(args);
        Emignatik.NxFileViewer.Services.EmptyDirectoryCleanup.Clean(ViewerDataDirectory.Root);
    }

    public override void Initialize() { Styles.Add(new FluentTheme()); Styles.Add(new global::Avalonia.Markup.Xaml.Styling.StyleInclude(new Uri("avares://NxFileViewerFork/")) { Source = new Uri("avares://Avalonia.Controls.DataGrid/Themes/Fluent.xaml") }); }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow();
            desktop.MainWindow = window;
            if (OperatingSystem.IsWindows())
            {
                try { ForkAssociationLauncher.RefreshInstalled(); }
                catch (Exception error) { window.ReportExplorerRequestError(error.Message); }
            }
            if (readyFile != null)
            {
                var full = Path.GetFullPath(readyFile);
                var updates = Path.Combine(AppContext.BaseDirectory,"Updates") + Path.DirectorySeparatorChar;
                if (full.StartsWith(updates, OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal) &&
                    System.IO.Path.GetFileName(full)=="ready")
                    window.Opened += (_, _) => System.IO.File.WriteAllText(full,"ready");
            }
            window.InitializeExplorerQueue(request, startupError);
            if (OperatingSystem.IsWindows() && instance != null)
                instance.Listen(incoming => Dispatcher.UIThread.Post(() => window.EnqueueExplorerRequest(incoming)),
                    error => Dispatcher.UIThread.Post(() => window.ReportExplorerRequestError(error.Message)));
        }
        base.OnFrameworkInitializationCompleted();
    }
}
