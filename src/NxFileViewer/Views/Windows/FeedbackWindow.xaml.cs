using System;
using System.Diagnostics;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Emignatik.NxFileViewer.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace Emignatik.NxFileViewer.Views.Windows;

public partial class FeedbackWindow : Window
{
    internal const string Repository = "https://github.com/sandmaennchen5/fork_NxFileViewer";
    private static readonly HttpClient Client = new(new HttpClientHandler { AllowAutoRedirect = false })
    {
        Timeout = TimeSpan.FromSeconds(60)
    };

    private readonly CancellationTokenSource _lifetime = new();
    private readonly Uri? _endpoint;
    private readonly bool _german;
    private Uri? _issue;
    private bool _submitted;
    private readonly LogAnonymizer _logAnonymizer = new();

    public FeedbackWindow(bool german)
    {
        InitializeComponent();
        App.ServiceProvider.GetService<Emignatik.NxFileViewer.Styling.Theme.IThemeService>()?.RegisterWindow(this);
        _german = german;
        Title = Text("Fehler / Funktionswunsch melden", "Report a bug / request a feature");
        Notice.Text = Text(
            "Hier kannst du Fehler und Funktionswünsche melden. Der Bericht wird öffentlich auf GitHub veröffentlicht. Entferne persönliche Angaben aus dem Text.",
            "Use this form for bugs and feature requests. The report will be public on GitHub. Remove personal information from the text.");
        GitHubStatus.Text = Text("GitHub: Anmeldung wird beim Senden im Browser bestätigt.",
            "GitHub: Sign-in is confirmed in the browser when sending.");
        DisplayNameLabel.Content = Text("Anzeigename ohne GitHub (optional)", "Display name without GitHub (optional)");
        DisplayNameHint.Text = Text("Wird öffentlich im Issue angezeigt. Buchstaben, Zahlen, Leerzeichen, Punkt, _ und - sind erlaubt.",
            "Shown publicly in the issue. Letters, numbers, spaces, dots, _ and - are allowed.");
        DisplayName.Text = App.Features.ReportDisplayName;
        TitleLabel.Content = Text("Titel", "Title");
        DescriptionLabel.Content = Text("Beschreibung (Fehler oder Funktionswunsch)", "Description (bug or feature request)");
        WithGitHub.Content = Text("Mit GitHub melden", "Report with GitHub");
        WithoutGitHub.Content = Text("Ohne GitHub-Konto melden", "Report without a GitHub account");
        IncludeLog.Content = Text("Bereinigten Log-Auszug öffentlich im GitHub-Issue teilen", "Share a sanitized log excerpt in the public GitHub issue");
        PrivateLog.Content = Text("Original-Sitzungslog privat an den Betreiber senden", "Send the original session log privately to the maintainer");
        PrivateLogHint.Text = Text(
            "Unveränderte Datei, maximal 5 MiB. Kann persönliche Pfade und Namen enthalten. Download nur im geschützten Adminbereich. Beide Optionen sind unabhängig wählbar.",
            "Unmodified file, up to 5 MiB. May contain personal paths and names. Download requires admin sign-in. Both options can be selected independently.");
        LogPreviewLabel.Content = Text("Bereinigter Log-Auszug (bitte prüfen, vor dem Senden bearbeitbar)",
            "Sanitized log excerpt (please review, editable before sending)");
        var endpoint = App.Features.ReportEndpoint;
        if (Uri.TryCreate(endpoint, UriKind.Absolute, out var url) &&
            url.Scheme == Uri.UriSchemeHttps && url.UserInfo.Length == 0 &&
            url.Query.Length == 0 && url.Fragment.Length == 0)
            _endpoint = url;
        else if (!string.IsNullOrWhiteSpace(endpoint))
            Status.Text = Text("Die URL des Meldedienstes ist ungültig.", "The reporting service URL is invalid.");

        if (_endpoint is null)
        {
            WithGitHub.IsEnabled = false;
            WithoutGitHub.IsEnabled = false;
            PrivateLog.IsEnabled = false;
            if (Status.Text.Length == 0)
                Status.Text = Text("Zum Senden aus der App muss der Meldedienst eingerichtet sein.", "The reporting service must be configured to submit reports from the app.");
        }
        Closed += (_, _) => _lifetime.Cancel();
    }

    private string Text(string german, string english) => _german ? german : english;

    internal static void OpenBrowser(string url) =>
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

    private async void WithGitHub_Click(object sender, RoutedEventArgs e) => await SubmitAsync("github");

