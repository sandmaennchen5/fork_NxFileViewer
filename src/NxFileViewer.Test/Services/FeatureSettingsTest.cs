using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Emignatik.NxFileViewer.Services.Nand;
using Emignatik.NxFileViewer.Services.Nsz;
using Emignatik.NxFileViewer.Settings;
using Emignatik.NxFileViewer.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services;

public sealed class FeatureSettingsTest
{
    [Fact]
    public void ExperimentalPackageOperationsAreOffForNewAndExistingSettings()
    {
        foreach (var settings in new[] { new AppSettings(), JsonSerializer.Deserialize<AppSettings>("{}")! })
        {
            settings.InitializeFeatures();
            Assert.False(settings.EnablePackageOperations);
            Assert.False(settings.PackageOperationsActive);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExperimentalPackageOperationsPersistAndRequireRestart(bool enabled)
    {
        var settings = new AppSettings();
        settings.InitializeFeatures();
        new ShallowCopier().Copy<IAppSettings>(new AppSettings { EnablePackageOperations = enabled }, settings);
        settings.InitializeFeatures();
        Assert.False(settings.PackageOperationsActive);
        var json = JsonSerializer.Serialize(settings);
        Assert.DoesNotContain("PackageOperationsActive", json);
        var restarted = JsonSerializer.Deserialize<AppSettings>(json)!;
        restarted.InitializeFeatures();
        Assert.Equal(enabled, restarted.PackageOperationsActive);
        restarted.EnablePackageOperations = !enabled;
        Assert.Equal(enabled, restarted.PackageOperationsActive);
    }

    [Fact]
    public void DisabledPackageCommandsCannotRunEvenWithConfiguredEngine()
    {
        var engine = System.IO.Path.GetTempFileName();
        try
        {
            var settings = new AppSettings { RustPackageExecutablePath = engine };
            settings.InitializeFeatures();
            var background = new Emignatik.NxFileViewer.Services.BackgroundTask.MainBackgroundTaskRunnerService();
            var actions = new Emignatik.NxFileViewer.Services.PackageOperations.PackageActions(settings, background, null!, null!, null!);
            Assert.False(actions.IsEnabled);
            foreach (var command in new[] { actions.MergeNspCommand, actions.MergeXciCommand, actions.ConvertNspCommand, actions.ConvertXciCommand, actions.SplitCommand })
            {
                Assert.False(command.CanExecute(null));
                command.Execute(null); // Must return before accessing services or opening dialogs.
            }
            settings.EnablePackageOperations = true;
            Assert.False(actions.MergeNspCommand.CanExecute(null));
            var restarted = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(settings))!;
            restarted.InitializeFeatures();
            var enabledActions = new Emignatik.NxFileViewer.Services.PackageOperations.PackageActions(restarted, background, null!, null!, null!);
            Assert.True(enabledActions.IsEnabled);
            foreach (var command in new[] { enabledActions.MergeNspCommand, enabledActions.MergeXciCommand, enabledActions.ConvertNspCommand, enabledActions.ConvertXciCommand, enabledActions.SplitCommand })
                Assert.True(command.CanExecute(null));
        }
        finally { System.IO.File.Delete(engine); }
    }
    [Fact]
    public void OldSettingsKeepFeaturesEnabled()
    {
        var settings = JsonSerializer.Deserialize<AppSettings>("{}")!;
        settings.InitializeFeatures();
        Assert.True(settings.PluginsActive);
        Assert.True(settings.MtpActive);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public void SavedChangesOnlyTakeEffectOnNextStartup(bool plugins, bool mtp)
    {
        var settings = new AppSettings();
        settings.InitializeFeatures();
        var draft = new AppSettings { EnablePlugins = plugins, EnableMtp = mtp };
        new ShallowCopier().Copy<IAppSettings>(draft, settings);
        settings.InitializeFeatures();
        Assert.True(settings.PluginsActive);
        Assert.True(settings.MtpActive);
        var json = JsonSerializer.Serialize(settings);
        Assert.DoesNotContain("PluginsActive", json);
        Assert.DoesNotContain("MtpActive", json);
        var restarted = JsonSerializer.Deserialize<AppSettings>(json)!;
        restarted.InitializeFeatures();
        Assert.Equal(plugins, restarted.PluginsActive);
        Assert.Equal(mtp, restarted.MtpActive);
        restarted.EnablePlugins = true;
        restarted.EnableMtp = true;
        Assert.Equal(plugins, restarted.PluginsActive);
        Assert.Equal(mtp, restarted.MtpActive);
    }

    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(false, true, true, true)]
    [InlineData(true, false, true, false)]
    [InlineData(true, true, false, true)]
    public void OptionalFeaturesPersistIndependentlyAfterRestart(bool nsz, bool nand, bool mtp, bool firmware)
    {
        var settings = new AppSettings();
        settings.InitializeFeatures();
        var draft = new AppSettings { EnableNszPlugin = nsz, EnableNandPlugin = nand, EnableMtp = mtp, EnableFirmwareCheck = firmware };
        new ShallowCopier().Copy<IAppSettings>(draft, settings);
        Assert.True(settings.NszPluginActive);
        Assert.True(settings.NandPluginActive);
        Assert.True(settings.MtpActive);
        Assert.True(settings.FirmwareCheckActive);
        var restarted = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(settings))!;
        restarted.InitializeFeatures();
        Assert.Equal(nsz, restarted.NszPluginActive);
        Assert.Equal(nand, restarted.NandPluginActive);
        Assert.Equal(mtp, restarted.MtpActive);
        Assert.Equal(firmware, restarted.FirmwareCheckActive);
        Assert.Equal(nsz || nand, restarted.PluginsActive);
    }

