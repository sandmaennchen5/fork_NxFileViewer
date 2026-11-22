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

namespace NxFileViewer.Avalonia;

public partial class MainWindow
{
    private sealed record UpdateRow(string Name, string Installed, string Latest, string Status, string ReleaseUrl);
    private CancellationTokenSource? _updateCancellation;

    private static JsonDocument ReadUpdateConfiguration(string resource)
    {
        using var stream = typeof(MainWindow).Assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidDataException("Missing update configuration.");
        return JsonDocument.Parse(stream);
    }

    private async void CheckUpdates(object? sender, RoutedEventArgs e)
    {
        if (_updateCancellation != null) return;
        using var cancellation = new CancellationTokenSource();
        _updateCancellation = cancellation;
        UpdateCheckButton.IsEnabled = false;
        UpdateCancelButton.IsEnabled = true;
        UpdateStatus.Text = _text.Update_Checking;
        try
        {
            using var config = ReadUpdateConfiguration("Viewer.ExternalSources.json");
            using var manifest = ReadUpdateConfiguration("Viewer.PackageManifest.json");
            var source = manifest.RootElement.GetProperty("UpdateSources").EnumerateArray()
                .Select(value => value.GetString()).FirstOrDefault(value => value?.StartsWith("github:", StringComparison.Ordinal) == true);
            var applicationRepository = source?[7..] ?? config.RootElement.GetProperty("FirmwareHashes").GetProperty("Repository").GetString()!;
            var components = new[]
            {
                (Name: "NxFileViewer Fork · Avalonia", Repository: applicationRepository, Path: Environment.ProcessPath, IsApplication: true),
                (Name: "NxNandManager", Repository: config.RootElement.GetProperty("NandRepository").GetString()!, Path: NandGuiPath.Text, IsApplication: false),
                (Name: "nicoboss/nsz", Repository: config.RootElement.GetProperty("NszRepository").GetString()!, Path: NszGuiPath.Text, IsApplication: false),
                (Name: "nscb rust", Repository: config.RootElement.GetProperty("RustPackageRepository").GetString()!, Path: RustBackendPath.Text, IsApplication: false),
                (Name: "NSCB Desktop", Repository: config.RootElement.GetProperty("RustDesktopRepository").GetString()!, Path: RustDesktopPath.Text, IsApplication: false)
            };
            var rows = new List<UpdateRow>();
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("NxFileViewerFork-Avalonia-Updater/1.0");
            foreach (var component in components)
            {
                cancellation.Token.ThrowIfCancellationRequested();
                if (!Regex.IsMatch(component.Repository, @"^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$"))
                    throw new InvalidDataException("Invalid update repository.");
                var releasePage = "https://github.com/" + component.Repository + "/releases";
                var installed = component.IsApplication
                    ? typeof(MainWindow).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0]
                    : ReadInstalledVersion(component.Path);
                try
                {
                    var endpoint = "https://api.github.com/repos/" + component.Repository +
                        (component.IsApplication ? "/releases?per_page=30" : "/releases/latest");
                    using var response = await client.GetAsync(endpoint, cancellation.Token);
                    response.EnsureSuccessStatusCode();
                    await using var body = await response.Content.ReadAsStreamAsync(cancellation.Token);
                    using var json = await JsonDocument.ParseAsync(body, cancellationToken: cancellation.Token);
                    var releases = component.IsApplication ? json.RootElement.EnumerateArray().ToArray() : new[] { json.RootElement };
                    var architecture = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
                    var release = releases.FirstOrDefault(item => !item.GetProperty("draft").GetBoolean() && (_preferences.IncludePreReleases || !item.GetProperty("prerelease").GetBoolean()) &&
                        (!component.IsApplication || item.GetProperty("assets").EnumerateArray().Any(asset =>
                        {
                            var name = asset.GetProperty("name").GetString()!;
                            return name.Contains("Avalonia", StringComparison.OrdinalIgnoreCase) &&
                                !name.Contains("LogViewer", StringComparison.OrdinalIgnoreCase) &&
                                name.EndsWith("_" + architecture + ".zip", StringComparison.OrdinalIgnoreCase);
                        })));
                    if (release.ValueKind == JsonValueKind.Undefined)
                    {
                        rows.Add(new UpdateRow(component.Name, installed, "—", _text.Update_Current, releasePage));
                        continue;
                    }
                    var latest = release.GetProperty("tag_name").GetString()!;
                    var newer = ViewerVersion.TryParse(installed, out var before) && ViewerVersion.TryParse(latest, out var after)
                        ? after.CompareTo(before) > 0 : !string.Equals(installed.TrimStart('v'), latest.TrimStart('v'), StringComparison.OrdinalIgnoreCase);
                    var status = installed == _text.Component_NotInstalled ? _text.Component_NotInstalled :
                        newer ? string.Format(_text.Update_Available, latest) : _text.Update_Current;
                    rows.Add(new UpdateRow(component.Name, installed, latest, status, releasePage));
                }
                catch (Exception ex) when (ex is HttpRequestException or JsonException or IOException)
                {
                    rows.Add(new UpdateRow(component.Name, installed, "—", ex.Message, releasePage));
                }
                if (!_closed) UpdateResults.ItemsSource = rows.ToArray();
            }
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
            }
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

    private void OpenUpdateRelease(object? sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.DataContext is not UpdateRow row) return;
        try { using var process = Process.Start(new ProcessStartInfo(row.ReleaseUrl) { UseShellExecute = true }); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        { UpdateStatus.Text = ex.Message; }
    }
}
