using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Emignatik.NxFileViewer.Services.Integrity;
using Emignatik.NxFileViewer.Settings;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services;

public sealed class BatchColumnLayoutTest
{
    [Fact]
    public void RemembersSelectionOrderAndWidthThroughSettingsSerialization()
    {
        OnSta(() =>
        {
            var grid = Grid();
            var defaults = BatchColumnLayout.Capture(grid.Columns);
            grid.Columns[0].Visibility = Visibility.Collapsed;
            grid.Columns[1].DisplayIndex = 0;
            grid.Columns[1].Width = 321;
            var settings = new AppSettings { BatchTableLayout = BatchColumnLayout.Capture(grid.Columns) };
            var restored = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(settings))!;
            var other = Grid();
            BatchColumnLayout.Apply(other.Columns, restored.BatchTableLayout);
            Assert.Equal(Visibility.Collapsed, other.Columns[0].Visibility);
            Assert.Equal(0, other.Columns[1].DisplayIndex);
            Assert.Equal(321, other.Columns[1].Width.Value);
            BatchColumnLayout.Apply(other.Columns, defaults);
            Assert.All(other.Columns, column => Assert.Equal(Visibility.Visible, column.Visibility));
            Assert.Equal(0, other.Columns[0].DisplayIndex);
            Assert.Equal(120, other.Columns[1].Width.Value);
        });
    }

    [Fact]
    public void HandlesOlderAndDamagedLayoutsWithoutLosingNewColumns()
    {
        OnSta(() =>
        {
            var grid = Grid();
            BatchColumnLayout.Apply(grid.Columns, "{broken");
            BatchColumnLayout.Apply(grid.Columns, "null");
            BatchColumnLayout.Apply(grid.Columns, JsonSerializer.Serialize(new[]
            {
                new BatchColumnState("Removed", 0, false, 120, DataGridLengthUnitType.Pixel),
                new BatchColumnState("Title", 99, false, -10, DataGridLengthUnitType.Pixel),
                new BatchColumnState("Title", 100, true, 250, DataGridLengthUnitType.Pixel)
            }));
            Assert.Equal(0, grid.Columns[1].DisplayIndex);
            Assert.Equal(Visibility.Collapsed, grid.Columns[1].Visibility);
            Assert.Equal(120, grid.Columns[1].Width.Value);
            Assert.Equal(Visibility.Visible, grid.Columns[0].Visibility);
            Assert.Equal(2, grid.Columns.Select(column => column.DisplayIndex).Distinct().Count());
        });
    }
    private static DataGrid Grid()
    {
        var grid = new DataGrid();
        grid.Columns.Add(new DataGridTextColumn { SortMemberPath = "FileName", Width = 120 });
        grid.Columns.Add(new DataGridTextColumn { SortMemberPath = "Title", Width = 120 });
        return grid;
    }
    private static void OnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { action(); } catch (Exception ex) { failure = ex; } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start(); thread.Join();
        if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
