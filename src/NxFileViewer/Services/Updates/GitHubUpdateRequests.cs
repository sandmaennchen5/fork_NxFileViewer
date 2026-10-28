using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Emignatik.NxFileViewer.Localization;

namespace Emignatik.NxFileViewer.Services.Updates;

public sealed record GitHubUpdateResponse(string Body, DateTimeOffset CheckedAt, bool Cached, DateTimeOffset? RetryAt)
{
    public string Notice => RetryAt.HasValue
        ? string.Format(LocalizationManager.Instance.Current.Keys.Update_RateLimited, RetryAt.Value.LocalDateTime) + " " + string.Format(LocalizationManager.Instance.Current.Keys.Update_CachedAt, CheckedAt.LocalDateTime)
        : Cached ? string.Format(LocalizationManager.Instance.Current.Keys.Update_CachedAt, CheckedAt.LocalDateTime) : "";
}
public sealed class GitHubRateLimitException(DateTimeOffset retryAt) : HttpRequestException
{
    public DateTimeOffset RetryAt { get; } = retryAt;
    public override string Message => string.Format(LocalizationManager.Instance.Current.Keys.Update_RateLimited, RetryAt.LocalDateTime);
}

/// <summary>Shared serialized metadata requests; asset downloads remain separate.</summary>
public sealed class GitHubUpdateRequests
{
    private static readonly Lazy<GitHubUpdateRequests> Default = new(() => new(new HttpClient { Timeout = TimeSpan.FromSeconds(30) }, Emignatik.NxFileViewer.Services.ApplicationDataPaths.UpdateCache(AppContext.BaseDirectory)));
    private static readonly ConditionalWeakTable<HttpClient, GitHubUpdateRequests> Custom = new();
    public static GitHubUpdateRequests For(HttpClient? client) => client == null ? Default.Value : Custom.GetValue(client, c => new(c));
    private readonly HttpClient _client;
    private readonly string? _directory;
    private readonly Func<DateTimeOffset> _now;
    private readonly SemaphoreSlim _gate = new(1);
    private readonly Dictionary<string, Entry> _entries = new();
    private DateTimeOffset? _retryAt;
    private long _generation;
    private readonly Dictionary<string, long> _completed = new();
    private sealed record Entry(string Body, DateTimeOffset CheckedAt);
    public GitHubUpdateRequests(HttpClient client, string? directory = null, Func<DateTimeOffset>? now = null)
    {
        _client = client; _directory = directory; _now = now ?? (() => DateTimeOffset.UtcNow);
        if (directory != null)
            try { _retryAt = JsonSerializer.Deserialize<DateTimeOffset?>(File.ReadAllText(Path.Combine(directory, "retry.json"))); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { }
    }
    private string CachePath(string url) => Path.Combine(_directory!, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url))) + ".json");
    private void Save(string path, object value)
    {
        if (_directory == null) return;
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { Directory.CreateDirectory(_directory); File.WriteAllText(temporary, JsonSerializer.Serialize(value)); File.Move(temporary, path, true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        finally { try { if (File.Exists(temporary)) File.Delete(temporary); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { } }
    }
    public async Task<GitHubUpdateResponse> GetAsync(string url, bool automatic, CancellationToken token)
    {
        var uri = new Uri(url);
        if (uri.Scheme != "https" || uri.Host != "api.github.com") throw new ArgumentException("Expected a GitHub API URL.", nameof(url));
        var started = Interlocked.Read(ref _generation);
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            _entries.TryGetValue(url, out var entry);
            if (entry == null && _directory != null)
                try
                {
                    entry = JsonSerializer.Deserialize<Entry>(File.ReadAllText(CachePath(url)));
                    if (entry != null)
                    {
                        if (string.IsNullOrWhiteSpace(entry.Body) || entry.CheckedAt == default) entry = null;
                        else { using var valid = JsonDocument.Parse(entry.Body); }
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { entry = null; }
            var now = _now();
            if (_retryAt > now)
                return entry != null ? new(entry.Body, entry.CheckedAt, true, _retryAt) : throw new GitHubRateLimitException(_retryAt.Value);
            if (entry != null && ((_completed.TryGetValue(url, out var completed) && completed > started) || (automatic && now - entry.CheckedAt < TimeSpan.FromHours(6))))
                return new(entry.Body, entry.CheckedAt, true, null);
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.UserAgent.ParseAdd("NxFileViewer-Updates/1.0");
            if (!automatic) request.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };
            using var response = await _client.SendAsync(request, token).ConfigureAwait(false);
            if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
            {
                var body = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
                var exhausted = response.Headers.TryGetValues("X-RateLimit-Remaining", out var remaining) && remaining.Contains("0");
                if (exhausted || response.StatusCode == HttpStatusCode.TooManyRequests || response.Headers.RetryAfter != null || body.Contains("rate limit", StringComparison.OrdinalIgnoreCase))
                {
                    var retry = response.Headers.RetryAfter;
                    _retryAt = retry?.Date ?? (retry?.Delta is { } delta ? now + delta : now.AddMinutes(1));
                    if (exhausted && response.Headers.TryGetValues("X-RateLimit-Reset", out var resets) && long.TryParse(resets.FirstOrDefault(), out var seconds))
                        try { var reset = DateTimeOffset.FromUnixTimeSeconds(seconds); if (reset > _retryAt) _retryAt = reset; } catch (ArgumentOutOfRangeException) { }
                    if (_retryAt <= now) _retryAt = now.AddMinutes(1);
                    if (_directory != null) Save(Path.Combine(_directory, "retry.json"), _retryAt);
                    return entry != null ? new(entry.Body, entry.CheckedAt, true, _retryAt) : throw new GitHubRateLimitException(_retryAt.Value);
                }
            }
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
            using var validated = JsonDocument.Parse(json);
            if (response.Headers.TryGetValues("X-RateLimit-Remaining", out var left) && left.Contains("0") &&
                response.Headers.TryGetValues("X-RateLimit-Reset", out var next) && long.TryParse(next.FirstOrDefault(), out var epoch))
            {
                try
                {
                    var reset = DateTimeOffset.FromUnixTimeSeconds(epoch);
                    if (reset > _now())
                    {
                        _retryAt = reset;
                        if (_directory != null) Save(Path.Combine(_directory, "retry.json"), reset);
                    }
                }
                catch (ArgumentOutOfRangeException) { }
            }
            entry = new(json, _now());
            _entries[url] = entry;
            _completed[url] = Interlocked.Increment(ref _generation);
            if (_directory != null) Save(CachePath(url), entry);
            return new(entry.Body, entry.CheckedAt, false, null);
        }
        finally { _gate.Release(); }
    }
}
