using System;
using System.Collections;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Windows.Data;
using Emignatik.NxFileViewer.Models.Overview;
using Emignatik.NxFileViewer.Services.Integrity;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services;

public sealed class BatchVersionSortTest
{
    [Theory]
    [InlineData("2.0.0", "12.0.0")]
    [InlineData("2.0.0 / 12.0.0", "2.0.0 / 23.0.0")]
    [InlineData("2.0.0 / 12.0.0", "12.0.0")]
    [InlineData("12.0.0", "23.0.0")]
    [InlineData("1.2.0", "1.12.0")]
    [InlineData("1.0.2", "1.0.12")]
    [InlineData("1.0.0-beta.2", "1.0.0-beta.12")]
    [InlineData("1.0.0-rc.1", "1.0.0")]
    [InlineData("1.0.0.2", "1.0.0.12")]
    [InlineData("", "1.0.0")]
    [InlineData("unknown", "1.0.0")]
    public void ComparesVersionComponentsAndPrereleases(string lower, string higher)
    {
        Assert.True(new BatchVersionSortKey(lower).CompareTo(new BatchVersionSortKey(higher)) < 0);
        Assert.True(new BatchVersionSortKey(higher).CompareTo(new BatchVersionSortKey(lower)) > 0);
    }

    [Theory]
    [InlineData("v1.0.0", "1.0.0")]
    [InlineData("1.0.0+build1", "1.0.0+build2")]
    [InlineData("1.0", "1.0.0")]
    public void NormalizesNumericVersionsAndIgnoresBuildMetadata(string left, string right) =>
        Assert.Equal(0, new BatchVersionSortKey(left).CompareTo(new BatchVersionSortKey(right)));

    [Fact]
    public void WpfSortsFirmwareAndDisplayVersionsInBothDirections()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var values = new[] { "23.0.0", "2.0.0", "12.0.0" };
                var rows = values.Select(value => new BatchIntegrityResult(value + ".nsp", "NSP", "", "", "", NcasIntegrity.Unchecked, null)
                { SystemVersion = value, SystemUpdateVersion = value, DisplayVersion = value }).ToArray();
                foreach (var property in new[] { "SystemVersionSortKey", "SystemUpdateVersionSortKey", "DisplayVersionSortKey" })
                {
                    var view = new ListCollectionView(rows);
                    view.SortDescriptions.Add(new SortDescription(property, ListSortDirection.Ascending));
                    Assert.Equal(new[] { "2.0.0", "12.0.0", "23.0.0" }, view.Cast<BatchIntegrityResult>().Select(row => row.DisplayVersion));
                    view.SortDescriptions[0] = new SortDescription(property, ListSortDirection.Descending);
                    Assert.Equal(new[] { "23.0.0", "12.0.0", "2.0.0" }, view.Cast<BatchIntegrityResult>().Select(row => row.DisplayVersion));
                }
                var packageView = new ListCollectionView(values.Select(value => new BatchPackageSummary("", "", "", "")
                { SystemVersion = value, DisplayVersion = value }).ToArray());
                packageView.SortDescriptions.Add(new SortDescription("DisplayVersionSortKey", ListSortDirection.Ascending));
                Assert.Equal(new[] { "2.0.0", "12.0.0", "23.0.0" }, packageView.Cast<BatchPackageSummary>().Select(row => row.DisplayVersion));
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start(); thread.Join();
        if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
}