    private async void WithoutGitHub_Click(object sender, RoutedEventArgs e) => await SubmitAsync("anonymous");

    private void IssueLink_Click(object sender, RoutedEventArgs e)
    {
        try { OpenBrowser(_issue!.AbsoluteUri); }
        catch (Win32Exception) { Status.Text = _issue!.AbsoluteUri; }
    }

    private void IncludeLog_Click(object sender, RoutedEventArgs e)
    {
        if (IncludeLog.IsChecked != true)
        {
            LogPreview.Visibility = Visibility.Collapsed;
            return;
        }

        try
        {
            if (LogPreview.Visibility == Visibility.Collapsed && LogExcerpt.Text.Length == 0)
            {
                var path = App.ServiceProvider.GetRequiredService<AppLoggerProvider>().SessionLogPath;
                if (path is null)
                    throw new FileNotFoundException();

                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var length = (int)Math.Min(stream.Length, 4096);
                var offset = stream.Seek(-length, SeekOrigin.End);
                var bytes = new byte[length];
                stream.ReadExactly(bytes);
                var firstLineEnd = offset > 0 ? Array.IndexOf(bytes, (byte)'\n') : -1;
                var start = offset > 0 && firstLineEnd < 0 ? length : firstLineEnd + 1;
                LogExcerpt.Text = _logAnonymizer.Anonymize(Encoding.UTF8.GetString(bytes, start, length - start));
            }

            LogPreview.Visibility = Visibility.Visible;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            IncludeLog.IsChecked = false;
            Status.Text = Text("Das aktuelle Sitzungslog konnte nicht gelesen werden.", "The current session log could not be read.");
        }
    }

