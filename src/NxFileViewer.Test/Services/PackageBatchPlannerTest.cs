using NxFileViewer.Avalonia;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services;

public sealed class PackageBatchPlannerTest
{
    [Fact]
    public void MergeRunsOneJobForAllSelectedSourcesInInputOrder()
    {
        string[] paths = ["base.nsp", "update.xcz", "dlc.nsz"];
        Assert.Equal(paths, Assert.Single(MainWindow.GroupBatchPackageInputs("Merge", paths)));
    }

    [Theory]
    [InlineData("Split")]
    [InlineData("Xci")]
    [InlineData("Compress")]
    public void OtherActionsRunOneJobPerSource(string action)
    {
        var groups = MainWindow.GroupBatchPackageInputs(action, ["first.nsp", "second.nsp"]);
        Assert.Collection(groups, first => Assert.Equal("first.nsp", Assert.Single(first)),
            second => Assert.Equal("second.nsp", Assert.Single(second)));
    }
}
