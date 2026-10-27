using System.Text.Json;
using System.Text.Json.Serialization;
using Emignatik.NxFileViewer.Settings;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services;

public class DefaultSettingsTest
{
    [Fact]
    public void EveryConfiguredDefaultMatchesTheEffectiveSetting()
    {
        using var stream = typeof(AppSettings).Assembly.GetManifestResourceStream("Viewer.DefaultSettings.json")!;
        using var doc = JsonDocument.Parse(stream);
        var settings = new AppSettings();
        var options = new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } };
        foreach (var section in new[] { "Settings", "RenamingOptions" })
        {
            object target = section == "Settings" ? settings : settings.RenamingOptions;
            foreach (var entry in doc.RootElement.GetProperty(section).EnumerateObject())
            {
                var property = target.GetType().GetProperty(entry.Name);
                Assert.NotNull(property);
                Assert.Equal(entry.Value.Deserialize(property.PropertyType, options), property.GetValue(target));
            }
        }
    }

    [Fact]
    public void SavedValuesOverrideDefaultsAndMissingValuesUseDefaults()
    {
        var settings = JsonSerializer.Deserialize<AppSettings>("""
            {"EnableMtp":false,"BatchHistoryLimit":17,"Theme":"Dark",
             "RenamingOptions":{"IncludeSubdirectories":false,"ApplicationPattern":"custom-{TitleId}"}}
            """)!;
        Assert.False(settings.EnableMtp);
        Assert.Equal(17, settings.BatchHistoryLimit);
        Assert.Equal("Dark", settings.Theme.ToString());
        Assert.Equal(DefaultSettings.Get<bool>("Settings", "EnableFirmwareCheck"), settings.EnableFirmwareCheck);
        Assert.False(settings.RenamingOptions.IncludeSubdirectories);
        Assert.Equal("custom-{TitleId}", settings.RenamingOptions.ApplicationPattern);
        Assert.Equal(DefaultSettings.Get<bool>("RenamingOptions", "IsSimulation"), settings.RenamingOptions.IsSimulation);
    }

    [Fact]
    public void LegacyPluginSettingAndExplicitIndividualOverridesStillLoad()
    {
        var legacy = JsonSerializer.Deserialize<AppSettings>("""{"EnablePlugins":false}""")!;
        Assert.False(legacy.EnableNszPlugin);
        Assert.False(legacy.EnableNandPlugin);
        var individual = JsonSerializer.Deserialize<AppSettings>("""{"EnablePlugins":false,"EnableNszPlugin":true}""")!;
        Assert.True(individual.EnableNszPlugin);
        Assert.False(individual.EnableNandPlugin);
    }
}