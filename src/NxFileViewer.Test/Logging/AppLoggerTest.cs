using Microsoft.Extensions.DependencyInjection;
using Emignatik.NxFileViewer.Logging;
using Emignatik.NxFileViewer.Settings;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Logging;

public class AppLoggerTest
{
    [Theory]
    [InlineData("Services.Nsz.Worker", "Nsz")]
    [InlineData("Services.Nand.Worker", "Nand")]
    [InlineData("Views.Windows.NandViewModel", "Nand")]
    [InlineData("Services.Mtp.Worker", "Mtp")]
    [InlineData("Views.Windows.SphairaMtpWindow", "Mtp")]
    [InlineData("Services.PackageOperations.Worker", "Package")]
    public void OverridesGlobalMinimumAndUpdatesLive(string category, string plugin)
    {
        var settings = new AppSettings { LogLevel = LogLevel.Warning };
        var property = typeof(AppSettings).GetProperty(plugin + "LogLevel")!;
        var count = 0;
        var logger = new AppLogger(settings, "Emignatik.NxFileViewer." + category, (_, _) => count++);
        Assert.False(logger.IsEnabled(LogLevel.Debug));
        property.SetValue(settings, PluginLogLevel.Debug);
        logger.LogDebug("detail");
        Assert.Equal(1, count);
        property.SetValue(settings, PluginLogLevel.None);
        logger.LogCritical("hidden");
        Assert.Equal(1, count);
        property.SetValue(settings, PluginLogLevel.Inherit);
        Assert.False(logger.IsEnabled(LogLevel.Information));
        Assert.True(logger.IsEnabled(LogLevel.Error));
        Assert.False(logger.IsEnabled(LogLevel.None));
    }

    [Fact]
    public void PluginOverrideDoesNotChangeApplicationOrSimilarCategories()
    {
        var settings = new AppSettings { LogLevel = LogLevel.Warning, NszLogLevel = PluginLogLevel.Trace };
        foreach (var category in new[] { "Emignatik.NxFileViewer.Services.NszOther.Worker", "Emignatik.NxFileViewer.FileLoading.FileLoader" })
            Assert.False(new AppLogger(settings, category, (_, _) => {}).IsEnabled(LogLevel.Debug));
    }

    [Fact]
    public void FactoryAllowsPluginDebugAndTraceBeforeProviderFiltering()
    {
        var settings = new AppSettings { LogLevel = LogLevel.Warning, NszLogLevel = PluginLogLevel.Trace };
        var services = new ServiceCollection();
        services.AddSingleton<IAppSettings>(settings);
        services.AddLogging(builder => builder.AddAppLoggerProvider());
        using var provider = services.BuildServiceProvider();
        var count = 0;
        provider.GetRequiredService<IAppLoggerProvider>().Log += (_, _) => count++;
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("Emignatik.NxFileViewer.Services.Nsz.Worker");
        logger.LogTrace("plugin trace");
        logger.LogDebug("plugin debug");
        Assert.Equal(2, count);
        settings.NszLogLevel = PluginLogLevel.None;
        logger.LogCritical("hidden");
        Assert.Equal(2, count);
    }

    [Fact]
    public void SettingsRoundTripAndOldSettingsInherit()
    {
        var original = new AppSettings { NszLogLevel = PluginLogLevel.Debug, NandLogLevel = PluginLogLevel.None,
            MtpLogLevel = PluginLogLevel.Warning, PackageLogLevel = PluginLogLevel.Error };
        var restored = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(System.Text.Json.JsonSerializer.Serialize(original))!;
        Assert.Equal(original.NszLogLevel, restored.NszLogLevel);
        Assert.Equal(original.NandLogLevel, restored.NandLogLevel);
        Assert.Equal(original.MtpLogLevel, restored.MtpLogLevel);
        Assert.Equal(original.PackageLogLevel, restored.PackageLogLevel);
        Assert.Equal(PluginLogLevel.Inherit, System.Text.Json.JsonSerializer.Deserialize<AppSettings>("{}")!.NszLogLevel);
    }

    [Fact]
    public void GlobalNoneSuppressesAllActualLevels()
    {
        var settings = new AppSettings { LogLevel = LogLevel.None };
        var logger = new AppLogger(settings, null, (_, _) => Assert.Fail("Unexpected log"));
        logger.LogCritical("hidden");
        Assert.False(logger.IsEnabled(LogLevel.None));
        var source = new LoggerSource { Level = LogLevel.None };
        Assert.False(source.IsEnabled(LogLevel.Critical));
        Assert.False(source.IsEnabled(LogLevel.None));
    }
}
