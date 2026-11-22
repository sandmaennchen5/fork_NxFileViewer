using System;
using System.IO;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Emignatik.NxFileViewer.Services.Updates;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Emignatik.NxFileViewer.Logging;

namespace Emignatik.NxFileViewer.Services;

internal static partial class UsageStatisticsService
{
    private const int ReportIntervalDays = 30;

    private static readonly HttpClient Client = new(new HttpClientHandler { AllowAutoRedirect = false })
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    internal static string BuildVersion { get; } = typeof(UsageStatisticsService).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(UsageStatisticsService).Assembly.GetName().Version?.ToString() ?? "unknown";

    internal static async Task SendStartupAsync(Settings.IAppSettings settings)
    {
        if (!settings.UsageStatisticsChoiceConfirmed || !settings.SendAnonymousUsageStatistics) return;
        try
        {
            var report = CommunityPollService.Endpoint(settings.ReportEndpoint);
            if (report is null) return;
            var participant = SupportIdentityClient.Participant();
            var buildVersion = BuildVersion;
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NxFileViewerFork");
            var day = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var endpoint = new Uri(report, "usage.php").AbsoluteUri;
            var environment = SessionEnvironment.Capture();
            var fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(environment)));
            var payload = new { participant, version = buildVersion.Split('+')[0], buildVersion, environment };
            await SendOnceAsync(directory, new UsageState(day, buildVersion, endpoint, participant, fingerprint), async () =>
            {
                if (!settings.UsageStatisticsChoiceConfirmed || !settings.SendAnonymousUsageStatistics) return;
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
        if (previous is not null &&
            previous.Endpoint == attempt.Endpoint && previous.Participant == attempt.Participant &&
            previous.EnvironmentFingerprint == attempt.EnvironmentFingerprint &&
            DateOnly.TryParseExact(previous.Day, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var lastDay) &&
            DateOnly.TryParseExact(attempt.Day, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var today) &&
            today.DayNumber - lastDay.DayNumber < ReportIntervalDays &&
            !IsUpgrade(previous.BuildVersion, attempt.BuildVersion))
            return;

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

    internal static bool IsUpgrade(string previous, string current)
    {
        // Published previews use beta4, beta5, etc. Compare the suffix as a number.
        static string Normalize(string? version) => Regex.Replace(version ?? "", @"-beta(?=\d+(?:\+|$))", "-beta.");
        return ViewerVersion.TryParse(Normalize(previous), out var before) &&
            ViewerVersion.TryParse(Normalize(current), out var after) && after.CompareTo(before) > 0;
    }

    private sealed record UsageState(string Day, string BuildVersion, string Endpoint, string Participant, string? EnvironmentFingerprint);
}
