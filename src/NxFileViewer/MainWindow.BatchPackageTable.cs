using System.Collections.Generic;
using System.Linq;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace NxFileViewer.Avalonia;

public partial class MainWindow
{
    private static readonly HashSet<string> PackageDetailColumns =
    [
        nameof(BatchRow.ContentType), nameof(BatchRow.TitleId), nameof(BatchRow.Version),
        nameof(BatchRow.MinimumSystemVersion), nameof(BatchRow.BaseTitleId), nameof(BatchRow.MasterKey),
        nameof(BatchRow.MinimumApplicationVersion), nameof(BatchRow.Distribution), nameof(BatchRow.BuildId),
        nameof(BatchRow.Demo), nameof(BatchRow.Security), nameof(BatchRow.Permissions), nameof(BatchRow.AcidSignature),
        nameof(BatchRow.Title), nameof(BatchRow.Publisher), nameof(BatchRow.DisplayVersion), nameof(BatchRow.Languages)
    ];
    private readonly HashSet<DataGrid> _batchPackageTables = [];
    private bool _resettingBatchColumns;

    private void BatchExpandPackagesChanged(object? sender, RoutedEventArgs e)
    {
        if (BatchResults is { } grid && sender is CheckBox checkBox)
            grid.RowDetailsVisibilityMode = checkBox.IsChecked == true
                ? DataGridRowDetailsVisibilityMode.Visible
                : DataGridRowDetailsVisibilityMode.VisibleWhenSelected;
    }

    private bool IsBatchPackageTableEvent(object? source) =>
        (source as global::Avalonia.Visual)?.FindAncestorOfType<DataGrid>(includeSelf: true) is { } grid &&
        _batchPackageTables.Contains(grid);

    private void LoadBatchPackageTable(object? sender, DataGridRowDetailsEventArgs e)
    {
        if (e.DetailsElement is not DataGrid grid || e.Row.DataContext is not BatchRow { ShowPackageDetails: true } row) return;
        _batchPackageTables.Add(grid);
        grid.CanUserReorderColumns = false;
        grid.CanUserResizeColumns = false;
        grid.ItemsSource = new DataGridCollectionView(row.PackageDetails.ToArray());
        SetBatchPackageColumns(grid);
    }

    private void UnloadBatchPackageTable(object? sender, DataGridRowDetailsEventArgs e)
    {
        if (e.DetailsElement is DataGrid grid)
        {
            _batchPackageTables.Remove(grid);
            grid.ItemsSource = null;
        }
    }

    private void RefreshBatchPackageTables()
    {
        if (_resettingBatchColumns) return;
        foreach (var grid in _batchPackageTables.ToArray()) SetBatchPackageColumns(grid);
    }

    private void SetBatchPackageColumns(DataGrid grid)
    {
        var view = grid.ItemsSource as DataGridCollectionView;
        var sorts = view?.SortDescriptions.ToArray();
        grid.Columns.Clear();
        grid.Columns.Add(new DataGridTextColumn { Header = "Nr.", Binding = new Binding(nameof(BatchRow.PartNumber)), Width = new DataGridLength(42) });
        foreach (var source in BatchResults.Columns.OrderBy(column => column.DisplayIndex))
        {
            var path = ColumnPath(source);
            if (path == null || !PackageDetailColumns.Contains(path)) continue;
            var minimumWidth = path switch
            {
                nameof(BatchRow.TitleId) or nameof(BatchRow.BaseTitleId) => 145,
                nameof(BatchRow.Title) => 240,
                _ => 90
            };
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = source.Header,
                Binding = new Binding(path),
                IsVisible = source.IsVisible,
                MinWidth = minimumWidth,
                Width = DataGridLength.Auto,
                SortMemberPath = source.SortMemberPath
            });
        }
        if (view == null || sorts == null) return;
        using (view.DeferRefresh())
        {
            view.SortDescriptions.Clear();
            foreach (var sort in sorts.Where(sort => grid.Columns.Any(column => column.IsVisible &&
                (string.IsNullOrEmpty(column.SortMemberPath) ? ColumnPath(column) : column.SortMemberPath) == sort.PropertyPath)))
                view.SortDescriptions.Add(sort);
        }
    }
}
