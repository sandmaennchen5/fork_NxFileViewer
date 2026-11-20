using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Emignatik.NxFileViewer.Services;

internal static class CommunityPollService
{
    private static readonly HttpClient Client = new(new HttpClientHandler { AllowAutoRedirect = false })
    {
        Timeout = TimeSpan.FromSeconds(20)
    };
    internal static Uri? Endpoint(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var url) &&
        url.Scheme == "https" && url.UserInfo.Length == 0 && url.Query.Length == 0 && url.Fragment.Length == 0
            ? url : null;

    internal static async Task<List<Poll>> LoadAsync(Uri endpoint, CancellationToken cancellation, int? poll = null, int? option = null, string? language = null, string? action = null, string? answer = null)
    {
        var participant = SupportIdentityService.Participant();
        language ??= Localization.CommunityLanguage.IsGerman ? "de" : "en";
        var api = endpoint.AbsoluteUri + "?api=1&lang=" + (language == "de" ? "de" : "en");
        PollResponse? result;
        if (poll.HasValue)
        {
            using var response = await Client.PostAsJsonAsync(api, new { participant, poll, option, action = action ?? "vote", answer }, cancellation);
            response.EnsureSuccessStatusCode();
            result = await response.Content.ReadFromJsonAsync<PollResponse>(cancellation);
        }
        else
        {
            result = await Client.GetFromJsonAsync<PollResponse>(api + "&participant=" + participant, cancellation);
        }
        return result?.Polls ?? throw new IOException("Invalid poll response");
    }

    public sealed record PollResponse(List<Poll> Polls);
    public sealed record Poll(int Id, string Question, bool Closed, int? SelectedOption, List<Option> Options,
        string Kind = "choice", bool CanParticipate = true, bool NeedsResponse = true,
        bool ShowResults = true, bool HasResponded = false, bool Declined = false,
        string? ResponseText = null, int ResponseCount = 0, bool ShowInApp = true)
    {
        public bool AwaitingResponse => NeedsResponse && CanParticipate && !Closed && !Declined && !HasResponded && SelectedOption is null;
    }
    public sealed record Option(int Id, string Label, int Votes, bool Active = true);
}