    private async Task SubmitAsync(string mode)
    {
        if (_submitted || _endpoint is null) return;
        var title = IssueTitle.Text.Trim();
        var description = Description.Text.Trim();
        if (title.Length == 0 || description.Length == 0)
        {
            Status.Text = Text("Bitte fülle beide Felder aus.", "Fill in both fields.");
            return;
        }

        var displayName = mode == "anonymous" ? DisplayName.Text.Trim() : "";
        if (displayName.Length > 0 && !System.Text.RegularExpressions.Regex.IsMatch(
                displayName, @"\A[\p{L}\p{N} ._-]{1,40}\z"))
        {
            Status.Text = Text("Bitte verwende für den Anzeigenamen nur die angegebenen Zeichen.",
                "Use only the listed characters for the display name.");
            return;
        }
        if (mode == "anonymous") App.Features.ReportDisplayName = displayName;

        string participant;
        try { participant = Emignatik.NxFileViewer.Services.SupportIdentityService.Participant(); }
        catch (IOException)
        {
            Status.Text = Text("Die Systemkennung konnte nicht ermittelt werden.", "Could not determine the system identity.");
            return;
        }

        var assembly = typeof(FeedbackWindow).Assembly;
        var assemblyVersion = assembly.GetName().Version;
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assemblyVersion?.ToString();
        var body = description;
        var systemDetails = $"NxFileViewer: {version}\n{SessionEnvironment.Describe()}";
        var log = _logAnonymizer.Anonymize(LogExcerpt.Text).TrimEnd();
        LogExcerpt.Text = log;
        if (IncludeLog.IsChecked == true && log.Length > 0)
        {
            var codeBlock = log.ReplaceLineEndings("\n").Replace("\n", "\n    ");
            body += $"\n\n### {Text("Sitzungslog (Auszug)", "Session log (excerpt)")}\n\n    {codeBlock}";
        }

        var submission = JsonSerializer.SerializeToUtf8Bytes(new { mode, title, body, displayName, participant, systemDetails });
        if (submission.Length > 32768 || Encoding.UTF8.GetByteCount(body) > 24000)
        {
            Status.Text = Text("Die Meldung ist zu lang. Kürze die Beschreibung oder den Log-Auszug.",
                "The report is too long. Shorten the description or the log excerpt.");
            return;
        }
        var includePrivateLog = PrivateLog.IsChecked == true;
        WithGitHub.IsEnabled = WithoutGitHub.IsEnabled = false;
        PrivateLog.IsEnabled = false;
        IncludeLog.IsEnabled = false;
        DisplayName.IsReadOnly = true;
        IssueTitle.IsReadOnly = Description.IsReadOnly = LogExcerpt.IsReadOnly = true;
        var cancellation = _lifetime.Token;
        string? savedReceipt = null;
        var systemDetailsConfirmed = true;
        try
        {
            Status.Text = Text("Meldung wird vorbereitet …", "Preparing report …");
            PrivateLogPayload? privateLog = null;
            if (includePrivateLog)
            {
                try { privateLog = await Task.Run(ReadPrivateSessionLog, cancellation); }
                catch (InvalidDataException)
                {
                    Status.Text = Text("Das Original-Sitzungslog überschreitet die maximale Größe von 5 MiB.",
                        "The original session log exceeds the maximum size of 5 MiB.");
                    return;
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    Status.Text = Text("Das Original-Sitzungslog konnte nicht gelesen werden. Die Meldung wurde nicht gesendet.",
                        "The original session log could not be read. The report was not sent.");
                    return;
                }
            }
            var fingerprint = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(submission));
            if (_submissionFingerprint != fingerprint)
            {
                _submissionId = Guid.NewGuid().ToString("N");
                _submissionFingerprint = fingerprint;
            }
            var payload = new { mode, title, body, displayName, participant, systemDetails, privateLog, submissionId = _submissionId };
            if (JsonSerializer.SerializeToUtf8Bytes(payload).Length > 8 * 1024 * 1024)
            {
                Status.Text = Text("Die Meldung mit Original-Log ist zu groß.", "The report with the original log is too large.");
                return;
            }
            Status.Text = Text("Meldung wird gesendet …", "Sending report …");
            using var response = await PostAsync(payload, cancellation);
            var result = response.RootElement;
            systemDetailsConfirmed = result.TryGetProperty("systemDetailsStored", out var detailsStored) &&
                detailsStored.ValueKind == JsonValueKind.True;
            if (result.TryGetProperty("receipt", out var receiptValue)) savedReceipt = receiptValue.GetString();
            var privateLogConfirmed = !includePrivateLog ||
                (result.TryGetProperty("privateLogStored", out var stored) && stored.ValueKind == JsonValueKind.True);
            if (mode == "github" && !privateLogConfirmed)
            {
                Status.Text = Text("Der Webdienst bestätigt keinen privaten Log-Upload. Bitte den Webdienst aktualisieren.",
                    "The service did not confirm private log storage. Update the web service.");
                return;
            }
            if (ShowStoredReport(result)) return;
            if (mode == "github")
            {
                var authorization = result.GetProperty("authorization").GetString();
                if (!Uri.TryCreate(authorization, UriKind.Absolute, out var url) ||
                    url.GetLeftPart(UriPartial.Path) != "https://github.com/login/oauth/authorize" ||
                    url.UserInfo.Length != 0)
                    throw new JsonException();

                var ticket = result.GetProperty("ticket").GetString();
                OpenBrowser(url.AbsoluteUri);
                GitHubStatus.Text = Text("GitHub: Bestätigung im Browser ausstehend.",
                    "GitHub: Waiting for confirmation in the browser.");
                Status.Text = Text("Bestätige die Meldung im Browser.", "Confirm the report in your browser.");
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
                deadline.CancelAfter(TimeSpan.FromMinutes(10));
                while (true)
                {
                    await Task.Delay(TimeSpan.FromSeconds(3), deadline.Token);
                    using var status = await PostAsync(new { mode = "status", ticket }, deadline.Token);
                    if (status.RootElement.GetProperty("status").GetString() == "pending")
                        continue;
                    if (ShowStoredReport(status.RootElement)) break;
                    GitHubStatus.Text = Text("GitHub: Meldung autorisiert.", "GitHub: Report authorized.");
                    if (status.RootElement.TryGetProperty("login", out var login) && login.ValueKind == JsonValueKind.String)
                        GitHubStatus.Text = Text($"GitHub: Als @{login.GetString()} für diese Meldung bestätigt.",
                            $"GitHub: Confirmed as @{login.GetString()} for this report.");
                    ShowIssue(status.RootElement);
                    break;
                }
            }
            else
            {
                ShowIssue(result);
                if (!privateLogConfirmed)
                    Status.Text += Text(" Der private Log-Upload wurde nicht bestätigt. Bitte den Webdienst aktualisieren.",
                        " Private log storage was not confirmed. Update the web service.");
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (HttpRequestException error) when (error.StatusCode == HttpStatusCode.TooManyRequests)
        {
            Status.Text = Text("Zu viele Meldungen. Versuche es später erneut.", "Too many reports. Try again later.");
        }
        catch (Exception error) when (error is HttpRequestException or IOException or UnauthorizedAccessException or OperationCanceledException or JsonException or
                                      InvalidOperationException or System.Collections.Generic.KeyNotFoundException or Win32Exception)
        {
            if (savedReceipt is not null)
            {
                _submitted = true;
                Status.Text = Text($"Meldung beim Betreiber gespeichert (Nr. {savedReceipt}). Die GitHub-Bestätigung konnte nicht abgeschlossen werden.",
                    $"Report saved for the maintainer (receipt {savedReceipt}). GitHub confirmation could not be completed.");
                GitHubStatus.Text = Text("GitHub: Bestätigung ausstehend.", "GitHub: Confirmation pending.");
            }
            else
                Status.Text = Text("Die Erstellung konnte nicht bestätigt werden. Prüfe vor einem neuen Versuch die GitHub-Issues.",
                    "Issue creation could not be confirmed. Check GitHub Issues before trying again.");
        }
        finally
        {
            if (_submitted && !systemDetailsConfirmed)
                Status.Text += Text(" Der Meldedienst hat die Speicherung der Systemdaten nicht bestätigt.",
                    " The reporting service did not confirm that it stored the system details.");
            WithGitHub.IsEnabled = !_submitted;
            WithoutGitHub.IsEnabled = !_submitted;
            IncludeLog.IsEnabled = !_submitted;
            PrivateLog.IsEnabled = !_submitted;
            DisplayName.IsReadOnly = _submitted;
            if (mode == "github" && !_submitted)
                GitHubStatus.Text = Text("GitHub: Bestätigung konnte nicht abgeschlossen werden.",
                    "GitHub: Confirmation could not be completed.");
            IssueTitle.IsReadOnly = Description.IsReadOnly = LogExcerpt.IsReadOnly = _submitted;
        }
    }

    private static PrivateLogPayload ReadPrivateSessionLog()
    {
        var path = App.ServiceProvider.GetRequiredService<AppLoggerProvider>().SessionLogPath;
        if (path is null) throw new FileNotFoundException();
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return CompressPrivateLog(stream);
    }

    private static PrivateLogPayload CompressPrivateLog(Stream stream)
    {
        var length = stream.Length;
        if (length > 5 * 1024 * 1024) throw new InvalidDataException();
        var bytes = new byte[(int)length];
        stream.ReadExactly(bytes);
        using var buffer = new MemoryStream();
        using (var gzip = new GZipStream(buffer, CompressionLevel.Optimal, leaveOpen: true))
            gzip.Write(bytes);
        return new PrivateLogPayload(Convert.ToBase64String(buffer.ToArray()), bytes.Length);
    }

    private sealed record PrivateLogPayload(string GzipBase64, int Size);

    private string? _submissionFingerprint;
    private string _submissionId = Guid.NewGuid().ToString("N");

    private bool ShowStoredReport(JsonElement result)
    {
        if (!result.TryGetProperty("status", out var state) || state.GetString() is "created" or "pending")
            return false;
        if (!result.TryGetProperty("receipt", out var value)) return false;
        var receipt = value.GetString();
        _submitted = true;
        Status.Text = Text($"Meldung beim Betreiber gespeichert (Nr. {receipt}). GitHub-Übertragung steht noch aus.",
            $"Report saved for the maintainer (receipt {receipt}). GitHub delivery is pending.");
        if (state.GetString() == "needs_auth")
            Status.Text += Text(" Die GitHub-Anmeldung konnte nicht abgeschlossen werden; der Betreiber kann die Meldung trotzdem lesen.",
                " GitHub authorization could not be completed; the maintainer can still read the report.");
        else if (state.GetString() is "review" or "blocked")
            Status.Text += Text(" Der Betreiber prüft die Übertragung.", " The maintainer will review delivery.");
        return true;
    }

    private async Task<JsonDocument> PostAsync<T>(T data, CancellationToken cancellation)
    {
        using var response = await Client.PostAsJsonAsync(_endpoint, data, cancellation);
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellation));
    }

    private void ShowIssue(JsonElement result)
    {
        var number = result.GetProperty("number").GetInt32();
        if (number <= 0) throw new JsonException();
        _submitted = true;
        _issue = new Uri($"{Repository}/issues/{number}");
        Status.Text = Text("Issue wurde erstellt.", "Issue created.");
        IssueLink.Content = $"GitHub #{number}";
        IssueLink.Visibility = Visibility.Visible;
    }
}
