using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace Emignatik.NxFileViewer.Services.FileAssociations;

public static class ExplorerIntegration
{
    public const string ClassId = "{872F5F52-BC53-4A69-B1D1-8583B20CA4B1}";
    public const string PackageName = "NxFileViewerFork.Explorer";
    public const string ConfigurationKey = @"Software\NxFileViewerFork\ExplorerIntegration";
    public static string Architecture => RuntimeInformation.OSArchitecture switch {
        System.Runtime.InteropServices.Architecture.X64 => "x64",
        System.Runtime.InteropServices.Architecture.Arm64 => "arm64",
        System.Runtime.InteropServices.Architecture.X86 => "x86",
        _ => throw new PlatformNotSupportedException("Unsupported Explorer architecture.")
    };

    public static IReadOnlyList<AssociationEntry> ClassicEntries(string executable, string library, string language = "Auto")
    {
        executable = ValidateExecutable(executable);
        library = Path.GetFullPath(library);
        var entries = new List<AssociationEntry> {
            new(ConfigurationKey, "Executable", executable),
            new(ConfigurationKey, "Language", language),
            new(@"Software\Classes\CLSID\" + ClassId + @"\InprocServer32", "", library),
            new(@"Software\Classes\CLSID\" + ClassId + @"\InprocServer32", "ThreadingModel", "Apartment")
        };
        foreach (var type in new[] { "*", "Directory" })
        {
            var key = @"Software\Classes\" + type + @"\shell\NxFileViewerFork";
            entries.Add(new(key, "MUIVerb", "NxFileViewer Fork"));
            entries.Add(new(key, "ExplorerCommandHandler", ClassId));
            entries.Add(new(key, "Icon", "\"" + executable + "\",0"));
            entries.Add(new(key, "MultiSelectModel", "Player"));
        }
        return entries;
    }

    public static byte[] NativePayload(string architecture)
    {
        if (architecture is not ("x64" or "x86" or "arm64")) throw new ArgumentException("Unsupported Explorer architecture.");
        using var stream = typeof(ExplorerIntegration).Assembly.GetManifestResourceStream("Viewer.Explorer." + architecture)
            ?? throw new FileNotFoundException("Explorer extension missing. Build src/NxFileViewer.Explorer/build.ps1 before publishing.");
        using var bytes = new MemoryStream(); stream.CopyTo(bytes); return bytes.ToArray();
    }

    public static void RegisterClassic()
    {
        var executable = CurrentExecutable();
        var payload = NativePayload(Architecture);
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NxFileViewerFork",
            "ExplorerIntegration", Architecture, Convert.ToHexString(SHA256.HashData(payload)));
        Directory.CreateDirectory(directory);
        var library = Path.Combine(directory, "NxFileViewer.Explorer.dll");
        if (!File.Exists(library))
        {
            var temporary = library + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try { File.WriteAllBytes(temporary, payload); File.Move(temporary, library, true); }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        using var user = UserRegistry();
        foreach (var entry in ClassicEntries(executable, library, Localization.LocalizationManager.Instance.Current.Keys.CultureName))
        {
            using var key = user.CreateSubKey(entry.Key);
            key.SetValue(entry.Name, entry.Value, RegistryValueKind.String);
        }
        NotifyShell();
    }

    public static void RemoveClassic()
    {
        using var user = UserRegistry();
        foreach (var type in new[] { "*", "Directory" })
            user.DeleteSubKeyTree(@"Software\Classes\" + type + @"\shell\NxFileViewerFork", false);
        user.DeleteSubKeyTree(@"Software\Classes\CLSID\" + ClassId, false);
        // Keep immutable DLL versions: Explorer may still have them loaded.
        NotifyShell();
    }

    public static async Task InstallModern(string packagePath)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)) throw new PlatformNotSupportedException("Windows 11 is required.");
        var executable = CurrentExecutable();
        var package = Path.GetFullPath(packagePath);
        if (!File.Exists(package) || !Path.GetExtension(package).Equals(".msix", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Select the signed NxFileViewer Explorer MSIX package.");
        // The package identity is checked before installation. Certificate trust is left to Windows.
        await RunPowerShell("Add-Type -AssemblyName System.IO.Compression.FileSystem; " +
            "$zip=[IO.Compression.ZipFile]::OpenRead(" + Quote(package) + "); try { " +
            "$entry=$zip.GetEntry('AppxManifest.xml'); if (!$entry) { throw 'Package manifest missing.' }; " +
            "$reader=[IO.StreamReader]::new($entry.Open()); try { [xml]$manifest=$reader.ReadToEnd() } finally { $reader.Dispose() }; " +
            "if ($manifest.Package.Identity.Name -ne '" + PackageName + "') { throw 'Not a NxFileViewer Explorer package.' } " +
            "} finally { $zip.Dispose() }; Add-AppxPackage -Path " + Quote(package));
        using var user = UserRegistry();
        using var config = user.CreateSubKey(ConfigurationKey);
        config.SetValue("Executable", executable);
        config.SetValue("Language", Localization.LocalizationManager.Instance.Current.Keys.CultureName);
        NotifyShell();
    }

    public static Task<ExplorerInstallStage> InstallModernFromRelease(bool includePrereleases, IProgress<ExplorerInstallStage>? progress = null) =>
        new ExplorerPackageService().InstallAsync(Architecture, includePrereleases,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NxFileViewerFork", "ExplorerDownloads"),
            new ModernDeployment(), progress, System.Threading.CancellationToken.None);

    private sealed class ModernDeployment : IExplorerPackageDeployment
    {
        public async Task<Version?> InstalledVersionAsync()
        {
            var result = await RunPowerShell("$package=Get-AppxPackage -Name '" + PackageName + "' | Sort-Object { [Version]$_.Version } -Descending | Select-Object -First 1; if ($package) { $package.Version.ToString() }");
            return string.IsNullOrWhiteSpace(result) ? null : Version.Parse(result.Trim());
        }
        public bool IsTrusted(X509Certificate2 certificate)
        {
            using var store = new X509Store(StoreName.TrustedPeople, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadOnly);
            return store.Certificates.Find(X509FindType.FindByThumbprint, certificate.Thumbprint, false).Count > 0;
        }
        public async Task TrustAsync(X509Certificate2 certificate)
        {
            using var resource = typeof(ExplorerIntegration).Assembly.GetManifestResourceStream("Viewer.Explorer.Trust.ps1")
                ?? throw new FileNotFoundException("Certificate consent helper missing.");
            using var reader = new StreamReader(resource);
            var script = (await reader.ReadToEndAsync()).Replace("@CERTIFICATE_THUMBPRINT@", certificate.Thumbprint);
            // Embed only public DER certificate bytes, never execute scripts supplied by a download.
            var command = "& { " + script + "\n } -CertificateBase64 '" + Convert.ToBase64String(certificate.RawData) + "'";
            var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            var system = Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess ? "Sysnative" : "System32";
            var start = new ProcessStartInfo(Path.Combine(windows, system, @"WindowsPowerShell\v1.0\powershell.exe")) {
                UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Normal,
                Arguments = "-NoProfile -ExecutionPolicy Bypass -EncodedCommand " + Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(command))
            };
            // This visible window is the requested interactive user/admin confirmation.
            using var process = Process.Start(start) ?? throw new IOException("Could not start certificate confirmation.");
            await process.WaitForExitAsync();
            if (process.ExitCode != 0) throw new OperationCanceledException("Certificate confirmation was cancelled or failed.");
        }
        public Task InstallAsync(string package) => InstallModern(package);
        public void RefreshConfiguration()
        {
            using var user = UserRegistry();
            using var config = user.CreateSubKey(ConfigurationKey);
            config.SetValue("Executable", CurrentExecutable());
            config.SetValue("Language", Localization.LocalizationManager.Instance.Current.Keys.CultureName);
            NotifyShell();
        }
    }
    public static void UpdateLanguage(string language)
    {
        using var user = UserRegistry();
        using var config = user.OpenSubKey(ConfigurationKey, true);
        if (config == null || config.GetValue("Executable") is not string) return;
        config.SetValue("Language", language);
        var executable = CurrentExecutable();
        config.SetValue("Executable", executable);
        foreach (var type in new[] { "*", "Directory" })
        {
            using var menu = user.OpenSubKey(@"Software\Classes\" + type + @"\shell\NxFileViewerFork", true);
            menu?.SetValue("Icon", "\"" + executable + "\",0");
        }
        NotifyShell();
    }

    public static async Task RemoveModern()
    {
        await RunPowerShell("Get-AppxPackage -Name '" + PackageName + "' | Remove-AppxPackage");
        NotifyShell();
    }

    private static string CurrentExecutable() => ValidateExecutable(Environment.ProcessPath ?? "");
    private static string ValidateExecutable(string executable)
    {
        _ = Emignatik.NxFileViewer.Services.Updates.ViewerPackageLayout.ExecutableName(Path.GetFileName(executable));
        return Path.GetFullPath(executable);
    }
    private static RegistryKey UserRegistry() => RegistryKey.OpenBaseKey(RegistryHive.CurrentUser,
        Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Registry32);
    private static string Quote(string value) => "'" + value.Replace("'", "''") + "'";
    private static async Task<string> RunPowerShell(string command)
    {
        var script = "$ErrorActionPreference='Stop'; try { " + command + " } catch { [Console]::Error.WriteLine($_.Exception.Message); exit 1 }";
        // Use native Windows PowerShell even when the viewer itself is a 32-bit process.
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var system = Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess ? "Sysnative" : "System32";
        var start = new ProcessStartInfo(Path.Combine(windows, system, @"WindowsPowerShell\v1.0\powershell.exe")) {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true
        };
        start.Environment["PSModulePath"] = Path.Combine(windows, @"System32\WindowsPowerShell\v1.0\Modules") + ";" + Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"WindowsPowerShell\Modules");
        start.ArgumentList.Add("-NoProfile"); start.ArgumentList.Add("-NonInteractive");
        start.ArgumentList.Add("-EncodedCommand"); start.ArgumentList.Add(Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(script)));
        using var process = Process.Start(start) ?? throw new IOException("Could not start Windows package registration.");
        var error = process.StandardError.ReadToEndAsync();
        var output = process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        var result = await output;
        if (process.ExitCode != 0) throw new InvalidOperationException(await error);
        await error;
        return result;
    }
    private static void NotifyShell() => SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(uint eventId, uint flags, IntPtr item1, IntPtr item2);
}