    [Fact]
    public void LegacyCombinedSwitchAllowsIndependentOverrides()
    {
        var settings = JsonSerializer.Deserialize<AppSettings>("{\"EnablePlugins\":false,\"EnableNszPlugin\":true}")!;
        settings.InitializeFeatures();
        Assert.True(settings.NszPluginActive);
        Assert.False(settings.NandPluginActive);
        Assert.True(settings.MtpActive);
        Assert.True(settings.FirmwareCheckActive);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DisablingOnePluginDoesNotBlockTheOther(bool nszEnabled)
    {
        var settings = new AppSettings { EnableNszPlugin = nszEnabled, EnableNandPlugin = !nszEnabled,
            NszExecutablePath = "custom-nsz.exe", NandExecutablePath = "custom-nand.exe" };
        settings.InitializeFeatures();
        var checkedNsz = false;
        var nsz = new NszPluginManager(settings, NullLogger<NszPluginManager>.Instance,
            compatibilityCheck: (_, _) => checkedNsz = true);
        var nand = new NandPluginManager(settings);
        if (nszEnabled)
        {
            nsz.Prepare(null!, CancellationToken.None);
            Assert.True(checkedNsz);
            Assert.Throws<InvalidOperationException>(() => nand.Prepare(null!, CancellationToken.None));
        }
        else
        {
            nand.Prepare(null!, CancellationToken.None);
            Assert.Throws<InvalidOperationException>(() => nsz.Prepare(null!, CancellationToken.None));
            Assert.False(checkedNsz);
        }
    }

    [Fact]
    public void NandMainSwitchPersistsAndDisablesItsPluginUntilNextStartup()
    {
        var settings = new AppSettings { EnableNand = false, EnableNandPlugin = true };
        settings.InitializeFeatures();
        Assert.False(settings.NandActive);
        Assert.False(settings.NandPluginActive);
        Assert.True(settings.NszPluginActive);
        var disabled = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(settings))!;
        disabled.InitializeFeatures();
        Assert.False(disabled.NandActive);
        Assert.True(disabled.EnableNandPlugin);
        settings.EnableNand = true;
        Assert.False(settings.NandActive);
        Assert.False(settings.NandPluginActive);
        var restarted = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(settings))!;
        restarted.InitializeFeatures();
        Assert.True(restarted.NandActive);
        Assert.True(restarted.NandPluginActive);
    }

    [Fact]
    public void DisabledNandCannotReadNativePartitionsOrInvokeExternalTools()
    {
        var settings = new AppSettings { EnableNand = false, EnableNandPlugin = true };
        settings.InitializeFeatures();
        var nand = new NandCliPlugin(settings);
        Assert.False(nand.IsNandEnabled);
        Assert.False(nand.IsEnabled);
        Assert.Throws<InvalidOperationException>(() => nand.ExplorerPartitions("missing.bin", CancellationToken.None));
        Assert.Throws<InvalidOperationException>(() => nand.ListExplorer("missing.bin", null!, "/", CancellationToken.None));
        Assert.Throws<InvalidOperationException>(() => nand.ExportExplorer("missing.bin", null!, null!, "unused.bin", CancellationToken.None));
        Assert.Throws<InvalidOperationException>(() => nand.ReadInformation("missing.bin", null!, CancellationToken.None));
        var manager = new NandPluginManager(settings);
        Assert.Throws<InvalidOperationException>(() => manager.Prepare(null!, CancellationToken.None));
    }

    [Fact]
    public async Task DisabledPluginsCannotPrepareDownloadOrRollback()
    {
        var settings = new AppSettings { EnablePlugins = false };
        settings.InitializeFeatures();
        var nand = new NandPluginManager(settings);
        var nsz = new NszPluginManager(settings, NullLogger<NszPluginManager>.Instance);
        Assert.Empty(nand.ExecutablePath);
        Assert.Empty(nsz.ExecutablePath);
        Assert.False(nand.CanRollback);
        Assert.False(nsz.CanRollback);
        Assert.Throws<InvalidOperationException>(() => nand.Prepare(null!, CancellationToken.None));
        Assert.Throws<InvalidOperationException>(() => nsz.Prepare(null!, CancellationToken.None));
        Assert.Throws<InvalidOperationException>(() => nand.Rollback());
        Assert.Throws<InvalidOperationException>(() => nsz.Rollback());
        await Assert.ThrowsAsync<InvalidOperationException>(() => nand.UpdateAsync(null!, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => nsz.UpdateAsync(null!, CancellationToken.None));
    }
}
