using Emignatik.NxFileViewer.Services.PackageOperations;
using Emignatik.NxFileViewer.Settings;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services;

public sealed class PackageEngineTest
{
    [Fact]
    public void RustHasExplicitKeyPathAndNoPythonPatchArguments()
    {
        var args = PackageCommandArguments.BuildArguments(PackageEngine.Rust, PackageOperation.Merge,
            new[] { @"C:\Game Files\base.nsp", @"C:\Game Files\update.nsp" }, "output", "xci", "prod.keys");
        Assert.Contains(@"C:\Game Files\base.nsp", args);
        Assert.Contains("--keys", args);
        Assert.DoesNotContain("false", args); // Rust keypatch accepts u8, not Python's 'false'.
    }

    [Fact]
    public void KronosMergeUsesListAndCalculateRatherThanPathsAsOperationArgument()
    {
        var args = PackageCommandArguments.BuildArguments(PackageEngine.Kronos, PackageOperation.Merge,
            new[] { "base.nsp", "update.nsp" }, "output", "nsp", "keys", "inputs.txt");
        Assert.Equal(new[] { "--direct_multi", "calculate", "-tfile", "inputs.txt", "-o", "output", "-t", "nsp", "-pv", "false", "-kp", "false" }, args);
        Assert.DoesNotContain("--keys", args);
    }

    [Theory]
    [InlineData(PackageEngine.Rust, "--dspl")]
    [InlineData(PackageEngine.Kronos, "--direct_splitter")]
    public void SplitProducesRepackedFilesRatherThanExtractionFolders(PackageEngine engine, string flag)
    {
        var args = PackageCommandArguments.BuildArguments(engine, PackageOperation.Split,
            new[] { "multi.nsp" }, "out", "nsp", "keys");
        Assert.Equal(flag, args[0]);
        Assert.DoesNotContain("--splitter", args);
    }

    [Theory]
    [InlineData("nsp")]
    [InlineData("xci")]
    public void RustSplitPassesRequestedOutputFormat(string format)
    {
        var args = PackageCommandArguments.BuildArguments(PackageEngine.Rust, PackageOperation.Split, new[] { "super.nsp" }, "out", format, "keys");
        Assert.Equal(new[] { "--dspl", "super.nsp", "-o", "out", "-t", format, "--keys", "keys" }, args);
    }
}
