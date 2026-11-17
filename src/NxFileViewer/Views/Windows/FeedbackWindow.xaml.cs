using System;
using System.Diagnostics;
using System.ComponentModel;
using System.IO;
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
        Timeout = TimeSpan.FromSeconds(20)
    };

    private readonly CancellationTokenSource _lifetime = new();
    private readonly Uri? _endpoint;
    private readonly bool _german;
    private Uri? _issue;
    private bool _submitted;

    public FeedbackWindow(bool german)
    {
        InitializeComponent();
        _german = german;
        Title = Text("Fehler melden", "Report a bug");
        Notice.Text = Text(
            "Der Bericht wird öffentlich auf GitHub veröffentlicht. Entferne persönliche Angaben aus dem Text.",
            "The report will be public on GitHub. Remove personal information from the text.");
        TitleLabel.Content = Text("Titel", "Title");
        DescriptionLabel.Content = Text("Beschreibung und Schritte zum Nachstellen", "Description and steps to reproduce");
        WithGitHub.Content = Text("Mit GitHub melden", "Report with GitHub");
        WithoutGitHub.Content = Text("Ohne GitHub-Konto melden", "Report without a GitHub account");
        IncludeLog.Content = Text("Aktuelles Sitzungslog anhängen", "Attach current session log");
        LogPreviewLabel.Content = Text("Öffentlicher Log-Auszug (vor dem Senden bearbeitbar)",
            "Public log excerpt (editable before sending)");
        var endpoint = App.Features.ReportEndpoint;
        if (Uri.TryCreate(endpoint, UriKind.Absolute, out var url) &&
            url.Scheme == Uri.UriSchemeHttps && url.UserInfo.Length == 0 &&
            url.Query.Length == 0 && url.Fragment.Length == 0)
            _endpoint = url;
        else if (!string.IsNullOrWhiteSpace(endpoint))
            Status.Text = Text("Die URL des Meldedienstes ist ungültig.", "The reporting service URL is invalid.");

        if (_endpoint is null)
        {
            WithGitHub.Content = Text("Auf GitHub melden", "Report on GitHub");
            WithoutGitHub.IsEnabled = false;
            if (Status.Text.Length == 0)
                Status.Text = Text("Der Meldedienst ist noch nicht eingerichtet.", "The reporting service has not been configured yet.");
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
                LogExcerpt.Text = Encoding.UTF8.GetString(bytes, firstLineEnd + 1, length - firstLineEnd - 1);
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
        if (_submitted) return;
        var title = IssueTitle.Text.Trim();
        var description = Description.Text.Trim();
        if (title.Length == 0 || description.Length == 0)
        {
            Status.Text = Text("Bitte fülle beide Felder aus.", "Fill in both fields.");
            return;
        }

        var assembly = typeof(FeedbackWindow).Assembly;
        var assemblyVersion = assembly.GetName().Version;
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assemblyVersion?.ToString();
        var body = $"{description}\n\nNxFileViewer Version: {version}; Assembly: {assemblyVersion}.";
        var log = LogExcerpt.Text.TrimEnd();
        if (IncludeLog.IsChecked == true && log.Length > 0)
        {
            var codeBlock = log.ReplaceLineEndings("\n").Replace("\n", "\n    ");
            body += $"\n\n### {Text("Sitzungslog (Auszug)", "Session log (excerpt)")}\n\n    {codeBlock}";
        }

        var submission = JsonSerializer.SerializeToUtf8Bytes(new { mode, title, body });
        if (_endpoint is not null && (submission.Length > 32768 || Encoding.UTF8.GetByteCount(body) > 24000))
        {
            Status.Text = Text("Die Meldung ist zu lang. Kürze die Beschreibung oder den Log-Auszug.",
                "The report is too long. Shorten the description or the log excerpt.");
            return;
        }
        WithGitHub.IsEnabled = WithoutGitHub.IsEnabled = false;
        IncludeLog.IsEnabled = false;
        IssueTitle.IsReadOnly = Description.IsReadOnly = LogExcerpt.IsReadOnly = true;
        var cancellation = _lifetime.Token;
        try
        {
            if (_endpoint is null)
            {
                var draftUrl = $"{Repository}/issues/new?title={Uri.EscapeDataString(title)}&body={Uri.EscapeDataString(body)}";
                if (draftUrl.Length > 8000)
                {
                    Status.Text = Text("Der GitHub-Link ist zu lang. Kürze die Beschreibung oder den Log-Auszug.",
                        "The GitHub link is too long. Shorten the description or the log excerpt.");
                    return;
                }
                OpenBrowser(draftUrl);
                return;
            }

            Status.Text = Text("Meldung wird gesendet …", "Sending report …");
            using var response = await PostAsync(new { mode, title, body }, cancellation);
            var result = response.RootElement;
            if (mode == "github")
            {
                var authorization = result.GetProperty("authorization").GetString();
                if (!Uri.TryCreate(authorization, UriKind.Absolute, out var url) ||
                    url.GetLeftPart(UriPartial.Path) != "https://github.com/login/oauth/authorize" ||
                    url.UserInfo.Length != 0)
                    throw new JsonException();

                var ticket = result.GetProperty("ticket").GetString();
                OpenBrowser(url.AbsoluteUri);
                Status.Text = Text("Bestätige die Meldung im Browser.", "Confirm the report in your browser.");
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
                deadline.CancelAfter(TimeSpan.FromMinutes(10));
                while (true)
                {
                    await Task.Delay(TimeSpan.FromSeconds(3), deadline.Token);
                    using var status = await PostAsync(new { mode = "status", ticket }, deadline.Token);
                    if (status.RootElement.GetProperty("status").GetString() == "pending")
                        continue;
                    ShowIssue(status.RootElement);
                    break;
                }
            }
            else
            {
                ShowIssue(result);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (HttpRequestException error) when (error.StatusCode == HttpStatusCode.TooManyRequests)
        {
            Status.Text = Text("Zu viele Meldungen. Versuche es später erneut.", "Too many reports. Try again later.");
        }
        catch (Exception error) when (error is HttpRequestException or OperationCanceledException or JsonException or
                                      InvalidOperationException or System.Collections.Generic.KeyNotFoundException or Win32Exception)
        {
            Status.Text = Text("Die Erstellung konnte nicht bestätigt werden. Prüfe vor einem neuen Versuch die GitHub-Issues.",
                "Issue creation could not be confirmed. Check GitHub Issues before trying again.");
        }
        finally
        {
            WithGitHub.IsEnabled = !_submitted;
            WithoutGitHub.IsEnabled = !_submitted && _endpoint is not null;
            IncludeLog.IsEnabled = !_submitted;
            IssueTitle.IsReadOnly = Description.IsReadOnly = LogExcerpt.IsReadOnly = _submitted;
        }
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
