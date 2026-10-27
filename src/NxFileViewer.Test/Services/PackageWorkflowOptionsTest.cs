using System;
using System.IO;
using System.Linq;
using System.Threading;
using Emignatik.NxFileViewer.Services.PackageOperations;
using Emignatik.NxFileViewer.Settings;
using Emignatik.NxFileViewer.Views.Windows;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services;

public sealed class PackageWorkflowOptionsTest
{
    [Theory]
    [InlineData(PackageEngine.RustWithKronosBackup, PackageEngine.Rust, PackageEngine.Kronos)]
    [InlineData(PackageEngine.KronosWithRustBackup, PackageEngine.Kronos, PackageEngine.Rust)]
    public void PackageBackupRetriesOnlyOnce(PackageEngine mode, PackageEngine first, PackageEngine backup)
    {
        var calls = new System.Collections.Generic.List<PackageEngine>();
        var result = PackageEngineRetry.Run(mode, (engine, attempt) => {
            calls.Add(engine);
            if (attempt == 0) throw new IOException("primary failed");
            return "validated";
        }, CancellationToken.None);
        Assert.Equal("validated", result);
        Assert.Equal(new[] { first, backup }, calls);
        var failure = Assert.Throws<AggregateException>(() => PackageEngineRetry.Run<string>(mode,
            (_, _) => throw new IOException("failed"), CancellationToken.None));
        Assert.Equal(2, failure.InnerExceptions.Count);
    }
    [Fact]
    public void CancellationNeverStartsBackup()
    {
        using var cancel = new CancellationTokenSource();
        var calls = 0;
        Assert.ThrowsAny<OperationCanceledException>(() => PackageEngineRetry.Run<string>(PackageEngine.RustWithKronosBackup,
            (_, _) => { calls++; cancel.Cancel(); throw new OperationCanceledException(cancel.Token); }, cancel.Token));
        Assert.Equal(1, calls);
    }
    [Fact]
    public void OutputModesResolvePerSourceAndDoNotCreateGuidDirectories()
    {
        var source = Path.Combine(Path.GetTempPath(), "games", "game.nsz");
        var custom = Path.Combine(Path.GetTempPath(), "output");
        Assert.Equal(Path.GetDirectoryName(source), OperationOutputSelection.Resolve(OutputLocation.NextToSource, custom, "Split", source));
        Assert.Equal(Path.Combine(Path.GetDirectoryName(source)!, "Split"), OperationOutputSelection.Resolve(OutputLocation.ActionSubdirectory, custom, "Split", source));
        Assert.Equal(custom, OperationOutputSelection.Resolve(OutputLocation.Custom, custom, "Split", source));
        Assert.Throws<ArgumentException>(() => OperationOutputSelection.Resolve(OutputLocation.Custom, "", "Split", source));
    }
    [Fact]
    public void PublicationUsesExistingFolderAndNeverOverwritesExistingFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var ready = Path.Combine(root, "ready"); var destination = Path.Combine(root, "Split");
        Directory.CreateDirectory(ready); Directory.CreateDirectory(destination);
        try
        {
            var source = Path.Combine(ready, "game.nsp");
            File.WriteAllText(source, "validated");
            File.WriteAllText(Path.Combine(destination, "keep.txt"), "existing");
            PackageOutputPublisher.Publish(ready, destination, CancellationToken.None, mergeIntoExisting: true);
            Assert.Equal("validated", File.ReadAllText(Path.Combine(destination, "game.nsp")));
            Assert.Equal("existing", File.ReadAllText(Path.Combine(destination, "keep.txt")));
            Assert.Empty(Directory.GetDirectories(destination));
            File.WriteAllText(source, "second");
            Assert.Throws<IOException>(() => PackageOutputPublisher.Publish(ready, destination, CancellationToken.None, mergeIntoExisting: true));
            Assert.Equal("second", File.ReadAllText(source));
            Assert.Equal("validated", File.ReadAllText(Path.Combine(destination, "game.nsp")));
            Assert.Equal(2, Directory.GetFiles(destination).Length);
        }
        finally { Directory.Delete(root, true); }
    }
    [Fact]
    public void ApplyingPluginSettingsIncludesOutputDefaults()
    {
        var source = new AppSettings { MergeOutputLocation = OutputLocation.Custom,
            SplitOutputLocation = OutputLocation.NextToSource,
            CompressionOutputLocation = OutputLocation.Custom,
            DecompressionOutputLocation = OutputLocation.NextToSource,
            CustomOutputDirectory = @"C:\Output", PackageEngine = PackageEngine.KronosWithRustBackup };
        var target = new AppSettings();
        typeof(SettingsWindowViewModel).GetMethod("CopyPluginSettings",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!.Invoke(null, new object[] { source, target });
        Assert.Equal(source.MergeOutputLocation, target.MergeOutputLocation);
        Assert.Equal(source.SplitOutputLocation, target.SplitOutputLocation);
        Assert.Equal(source.CompressionOutputLocation, target.CompressionOutputLocation);
        Assert.Equal(source.DecompressionOutputLocation, target.DecompressionOutputLocation);
        Assert.Equal(source.CustomOutputDirectory, target.CustomOutputDirectory);
        Assert.Equal(source.PackageEngine, target.PackageEngine);
    }
}