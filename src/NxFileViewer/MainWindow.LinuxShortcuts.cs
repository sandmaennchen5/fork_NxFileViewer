using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace NxFileViewer.Avalonia;

public partial class MainWindow
{
    private void AddLinuxShortcutSettings()
    {
        var german = _text.CultureName.StartsWith("de", StringComparison.OrdinalIgnoreCase);
        var group = ProgramGroup(german ? "Verknüpfungen" : "Shortcuts");
        var status = new TextBlock { TextWrapping = TextWrapping.Wrap };
        var menu = new Button
        {
            Content = german ? "Im Anwendungsmenü erstellen" : "Create in applications menu",
            Margin = new Thickness(0, 0, 8, 4)
        };
        var desktop = new Button { Content = german ? "Auf dem Desktop erstellen" : "Create on desktop" };
        menu.Click += (_, _) => CreateShortcut(false);
        desktop.Click += (_, _) => CreateShortcut(true);
        group.Children.Add(new WrapPanel { Children = { menu, desktop } });
        group.Children.Add(status);

        void CreateShortcut(bool onDesktop)
        {
            try
            {
                var appImage = ViewerDataDirectory.IsAppImage;
                var executable = (appImage ? Environment.GetEnvironmentVariable("APPIMAGE") : Environment.ProcessPath)
                    ?? throw new IOException("Executable path unavailable.");
                var dataHome = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData,
                    Environment.SpecialFolderOption.DoNotVerify);
                var directory = onDesktop
                    ? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory, Environment.SpecialFolderOption.DoNotVerify)
                    : Path.Combine(dataHome, "applications");
                if (string.IsNullOrEmpty(directory) || (onDesktop &&
                    Path.TrimEndingDirectorySeparator(directory) == Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)))
                    throw new IOException(german ? "Kein Desktop-Ordner eingerichtet." : "No desktop folder is configured.");
                var launcher = LinuxDesktopLauncher.Create(executable, dataHome, directory, appImage);
                status.Text = (german ? "Verknüpfung erstellt" : "Shortcut created") + Environment.NewLine + launcher;
                if (onDesktop)
                    status.Text += Environment.NewLine + (german ? "Je nach Desktop einmal „Starten erlauben“ wählen."
                        : "Depending on your desktop, choose Allow Launching once.");
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
            {
                status.Text = error.Message;
            }
        }
    }
}
