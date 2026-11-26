using System.IO;
using System.Threading.Tasks;
using Emignatik.NxFileViewer.Settings;
using Emignatik.NxFileViewer.Test.Views;
using NxFileViewer.Avalonia;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services;

[Collection("Avalonia desktop")]
public sealed class PackageWorkflowOptionsTest
{
    [Theory]
    [InlineData("{}", false)]
    [InlineData("""{"IncludeViewerPrereleases":true}""", true)]
    [InlineData("""{"IncludeViewerPrereleases":true,"AvaloniaPreferences":{}}""", true)]
    [InlineData("""{"IncludeViewerPrereleases":true,"AvaloniaPreferences":{"IncludePreReleases":false}}""", false)]
    [InlineData("""{"IncludeViewerPrereleases":false,"AvaloniaPreferences":{"IncludePreReleases":true}}""", true)]
    public Task PreservesTheSavedPrereleasePreference(string settings, bool expected) =>
        AvaloniaWorkspace.RunAsync(() =>
        {
            File.WriteAllText(Path.Combine(ViewerDataDirectory.Root, ViewerPreferences.FileName), settings);
            Assert.Equal(expected, ViewerPreferences.Load().IncludePreReleases);
        }, TestContext.Current.CancellationToken);

    [Fact]
    public Task MigratedOutputDefaultsSurviveAvaloniaSettingsSaveAndReload() =>
        AvaloniaWorkspace.RunAsync(() =>
        {
            var old = new AppSettings
            {
                MergeOutputLocation = OutputLocation.Custom,
                SplitOutputLocation = OutputLocation.NextToSource,
                CompressionOutputLocation = OutputLocation.Custom,
                DecompressionOutputLocation = OutputLocation.ActionSubdirectory,
                CustomOutputDirectory = ViewerDataDirectory.Root,
                PackageEngine = PackageEngine.KronosWithRustBackup
            };
            ViewerPreferences.FromShared(old).Save();

            var restored = ViewerPreferences.Load();
            Assert.NotNull(restored.OutputModes);
            Assert.Equal((int)OutputLocation.Custom, restored.OutputModes["Merge"]);
            Assert.Equal((int)OutputLocation.NextToSource, restored.OutputModes["Split"]);
            Assert.Equal((int)OutputLocation.Custom, restored.OutputModes["Compress"]);
            Assert.Equal((int)OutputLocation.ActionSubdirectory, restored.OutputModes["Decompress"]);
            Assert.Equal(old.CustomOutputDirectory, restored.CustomOutputFolder);
            Assert.Equal(old.PackageEngine, restored.SharedSettings.PackageEngine);
        }, TestContext.Current.CancellationToken);
}
