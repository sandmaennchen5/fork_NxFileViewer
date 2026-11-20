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

    private Task SendAsync(string day, string version, Func<Task> send)
    {
        var service = typeof(Emignatik.NxFileViewer.Logging.SessionFileLog).Assembly
            .GetType("Emignatik.NxFileViewer.Services.UsageStatisticsService", true)!;
        var stateType = service.GetNestedType("UsageState", BindingFlags.NonPublic)!;
        var state = Activator.CreateInstance(stateType, day, version, "https://example.test/usage.php", new string('a', 64));
        return (Task)service.GetMethod("SendOnceAsync", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, new object?[] { _directory, state, send })!;
    }

    [Fact]
    public async Task SameDayIsSkippedButNewDayAndVersionAreSent()
    {
        var count = 0;
        Task Send() { count++; return Task.CompletedTask; }
        await SendAsync("2026-11-19", "1.0", Send);
        await SendAsync("2026-11-19", "1.0", Send);
        Assert.Equal(1, count);
        await SendAsync("2026-11-20", "1.0", Send);
        await SendAsync("2026-11-20", "2.0", Send);
        Assert.Equal(3, count);
    }

    [Fact]
    public async Task OfflineAttemptIsNotRepeatedUntilVersionOrDayChanges()
    {
        var count = 0;
        Task Fail() { count++; throw new HttpRequestException("offline"); }
        await Assert.ThrowsAsync<HttpRequestException>(() => SendAsync("2026-11-19", "1.0", Fail));
        await SendAsync("2026-11-19", "1.0", Fail);
        Assert.Equal(1, count);
        await Assert.ThrowsAsync<HttpRequestException>(() => SendAsync("2026-11-19", "2.0", Fail));
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task ConcurrentLaunchCannotSendTwice()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var count = 0;
        var first = SendAsync("2026-11-19", "1.0", async () =>
        {
            count++;
            started.SetResult();
            await release.Task;
        });
        await started.Task;
        try
        {
            await Assert.ThrowsAsync<IOException>(() => SendAsync("2026-11-19", "1.0", () =>
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
        Task Send() { count++; return Task.CompletedTask; }
        await SendAsync("2026-11-19", "1.0", Send);
        await SendAsync("2026-11-19", "1.0", Send);
        Assert.Equal(1, count);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }
}
