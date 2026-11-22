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
        ProgramArchitectureText.Text = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
        ProgramRuntimeText.Text = assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Any(item => item.Key == "ViewerSelfContained" && item.Value == "true") ? _text.Info_WithRuntime : _text.Info_WithoutRuntime;
        CreditsText.Text = ReadInfoResource("Viewer.Credits");
        LicenseText.Text = ReadInfoResource("Viewer.License");
        ThirdPartyText.Text = ReadInfoResource("Viewer.ThirdPartyNotices");
        ShowSupportIdentity();
        InfoTab.PropertyChanged += (_, change) =>
        {
            if (change.Property == TabItem.IsSelectedProperty && InfoTab.IsSelected) RefreshSupportIdentity(null, new RoutedEventArgs());
        };
    }

    private async System.Threading.Tasks.Task ConfirmSystemReport()
    {
        var settings = _preferences.SharedSettings;
        var version = Emignatik.NxFileViewer.Services.UsageStatisticsService.BuildVersion;
        if (Emignatik.NxFileViewer.Settings.SystemReportChoice.ShouldConfirmSystemReport(settings, version))
        {
            var dialog = new UsageStatisticsConsentWindow(settings.SendAnonymousUsageStatistics) { RequestedThemeVariant = ActualThemeVariant };
            await dialog.ShowDialog(this);
            settings.SendAnonymousUsageStatistics = dialog.StatisticsEnabled;
            settings.UsageStatisticsChoiceConfirmed = true; settings.UsageStatisticsChoiceVersion = version;
            _editedSharedSettings.SendAnonymousUsageStatistics = settings.SendAnonymousUsageStatistics; _editedSharedSettings.UsageStatisticsChoiceConfirmed = true; _editedSharedSettings.UsageStatisticsChoiceVersion = version;
            try { _preferences.Save(); } catch (System.Exception ex) when (ex is IOException or System.UnauthorizedAccessException) { SettingsStatus.Text = ex.Message; }
        }
        if (!_closed) await Emignatik.NxFileViewer.Services.UsageStatisticsService.SendStartupAsync(settings);
    }
    private async void OpenFeedback(object? sender, RoutedEventArgs e)
    {
        var dialog = new FeedbackWindow(_text.CultureName.StartsWith("de", System.StringComparison.OrdinalIgnoreCase), _preferences.SharedSettings, _sessionLogPath) { RequestedThemeVariant = ActualThemeVariant };
        await dialog.ShowDialog(this);
        ShowSupportIdentity();
        _editedSharedSettings.SupportId = _preferences.SharedSettings.SupportId;
        _editedSharedSettings.SupportIdEndpoint = _preferences.SharedSettings.SupportIdEndpoint;
        try { _preferences.Save(); } catch (System.Exception ex) when (ex is IOException or System.UnauthorizedAccessException) { SettingsStatus.Text = ex.Message; }
    }
    private async void OpenPolls(object? sender, RoutedEventArgs e)
    {
        var dialog = new PollsWindow(_text.CultureName.StartsWith("de", System.StringComparison.OrdinalIgnoreCase), _preferences.SharedSettings.PollEndpoint) { RequestedThemeVariant = ActualThemeVariant };
        await dialog.ShowDialog(this);
    }
    private void OpenInfoLink(object? sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is string url) Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
    private void ShowSupportIdentity()
    {
        SupportIdentityText.Text = SupportIdentityState.Cached(_preferences.SharedSettings);
        CopySupportButton.IsEnabled = SearchSupportButton.IsEnabled = !string.IsNullOrEmpty(SupportIdentityText.Text);
    }
    private System.Threading.CancellationTokenSource? _supportIdentityCancellation;
    private async void RefreshSupportIdentity(object? sender, RoutedEventArgs e)
    {
        if (_supportIdentityCancellation != null || _closed) return;
        var settings = _preferences.SharedSettings;
        ShowSupportIdentity();
        if (Emignatik.NxFileViewer.Services.SupportIdentityClient.Endpoint(settings.ReportEndpoint) == null) return;
        using var cancellation = new System.Threading.CancellationTokenSource();
        _supportIdentityCancellation = cancellation;
        RefreshSupportButton.IsEnabled = false;
        SupportIdentityStatus.Text = _text.CultureName.StartsWith("de") ? "Support-ID wird geprüft …" : "Checking support ID …";
        try
        {
            await SupportIdentityState.RefreshAsync(settings, cancellation.Token);
            if (_closed || !ReferenceEquals(settings, _preferences.SharedSettings)) return;
            ShowSupportIdentity();
            _editedSharedSettings.SupportId = settings.SupportId;
            _editedSharedSettings.SupportIdEndpoint = settings.SupportIdEndpoint;
            SupportIdentityStatus.Text = _text.CultureName.StartsWith("de") ? "Öffentlich in deinen Meldungen sichtbar." : "Publicly visible in your reports.";
            _preferences.Save();
        }
        catch (System.Exception ex) when (ex is IOException or System.UnauthorizedAccessException or System.Net.Http.HttpRequestException or System.OperationCanceledException or System.Text.Json.JsonException)
        {
            if (!_closed) SupportIdentityStatus.Text = _text.CultureName.StartsWith("de")
                ? "Support-ID konnte nicht geprüft werden. Eine angezeigte ID stammt aus dem letzten Abruf."
                : "Could not verify support ID. Any displayed ID is from the last successful request.";
        }
        finally { _supportIdentityCancellation = null; if (!_closed) RefreshSupportButton.IsEnabled = true; }
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
