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

    public static ProcessStartInfo CreateDesktopStartInfo(string executable, string backend, string? keysPath, string sessionDirectory)
    {
        var info = CreateStartInfo(executable, false);
        var tools = Path.Combine(sessionDirectory, "nscb-desktop-tools");
        Directory.CreateDirectory(tools);
        File.Copy(backend, Path.Combine(tools, "nscb_rust.exe"));
        if (!string.IsNullOrWhiteSpace(keysPath) && File.Exists(keysPath))
            File.Copy(keysPath, Path.Combine(tools, "prod.keys"));
        info.UseShellExecute = false;
        info.Environment["TEMP"] = sessionDirectory;
        info.Environment["TMP"] = sessionDirectory;
        info.Environment["TMPDIR"] = sessionDirectory;
        return info;
    }

    public static void OpenDesktop(string executable, string backend, string? keysPath)
    {
        var session = Path.Combine(Path.GetTempPath(), "NxFileViewer", "DesktopGui", Guid.NewGuid().ToString("N"));
        try
        {
            var process = Process.Start(CreateDesktopStartInfo(executable, backend, keysPath, session));
            if (process == null) { Cleanup(session); return; }
            process.EnableRaisingEvents = true;
            process.Exited += (_, _) => { Cleanup(session); process.Dispose(); };
            if (process.HasExited) { Cleanup(session); process.Dispose(); }
        }
        catch { Cleanup(session); throw; }
    }
    public static string? KronosLauncherPath(string directory)
    {
        foreach (var name in new[] { "NSCB.bat", "NSCB.exe" })
        {
            var path = Path.Combine(directory, name);
            if (File.Exists(path)) return path;
        }
        return null;
    }

    public static ProcessStartInfo CreateKronosStartInfo(string directory, string? keysPath, string workspace)
    {
        var root = Path.GetFullPath(directory);
        var starter = KronosLauncherPath(root) ?? throw new FileNotFoundException("Kronos requires NSCB.bat or NSCB.exe.");
        if (string.IsNullOrWhiteSpace(keysPath) || !File.Exists(keysPath))
            throw new FileNotFoundException("Kronos requires the selected prod.keys file.");
        CopyKronosTree(Path.Combine(root, "ztools"), Path.Combine(workspace, "ztools"));
        if (Directory.Exists(Path.Combine(root, "zconfig")))
            CopyKronosTree(Path.Combine(root, "zconfig"), Path.Combine(workspace, "zconfig"));
        foreach (var name in new[] { "NSCB.bat", "NSCB.exe" })
        {
            var source = Path.Combine(root, name);
            if (!File.Exists(source)) continue;
            if ((File.GetAttributes(source) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Linked Kronos launchers are not supported.");
            File.Copy(source, Path.Combine(workspace, name));
        }
        // Upstream batch defaults use the system py launcher. Keep the private runtime.
        var python = Path.Combine(root, "runtime", "python.exe");
        var config = Path.Combine(workspace, "zconfig");
        if (File.Exists(python) && Directory.Exists(config))
        {
            foreach (var path in Directory.GetFiles(config, "*.cmd", SearchOption.AllDirectories))
            {
                var text = File.ReadAllText(path);
                text = System.Text.RegularExpressions.Regex.Replace(text,
                    "(?im)^set\\s+\"?(pycommandw?)=[^\\r\\n]*", match => {
                        var variable = match.Groups[1].Value;
                        var executable = variable.Equals("pycommandw", StringComparison.OrdinalIgnoreCase) &&
                            File.Exists(Path.Combine(root, "runtime", "pythonw.exe"))
                            ? Path.Combine(root, "runtime", "pythonw.exe") : python;
                        return "set \"" + variable + "=\"" + executable + "\"\"";
                    });
                File.WriteAllText(path, text);
            }
        }
        File.Copy(keysPath, Path.Combine(workspace, "ztools", "keys.txt"), true);
        var batch = Path.GetExtension(starter).Equals(".bat", StringComparison.OrdinalIgnoreCase);
        var info = new ProcessStartInfo(batch
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe")
            : Path.Combine(workspace, "NSCB.exe"))
        {
            WorkingDirectory = workspace,
            UseShellExecute = false,
            CreateNoWindow = false
        };
        if (batch)
            foreach (var argument in new[] { "/d", "/c", "NSCB.bat" }) info.ArgumentList.Add(argument);
        return info;
    }

    public static void OpenKronos(string directory, string? keysPath)
    {
        // This is the requested interactive NSCB console. Keep its outputs/config;
        // remove only the private key copy when the launcher terminates.
        var workspace = Path.Combine(Utils.PathHelper.CurrentAppDir, "Plugins", "KronosGuiSessions", Guid.NewGuid().ToString("N"));
        try
        {
            var process = Process.Start(CreateKronosStartInfo(directory, keysPath, workspace))
                ?? throw new IOException("Kronos could not be started.");
            process.EnableRaisingEvents = true;
            process.Exited += (_, _) => { CleanupKronosKeys(workspace); process.Dispose(); };
            if (process.HasExited) { CleanupKronosKeys(workspace); process.Dispose(); }
        }
        catch { CleanupKronosKeys(workspace); throw; }
    }
    private static void CopyKronosTree(string source, string destination)
    {
        if ((File.GetAttributes(source) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Linked Kronos directories are not supported.");
        Directory.CreateDirectory(destination);
        foreach (var path in Directory.GetFiles(source))
        {
            var name = Path.GetFileName(path);
            if (name.Equals("keys.txt", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("dev_keys.txt", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith(".keys", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("keys.dat", StringComparison.OrdinalIgnoreCase)) continue;
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Linked Kronos files are not supported.");
            File.Copy(path, Path.Combine(destination, name), false);
        }
        foreach (var path in Directory.GetDirectories(source))
        {
            var name = Path.GetFileName(path);
            if (name is "__pycache__" or "DB") continue;
            CopyKronosTree(path, Path.Combine(destination, name));
        }
    }

    public static void CleanupKronosKeys(string workspace)
    {
        try { File.Delete(Path.Combine(workspace, "ztools", "keys.txt")); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
    private static void Cleanup(string directory)
    {
        try { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
