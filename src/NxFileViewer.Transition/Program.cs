using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Emignatik.NxFileViewer.Services.Updates;

namespace NxFileViewer.Transition;

public static class Program
{
    [STAThread]
    public static void Main()
    {
        var app = new Application();
        var text = new TextBlock { Text = "NxFileViewer wird auf NxFileViewer Fork umgestellt … / Upgrading to NxFileViewer Fork …", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(20) };
        var retry = new Button { Content = "Erneut versuchen / Retry", Margin = new Thickness(20), IsEnabled = false };
        var panel = new StackPanel(); panel.Children.Add(text); panel.Children.Add(retry);
        var window = new Window { Title = "NxFileViewer Fork – Upgrade", Width = 560, Height = 240, Content = panel, WindowStartupLocation = WindowStartupLocation.CenterScreen };
        var busy = true;
        window.Closing += (_, e) => { if (busy) e.Cancel = true; };
        async Task Run()
        {
            busy = true; retry.IsEnabled = false;
            try
            {
                // Older update helpers regard exiting in their first two seconds as a failed launch.
                await Task.Delay(3500);
                var root = Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory);
                var current = Environment.ProcessPath ?? throw new IOException("Executable path unavailable.");
                if (!string.Equals(Path.GetDirectoryName(current), root, StringComparison.OrdinalIgnoreCase)) throw new IOException("Extract the transition EXE beside the existing application first.");
                var architecture = RuntimeInformation.ProcessArchitecture switch { Architecture.X64 => "x64", Architecture.X86 => "x86", _ => throw new PlatformNotSupportedException() };
                var bundled = Assembly.GetExecutingAssembly().GetCustomAttributes<AssemblyMetadataAttribute>().Any(a => a.Key == "ViewerSelfContained" && string.Equals(a.Value, "true", StringComparison.OrdinalIgnoreCase));
                using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
                client.DefaultRequestHeaders.UserAgent.ParseAdd("NxFileViewerFork-Transition/1.0");
                text.Text = "Lade das neue Programmpaket … / Downloading the new application package …";
                var release = await TransitionRelease.ResolveAsync(client, architecture, bundled, CancellationToken.None, TransitionRelease.IncludePrereleases(root));
                var prepared = await new ViewerUpdateService(client).PrepareAsync(release, root, CancellationToken.None);
                if (prepared.EntryPoint.Equals("NxFileViewer.exe", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Transition must install the renamed application.");
                text.Text = "Übernehme Daten und installiere NxFileViewer Fork … / Migrating data and installing NxFileViewer Fork …";
                await Task.Run(() => LegacyDataMigration.Move(root));
                var script = Path.Combine(prepared.Directory, "install.ps1");
                using (var input = Assembly.GetExecutingAssembly().GetManifestResourceStream("ViewerUpdate.InstallUpdate.ps1")!)
                using (var output = File.Create(script)) await input.CopyToAsync(output);
                var plan = Path.Combine(prepared.Directory, "plan.json");
                await File.WriteAllTextAsync(plan, JsonSerializer.Serialize(new {
                    SchemaVersion = 2, ApplicationId = ViewerPackageLayout.ApplicationId, TargetRoot = root,
                    Target = Path.Combine(root, prepared.EntryPoint), PreviousTarget = current, ProcessId = Environment.ProcessId,
                    ProcessStartTicks = Process.GetCurrentProcess().StartTime.ToUniversalTime().Ticks,
                    OriginalExecutableHash = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(current))),
                    Files = prepared.Files, Directories = prepared.Directories, RemoveDirectories = prepared.RemoveDirectories
                }));
                var start = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe")) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = prepared.Directory };
                foreach (var arg in new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", script, "-PlanPath", plan }) start.ArgumentList.Add(arg);
                using var helper = Process.Start(start) ?? throw new IOException("Update helper unavailable.");
                var deadline = DateTime.UtcNow.AddSeconds(60);
                while (!File.Exists(Path.Combine(prepared.Directory, "ready")))
                {
                    if (helper.HasExited || DateTime.UtcNow >= deadline)
                    {
                        if (!helper.HasExited) helper.Kill(true);
                        var error = Path.Combine(prepared.Directory, "error.txt");
                        throw new IOException(File.Exists(error) ? await File.ReadAllTextAsync(error) : "Update helper failed to initialize.");
                    }
                    await Task.Delay(100);
                }
                busy = false; app.Shutdown();
            }
            catch (Exception ex)
            {
                busy = false; retry.IsEnabled = true;
                text.Text = "Upgrade nicht abgeschlossen / Upgrade incomplete:\n" + ex.Message + "\n\nInternetverbindung prüfen und erneut versuchen. / Check the connection and retry.";
            }
        }
        retry.Click += async (_, _) => await Run();
        window.ContentRendered += async (_, _) => await Run();
        app.Run(window);
    }
}