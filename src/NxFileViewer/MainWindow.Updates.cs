using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Emignatik.NxFileViewer.Services.Updates;
using NxFileViewer.Avalonia.Updates;
using Emignatik.NxFileViewer.Services;

namespace NxFileViewer.Avalonia;

public partial class MainWindow
{
    private sealed record UpdateRow(string Installed, string Status)
    {
        public AvaloniaRelease? ApplicationRelease { get; init; }
        public string InstallText { get; init; } = "";
        public bool CanInstall => ApplicationRelease != null;
        public bool Available { get; init; }
        public bool ShowStatus => !string.Equals(Installed, Status, StringComparison.Ordinal);
    }
    private static string CurrentUpdateRuntime => (OperatingSystem.IsWindows() ? "win-" : OperatingSystem.IsLinux() ? "linux-" : OperatingSystem.IsMacOS() ? "osx-" : throw new PlatformNotSupportedException()) + RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
    private CancellationTokenSource? _updateCancellation;
    private bool _applicationUpdatePending;
    private const string ApplicationUpdateName = "NxFileViewer Fork · Avalonia";
    private readonly Dictionary<string, string?> _pluginUpdateStatuses = new();
    private readonly Dictionary<string, TextBlock> _pluginUpdateMessages = new();
    private readonly HashSet<string> _availablePluginUpdates = new();
    private bool _applicationUpdateAvailable;

    private void RefreshHomeUpdateNotice()
    {
        var extras = _availablePluginUpdates.Count > 0 ||
            UpdateTitleDbAvailable.Classes.Contains("update-available") ||
            UpdateFirmwareAvailable.Classes.Contains("update-available");
        HomeUpdateText.IsVisible = _applicationUpdateAvailable;
        HomeInstallUpdate.IsVisible = _applicationUpdateAvailable;
        HomeExtraUpdateText.IsVisible = extras;
        HomeExtraUpdateText.Text = _text.CultureName.StartsWith("de", StringComparison.OrdinalIgnoreCase)
            ? "Updates für Zusätze und Plugins verfügbar." : "Updates for add-ons and plugins are available.";
        HomeUpdatePanel.IsVisible = _applicationUpdateAvailable || extras;
        HomeUpdatesButton.Classes.Set("update-available", HomeUpdatePanel.IsVisible);
    }

    private async Task CheckDataUpdatesAsync(GitHubUpdateRequests requests, bool automatic, CancellationToken token)
    {
        async Task Check(TextBlock label, Func<Task<bool>> check)
        {
            try
            {
                var available = await check();
                if (!_closed) SetUpdateMessage(label, available ? _text.Component_UpdateAvailable : null, available);
            }
            catch (Exception error) when (error is IOException or InvalidDataException or HttpRequestException or JsonException or InvalidOperationException)
            {
                if (!_closed) SetUpdateMessage(label, error.Message);
            }
        }

        var titleDb = TitleDbSources.Resolve(_preferences.TitleDbRepository);
        var region = TitleDbRegion;
        var titleCheck = Check(UpdateTitleDbAvailable, async () =>
        {
            foreach (var catalog in new[] { region, TitleDbSources.FallbackRegion }.Distinct())
            {
                var response = await requests.GetAsync(titleDb.ApiUrl(catalog + ".json"), automatic, token);
                using var json = JsonDocument.Parse(response.Body);
                var path = TitleDbSources.CatalogPath(ApplicationDataPaths.TitleDb(ViewerDataDirectory.Root), titleDb, catalog);
                if (!RepositoryMirrors.MatchesGitBlob(path, json.RootElement.GetProperty("sha").GetString()!)) return true;
            }
            return false;
        });
        var firmwareCheck = Check(UpdateFirmwareAvailable, async () =>
        {
            var mirrors = new RepositoryMirrors(requests: requests, automatic: automatic);
            var entries = await mirrors.FirmwareListingAsync(ExternalSources.Current.FirmwareHashes, token);
            var folder = ApplicationDataPaths.FirmwareHashes(ViewerDataDirectory.Root);
            return entries.Any(entry => !RepositoryMirrors.MatchesGitBlob(Path.Combine(folder, entry.Name), entry.Sha));
        });
        await Task.WhenAll(titleCheck, firmwareCheck);
        if (!_closed) RefreshHomeUpdateNotice();
    }

