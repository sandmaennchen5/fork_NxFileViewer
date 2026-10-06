using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Emignatik.NxFileViewer.Localization;
using Emignatik.NxFileViewer.Services.BackgroundTask;
using Emignatik.NxFileViewer.Services.BackgroundTask.RunnableImpl;
using Emignatik.NxFileViewer.Settings;
using Emignatik.NxFileViewer.Utils.MVVM;
using Emignatik.NxFileViewer.Utils.MVVM.Commands;
using Microsoft.Extensions.Logging;

namespace Emignatik.NxFileViewer.Services.Updates;

public sealed class ViewerUpdateActions : NotifyPropertyChangedBase
{
    private readonly ViewerUpdateService _service;
    private readonly IAppSettings _settings;
    private readonly IMainBackgroundTaskRunnerService _background;
    private readonly ILogger<ViewerUpdateActions> _logger;
    private ViewerRelease? _release;
    private bool _busy;
    private string _status = "";
    public ViewerUpdateActions(ViewerUpdateService service, IAppSettings settings,
        IMainBackgroundTaskRunnerService background, ILogger<ViewerUpdateActions> logger)
    {
        _service = service; _settings = settings; _background = background; _logger = logger;
        CheckCommand = new RelayCommand(() => _ = CheckAsync(false), () => !_busy);
        InstallCommand = new RelayCommand(Install, () => !_busy && !_background.IsRunning && _release != null && (!_release.IsPrerelease || _settings.IncludeViewerPrereleases));
        _background.PropertyChanged += (_, _) => InstallCommand.TriggerCanExecuteChanged(true);
        _settings.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName != nameof(IAppSettings.IncludeViewerPrereleases)) return;
            _release = null;
            Status = "";
            InstallCommand.TriggerCanExecuteChanged(true);
        };
    }
    public RelayCommand CheckCommand { get; }
    public RelayCommand InstallCommand { get; }
    public string Status { get => _status; private set { _status = value; NotifyPropertyChanged(); } }
    private void SetBusy(bool value)
    {
        _busy = value;
        CheckCommand.TriggerCanExecuteChanged();
        InstallCommand.TriggerCanExecuteChanged();
    }
    public async Task CheckAsync(bool automatic)
    {
        if (_busy || (automatic && !_settings.CheckViewerUpdatesOnStartup)) return;
        SetBusy(true);
        Status = LocalizationManager.Instance.Current.Keys.Update_Checking;
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var architecture = RuntimeInformation.ProcessArchitecture switch
            { Architecture.X64 => "x64", Architecture.X86 => "x86", _ => throw new NotSupportedException("No release package for this architecture.") };
            var includePrereleases = _settings.IncludeViewerPrereleases;
            _release = await _service.CheckAsync(Assembly.GetExecutingAssembly().GetName().Version!, architecture, timeout.Token, includePrereleases);
            if (includePrereleases != _settings.IncludeViewerPrereleases) { _release = null; Status = ""; return; }
            Status = _release == null ? LocalizationManager.Instance.Current.Keys.Update_Current :
                string.Format(LocalizationManager.Instance.Current.Keys.Update_Available, ReleaseLabel(_release));
            if (!automatic) MessageBox.Show(Application.Current.MainWindow, Status, "NxFileViewer Update", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _release = null;
            Status = LocalizationManager.Instance.Current.Keys.Update_Failed;
            _logger.LogWarning(ex, "Viewer update check failed.");
            if (!automatic) MessageBox.Show(Status + Environment.NewLine + ex.Message, "NxFileViewer Update", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally { SetBusy(false); }
    }
    private static string ReleaseLabel(ViewerRelease release) => release.DisplayVersion +
        (release.IsPrerelease ? " (" + LocalizationManager.Instance.Current.Keys.Update_Prerelease + ")" : "");
    private async void Install()
    {
        if (!InstallCommand.CanExecute(null)) return;
        var release = _release!;
        if (MessageBox.Show(Application.Current.MainWindow,
            string.Format(LocalizationManager.Instance.Current.Keys.Update_Confirm, ReleaseLabel(release)),
            "NxFileViewer Update", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        SetBusy(true);
        var main = Application.Current.MainWindow;
        try
        {
            var target = Path.Combine(AppContext.BaseDirectory, "NxFileViewer.exe");
            if (!string.Equals(Environment.ProcessPath, target, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Automatic installation requires the portable NxFileViewer.exe release.");
            var prepared = await _background.RunAsync(new RunnableRelay<PreparedViewerUpdate>((progress, token) =>
            {
                progress.SetText(LocalizationManager.Instance.Current.Keys.Update_Downloading);
                return _service.PrepareAsync(release, AppContext.BaseDirectory, token).GetAwaiter().GetResult();
            }) { SupportsCancellation = true });
            main.IsEnabled = false;
            Status = LocalizationManager.Instance.Current.Keys.Update_Installing;
            var script = Path.Combine(prepared.Directory, "install.ps1");
            await using (var input = Assembly.GetExecutingAssembly().GetManifestResourceStream("ViewerUpdate.InstallUpdate.ps1")!)
            await using (var output = File.Create(script)) await input.CopyToAsync(output);
            var hash = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(prepared.Executable)));
            var plan = Path.Combine(prepared.Directory, "plan.json");
            await File.WriteAllTextAsync(plan, JsonSerializer.Serialize(new { Target = target, ProcessId = Environment.ProcessId, ExecutableHash = hash }));
            var start = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe"))
            { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = prepared.Directory };
            foreach (var arg in new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", script, "-PlanPath", plan }) start.ArgumentList.Add(arg);
            using var helper = Process.Start(start) ?? throw new IOException("Could not start the update helper.");
            var ready = Path.Combine(prepared.Directory, "ready");
            var deadline = DateTime.UtcNow.AddSeconds(15);
            while (!File.Exists(ready))
            {
                if (helper.HasExited || DateTime.UtcNow >= deadline)
                {
                    if (!helper.HasExited) helper.Kill(entireProcessTree: true);
                    throw new IOException("Update helper could not initialize. The current application was retained.");
                }
                await Task.Delay(100);
            }
            // Closing normally saves settings. The helper waits for this process to exit.
            Application.Current.Shutdown();
        }
        catch (OperationCanceledException) { Status = LocalizationManager.Instance.Current.Keys.Update_Cancelled; }
        catch (Exception ex)
        {
            Status = LocalizationManager.Instance.Current.Keys.Update_Failed;
            _logger.LogError(ex, "Viewer installation failed.");
            MessageBox.Show(Status + Environment.NewLine + ex.Message, "NxFileViewer Update", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { main.IsEnabled = true; SetBusy(false); }
    }
}
