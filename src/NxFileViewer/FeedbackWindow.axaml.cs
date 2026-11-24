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
using Avalonia.Controls;
using Avalonia.Interactivity;
using Emignatik.NxFileViewer.Settings;
using Emignatik.NxFileViewer.Logging;

namespace NxFileViewer.Avalonia;

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
    private bool _sending;
    private bool _signInCanceledByUser;
    private CancellationTokenSource? _signInCancellation;
    private string? _signInTicket;
    private DateTime _signInExpires;
    private readonly LogAnonymizer _logAnonymizer = new();

    public FeedbackWindow() : this(true, new AppSettings { ReportEndpoint = "" }, "") { }
    private readonly AppSettings _settings;
    private readonly string _sessionLogPath;
    public FeedbackWindow(bool german, AppSettings settings, string sessionLogPath)
    {
        _settings = settings; _sessionLogPath = sessionLogPath;
        InitializeComponent();
        WpfTheme.Apply(this);
        SupportIdText.Text = "Support-ID: " + SupportIdentityState.Cached(settings);
        _german = german;
        Title = Text("Fehler / Funktionswunsch melden", "Report a bug / request a feature");
        Notice.Text = Text(
            "Hier kannst du Fehler und Funktionswünsche melden. Der Bericht wird öffentlich auf GitHub veröffentlicht. Entferne persönliche Angaben aus dem Text.",
            "Use this form for bugs and feature requests. The report will be public on GitHub. Remove personal information from the text.");
        GitHubStatus.Text = Text("GitHub: Bitte zuerst anmelden. Dabei wird noch keine Meldung gesendet.",
            "GitHub: Sign in first. This does not send a report.");
        DisplayNameLabel.Content = Text("Anzeigename ohne GitHub (optional)", "Display name without GitHub (optional)");
        DisplayNameHint.Text = Text("Wird öffentlich im Issue angezeigt. Buchstaben, Zahlen, Leerzeichen, Punkt, _ und - sind erlaubt.",
            "Shown publicly in the issue. Letters, numbers, spaces, dots, _ and - are allowed.");
        DisplayName.Text = _settings.ReportDisplayName;
        TitleLabel.Content = Text("Titel", "Title");
        DescriptionLabel.Content = Text("Beschreibung (Fehler oder Funktionswunsch)", "Description (bug or feature request)");
        SignInGitHub.Content = Text("Mit GitHub anmelden", "Sign in with GitHub");
        WithGitHub.Content = Text("Auf GitHub melden (mit Anmeldung)", "Report on GitHub (signed in)");
        GitHubAccountDescription.Text = Text("Melden mit deinem GitHub-Account", "Report with your GitHub account");
        WithGitHub.IsEnabled = false;
        WithoutGitHub.Content = Text("Auf GitHub melden (ohne Anmeldung)", "Report on GitHub (without signing in)");
        IncludeLog.Content = Text("Bereinigten Log-Auszug öffentlich im GitHub-Issue teilen", "Share a sanitized log excerpt in the public GitHub issue");
        PrivateLog.Content = Text("Original-Sitzungslog privat an den Betreiber senden", "Send the original session log privately to the maintainer");
        PrivateLogHint.Text = Text(
            "Unveränderte Datei, maximal 5 MiB. Kann persönliche Pfade und Namen enthalten. Download nur im geschützten Adminbereich. Beide Optionen sind unabhängig wählbar.",
            "Unmodified file, up to 5 MiB. May contain personal paths and names. Download requires admin sign-in. Both options can be selected independently.");
        LogPreviewLabel.Content = Text("Bereinigter Log-Auszug (bitte prüfen, vor dem Senden bearbeitbar)",
            "Sanitized log excerpt (please review, editable before sending)");
        var endpoint = _settings.ReportEndpoint;
        if (Uri.TryCreate(endpoint, UriKind.Absolute, out var url) &&
            url.Scheme == Uri.UriSchemeHttps && url.UserInfo.Length == 0 &&
            url.Query.Length == 0 && url.Fragment.Length == 0)
            _endpoint = url;
        else if (!string.IsNullOrWhiteSpace(endpoint))
            Status.Text = Text("Die URL des Meldedienstes ist ungültig.", "The reporting service URL is invalid.");

        if (_endpoint is null)
        {
            SignInGitHub.IsEnabled = false;
            WithGitHub.IsEnabled = false;
            WithoutGitHub.IsEnabled = false;
            PrivateLog.IsEnabled = false;
            if (string.IsNullOrEmpty(Status.Text))
                Status.Text = Text("Zum Senden aus der App muss der Meldedienst eingerichtet sein.", "The reporting service must be configured to submit reports from the app.");
        }
        Closed += (_, _) => _lifetime.Cancel();
        Opened += async (_, _) =>
        {
            if (Screens.ScreenFromWindow(this) is { } screen) MaxHeight = screen.WorkingArea.Height / screen.Scaling - 32;
            if (_endpoint == null) return;
            try
            {
                await SupportIdentityState.RefreshAsync(_settings, _lifetime.Token);
                if (!_lifetime.IsCancellationRequested) SupportIdText.Text = "Support-ID: " + SupportIdentityState.Cached(_settings);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or HttpRequestException or OperationCanceledException or JsonException)
            {
                if (!_lifetime.IsCancellationRequested)
                    SupportIdText.Text = "Support-ID: " + SupportIdentityState.Cached(_settings) + "\n" +
                        Text("Abruf fehlgeschlagen: ", "Could not load ID: ") + error.GetBaseException().Message;
            }
        };
    }

    private string Text(string german, string english) => _german ? german : english;

    internal static void OpenBrowser(string url) =>
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

    private async void SignInGitHub_Click(object sender, RoutedEventArgs e)
    {
        if (_submitted || _sending || _endpoint is null) return;
        _signInCanceledByUser = false;
        Status.Text = "";
        GitHubUser.Text = "";
        _signInTicket = null;
        SignInGitHub.IsEnabled = WithGitHub.IsEnabled = false;
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
            _signInCancellation = deadline;
            deadline.CancelAfter(TimeSpan.FromMinutes(10));
            using var response = await PostAsync(new { mode = "signin" }, deadline.Token);
            var result = response.RootElement;
            var authorization = result.GetProperty("authorization").GetString();
            var ticket = result.GetProperty("ticket").GetString();
            if (ticket is null || !System.Text.RegularExpressions.Regex.IsMatch(ticket, "\\A[a-f0-9]{64}\\z") ||
                !Uri.TryCreate(authorization, UriKind.Absolute, out var url) ||
                url.GetLeftPart(UriPartial.Path) != "https://github.com/login/oauth/authorize" || url.UserInfo.Length != 0)
                throw new JsonException();
            OpenBrowser(url.AbsoluteUri);
            GitHubStatus.Text = Text("GitHub: Anmeldung im Browser bestätigen …", "GitHub: Confirm sign-in in your browser …");
            while (true)
            {
                await Task.Delay(TimeSpan.FromSeconds(3), deadline.Token);
                using var status = await PostAsync(new { mode = "signin_status", ticket }, deadline.Token);
                deadline.Token.ThrowIfCancellationRequested();
                var state = status.RootElement.GetProperty("status").GetString();
                if (state is "pending" or "processing") continue;
                if (state != "authorized") throw new InvalidOperationException();
                // Older service versions confirm authorization without returning a login name.
                var login = status.RootElement.TryGetProperty("login", out var loginValue) && loginValue.ValueKind == JsonValueKind.String
                    ? loginValue.GetString() : null;
                GitHubUser.Text = !string.IsNullOrWhiteSpace(login) ? "GitHub User: " + login
                    : Text("GitHub User: angemeldet (Name nicht verfügbar)", "GitHub User: signed in (name unavailable)");
                if (string.IsNullOrWhiteSpace(login))
                    Status.Text = Text("Die Anmeldung ist bestätigt. Für die Anzeige des Benutzernamens bitte den Meldedienst aktualisieren.",
                        "Sign-in is confirmed. Update the reporting service to display the account name.");
                _signInTicket = ticket;
                _signInExpires = DateTime.UtcNow.AddMinutes(9);
                GitHubStatus.Text = Text("GitHub: Anmeldung erfolgreich. Du kannst die Meldung jetzt senden.",
                    "GitHub: Sign-in successful. You can now send the report.");
                break;
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested || _signInCanceledByUser) { }
        catch (HttpRequestException error) when (error.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed)
        {
            GitHubStatus.Text = Text("GitHub: Der Meldedienst unterstützt diese Anmeldung noch nicht.",
                "GitHub: The reporting service does not support this sign-in yet.");
            Status.Text = Text("Bitte die aktuelle tools/feedback/public/report.php auf dem Server bereitstellen. Es wurde keine Meldung gesendet.",
                "Deploy the current tools/feedback/public/report.php to the server. No report was sent.");
        }
        catch (HttpRequestException error) when (error.StatusCode == HttpStatusCode.TooManyRequests)
        {
            GitHubStatus.Text = Text("GitHub: Zu viele Anmeldeversuche. Bitte später erneut versuchen.",
                "GitHub: Too many sign-in attempts. Please try again later.");
            Status.Text = Text("Es wurde keine Meldung gesendet.", "No report was sent.");
        }

        catch (Exception error) when (error is HttpRequestException or IOException or OperationCanceledException or JsonException or
                                      InvalidOperationException or System.Collections.Generic.KeyNotFoundException or Win32Exception)
        {
            GitHubStatus.Text = Text("GitHub: Anmeldung fehlgeschlagen oder abgelaufen. Bitte erneut anmelden.",
                "GitHub: Sign-in failed or expired. Please sign in again.");
            Status.Text = Text("Es wurde keine Meldung gesendet. Prüfe die Verbindung und bestätige die Anmeldung im Browser.",
                "No report was sent. Check your connection and confirm sign-in in your browser.");
        }
        finally
        {
            _signInCancellation = null;
            SignInGitHub.IsEnabled = WithoutGitHub.IsEnabled = !_submitted && !_sending;
            WithGitHub.IsEnabled = !_submitted && !_sending && _signInTicket is not null && DateTime.UtcNow < _signInExpires;
        }
    }

    private async void WithGitHub_Click(object sender, RoutedEventArgs e) => await SubmitAsync("github");

    private async void WithoutGitHub_Click(object sender, RoutedEventArgs e)
    {
        _signInCanceledByUser = true;
        _signInCancellation?.Cancel();
        await SubmitAsync("anonymous");
    }

    private void IssueLink_Click(object sender, RoutedEventArgs e)
    {
        try { OpenBrowser(_issue!.AbsoluteUri); }
        catch (Win32Exception) { Status.Text = _issue!.AbsoluteUri; }
    }

    private void IncludeLog_Click(object sender, RoutedEventArgs e)
    {
        if (IncludeLog.IsChecked != true)
        {
            LogPreview.IsVisible = false;
            return;
        }

        try
        {
            if (!LogPreview.IsVisible && string.IsNullOrEmpty(LogExcerpt.Text))
            {
                var path = _sessionLogPath;
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

            LogPreview.IsVisible = true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            IncludeLog.IsChecked = false;
            Status.Text = Text("Das aktuelle Sitzungslog konnte nicht gelesen werden.", "The current session log could not be read.");
        }
    }

    private async Task SubmitAsync(string mode)
    {
        if (_submitted || _sending || _endpoint is null) return;
        if (mode == "github" && (_signInTicket is null || DateTime.UtcNow >= _signInExpires))
        {
            _signInTicket = null;
            GitHubUser.Text = "";
            WithGitHub.IsEnabled = false;
            GitHubStatus.Text = Text("GitHub: Bitte zuerst anmelden.", "GitHub: Please sign in first.");
            return;
        }
        var title = (IssueTitle.Text ?? "").Trim();
        var description = (Description.Text ?? "").Trim();
        if (title.Length == 0 || description.Length == 0)
        {
            Status.Text = Text("Bitte fülle beide Felder aus.", "Fill in both fields.");
            return;
        }

        var displayName = mode == "anonymous" ? (DisplayName.Text ?? "").Trim() : "";
        if (displayName.Length > 0 && !System.Text.RegularExpressions.Regex.IsMatch(
                displayName, @"\A[\p{L}\p{N} ._-]{1,40}\z"))
        {
            Status.Text = Text("Bitte verwende für den Anzeigenamen nur die angegebenen Zeichen.",
                "Use only the listed characters for the display name.");
            return;
        }
        if (mode == "anonymous") _settings.ReportDisplayName = displayName;

        string participant;
        try { participant = Emignatik.NxFileViewer.Services.SupportIdentityClient.Participant(); }
        catch (IOException)
        {
            Status.Text = Text("Die Systemkennung konnte nicht ermittelt werden.", "Could not determine the system identity.");
            return;
        }

        var body = description;
        var log = _logAnonymizer.Anonymize(LogExcerpt.Text ?? "").TrimEnd();
        LogExcerpt.Text = log;
        if (IncludeLog.IsChecked == true && log.Length > 0)
        {
            var codeBlock = log.ReplaceLineEndings("\n").Replace("\n", "\n    ");
            body += $"\n\n### {Text("Sitzungslog (Auszug)", "Session log (excerpt)")}\n\n    {codeBlock}";
        }

        var submission = JsonSerializer.SerializeToUtf8Bytes(new { mode, title, body, displayName, participant });
        if (submission.Length > 32768 || Encoding.UTF8.GetByteCount(body) > 24000)
        {
            Status.Text = Text("Die Meldung ist zu lang. Kürze die Beschreibung oder den Log-Auszug.",
                "The report is too long. Shorten the description or the log excerpt.");
            return;
        }
        var includePrivateLog = PrivateLog.IsChecked == true;
        _sending = true;
        SignInGitHub.IsEnabled = WithGitHub.IsEnabled = WithoutGitHub.IsEnabled = false;
        PrivateLog.IsEnabled = false;
        IncludeLog.IsEnabled = false;
        DisplayName.IsReadOnly = true;
        IssueTitle.IsReadOnly = Description.IsReadOnly = LogExcerpt.IsReadOnly = true;
        var cancellation = _lifetime.Token;
        string? savedReceipt = null;
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
            var payload = new { mode, title, body, displayName, participant, privateLog, submissionId = _submissionId, signinTicket = mode == "github" ? _signInTicket : null };
            if (JsonSerializer.SerializeToUtf8Bytes(payload).Length > 8 * 1024 * 1024)
            {
                Status.Text = Text("Die Meldung mit Original-Log ist zu groß.", "The report with the original log is too large.");
                return;
            }
            Status.Text = Text("Meldung wird gesendet …", "Sending report …");
            using var response = await PostAsync(payload, cancellation);
            var result = response.RootElement;
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
            ShowIssue(result);
            if (!privateLogConfirmed)
                Status.Text += Text(" Der private Log-Upload wurde nicht bestätigt. Bitte den Webdienst aktualisieren.",
                    " Private log storage was not confirmed. Update the web service.");

        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (HttpRequestException error) when (mode == "github" && error.StatusCode == HttpStatusCode.Unauthorized)
        {
            _signInTicket = null;
            GitHubUser.Text = "";
            GitHubStatus.Text = Text("GitHub: Anmeldung abgelaufen. Bitte erneut anmelden.", "GitHub: Sign-in expired. Please sign in again.");
            Status.Text = Text("Die Meldung wurde nicht angenommen.", "The report was not accepted.");
        }
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
            _sending = false;
            SignInGitHub.IsEnabled = !_submitted;
            WithGitHub.IsEnabled = !_submitted && _signInTicket is not null && DateTime.UtcNow < _signInExpires;
            WithoutGitHub.IsEnabled = !_submitted;
            IncludeLog.IsEnabled = !_submitted;
            PrivateLog.IsEnabled = !_submitted;
            DisplayName.IsReadOnly = _submitted;

            IssueTitle.IsReadOnly = Description.IsReadOnly = LogExcerpt.IsReadOnly = _submitted;
        }
    }

    private PrivateLogPayload ReadPrivateSessionLog()
    {
        var path = _sessionLogPath;
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
        IssueLink.IsVisible = true;
    }
}
