using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using Emignatik.NxFileViewer.FileLoading;
using Emignatik.NxFileViewer.Models;
using Emignatik.NxFileViewer.Models.Overview;
using Emignatik.NxFileViewer.Services.BackgroundTask;
using Emignatik.NxFileViewer.Services.Integrity;
using Emignatik.NxFileViewer.Services.Nsz;
using Emignatik.NxFileViewer.Services.PackageOperations;
using Emignatik.NxFileViewer.Settings;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services;

public sealed class PackageEngineTest
{
    [Fact]
    public void SelectionPersistsAndJobSnapshotSurvivesSettingsChanges()
    {
        var original = new AppSettings { PackageEngine = PackageEngine.Kronos,
            RustPackageExecutablePath = @"C:\Tools\nscb.exe", KronosPackageDirectory = @"C:\Tools\Kronos" };
        var saved = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(original))!;
        var job = PackageEngineConfiguration.Capture(saved);
        saved.PackageEngine = PackageEngine.Rust;
        saved.KronosPackageDirectory = "changed";
        Assert.Equal(PackageEngine.Kronos, job.Engine);
        Assert.Equal(original.KronosPackageDirectory, job.KronosDirectory);
        Assert.Equal(original.RustPackageExecutablePath, saved.RustPackageExecutablePath);
        Assert.Equal(PackageEngine.Rust, JsonSerializer.Deserialize<AppSettings>("{}")!.PackageEngine);
    }

    [Fact]
    public void RustHasExplicitKeyPathAndNoPythonPatchArguments()
    {
        var args = PackageOperationService.BuildArguments(PackageEngine.Rust, PackageOperation.Merge,
            new[] { @"C:\Game Files\base.nsp", @"C:\Game Files\update.nsp" }, "output", "xci", "prod.keys");
        Assert.Contains(@"C:\Game Files\base.nsp", args);
        Assert.Contains("--keys", args);
        Assert.DoesNotContain("false", args); // Rust keypatch accepts u8, not Python's 'false'.
    }

    [Fact]
    public void KronosMergeUsesListAndCalculateRatherThanPathsAsOperationArgument()
    {
        var args = PackageOperationService.BuildArguments(PackageEngine.Kronos, PackageOperation.Merge,
            new[] { "base.nsp", "update.nsp" }, "output", "nsp", "keys", "inputs.txt");
        Assert.Equal(new[] { "--direct_multi", "calculate", "-tfile", "inputs.txt", "-o", "output", "-t", "nsp", "-pv", "false", "-kp", "false" }, args);
        Assert.DoesNotContain("--keys", args);
    }

    [Theory]
    [InlineData(PackageEngine.Rust, "--dspl")]
    [InlineData(PackageEngine.Kronos, "--direct_splitter")]
    public void SplitProducesRepackedFilesRatherThanExtractionFolders(PackageEngine engine, string flag)
    {
        var args = PackageOperationService.BuildArguments(engine, PackageOperation.Split,
            new[] { "multi.nsp" }, "out", "nsp", "keys");
        Assert.Equal(flag, args[0]);
        Assert.DoesNotContain("--splitter", args);
    }

    [Theory]
    [InlineData(NcasIntegrity.Corrupted)]
    [InlineData(NcasIntegrity.Incomplete)]
    [InlineData(NcasIntegrity.Error)]
    public void InvalidInputCannotStartEngineOrPublishAndReleasesSource(NcasIntegrity integrity)
    {
        var root = Path.Combine(Path.GetTempPath(), "PackageEngineTest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var source = Path.Combine(root, "source.nsp");
            var keys = Path.Combine(root, "test.keys");
            var output = Path.Combine(root, "out");
            File.WriteAllText(source, "unchanged"); File.WriteAllText(keys, "empty test file");
            // If a process were started, this plain text source is not an executable.
            var config = new PackageEngineConfiguration(PackageEngine.Rust, source, "");
            var service = new PackageOperationService(new UnexpectedLoader(), new InvalidVerifier(integrity));
            Assert.Throws<InvalidDataException>(() => service.Run(config, PackageOperation.Convert,
                new[] { source }, output, "xci", keys, new Reporter(), TestContext.Current.CancellationToken));
            Assert.Empty(Directory.GetFileSystemEntries(output));
            Assert.Equal("unchanged", File.ReadAllText(source));
            using var unlocked = new FileStream(source, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void CancelledJobDoesNotCreateOutputOrStartEngine()
    {
        var service = new PackageOperationService(new UnexpectedLoader(), new InvalidVerifier(NcasIntegrity.Error));
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => service.Run(new(PackageEngine.Rust, "missing.exe", ""),
            PackageOperation.Merge, Array.Empty<string>(), "missing-output", "nsp", "missing.keys", new Reporter(), cancellation.Token));
    }

    [Fact]
    public void MergeOrdersActualContentTypesGameThenPatchThenDlcAndKeepsTiesStable()
    {
        var ordered = PackageOperationService.OrderMergeSources(new[] {
            ("dlc.nsp", new[] { LibHac.Ncm.ContentMetaType.AddOnContent }),
            ("update.nsp", new[] { LibHac.Ncm.ContentMetaType.Patch }),
            ("game.xci", new[] { LibHac.Ncm.ContentMetaType.Application }),
            ("second-game.nsp", new[] { LibHac.Ncm.ContentMetaType.Application }),
            ("super.nsp", new[] { LibHac.Ncm.ContentMetaType.AddOnContent, LibHac.Ncm.ContentMetaType.Application }),
            ("unknown.nsp", Array.Empty<LibHac.Ncm.ContentMetaType>())
        });
        Assert.Equal(new[] { "game.xci", "second-game.nsp", "super.nsp", "update.nsp", "dlc.nsp", "unknown.nsp" }, ordered);
        var args = PackageOperationService.BuildArguments(PackageEngine.Rust, PackageOperation.Merge, ordered, "out", "nsp", "keys");
        Assert.Equal(ordered, System.Linq.Enumerable.Take(System.Linq.Enumerable.Skip(args, 1), ordered.Length));
    }

    [Theory]
    [InlineData("nsp")]
    [InlineData("xci")]
    public void RustSplitPassesRequestedOutputFormat(string format)
    {
        var args = PackageOperationService.BuildArguments(PackageEngine.Rust, PackageOperation.Split, new[] { "super.nsp" }, "out", format, "keys");
        Assert.Equal(new[] { "--dspl", "super.nsp", "-o", "out", "-t", format, "--keys", "keys" }, args);
    }
    private sealed class UnexpectedLoader : IFileLoader
    {
        public NxFile Load(string path) => throw new InvalidOperationException("Loader must not run for invalid input.");
    }
    private sealed class InvalidVerifier(NcasIntegrity integrity) : IConversionVerifier
    {
        public BatchIntegrityResult Verify(string path, IProgressReporter progress, CancellationToken cancellationToken) =>
            new(path, "NSP", "NSP", "", "", integrity, "invalid fixture");
    }
    private sealed class Reporter : IProgressReporter
    {
        public void SetMode(bool value) { }
        public void SetText(string value) { }
        public void SetPercentage(double value) { }
    }
}
