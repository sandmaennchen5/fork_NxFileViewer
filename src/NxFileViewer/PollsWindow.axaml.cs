using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using static Emignatik.NxFileViewer.Services.CommunityPollService;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Controls;

namespace NxFileViewer.Avalonia;

public partial class PollsWindow : Window
{
    internal int? OpenPollCount { get; private set; }
    private readonly CancellationTokenSource _lifetime = new();
    private readonly bool _german;
    private readonly Uri? _endpoint;

    public PollsWindow() : this(true, "") { }
    public PollsWindow(bool german, string endpoint)
    {
        InitializeComponent();
        WpfTheme.Apply(this);
        _german = german;
        Title = Text("Umfragen", "Polls");
        RefreshButton.Content = Text("Aktualisieren", "Refresh");
        _endpoint = Endpoint(endpoint);
        Opened += async (_, _) => await RefreshAsync();
        Closed += (_, _) => _lifetime.Cancel();
    }

    private string Text(string de, string en) => _german ? de : en;
    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

    private async Task RefreshAsync(int? poll = null, int? option = null, string? action = null, string? answer = null)
    {
        if (_endpoint is null)
        {
            Status.Text = Text("Der Umfragedienst ist nicht eingerichtet.", "The poll service is not configured.");
            RefreshButton.IsEnabled = false;
            return;
        }
        RefreshButton.IsEnabled = PollList.IsEnabled = false;
        Status.Text = Text("Umfragen werden geladen …", "Loading polls …");
        try
        {
            var polls = await LoadAsync(_endpoint, _lifetime.Token, poll, option, _german ? "de" : "en", action, answer);
            OpenPollCount = polls.Count(item => item.ShowInApp && item.AwaitingResponse);
            Render(polls);
            Status.Text = polls.Count == 0
                ? Text("Zurzeit gibt es keine Umfragen.", "There are currently no polls.")
                : Text("Nicht teilnehmen blendet die Erinnerung aus. Du kannst deine Entscheidung später ändern.",
                    "Not participating dismisses the reminder. You can change your mind later.");
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception error) when (error is HttpRequestException or IOException or UnauthorizedAccessException or
                                      OperationCanceledException or System.Text.Json.JsonException)
        {
            // Preserve an unsent free-text draft. A refresh lets the user check whether a timed-out submission was accepted.
            Status.Text = Text("Umfragen konnten nicht geladen werden. Prüfe vor erneutem Senden mit Aktualisieren, ob deine Antwort gespeichert wurde.",
                "Could not load polls. Refresh before retrying to check whether your answer was saved.");
        }
        finally
        {
            RefreshButton.IsEnabled = PollList.IsEnabled = true;
        }
    }

    private static TextBlock PollText(string value, double size = 14)
    {
        var label = new TextBlock { Text = value, TextWrapping = TextWrapping.Wrap, FontSize = size };
        return label;
    }

    private static Button ActionButton(string label)
    {
        return new Button { Content = label, HorizontalAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(12, 5, 12, 5), Margin = new Thickness(0, 8, 8, 0) };
    }

    private void Render(List<Poll> polls)
    {
        PollList.Children.Clear();
        foreach (var poll in polls)
        {
            var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 24) };
            var heading = PollText(poll.Question, 18);
            heading.FontWeight = FontWeight.SemiBold;
            panel.Children.Add(heading);
            var state = poll.Declined ? Text("Du nimmst nicht teil.", "You are not participating.")
                : poll.HasResponded || poll.SelectedOption is not null ? Text("Deine Antwort wurde gespeichert.", "Your answer was saved.")
                : poll.Closed ? Text("Abgeschlossen", "Closed") : Text("Offen", "Open");
            panel.Children.Add(PollText(state));
            var canRespond = poll.CanParticipate && !poll.Closed && !poll.Declined && !poll.HasResponded && poll.SelectedOption is null;
            if (poll.Kind == "text")
            {
                panel.Children.Add(PollText(Text("Freitext ist nur für den Betreiber sichtbar (maximal 2000 Zeichen).",
                    "Free text is visible only to the maintainer (maximum 2000 characters).")));
                if (poll.ShowResults)
                    panel.Children.Add(PollText($"{poll.ResponseCount} " + Text("Antworten", "responses")));
                if (poll.ResponseText is not null)
                    panel.Children.Add(PollText(poll.ResponseText));
                else if (canRespond)
                {
                    var draft = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
                        MaxLength = 2000, MinHeight = 100, MaxHeight = 240, 
                        Margin = new Thickness(0, 8, 0, 0) };
                    panel.Children.Add(draft);
                    var send = ActionButton(Text("Antwort senden", "Send response"));
                    send.Click += async (_, _) =>
                    {
                        if (string.IsNullOrWhiteSpace(draft.Text)) { Status.Text = Text("Gib eine Antwort ein.", "Enter a response."); return; }
                        await RefreshAsync(poll.Id, action: "text", answer: draft.Text.Trim());
                    };
                    panel.Children.Add(send);
                }
            }
            else
            {
                var choices = new List<RadioButton>();
                foreach (var option in poll.Options)
                {
                    if (!option.Active && !poll.ShowResults && poll.SelectedOption != option.Id) continue;
                    var caption = option.Label;
                    if (!option.Active) caption += Text(" (Entfernt)", " (Removed)");
                    if (poll.ShowResults) caption += $" · {option.Votes} " + Text("Stimmen", "votes");
                    if (poll.SelectedOption == option.Id) caption += Text(" · Deine Stimme", " · Your vote");
                    if (!canRespond || !option.Active)
                    {
                        panel.Children.Add(PollText(caption));
                        continue;
                    }
                    var choice = new RadioButton { Content = PollText(caption), GroupName = "Poll" + poll.Id,
                        Tag = option.Id, Margin = new Thickness(0, 6, 0, 6) };
                    panel.Children.Add(choice);
                    choices.Add(choice);
                }
                if (canRespond)
                {
                    var submit = ActionButton(Text("Abstimmen", "Vote"));
                    submit.Click += async (_, _) =>
                    {
                        var selected = choices.Find(choice => choice.IsChecked == true);
                        if (selected is null) { Status.Text = Text("Wähle eine Antwort.", "Choose an answer."); return; }
                        await RefreshAsync(poll.Id, (int)selected.Tag!);
                    };
                    panel.Children.Add(submit);
                }
            }
            if (canRespond || (poll.Declined && !poll.Closed && poll.ShowInApp))
            {
                var decline = ActionButton(poll.Declined ? Text("Doch teilnehmen", "Participate after all") : Text("Nicht teilnehmen", "Not participating"));
                decline.Click += async (_, _) => await RefreshAsync(poll.Id, action: poll.Declined ? "unskip" : "skip");
                panel.Children.Add(decline);
            }
            PollList.Children.Add(panel);
        }
    }
}
