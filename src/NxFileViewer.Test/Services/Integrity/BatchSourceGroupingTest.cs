using System;
using System.Collections.Generic;
using Emignatik.NxFileViewer.Models.Overview;
using Emignatik.NxFileViewer.Services.Integrity;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services.Integrity;

public sealed class BatchSourceGroupingTest
{
    [Theory]
    [InlineData("C:/batch/games/one.nsp", "C:/batch/games")]
    [InlineData("C:/batch/bundle.zip::one.nsp", "C:/batch/bundle.zip")]
    [InlineData("C:/batch/bundle.7z::nested/two.nsp", "C:/batch/bundle.7z")]
    [InlineData("C:/batch/saves.zip", "C:/batch/saves.zip")]
    public void GroupsFilesByTheirActualFolderOrArchive(string path, string expected)
        => Assert.Equal(System.IO.Path.GetFullPath(expected), BatchSourceGrouping.Key(path));

    [Fact]
    public void ExpansionAndUiSubscriptionsDoNotLoseDictionaryEntries()
    {
        var row = new BatchIntegrityResult("multi.nsp", "NSP", "NSP", "Cdn", "None", NcasIntegrity.Unchecked, null);
        var rows = new Dictionary<BatchIntegrityResult, string> { [row] = "retained" };
        var hash = row.GetHashCode();
        row.PropertyChanged += (_, _) => { };
        row.PackagesExpanded = true;
        Assert.Equal(hash, row.GetHashCode());
        Assert.Equal("retained", rows[row]);
        row.PackagesExpanded = false;
        Assert.Equal("retained", rows[row]);
    }
}