    private static JsonDocument ReadUpdateConfiguration(string resource)
    {
        using var stream = typeof(MainWindow).Assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidDataException("Missing update configuration.");
        return JsonDocument.Parse(stream);
    }

    private static string ApplicationUpdateRepository()
    {
        using var manifest = ReadUpdateConfiguration("Viewer.PackageManifest.json");
        var source = manifest.RootElement.GetProperty("UpdateSources").EnumerateArray()
            .Select(value => value.GetString())
            .FirstOrDefault(value => value?.StartsWith("github:", StringComparison.Ordinal) == true);
        return source?[7..] ?? ExternalSources.Current.FirmwareHashes.Repository;
    }

    private string InstalledUpdateLabel(string installed) =>
        installed == _text.Component_NotInstalled || installed == _text.Component_CustomVersion
            ? installed.TrimEnd('.')
            : "Version " + installed.TrimStart('v');

    private void InitializeApplicationUpdateRow()
    {
        var version = typeof(MainWindow).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
            .InformationalVersion.Split('+')[0];
        SetUpdateHeading(ApplicationUpdateHeading, ApplicationUpdateName, InstalledUpdateLabel(version));
        ApplicationUpdateActions.Children.Add(CreateReleaseButton(ApplicationUpdateRepository()));
        UpdateStatus.Text = _text.Status_Ready;
    }

    private void RefreshUpdateDataSummaries()
    {
        var titleDb = TitleDbSources.Resolve(_preferences.TitleDbRepository);
        var region = TitleDbRegion;
        string CatalogStatus(string catalogRegion)
        {
            var path = TitleDbSources.CatalogPath(ApplicationDataPaths.TitleDb(ViewerDataDirectory.Root), titleDb, catalogRegion);
            return File.Exists(path)
                ? string.Format(_text.DataUpdate_TitleCatalogDate, catalogRegion, File.GetLastWriteTime(path).ToString("g",
                    System.Globalization.CultureInfo.GetCultureInfo(_text.CultureName)))
                : string.Format(_text.DataUpdate_TitleCatalogMissing, catalogRegion);
        }

        UpdateTitleDbSummary.Text = $"TitleDB - {titleDb.Repository}{Environment.NewLine}{CatalogStatus(region)}";
        if (region != TitleDbSources.FallbackRegion)
            UpdateTitleDbSummary.Text += Environment.NewLine + CatalogStatus(TitleDbSources.FallbackRegion);

        var firmwareVersion = InstalledDataStatus.LocalFirmwareVersion(ViewerDataDirectory.Root);
        var firmwareStatus = firmwareVersion == null ? _text.DataUpdate_NoLocalFirmware :
            string.Format(_text.DataUpdate_LocalFirmwareVersion, firmwareVersion);
        UpdateFirmwareSummary.Text = $"{_text.DataUpdate_Firmware} - {firmwareStatus}";
        RefreshUpdateExplorerSummary();
    }

    private async void UpdateFirmwareHashes(object? sender, RoutedEventArgs e)
    {
        if (_updateCancellation != null) return;
        var save = sender is Button { Tag: "Save" };
        using var workspaceJob = LockWorkspace(SettingsTab);
        using var cancellation = new CancellationTokenSource();
        _updateCancellation = cancellation;
        UpdateFirmwareActions.IsEnabled = UpdateCheckButton.IsEnabled = false;
        UpdateCancelButton.IsEnabled = true;
        UpdateStatus.Text = _text.DataUpdate_Working;
        try
        {
            var manifests = await new RepositoryMirrors().FirmwareManifestsAsync(
                ExternalSources.Current.FirmwareHashes, cancellation.Token);
            if (save)
                await Task.Run(() => Emignatik.NxFileViewer.Services.Integrity.FirmwareReferenceStore.Save(
                    manifests, ViewerDataDirectory.Root, cancellation.Token), cancellation.Token);
            if (_closed) return;
            RefreshUpdateDataSummaries();
            if (save)
            {
                SetUpdateMessage(UpdateFirmwareAvailable, null);
                RefreshHomeUpdateNotice();
            }
            UpdateStatus.Text = string.Format(save ? _text.DataUpdate_FirmwareSaved : _text.DataUpdate_FirmwareChecked, manifests.Count);
            var version = InstalledDataStatus.HighestFirmwareVersion(manifests.Values);
            if (!save && version != null)
                UpdateStatus.Text += " " + string.Format(_text.DataUpdate_OnlineFirmwareVersion, version);
        }
        catch (OperationCanceledException)
        {
            if (!_closed) UpdateStatus.Text = _text.Update_Cancelled;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or HttpRequestException or JsonException or UnauthorizedAccessException)
        {
            if (!_closed) UpdateStatus.Text = _text.Update_Failed + " " + ex.Message;
        }
        finally
        {
            _updateCancellation = null;
            if (!_closed)
            {
                UpdateFirmwareActions.IsEnabled = UpdateCheckButton.IsEnabled = true;
                UpdateCancelButton.IsEnabled = false;
            }
        }
    }

