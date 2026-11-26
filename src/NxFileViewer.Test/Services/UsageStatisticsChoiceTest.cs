using System.Text.Json;
using Emignatik.NxFileViewer.Settings;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services;

public sealed class UsageStatisticsChoiceTest
{
    [Theory]
    [InlineData("{}", false)]
    [InlineData("""{"UsageStatisticsChoiceConfirmed":true}""", false)]
    [InlineData("""{"SendAnonymousUsageStatistics":true}""", false)]
    [InlineData("""{"SendAnonymousUsageStatistics":true,"UsageStatisticsChoiceConfirmed":false}""", false)]
    [InlineData("""{"SendAnonymousUsageStatistics":true,"UsageStatisticsChoiceConfirmed":true}""", true)]
    [InlineData("""{"SendAnonymousUsageStatistics":false}""", true)]
    [InlineData("""{"SendAnonymousUsageStatistics":false,"UsageStatisticsChoiceConfirmed":true}""", true)]
    [InlineData("""{"SendAnonymousUsageStatistics":null}""", false)]
    [InlineData("""{"SendAnonymousUsageStatistics":"true","UsageStatisticsChoiceConfirmed":true}""", false)]
    public void SavedValuesDistinguishDefaultsFromAConfirmedChoice(string json, bool expected)
    {
        using var document = JsonDocument.Parse(json);
        Assert.Equal(expected, SystemReportChoice.IsUsageStatisticsChoiceConfirmed(document.RootElement));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ExplicitChoiceSurvivesSerialization(bool enabled)
    {
        var settings = new AppSettings
        {
            SendAnonymousUsageStatistics = enabled,
            UsageStatisticsChoiceConfirmed = true
        };
        var json = JsonSerializer.Serialize(settings);
        using var document = JsonDocument.Parse(json);
        var reopened = JsonSerializer.Deserialize<AppSettings>(json)!;
        Assert.True(SystemReportChoice.IsUsageStatisticsChoiceConfirmed(document.RootElement));
        Assert.True(reopened.UsageStatisticsChoiceConfirmed);
        Assert.Equal(enabled, reopened.SendAnonymousUsageStatistics);
    }

    [Theory]
    [InlineData(false, true, "4.1.1-beta4", "4.1.1-beta4", true)]
    [InlineData(true, true, "4.1.1-beta4", "4.1.1-beta5", false)]
    [InlineData(true, false, "", "4.1.1-beta4", false)]
    [InlineData(true, false, "4.1.1-beta4+old", "4.1.1-beta4+new", false)]
    [InlineData(true, false, "4.1.1-beta4", "4.1.1-beta5", false)]
    [InlineData(true, false, "4.1.1-beta5", "4.1.1-beta4", false)]
    public void StoredSystemReportChoicesAreNotAskedAgainAfterUpdates(
        bool confirmed, bool enabled, string choiceVersion, string currentVersion, bool expected)
    {
        var saved = JsonSerializer.Serialize(new AppSettings
        {
            UsageStatisticsChoiceConfirmed = confirmed,
            SendAnonymousUsageStatistics = enabled,
            UsageStatisticsChoiceVersion = choiceVersion
        });
        var reopened = JsonSerializer.Deserialize<AppSettings>(saved)!;

        Assert.Equal(expected, SystemReportChoice.ShouldConfirmSystemReport(reopened, currentVersion));
    }

    [Fact]
    public void AutomaticallySavedDefaultsStillRequireConfirmation()
    {
        var json = JsonSerializer.Serialize(new AppSettings());
        using var document = JsonDocument.Parse(json);
        Assert.False(SystemReportChoice.IsUsageStatisticsChoiceConfirmed(document.RootElement));
    }
}
