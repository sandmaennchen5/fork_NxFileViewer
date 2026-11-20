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
        Assert.Equal(expected, AppSettingsManager.IsUsageStatisticsChoiceConfirmed(document.RootElement));
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
        Assert.True(AppSettingsManager.IsUsageStatisticsChoiceConfirmed(document.RootElement));
        Assert.True(reopened.UsageStatisticsChoiceConfirmed);
        Assert.Equal(enabled, reopened.SendAnonymousUsageStatistics);
    }

    [Fact]
    public void AutomaticallySavedDefaultsStillRequireConfirmation()
    {
        var json = JsonSerializer.Serialize(new AppSettings());
        using var document = JsonDocument.Parse(json);
        Assert.False(AppSettingsManager.IsUsageStatisticsChoiceConfirmed(document.RootElement));
    }
}
