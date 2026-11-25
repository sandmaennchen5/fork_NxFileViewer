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
using static Emignatik.NxFileViewer.Services.FileAssociations.ExplorerRegistration;

namespace Emignatik.NxFileViewer.Services.FileAssociations;

[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public static class ExplorerIntegration
{
    public const string ClassId = "{872F5F52-BC53-4A69-B1D1-8583B20CA4B1}";
    public const string PackageName = ExplorerRegistration.PackageName;
    public const string ConfigurationKey = ExplorerRegistration.ConfigurationKey;
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
        CleanupUnusedVersions();
    }

    public static void RemoveClassic()
    {
        using var user = UserRegistry();
        foreach (var type in new[] { "*", "Directory" })
            user.DeleteSubKeyTree(@"Software\Classes\" + type + @"\shell\NxFileViewerFork", false);
        user.DeleteSubKeyTree(@"Software\Classes\CLSID\" + ClassId, false);
        NotifyShell();
        CleanupUnusedVersions();
    }

    public static async Task InstallModern(string packagePath)
    {
        await ExplorerRegistration.InstallAsync(packagePath, Localization.LocalizationManager.Instance.Current.Keys.CultureName);
        CleanupUnusedVersions();
    }

    public static Task<ExplorerInstallStage> InstallModernFromRelease(bool includePrereleases, IProgress<ExplorerInstallStage>? progress = null) =>
        new ExplorerPackageService().InstallAsync(Architecture, includePrereleases,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NxFileViewerFork", "ExplorerDownloads"),
            new ModernDeployment(), progress, System.Threading.CancellationToken.None);

    public static async Task<Version?> InstalledModernVersionAsync(System.Threading.CancellationToken token = default)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)) return null;
        using var timeout = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        var result = await RunPowerShell("$package=Get-AppxPackage -Name '" + PackageName + "' | Sort-Object { [Version]$_.Version } -Descending | Select-Object -First 1; if ($package) { $package.Version.ToString() }", timeout.Token);
        return string.IsNullOrWhiteSpace(result) ? null : Version.Parse(result.Trim());
    }

    private sealed class ModernDeployment : IExplorerPackageDeployment
    {
        public Task<Version?> InstalledVersionAsync() => InstalledModernVersionAsync();
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
            RegisterTarget(Localization.LocalizationManager.Instance.Current.Keys.CultureName);
            CleanupUnusedVersions();
        }
    }
    public static void SynchronizeFeatures(Settings.IAppSettings settings) =>
        ExplorerRegistration.SynchronizeFeatures(settings.EnableNszPlugin, settings.EnablePackageOperations, settings.EnableNup);

    public static void UpdateLanguage(string language)
    {
        CleanupUnusedVersions();
        using var user = UserRegistry();
        using var config = user.OpenSubKey(ConfigurationKey, true);
        if (config?.GetValue("Executable") is not string executable ||
            !string.Equals(executable, CurrentExecutable(), StringComparison.OrdinalIgnoreCase)) return;
        config.SetValue("Language", language);
        NotifyShell();
    }

    public static async Task RemoveModern()
    {
        await RunPowerShell("Get-AppxPackage -Name '" + PackageName + "' | Remove-AppxPackage");
        NotifyShell();
        CleanupUnusedVersions();
    }

    public static IReadOnlyList<string> CleanupUnusedVersions()
    {
        try
        {
            var active = new List<string>();
            // Protect registrations in both views, including older 32-bit registrations.
            foreach (var view in Environment.Is64BitOperatingSystem ? new[] { RegistryView.Registry64, RegistryView.Registry32 } : new[] { RegistryView.Registry32 })
            {
                using var user = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, view);
                using var key = user.OpenSubKey(@"Software\Classes\CLSID\" + ClassId + @"\InprocServer32");
                if (key?.GetValue("") is string library && !string.IsNullOrWhiteSpace(library)) active.Add(library);
            }
            var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NxFileViewerFork", "ExplorerIntegration");
            return ExplorerVersionCleanup.RemoveUnused(root, active);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException) { return Array.Empty<string>(); }
    }
}