    private void RefreshUpdateExplorerSummary()
    {
        if (!OperatingSystem.IsWindows()) return;
        try
        {
            using var user = Emignatik.NxFileViewer.Services.FileAssociations.ExplorerRegistration.UserRegistry();
            using var config = user.OpenSubKey(Emignatik.NxFileViewer.Services.FileAssociations.ExplorerRegistration.ConfigurationKey);
            var active = config?.GetValue("Executable") is string executable &&
                string.Equals(executable, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase);
            UpdateExplorerSummary.IsVisible = active;
            UpdateExplorerSummary.Text = _text.CultureName.StartsWith("de", StringComparison.OrdinalIgnoreCase)
                ? "Explorer-Kontextmenü - für Avalonia registriert"
                : "Explorer context menu - registered for Avalonia";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            UpdateExplorerSummary.IsVisible = false;
        }
    }

    private static void SetUpdateHeading(TextBlock heading, string name, string suffix)
    {
        heading.Inlines!.Clear();
        heading.Inlines.Add(new global::Avalonia.Controls.Documents.Run(name) { FontWeight = global::Avalonia.Media.FontWeight.Bold });
        heading.Inlines.Add(new global::Avalonia.Controls.Documents.Run(" - " + suffix));
    }

    private async void CheckUpdates(object? sender, RoutedEventArgs e)
    {
        if (_updateCancellation != null) return;
        using var workspaceJob = ReferenceEquals(sender, UpdateCheckButton) ? LockWorkspace(SettingsTab) : null;
        using var cancellation = new CancellationTokenSource();
        _updateCancellation = cancellation;
        UpdateCheckButton.IsEnabled = false;
        UpdateCancelButton.IsEnabled = true;
        HomeInstallUpdate.IsEnabled = false;
        UpdateStatus.Text = _text.Update_Checking;
        try
        {
            var config = ExternalSources.Current;
            var components = new[]
            {
                (Name: ApplicationUpdateName, Repository: ApplicationUpdateRepository(), Path: Environment.ProcessPath, IsApplication: true),
                (Name: "NxNandManager", Repository: config.NandRepository, Path: NandGuiPath.Text, IsApplication: false),
                (Name: "nicoboss/nsz", Repository: config.NszRepository, Path: NszCliPath.Text, IsApplication: false),
                (Name: "nscb_rust", Repository: config.RustPackageRepository, Path: RustBackendPath.Text, IsApplication: false),
                (Name: "NSCB Desktop", Repository: config.RustDesktopRepository, Path: RustDesktopPath.Text, IsApplication: false)
            };
            var requests = GitHubUpdateRequests.For(null);
            var automatic = ReferenceEquals(sender, this);
            foreach (var component in components)
            {
                cancellation.Token.ThrowIfCancellationRequested();
                if (!Regex.IsMatch(component.Repository, @"^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$"))
                    throw new InvalidDataException("Invalid update repository.");
                var installed = component.IsApplication
                    ? typeof(MainWindow).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0]
                    : ReadInstalledVersion(component.Path);
                UpdateRow row;
                try
                {
                    var endpoint = "https://api.github.com/repos/" + component.Repository +
                        (component.IsApplication ? "/releases?per_page=30" : "/releases/latest");
                    var response = await requests.GetAsync(endpoint, automatic, cancellation.Token);
                    using var json = JsonDocument.Parse(response.Body);
                    var releases = component.IsApplication ? json.RootElement.EnumerateArray().ToArray() : new[] { json.RootElement };
                    var release = releases.OrderByDescending(item => AvaloniaAppUpdater.ParseVersion(item.GetProperty("tag_name").GetString() ?? "")).FirstOrDefault(item => !item.GetProperty("draft").GetBoolean() && (_preferences.IncludePreReleases || !item.GetProperty("prerelease").GetBoolean()) &&
                        (!component.IsApplication || AvaloniaAppUpdater.SelectRelease(item, component.Repository, CurrentUpdateRuntime, ViewerDistribution.IsSelfContained) != null));
                    if (release.ValueKind == JsonValueKind.Undefined)
                    {
                        row = new UpdateRow(installed, _text.Update_Current);
                    }
                    else
                    {
                        var latest = release.GetProperty("tag_name").GetString()!;
                        var newer = component.IsApplication ? AvaloniaAppUpdater.IsNewer(installed, latest)
                            : ViewerVersion.TryParse(installed, out var before) && ViewerVersion.TryParse(latest, out var after)
                                ? after.CompareTo(before) > 0 : !string.Equals(installed.TrimStart('v'), latest.TrimStart('v'), StringComparison.OrdinalIgnoreCase);
                        var status = installed == _text.Component_NotInstalled ? _text.Component_NotInstalled :
                            newer ? string.Format(_text.Update_Available, latest) : _text.Update_Current;
                        row = new UpdateRow(installed, status)
                        {
                            ApplicationRelease = component.IsApplication && newer ? AvaloniaAppUpdater.SelectRelease(release, component.Repository, CurrentUpdateRuntime, ViewerDistribution.IsSelfContained) : null,
                            Available = newer && installed != _text.Component_NotInstalled,
                            InstallText = _text.Update_Install
                        };
                    }
                }
                catch (Exception ex) when (ex is HttpRequestException or JsonException or IOException)
                {
                    row = new UpdateRow(installed, ex.Message);
                }
                if (_closed) return;
                var statusMessage = row.ShowStatus ? row.Status : null;
                if (component.IsApplication)
                {
                    ApplicationInstallUpdate.DataContext = row;
                    ApplicationInstallUpdate.Content = row.InstallText;
                    ApplicationInstallUpdate.IsVisible = row.CanInstall;
                    SetUpdateMessage(ApplicationUpdateMessage, statusMessage, row.Available);
                    _applicationUpdateAvailable = row.CanInstall;
                    HomeUpdateText.Text = "NxFileViewer: " + row.Status;
                    HomeInstallUpdate.DataContext = row;
                    HomeInstallUpdate.Content = row.InstallText;
                }
                else
                {
                    _pluginUpdateStatuses[component.Name] = statusMessage;
                    if (_pluginUpdateMessages.TryGetValue(component.Name, out var message))
                    {
                        if (row.Available) _availablePluginUpdates.Add(component.Name);
                        else _availablePluginUpdates.Remove(component.Name);
                        SetUpdateMessage(message, statusMessage, row.Available);
                    }
                }
                RefreshHomeUpdateNotice();
            }
            await CheckDataUpdatesAsync(requests, automatic, cancellation.Token);
            if (!_closed) UpdateStatus.Text = _text.Status_Ready;
        }
        catch (OperationCanceledException)
        {
            if (!_closed) UpdateStatus.Text = _text.Update_Cancelled;
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidOperationException)
        {
            if (!_closed) UpdateStatus.Text = ex.Message;
        }
        finally
        {
            _updateCancellation = null;
            if (!_closed)
            {
                UpdateCheckButton.IsEnabled = true;
                UpdateCancelButton.IsEnabled = false;
                HomeInstallUpdate.IsEnabled = true;
            }
        }
    }

    private async void InstallApplicationUpdate(object? sender, RoutedEventArgs e)
    {
        if (_updateCancellation != null || (sender as Button)?.DataContext is not UpdateRow { ApplicationRelease: { } release }) return;
        Workspace.SelectedItem = SettingsTab;
        SettingsPages.SelectedItem = UpdatesTab;
        if (ViewerDataDirectory.IsAppImage)
        {
            UpdateStatus.Text = _text.CultureName.StartsWith("de", StringComparison.OrdinalIgnoreCase)
                ? "AppImage-Updates sind über GitHub Releases verfügbar. Lade dort das neue AppImage herunter."
                : "AppImage updates are available via GitHub Releases. Download the new AppImage there.";
            return;
        }
        if (_batchCancellation != null || _extractionCancellation != null || _firmwareCancellation != null || _mtpCancellation != null ||
            _nandCancellation != null || _ncaContentsCancellation != null || _renameBatchCancellation != null || _fileVerificationCancellation != null)
        {
            UpdateStatus.Text = _text.CultureName.StartsWith("de") ? "Bitte laufende Aufgaben vor dem Update abschließen." : "Finish running tasks before updating.";
            return;
        }
        if (!typeof(MainWindow).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Any(value => value.Key == "ViewerSingleFile" && bool.TryParse(value.Value, out var single) && single))
        {
            UpdateStatus.Text = _text.CultureName.StartsWith("de") ? "Automatische Updates benötigen die veröffentlichte Einzeldatei-Version." : "Automatic updates require the published single-file application.";
            return;
        }
        using var cancellation = new CancellationTokenSource();
        _updateCancellation = cancellation;
        _applicationUpdatePending = true;
        UpdateCheckButton.IsEnabled = false; UpdateCancelButton.IsEnabled = true;
        using var workspaceJob = LockWorkspace(SettingsTab);
        try
        {
            UpdateStatus.Text = _text.Update_Downloading;
            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("NxFileViewerFork-Avalonia-Updater/1.0");
            var stage = await AvaloniaAppUpdater.DownloadAsync(client, release, AppContext.BaseDirectory, CurrentUpdateRuntime, ViewerDistribution.IsSelfContained, cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            var job = AvaloniaAppUpdater.CreateJob(stage, Environment.ProcessPath ?? throw new IOException("Executable path unavailable."));
            UpdateStatus.Text = _text.Update_Installing;
            AvaloniaAppUpdater.StartWorker(job);
            if (global::Avalonia.Application.Current?.ApplicationLifetime is global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop) desktop.Shutdown();
        }
        catch (OperationCanceledException) { UpdateStatus.Text = _text.Update_Cancelled; }
        catch (Exception error) when (error is HttpRequestException or IOException or InvalidDataException or UnauthorizedAccessException or JsonException or InvalidOperationException or System.ComponentModel.Win32Exception)
        { UpdateStatus.Text = error.Message; }
        finally
        {
            _applicationUpdatePending = false;
            _updateCancellation = null;
            if (!_closed) { UpdateCheckButton.IsEnabled = true; UpdateCancelButton.IsEnabled = false; }
        }
    }

    private string ReadInstalledVersion(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return _text.Component_NotInstalled;
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        for (var depth = 0; depth < 3 && directory != null; depth++, directory = Path.GetDirectoryName(directory))
        {
            var state = Path.Combine(directory, "installation.json");
            if (!File.Exists(state)) continue;
            try
            {
                using var json = JsonDocument.Parse(File.ReadAllText(state));
                if (json.RootElement.TryGetProperty("Version", out var value)) return value.GetString() ?? _text.Component_CustomVersion;
            }
            catch (Exception ex) when (ex is JsonException or IOException) { }
        }
        return FileVersionInfo.GetVersionInfo(path).ProductVersion ?? _text.Component_CustomVersion;
    }

    private void CancelUpdateCheck(object? sender, RoutedEventArgs e) => _updateCancellation?.Cancel();

    private Button CreateReleaseButton(string repository)
    {
        var button = new Button { Content = "GitHub Releases…", Tag = "https://github.com/" + repository + "/releases" };
        button.Click += OpenUpdateRelease;
        return button;
    }

    private static void SetUpdateMessage(TextBlock text, string? message, bool available = false)
    {
        text.Text = message;
        text.IsVisible = !string.IsNullOrWhiteSpace(message);
        text.Classes.Set("update-available", available);
    }

    private void OpenUpdateRelease(object? sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not string url) return;
        try { using var process = Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        { UpdateStatus.Text = ex.Message; }
    }
}
