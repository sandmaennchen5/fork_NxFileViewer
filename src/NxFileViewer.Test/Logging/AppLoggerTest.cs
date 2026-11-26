using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Emignatik.NxFileViewer.Settings;
using Emignatik.NxFileViewer.Test.Views;
using NxFileViewer.Avalonia;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Logging;

[Collection("Avalonia desktop")]
public sealed class AppLoggerTest
{
    [Fact]
    public Task PluginMinimumOverridesGlobalLevelAndOffSuppressesCritical() =>
        AvaloniaWorkspace.RunAsync(() =>
        {
            (ViewerPreferences.Load() with
            {
                LogMinimumLevel = "WARN",
                PluginLogLevels = new Dictionary<string, string> { ["NSZ"] = "TRACE", ["NAND"] = "OFF", ["MTP"] = "" }
            }).Save();
            var window = new MainWindow();
            try
            {
                window.LogSession("DEBUG", "nsz-debug-kept", "NSZ");
                window.LogSession("CRITICAL", "nand-critical-hidden", "NAND");
                window.LogSession("DEBUG", "mtp-debug-hidden", "MTP");
                window.LogSession("DEBUG", "app-debug-hidden");
                window.LogSession("DEBUG", "similar-plugin-hidden", "NszOther");
                window.LogSession("WARN", "app-warning-kept");
            }
            finally { window.Close(); }
            var log = string.Join("\n", Directory.GetFiles(Path.Combine(ViewerDataDirectory.Root, "Logs"), "*.log")
                .Select(File.ReadAllText));
            Assert.Contains("nsz-debug-kept", log);
            Assert.Contains("app-warning-kept", log);
            Assert.DoesNotContain("-hidden", log);
        }, TestContext.Current.CancellationToken);

    [Fact]
    public void SettingsRoundTripAndOldSettingsInherit()
    {
        var original = new AppSettings
        {
            NszLogLevel = PluginLogLevel.Debug,
            NandLogLevel = PluginLogLevel.None,
            MtpLogLevel = PluginLogLevel.Warning,
            PackageLogLevel = PluginLogLevel.Error
        };
        var restored = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(original))!;
        Assert.Equal(original.NszLogLevel, restored.NszLogLevel);
        Assert.Equal(original.NandLogLevel, restored.NandLogLevel);
        Assert.Equal(original.MtpLogLevel, restored.MtpLogLevel);
        Assert.Equal(original.PackageLogLevel, restored.PackageLogLevel);
        Assert.Equal(PluginLogLevel.Inherit, JsonSerializer.Deserialize<AppSettings>("{}")!.NszLogLevel);
    }
}
