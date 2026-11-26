using Avalonia.Input.Platform;
using System.Linq;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Emignatik.NxFileViewer.Services;

namespace NxFileViewer.Avalonia;

public partial class MainWindow
{
    private CancellationTokenSource? _homePollCancellation;

    private void InitializeInfo()
    {
        var german = _text.CultureName.StartsWith("de", System.StringComparison.OrdinalIgnoreCase);
        HomePollButton.Content = german ? "Umfragen ansehen" : "View polls";
        HomeReportLabel.Text = german ? "Fehler melden" : "Report an issue";
        var assembly = typeof(MainWindow).Assembly;
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
            ?? assembly.GetName().Version?.ToString();
        ProgramVersionText.Text = "NxFileViewer Fork v" + version;
        Title = ProgramVersionText.Text;
        ProgramArchitectureText.Text = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
        var runtimePackage = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Any(item => item.Key == "ViewerSelfContained" && item.Value == "true") ? _text.Info_WithRuntime : _text.Info_WithoutRuntime;
        ProgramRuntimeText.Text = $"{RuntimeInformation.FrameworkDescription} ({runtimePackage})";
        CreditsText.Text = ReadInfoResource("Viewer.Credits");
        LicenseText.Text = ReadInfoResource("Viewer.License");
        ThirdPartyText.Text = ReadInfoResource("Viewer.ThirdPartyNotices");
        (string License, string ThirdParty) labels = _text switch
        {
            Emignatik.NxFileViewer.Localization.Keys.LocalizationKeys_DE => ("Programmlizenz", "Hinweise zu Drittkomponenten"),
            Emignatik.NxFileViewer.Localization.Keys.LocalizationKeys_FR => ("Licence du programme", "Mentions relatives aux composants tiers"),
            Emignatik.NxFileViewer.Localization.Keys.LocalizationKeys_ES => ("Licencia del programa", "Avisos de componentes de terceros"),
            _ => ("Program license", "Third-party notices")
        };
        SetDisclosureToggle(LicenseToggle, labels.License);
        SetDisclosureToggle(ThirdPartyToggle, labels.ThirdParty);
        ShowSupportIdentity();
        Opened += async (_, _) => await RefreshHomePollsAsync();
        InfoTab.PropertyChanged += (_, change) =>
        {
            if (change.Property == TabItem.IsSelectedProperty && InfoTab.IsSelected) RefreshSupportIdentity(null, new RoutedEventArgs());
        };
    }

    private static void SetDisclosureToggle(ToggleButton button, string label)
    {
        void UpdateLabel() => button.Content = (button.IsChecked == true ? "▾  " : "▸  ") + label;
        button.IsCheckedChanged += (_, _) => UpdateLabel();
        UpdateLabel();
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
    internal PollsWindow CreatePollsWindow() =>
        new(_text.CultureName.StartsWith("de", System.StringComparison.OrdinalIgnoreCase), _preferences.SharedSettings.PollEndpoint)
        {
            RequestedThemeVariant = ActualThemeVariant
        };

    private async void OpenPolls(object? sender, RoutedEventArgs e)
    {
        var dialog = CreatePollsWindow();
        await dialog.ShowDialog(this);
        if (_closed) return;
        if (dialog.OpenPollCount is { } count)
        {
            _homePollCancellation?.Cancel();
            ShowHomePollCount(count);
        }
        else await RefreshHomePollsAsync();
    }

    private async Task RefreshHomePollsAsync()
    {
        _homePollCancellation?.Cancel();
        ShowHomePollCount(0);
        if (CommunityPollService.Endpoint(_preferences.SharedSettings.PollEndpoint) is not { } endpoint) return;

        using var cancellation = new CancellationTokenSource();
        _homePollCancellation = cancellation;
        try
        {
            var language = _text.CultureName.StartsWith("de", System.StringComparison.OrdinalIgnoreCase) ? "de" : "en";
            var polls = await CommunityPollService.LoadAsync(endpoint, cancellation.Token, language: language);
            if (!_closed && !cancellation.IsCancellationRequested)
                ShowHomePollCount(polls.Count(poll => poll.ShowInApp && poll.AwaitingResponse));
        }
        catch (System.Exception error) when (error is System.Net.Http.HttpRequestException or IOException or
                                            System.UnauthorizedAccessException or System.OperationCanceledException or System.Text.Json.JsonException)
        {
            // The start page can be used without a poll response.
        }
        finally
        {
            if (ReferenceEquals(_homePollCancellation, cancellation)) _homePollCancellation = null;
        }
    }

    private void ShowHomePollCount(int count)
    {
        HomePollNotice.IsVisible = HomePollButton.IsVisible = count > 0;
        HomePollNotice.Text = _text.CultureName.StartsWith("de", System.StringComparison.OrdinalIgnoreCase)
            ? (count == 1 ? "Eine offene Umfrage" : $"{count} offene Umfragen")
            : (count == 1 ? "One open poll" : $"{count} open polls");
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
        if (Emignatik.NxFileViewer.Services.SupportIdentityClient.Endpoint(settings.ReportEndpoint) == null)
        {
            SupportIdentityStatus.Text = _text.CultureName.StartsWith("de") ? "Für die Support-ID ist kein gültiger Meldedienst eingerichtet." : "No valid reporting service is configured for the Support ID.";
            return;
        }
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
            if (!_closed)
            {
                SupportIdentityStatus.Text += " " + ex.GetBaseException().Message;
                LogSession("WARN", "Support identity: " + ex.GetBaseException().Message);
            }
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
