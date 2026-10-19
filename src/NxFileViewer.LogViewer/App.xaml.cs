using System.Windows;
namespace NxFileViewer.LogViewer;
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var window = new MainWindow();
        MainWindow = window;
        if (e.Args.Length > 0) window.Loaded += async (_, _) => await window.OpenPathAsync(e.Args[0]);
        window.Show();
    }
}
