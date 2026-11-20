using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Emignatik.NxFileViewer.Logging;

namespace Emignatik.NxFileViewer.Services;

internal static class UsageStatisticsService
{
    private static readonly HttpClient Client = new(new HttpClientHandler { AllowAutoRedirect = false })
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    internal static async Task SendStartupAsync()
    {
        if (!App.Features.UsageStatisticsChoiceConfirmed || !App.Features.SendAnonymousUsageStatistics) return;
        try
        {
            var report = CommunityPollService.Endpoint(App.Features.ReportEndpoint);
            if (report is null) return;
            var participant = SupportIdentityService.Participant();
            var buildVersion = typeof(UsageStatisticsService).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? typeof(UsageStatisticsService).Assembly.GetName().Version?.ToString() ?? "unknown";
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NxFileViewerFork");
            var day = DateTime.UtcNow.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            var endpoint = new Uri(report, "usage.php").AbsoluteUri;
            var payload = new { participant, version = buildVersion.Split('+')[0], buildVersion, environment = SessionEnvironment.Capture() };
            await SendOnceAsync(directory, new UsageState(day, buildVersion, endpoint, participant), async () =>
            {
                if (!App.Features.UsageStatisticsChoiceConfirmed || !App.Features.SendAnonymousUsageStatistics) return;
                using var response = await Client.PostAsJsonAsync(endpoint, payload);
                response.EnsureSuccessStatusCode();
            });
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or HttpRequestException or
                                      OperationCanceledException or JsonException or System.Security.SecurityException)
        {
            // Statistics are optional and must never interrupt application startup.
        }
    }

    private static async Task SendOnceAsync(string directory, UsageState attempt, Func<Task> send)
    {
        Directory.CreateDirectory(directory);
        using var gate = new FileStream(Path.Combine(directory, "usage-statistics.lock"),
            FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var statePath = Path.Combine(directory, "usage-statistics.json");
        UsageState? previous = null;
        try
        {
            if (File.Exists(statePath))
                previous = JsonSerializer.Deserialize<UsageState>(await File.ReadAllTextAsync(statePath));
        }
        catch (JsonException) { }
        if (previous == attempt) return;

        var temporary = Path.Combine(directory, "usage-statistics-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(attempt));
            File.Move(temporary, statePath, true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
        // Record attempts before networking, including failures and concurrent launches.
        await send();
    }

    private sealed record UsageState(string Day, string BuildVersion, string Endpoint, string Participant);
}
