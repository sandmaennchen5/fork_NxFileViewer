using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Emignatik.NxFileViewer.Services.Updates;

// The one-time transition has no persistent metadata cache or application dependencies.
public sealed record GitHubUpdateResponse(string Body) { public string Notice => ""; }
public sealed class GitHubUpdateRequests(HttpClient client)
{
    public static GitHubUpdateRequests For(HttpClient? client) => new(client ?? new HttpClient());
    public async Task<GitHubUpdateResponse> GetAsync(string url, bool automatic, CancellationToken token)
    {
        using var response = await client.GetAsync(url, token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return new(await response.Content.ReadAsStringAsync(token).ConfigureAwait(false));
    }
}