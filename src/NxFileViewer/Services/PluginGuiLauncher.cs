using System;
using System.Diagnostics;
using System.IO;

namespace Emignatik.NxFileViewer.Services;

public static class PluginGuiLauncher
{
    public static ProcessStartInfo CreateStartInfo(string executable, bool nand)
    {
        var info = new ProcessStartInfo(Path.GetFullPath(executable))
        {
            WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(executable))!,
            UseShellExecute = true
        };
        if (nand) info.ArgumentList.Add("--gui");
        return info;
    }

    public static ProcessStartInfo CreateKeyedStartInfo(string executable, bool nand, string? keysPath, string sessionDirectory)
    {
        var info = CreateStartInfo(executable, nand);
        if (string.IsNullOrWhiteSpace(keysPath) || !File.Exists(keysPath)) return info;
        Directory.CreateDirectory(sessionDirectory);
        if (nand)
        {
            File.Copy(keysPath, Path.Combine(sessionDirectory, "keys.dat"));
            info.WorkingDirectory = sessionDirectory;
        }
        else
        {
            // NSZ opens its GUI only without CLI arguments. Isolate its default key lookup.
            var pluginDirectory = Path.GetDirectoryName(info.FileName)!;
            foreach (var name in new[] { "prod.keys", "keys.txt" })
                if (File.Exists(Path.Combine(pluginDirectory, name)))
                    throw new IOException("NSZ: " + Path.Combine(pluginDirectory, name) +
                        " has priority over the NxFileViewer keys. Move this file before launching the GUI.");
            var keyDirectory = Path.Combine(sessionDirectory, ".switch");
            Directory.CreateDirectory(keyDirectory);
            File.Copy(keysPath, Path.Combine(keyDirectory, "prod.keys"));
            info.UseShellExecute = false;
            info.Environment["USERPROFILE"] = sessionDirectory;
            info.Environment["HOME"] = sessionDirectory;
            info.Environment["XDG_CONFIG_HOME"] = Path.Combine(sessionDirectory, ".config");
        }
        return info;
    }

    public static void Open(string executable, bool nand, string? keysPath = null)
    {
        var session = Path.Combine(Path.GetTempPath(), "NxFileViewer", "PluginGui", Guid.NewGuid().ToString("N"));
        try
        {
            var process = Process.Start(CreateKeyedStartInfo(executable, nand, keysPath, session));
            if (process == null) { Cleanup(session); return; }
            process.EnableRaisingEvents = true;
            process.Exited += (_, _) => { Cleanup(session); process.Dispose(); };
            if (process.HasExited) { Cleanup(session); process.Dispose(); }
        }
        catch { Cleanup(session); throw; }
    }

    private static void Cleanup(string directory)
    {
        try { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
