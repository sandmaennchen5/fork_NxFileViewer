using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Emignatik.NxFileViewer.Services.FileAssociations;
using NxFileViewer.Avalonia.Updates;

namespace NxFileViewer.Avalonia;

internal sealed class App(ExplorerInstance? instance, ExplorerLaunchRequest? request, string? readyFile, string? startupError) : Application
{
    private bool _dataReady;

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
        App? app = null;
        AppBuilder.Configure(() => app = new App(null, request, readyFile, startupError)).UsePlatformDetect().StartWithClassicDesktopLifetime(args);
        if (app?._dataReady == true) Emignatik.NxFileViewer.Services.EmptyDirectoryCleanup.Clean(ViewerDataDirectory.Root);
    }

    public override void Initialize() { Styles.Add(new FluentTheme()); Styles.Add(new global::Avalonia.Markup.Xaml.Styling.StyleInclude(new Uri("avares://NxFileViewerFork/")) { Source = new Uri("avares://Avalonia.Controls.DataGrid/Themes/Fluent.xaml") }); }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = TryInitializeMacData() is { } error
                ? CreateMigrationErrorWindow(desktop, error)
                : CreateMainWindow();
        base.OnFrameworkInitializationCompleted();
    }

    private static Exception? TryInitializeMacData()
    {
        try { ViewerDataDirectory.InitializeMacBundleData(); return null; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        { return error; }
    }

    private MainWindow CreateMainWindow()
    {
        _dataReady = true;
        var window = new MainWindow();
        if (OperatingSystem.IsWindows())
        {
            try { ForkAssociationLauncher.RefreshInstalled(); }
            catch (Exception error) { window.ReportExplorerRequestError(error.Message); }
        }
        if (readyFile != null)
        {
            var full = Path.GetFullPath(readyFile);
            var updates = Path.Combine(AppContext.BaseDirectory,"Updates") + Path.DirectorySeparatorChar;
            if ((AvaloniaAppUpdater.GetRunningAppImage() ?? AvaloniaAppUpdater.GetRunningMacBundle()) is { } package
                ? AvaloniaAppUpdater.IsNativePackageReadyPath(full, package)
                : full.StartsWith(updates, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) &&
                  Path.GetFileName(full) == "ready")
                window.Opened += (_, _) => System.IO.File.WriteAllText(full,"ready");
        }
        window.InitializeExplorerQueue(request, startupError);
        if (OperatingSystem.IsWindows() && instance != null)
            instance.Listen(incoming => Dispatcher.UIThread.Post(() => window.EnqueueExplorerRequest(incoming)),
                error => Dispatcher.UIThread.Post(() => window.ReportExplorerRequestError(error.Message)));
        return window;
    }

    private Window CreateMigrationErrorWindow(IClassicDesktopStyleApplicationLifetime desktop, Exception error)
    {
        var german = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "de";
        var message = new TextBlock { TextWrapping = global::Avalonia.Media.TextWrapping.Wrap };
        var retry = new Button { Content = german ? "Erneut versuchen" : "Retry" };
        var panel = new StackPanel { Margin = new Thickness(20), Spacing = 12 };
        panel.Children.Add(message);
        panel.Children.Add(retry);
        var window = new Window
        {
            Title = "NxFileViewer Fork", Width = 570, SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterScreen, Content = panel
        };
        void ShowError(Exception failure) => message.Text = (german
            ? "Die macOS-Programmdaten konnten nicht übernommen werden."
            : "The macOS application data could not be migrated.") + "\n\n" + failure.Message;
        ShowError(error);
        retry.Click += (_, _) =>
        {
            if (TryInitializeMacData() is { } failure)
            {
                ShowError(failure);
                return;
            }
            desktop.MainWindow = CreateMainWindow();
            desktop.MainWindow.Show();
            window.Close();
        };
        return window;
    }
}
