using System;
using System.Text.Json;
namespace Emignatik.NxFileViewer.Settings;
public static class SystemReportChoice
{
    public static bool IsUsageStatisticsChoiceConfirmed(JsonElement savedSettings)
    {
        if (savedSettings.ValueKind != JsonValueKind.Object ||
            !savedSettings.TryGetProperty(nameof(IAppSettings.SendAnonymousUsageStatistics), out var enabled) ||
            (enabled.ValueKind != JsonValueKind.True && enabled.ValueKind != JsonValueKind.False))
            return false;
        // Earlier versions only wrote false when statistics had been explicitly disabled.
        if (enabled.ValueKind == JsonValueKind.False) return true;
        return savedSettings.TryGetProperty(nameof(IAppSettings.UsageStatisticsChoiceConfirmed), out var confirmed) &&
            confirmed.ValueKind == JsonValueKind.True;
    }

    public static bool ShouldConfirmSystemReport(IAppSettings settings, string currentVersion) =>
        !settings.UsageStatisticsChoiceConfirmed;
}
