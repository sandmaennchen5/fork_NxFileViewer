using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Data;
using NxFileViewer.Avalonia;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Views;

[Collection("Avalonia desktop")]
public sealed class BatchColumnLayoutTest
{
    [Fact]
    public Task ClosingAndReopeningRestoresColumnVisibilityOrderAndWidth() =>
        AvaloniaWorkspace.RunAsync(() =>
        {
            var window = new MainWindow();
            var grid = window.FindControl<DataGrid>("BatchResults")!;
            var title = Column(grid, "Title");
            var file = Column(grid, "FileName");
            var titleIndex = file.DisplayIndex;
            try
            {
                title.DisplayIndex = titleIndex;
                title.Width = new DataGridLength(321);
                file.IsVisible = false;
            }
            finally { window.Close(); }

            var reopened = new MainWindow();
            try
            {
                var restored = reopened.FindControl<DataGrid>("BatchResults")!;
                Assert.False(Column(restored, "FileName").IsVisible);
                Assert.Equal(titleIndex, Column(restored, "Title").DisplayIndex);
                Assert.Equal(321, Column(restored, "Title").Width.Value);
            }
            finally { reopened.Close(); }
        }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData("{broken")]
    [InlineData("null")]
    [InlineData("""{"AvaloniaPreferences":{"BatchLayout":{"Columns":null,"Sort":null}}}""")]
    public Task MalformedSettingsLeaveAllColumnsUsable(string settings) =>
        AvaloniaWorkspace.RunAsync(() =>
        {
            File.WriteAllText(Path.Combine(ViewerDataDirectory.Root, ViewerPreferences.FileName), settings);
            var window = new MainWindow();
            try
            {
                var grid = window.FindControl<DataGrid>("BatchResults")!;
                Assert.True(Column(grid, "FileName").IsVisible);
                AssertUniqueIndices(grid);
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken);

    [Fact]
    public Task OlderLayoutIgnoresUnknownAndDuplicateColumnsAndInvalidWidths() =>
        AvaloniaWorkspace.RunAsync(() =>
        {
            var baseline = new MainWindow();
            var oldWidth = Column(baseline.FindControl<DataGrid>("BatchResults")!, "Title").Width;
            baseline.Close();
            (ViewerPreferences.Load() with
            {
                BatchLayout = new SavedBatchLayout(
                    [
                        new("Removed", 0, false, 250, DataGridLengthUnitType.Pixel),
                        new("Title", 99, false, -10, DataGridLengthUnitType.Pixel),
                        new("Title", 100, true, 250, DataGridLengthUnitType.Pixel)
                    ], [], true)
            }).Save();

            var window = new MainWindow();
            try
            {
                var grid = window.FindControl<DataGrid>("BatchResults")!;
                var title = Column(grid, "Title");
                Assert.False(title.IsVisible);
                Assert.Equal(oldWidth, title.Width);
                Assert.True(Column(grid, "FileName").IsVisible);
                Assert.Equal(grid.Columns.Count - 1, title.DisplayIndex);
                AssertUniqueIndices(grid);
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken);

    private static DataGridTextColumn Column(DataGrid grid, string path) =>
        grid.Columns.OfType<DataGridTextColumn>().Single(column => (column.Binding as Binding)?.Path == path);

    private static void AssertUniqueIndices(DataGrid grid) =>
        Assert.Equal(grid.Columns.Count, grid.Columns.Select(column => column.DisplayIndex).Distinct().Count());
}
