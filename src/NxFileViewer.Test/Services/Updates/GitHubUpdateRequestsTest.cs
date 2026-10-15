using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Emignatik.NxFileViewer.Services.Updates;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services.Updates;

public sealed class GitHubUpdateRequestsTest
{
    private const string Url = "https://api.github.com/repos/example/tool/releases/latest";
    private sealed class Handler : HttpMessageHandler
    {
        public int Calls;
        public Func<HttpResponseMessage> Reply = () => new(HttpStatusCode.OK) { Content = new StringContent("{\"tag_name\":\"v2\"}") };
        public TaskCompletionSource? Hold;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Interlocked.Increment(ref Calls);
            if (Hold != null) await Hold.Task.WaitAsync(token);
            return Reply();
        }
    }
    [Fact]
    public async Task ViewerUsesAutomaticCacheAndManualRefreshAndReportsSavedDate()
    {
        using var handler = new Handler { Reply = () => new(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"draft\":false,\"prerelease\":false,\"tag_name\":\"v1.0.0\",\"assets\":[]}")
        }};
        using var client = new HttpClient(handler);
        var viewer = new ViewerUpdateService(client);
        var token = TestContext.Current.CancellationToken;
        Assert.Null(await viewer.CheckAsync(new Version(2,0,0), "x64", token, automatic: true));
        Assert.Null(await viewer.CheckAsync(new Version(2,0,0), "x64", token, automatic: true));
        Assert.NotEmpty(viewer.LastCheckNotice);
        Assert.Equal(1, handler.Calls);
        Assert.Null(await viewer.CheckAsync(new Version(2,0,0), "x64", token));
        Assert.Empty(viewer.LastCheckNotice);
        Assert.Equal(2, handler.Calls);
    }
    [Fact]
    public async Task LastPermittedResponsePreventsAnotherRequestUntilReset()
    {
        using var handler = new Handler { Reply = () =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };
            response.Headers.Add("X-RateLimit-Remaining", "0");
            response.Headers.Add("X-RateLimit-Reset", DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds().ToString());
            return response;
        }};
        using var client = new HttpClient(handler);
        var requests = new GitHubUpdateRequests(client);
        await requests.GetAsync(Url, false, TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<GitHubRateLimitException>(() => requests.GetAsync(Url + "?other", false, TestContext.Current.CancellationToken));
        Assert.Equal(1, handler.Calls);
    }
    [Fact]
    public async Task AutomaticCacheSurvivesRestartButManualChecksRefreshAndSixHoursExpires()
    {
        var directory = Path.Combine(Path.GetTempPath(), "update-cache-" + Guid.NewGuid());
        var now = DateTimeOffset.UtcNow;
        using var handler = new Handler(); using var client = new HttpClient(handler);
        try
        {
            var first = new GitHubUpdateRequests(client, directory, () => now);
            Assert.False((await first.GetAsync(Url, true, TestContext.Current.CancellationToken)).Cached);
            var restarted = new GitHubUpdateRequests(client, directory, () => now);
            Assert.True((await restarted.GetAsync(Url, true, TestContext.Current.CancellationToken)).Cached);
            Assert.Equal(1, handler.Calls);
            Assert.False((await restarted.GetAsync(Url, false, TestContext.Current.CancellationToken)).Cached);
            Assert.Equal(2, handler.Calls);
            now += TimeSpan.FromHours(6);
            Assert.False((await restarted.GetAsync(Url, true, TestContext.Current.CancellationToken)).Cached);
            Assert.Equal(3, handler.Calls);
        }
        finally { Directory.Delete(directory, true); }
    }
    [Fact]
    public async Task ConcurrentManualRequestsShareOneResponseAndCancelledWaiterDoesNotCancelIt()
    {
        using var handler = new Handler { Hold = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        using var client = new HttpClient(handler);
        var requests = new GitHubUpdateRequests(client);
        var first = requests.GetAsync(Url, false, TestContext.Current.CancellationToken);
        var second = requests.GetAsync(Url, false, TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();
        var cancelled = requests.GetAsync(Url, false, cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
        handler.Hold.SetResult();
        await Task.WhenAll(first, second);
        Assert.Equal(1, handler.Calls);
        Assert.True(second.Result.Cached);
    }
    [Fact]
    public async Task RateLimitPersistsAcrossRestartAndAppliesToOtherEndpointsWithDatedFallback()
    {
        var directory = Path.Combine(Path.GetTempPath(), "update-limit-" + Guid.NewGuid());
        var now = DateTimeOffset.UtcNow;
        using var handler = new Handler(); using var client = new HttpClient(handler);
        try
        {
            var requests = new GitHubUpdateRequests(client, directory, () => now);
            var original = await requests.GetAsync(Url, false, TestContext.Current.CancellationToken);
            handler.Reply = () =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.Forbidden) { Content = new StringContent("rate limit exceeded") };
                response.Headers.Add("X-RateLimit-Remaining", "0");
                response.Headers.Add("X-RateLimit-Reset", now.AddMinutes(20).ToUnixTimeSeconds().ToString());
                return response;
            };
            var saved = await requests.GetAsync(Url, false, TestContext.Current.CancellationToken);
            Assert.Equal(original.CheckedAt, saved.CheckedAt);
            Assert.NotNull(saved.RetryAt);
            Assert.NotEmpty(saved.Notice);
            var restarted = new GitHubUpdateRequests(client, directory, () => now);
            Assert.NotNull((await restarted.GetAsync(Url, false, TestContext.Current.CancellationToken)).RetryAt);
            await Assert.ThrowsAsync<GitHubRateLimitException>(() => restarted.GetAsync(Url + "?other", false, TestContext.Current.CancellationToken));
            Assert.Equal(2, handler.Calls);
            now += TimeSpan.FromMinutes(21);
            handler.Reply = () => new(HttpStatusCode.OK) { Content = new StringContent("{}") };
            Assert.False((await restarted.GetAsync(Url, false, TestContext.Current.CancellationToken)).Cached);
            Assert.Equal(3, handler.Calls);
        }
        finally { Directory.Delete(directory, true); }
    }
    [Fact]
    public async Task RetryAfterStopsRequestsButOrdinaryForbiddenIsNotTreatedAsRateLimit()
    {
        using var handler = new Handler(); using var client = new HttpClient(handler);
        var requests = new GitHubUpdateRequests(client);
        handler.Reply = () => new(HttpStatusCode.Forbidden) { Content = new StringContent("permission denied") };
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => requests.GetAsync(Url, false, TestContext.Current.CancellationToken));
        Assert.IsNotType<GitHubRateLimitException>(error);
        handler.Reply = () =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromMinutes(3));
            return response;
        };
        await Assert.ThrowsAsync<GitHubRateLimitException>(() => requests.GetAsync(Url, false, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<GitHubRateLimitException>(() => requests.GetAsync(Url, false, TestContext.Current.CancellationToken));
        Assert.Equal(2, handler.Calls);
    }
}
