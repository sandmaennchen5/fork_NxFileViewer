using System;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services;

public sealed class UsageStatisticsTest : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "NxUsageTest-" + Guid.NewGuid());

    private Task SendAsync(string day, string version, Func<string, Task> send, string fingerprint = "system-a", bool enabled = false) =>
        Emignatik.NxFileViewer.Services.UsageStatisticsService.SendOnceAsync(_directory,
            new Emignatik.NxFileViewer.Services.UsageStatisticsService.UsageState(day, version, "https://example.test/usage.php", new string('a',64), fingerprint, enabled), send);

    [Fact]
    public async Task StandardReportsIgnoreWeeklyOptOutAndOnlySendOnChanges()
    {
        var kinds = new System.Collections.Generic.List<string>();
        Task Send(string kind) { kinds.Add(kind); return Task.CompletedTask; }
        await SendAsync("2026-10-01", "4.1.1-beta9+old", Send);
        await SendAsync("2026-11-20", "4.1.1-beta9+old", Send);
        Assert.Single(kinds);
        await SendAsync("2026-11-20", "4.1.1-beta9+rebuild", Send);
        await SendAsync("2026-11-20", "4.1.1-beta10", Send);
        await SendAsync("2026-11-20", "4.1.1-beta10", Send, "changed-architecture-or-distribution");
        await SendAsync("2026-12-20", "4.1.1-beta10", Send, "changed-architecture-or-distribution");
        Assert.Equal(4, kinds.Count);
        Assert.All(kinds, kind => Assert.Equal("standard",kind));
    }

    [Fact]
    public async Task EnabledReportsSendEverySevenDaysAndPreferenceChangesAreReported()
    {
        var kinds = new System.Collections.Generic.List<string>();
        Task Send(string kind) { kinds.Add(kind); return Task.CompletedTask; }
        await SendAsync("2026-11-01", "1.0.0", Send, enabled:true);
        await SendAsync("2026-11-07", "1.0.0", Send, enabled:true);
        Assert.Single(kinds);
        await SendAsync("2026-11-08", "1.0.0", Send, enabled:true);
        await SendAsync("2026-11-08", "1.0.0", Send, enabled:true);
        Assert.Equal(new[]{"standard","system"}, kinds);
        await SendAsync("2026-11-09", "1.0.0", Send, enabled:false);
        await SendAsync("2026-12-09", "1.0.0", Send, enabled:false);
        Assert.Equal(3, kinds.Count);
        Assert.Equal("standard",kinds[2]);
    }

    [Fact]
    public async Task FailedInitialReportIsRetriedUntilSuccessful()
    {
        var count = 0;
        Task Fail(string _) { count++; throw new HttpRequestException("offline"); }
        await Assert.ThrowsAsync<HttpRequestException>(() => SendAsync("2026-11-19", "1.0.0", Fail));
        await Assert.ThrowsAsync<HttpRequestException>(() => SendAsync("2026-11-19", "1.0.0", Fail));
        Assert.Equal(2,count);
        await SendAsync("2026-11-19", "1.0.0", _ => {count++;return Task.CompletedTask;});
        await SendAsync("2026-11-19", "1.0.0", Fail);
        Assert.Equal(3,count);
    }

    [Fact]
    public async Task ConcurrentLaunchCannotSendTwice()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var count = 0;
        var first = SendAsync("2026-11-19", "1.0", async _ =>
        {
            count++;
            started.SetResult();
            await release.Task;
        });
        await started.Task;
        try
        {
            await Assert.ThrowsAsync<IOException>(() => SendAsync("2026-11-19", "1.0", _ =>
            {
                count++;
                return Task.CompletedTask;
            }));
        }
        finally { release.SetResult(); }
        await first;
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task InvalidStateIsReplacedAndNextLaunchIsSkipped()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "usage-statistics.json"), "{invalid");
        var count = 0;
        Task Send(string _) { count++; return Task.CompletedTask; }
        await SendAsync("2026-11-19", "1.0", Send);
        await SendAsync("2026-11-19", "1.0", Send);
        Assert.Equal(1, count);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }
}
