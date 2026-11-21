using System.Linq;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace NxFileViewer.Avalonia;

public partial class MainWindow
{
    private void InitializeInfo()
    {
        var assembly = typeof(MainWindow).Assembly;
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
            ?? assembly.GetName().Version?.ToString();
        ProgramVersionText.Text = "NxFileViewer Fork v" + version;
        Title = "NxFileViewer Fork v" + version + " · Avalonia";
        ProgramArchitectureText.Text = RuntimeInformation.ProcessArchitecture.ToString();
        ProgramRuntimeText.Text = assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Any(item => item.Key == "ViewerSelfContained" && item.Value == "true") ? _text.Info_WithRuntime : _text.Info_WithoutRuntime;
        CreditsText.Text = ReadInfoResource("Viewer.Credits");
        LicenseText.Text = ReadInfoResource("Viewer.License");
        ThirdPartyText.Text = ReadInfoResource("Viewer.ThirdPartyNotices");
        PollsInfoButton.Tag = ReadCommunityEndpoint("PollEndpoint");
        SupportIdentityText.Text = ReadSupportIdentity();
        CopySupportButton.IsEnabled = SearchSupportButton.IsEnabled = !string.IsNullOrWhiteSpace(SupportIdentityText.Text);
    }

    private void OpenInfoLink(object? sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is string url) Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
    private static string ReadCommunityEndpoint(string key)
    {
        var path = Path.Combine(System.AppContext.BaseDirectory, "NxFileViewerFork.settings.json");
        try
        {
            if (File.Exists(path))
            {
                using var settings = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
                if (settings.RootElement.TryGetProperty(key, out var entry) && ValidCommunityEndpoint(entry.GetString()) is { } url) return url;
            }
        }
        catch (System.Exception ex) when (ex is IOException or System.UnauthorizedAccessException or System.Text.Json.JsonException) { }
        using var defaults = ReadUpdateConfiguration("Viewer.DefaultSettings.json");
        return ValidCommunityEndpoint(defaults.RootElement.GetProperty("Settings").GetProperty(key).GetString()) ?? "https://github.com/sandmaennchen5/fork_NxFileViewer";
    }
    private static string? ValidCommunityEndpoint(string? value) => System.Uri.TryCreate(value, System.UriKind.Absolute, out var uri) && uri.Scheme == "https" && uri.UserInfo.Length == 0 ? uri.AbsoluteUri : null;
    private sealed record SupportIdentityCache(string Endpoint, string Id);
    private static string SupportIdentityCachePath => Path.Combine(System.AppContext.BaseDirectory, "Cache", "Avalonia", "support-id.json");
    private System.Threading.CancellationTokenSource? _supportIdentityCancellation;
    private async void RefreshSupportIdentity(object? sender, RoutedEventArgs e)
    {
        if (_supportIdentityCancellation != null) return;
        using var cancellation = new System.Threading.CancellationTokenSource(); _supportIdentityCancellation = cancellation; RefreshSupportButton.IsEnabled = false;
        try
        {
            var endpoint = ReadCommunityEndpoint("ReportEndpoint");
            var id = await Emignatik.NxFileViewer.Services.SupportIdentityClient.RefreshAsync(endpoint, cancellation.Token);
            var path = SupportIdentityCachePath; Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temporary = path + "." + System.Guid.NewGuid().ToString("N") + ".tmp";
            try { File.WriteAllText(temporary, System.Text.Json.JsonSerializer.Serialize(new SupportIdentityCache(endpoint, id))); File.Move(temporary, path, true); }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            if (!_closed) { SupportIdentityText.Text = id; CopySupportButton.IsEnabled = SearchSupportButton.IsEnabled = true; }
        }
        catch (System.Exception ex) when (ex is IOException or System.UnauthorizedAccessException or System.Net.Http.HttpRequestException or System.OperationCanceledException or System.Text.Json.JsonException)
        { if (!_closed) StatusText.Text = ex.Message; }
        finally { _supportIdentityCancellation = null; if (!_closed) RefreshSupportButton.IsEnabled = true; }
    }
    private static string? ReadSupportIdentity()
    {
        try
        {
            if (File.Exists(SupportIdentityCachePath))
            {
                var cached = System.Text.Json.JsonSerializer.Deserialize<SupportIdentityCache>(File.ReadAllText(SupportIdentityCachePath));
                if (cached?.Endpoint == ReadCommunityEndpoint("ReportEndpoint") && cached.Id is { } cachedId && System.Text.RegularExpressions.Regex.IsMatch(cachedId, "^NX-(?:[A-F0-9]{4}-){4}[A-F0-9]{4}$")) return cached.Id;
            }
            var path = Path.Combine(System.AppContext.BaseDirectory, "NxFileViewerFork.settings.json");
            if (!File.Exists(path)) return null;
            using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
            if (document.RootElement.TryGetProperty("SupportId", out var id) &&
                System.Text.RegularExpressions.Regex.IsMatch(id.GetString() ?? "", "^NX-(?:[A-F0-9]{4}-){4}[A-F0-9]{4}$")) return id.GetString();
        }
        catch (System.Exception ex) when (ex is IOException or System.UnauthorizedAccessException or System.Text.Json.JsonException) { }
        return null;
    }
    private async void CopySupportIdentity(object? sender, RoutedEventArgs e)
    { if (Clipboard != null && SupportIdentityText.Text is { Length: > 0 } id) await Clipboard.SetTextAsync(id); }
    private void SearchSupportIdentity(object? sender, RoutedEventArgs e)
    { if (SupportIdentityText.Text is { Length: > 0 } id) Process.Start(new ProcessStartInfo("https://github.com/sandmaennchen5/fork_NxFileViewer/issues?q=" + System.Uri.EscapeDataString("is:issue \"" + id + "\"")) { UseShellExecute = true }); }
    private static string ReadInfoResource(string name)
    {
        using var stream = typeof(MainWindow).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidDataException("Missing application information: " + name);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
