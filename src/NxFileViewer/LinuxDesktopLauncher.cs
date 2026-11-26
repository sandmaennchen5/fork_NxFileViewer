using System;
using System.IO;

namespace NxFileViewer.Avalonia;

internal static class LinuxDesktopLauncher
{
    internal static string Create(string executable, string dataHome, string directory, bool appImage)
    {
        if (!Path.IsPathFullyQualified(executable) || !File.Exists(executable))
            throw new FileNotFoundException("The application executable could not be located.", executable);
        var id = appImage ? "NxFileViewerFork.AppImage" : "NxFileViewerFork";
        var icon = Path.Combine(Path.GetDirectoryName(executable)!, "nxfv-icon.png");
        if (appImage || !File.Exists(icon))
        {
            var iconDirectory = Directory.CreateDirectory(Path.Combine(dataHome, "icons")).FullName;
            icon = Path.Combine(iconDirectory, id + ".png");
            using var resource = typeof(LinuxDesktopLauncher).Assembly.GetManifestResourceStream("Viewer.LinuxLauncherIcon")
                ?? throw new InvalidOperationException("The application icon is missing.");
            WriteAtomically(icon, resource.CopyTo);
        }

        var launcher = Path.Combine(Directory.CreateDirectory(directory).FullName, id + ".desktop");
        var command = executable.Replace("\\", "\\\\").Replace("\"", "\\\"")
            .Replace(((char)96).ToString(), "\\" + (char)96).Replace("$", "\\$").Replace("%", "%%");
        var contents = "[Desktop Entry]\nType=Application\nName=NxFileViewer Fork\n" +
            $"Exec=\"{EscapeValue(command)}\" %F\nIcon={EscapeValue(icon)}\nTerminal=false\nCategories=Utility;\n";
        WriteAtomically(launcher, output =>
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(contents);
            output.Write(bytes);
            if (OperatingSystem.IsLinux())
                File.SetUnixFileMode(output.Name, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        });
        return launcher;
    }

    private static string EscapeValue(string value) =>
        value.Replace("\\", "\\\\").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");

    private static void WriteAtomically(string path, Action<FileStream> write)
    {
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write)) write(output);
            File.Move(temporary, path, overwrite: true);
        }
        finally { File.Delete(temporary); }
    }
}
