using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace Emignatik.NxFileViewer.Services.FileAssociations;

[SupportedOSPlatform("windows")]
internal static class ExplorerRegistration
{
    internal const string PackageName = "NxFileViewerFork.Explorer";
    internal const string ConfigurationKey = @"Software\NxFileViewerFork\ExplorerIntegration";

    internal static async Task<bool> IsModernInstalledAsync() =>
        !string.IsNullOrWhiteSpace(await RunPowerShell("Get-AppxPackage -Name '" + PackageName + "' | Select-Object -First 1 -ExpandProperty Name"));

    internal static void RegisterTarget(string language)
    {
        var executable = CurrentExecutable();
        using var user = UserRegistry();
        using var config = user.CreateSubKey(ConfigurationKey);
        config.SetValue("Executable", executable, RegistryValueKind.String);
        config.SetValue("Language", language, RegistryValueKind.String);
        foreach (var type in new[] { "*", "Directory" })
        {
            using var menu = user.OpenSubKey(@"Software\Classes\" + type + @"\shell\NxFileViewerFork", true);
            menu?.SetValue("Icon", "\"" + executable + "\",0");
        }
        NotifyShell();
    }

    internal static void SynchronizeFeatures(bool compression, bool packageOperations, bool nup)
    {
        using var user = UserRegistry();
        using var config = user.OpenSubKey(ConfigurationKey, true);
        if (config?.GetValue("Executable") is not string executable ||
            !string.Equals(executable, CurrentExecutable(), StringComparison.OrdinalIgnoreCase)) return;
        config.SetValue("CompressionEnabled", compression ? 1 : 0, RegistryValueKind.DWord);
        config.SetValue("PackageOperationsEnabled", packageOperations ? 1 : 0, RegistryValueKind.DWord);
        config.SetValue("NupEnabled", nup ? 1 : 0, RegistryValueKind.DWord);
    }

    internal static async Task InstallAsync(string packagePath, string language)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)) throw new PlatformNotSupportedException("Windows 11 is required.");
        _ = CurrentExecutable();
        var package = Path.GetFullPath(packagePath);
        if (!File.Exists(package) || !Path.GetExtension(package).Equals(".msix", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Select the signed NxFileViewer Fork Explorer MSIX package.");
        // The package identity is checked before installation. Certificate trust is left to Windows.
        await RunPowerShell("Add-Type -AssemblyName System.IO.Compression.FileSystem; " +
            "$zip=[IO.Compression.ZipFile]::OpenRead(" + Quote(package) + "); try { " +
            "$entry=$zip.GetEntry('AppxManifest.xml'); if (!$entry) { throw 'Package manifest missing.' }; " +
            "$reader=[IO.StreamReader]::new($entry.Open()); try { [xml]$manifest=$reader.ReadToEnd() } finally { $reader.Dispose() }; " +
            "if ($manifest.Package.Identity.Name -ne '" + PackageName + "') { throw 'Not a NxFileViewer Fork Explorer package.' } " +
            "} finally { $zip.Dispose() }; Add-AppxPackage -Path " + Quote(package));
        RegisterTarget(language);
    }
    internal static string CurrentExecutable() => ValidateExecutable(Environment.ProcessPath ?? "");
    internal static string ValidateExecutable(string executable)
    {
        _ = Emignatik.NxFileViewer.Services.Updates.ViewerPackageLayout.ExecutableName(Path.GetFileName(executable));
        return Path.GetFullPath(executable);
    }
    internal static RegistryKey UserRegistry() => RegistryKey.OpenBaseKey(RegistryHive.CurrentUser,
        Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Registry32);
    internal static string Quote(string value) => "'" + value.Replace("'", "''") + "'";
    internal static async Task<string> RunPowerShell(string command, System.Threading.CancellationToken token = default)
    {
        var script = "$ErrorActionPreference='Stop'; $ProgressPreference='SilentlyContinue'; " +
            "[Console]::OutputEncoding=[Text.Encoding]::UTF8; try { " + command +
            " } catch { [Console]::Error.WriteLine($_.Exception.Message); exit 1 }";
        // Use native Windows PowerShell even when the viewer itself is a 32-bit process.
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var system = Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess ? "Sysnative" : "System32";
        var start = new ProcessStartInfo(Path.Combine(windows, system, @"WindowsPowerShell\v1.0\powershell.exe")) {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8, StandardErrorEncoding = System.Text.Encoding.UTF8
        };
        start.Environment["PSModulePath"] = Path.Combine(windows, @"System32\WindowsPowerShell\v1.0\Modules") + ";" + Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"WindowsPowerShell\Modules");
        start.ArgumentList.Add("-NoProfile"); start.ArgumentList.Add("-NonInteractive");
        start.ArgumentList.Add("-EncodedCommand"); start.ArgumentList.Add(Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(script)));
        using var process = Process.Start(start) ?? throw new IOException("Could not start Windows package registration.");
        var error = process.StandardError.ReadToEndAsync();
        var output = process.StandardOutput.ReadToEndAsync();
        try { await process.WaitForExitAsync(token); }
        catch (OperationCanceledException)
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { }
            throw;
        }
        var result = await output;
        if (process.ExitCode != 0) throw new InvalidOperationException(await error);
        await error;
        return result;
    }
    internal static void NotifyShell() => SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(uint eventId, uint flags, IntPtr item1, IntPtr item2);
}
