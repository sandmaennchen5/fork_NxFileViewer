using System;
using System.ComponentModel;
using System.Linq;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;

namespace NxFileViewer.Avalonia;

internal sealed record SavedBatchColumn(string BindingPath, int Index, bool Visible, double Width, DataGridLengthUnitType Unit);
internal sealed record SavedBatchSort(string BindingPath, ListSortDirection Direction);
internal sealed record SavedBatchLayout(SavedBatchColumn[] Columns, SavedBatchSort[] Sort, bool DetailsVisible);

public partial class MainWindow
{
    private SavedBatchSort[] _batchSort = [];

    private static string? ColumnPath(DataGridColumn column) => ((column as DataGridBoundColumn)?.Binding as Binding)?.Path;

    private void RestoreBatchLayout()
    {
        if (_preferences.BatchLayout is not { } layout) return;
        _batchSort = layout.Sort.Where(sort => Enum.IsDefined(sort.Direction) &&
            BatchResults.Columns.Any(column => ColumnPath(column) == sort.BindingPath)).ToArray();
        foreach (var saved in layout.Columns.OrderBy(column => column.Index))
        {
            var column = BatchResults.Columns.FirstOrDefault(column => ColumnPath(column) == saved.BindingPath);
            if (column == null) continue;
            column.DisplayIndex = Math.Clamp(saved.Index, 0, BatchResults.Columns.Count - 1);
            column.IsVisible = saved.Visible;
            if (double.IsFinite(saved.Width) && saved.Width >= 0 && Enum.IsDefined(saved.Unit))
                column.Width = new DataGridLength(saved.Width, saved.Unit);
        }
        BatchDetailsToggle.IsChecked = layout.DetailsVisible;
        ToggleBatchDetails(null, new RoutedEventArgs());
    }

    private SavedBatchLayout CaptureBatchLayout()
    {
        ReadBatchSort();
        return new SavedBatchLayout(
            BatchResults.Columns.Select(column => new SavedBatchColumn(ColumnPath(column) ?? "",
                column.DisplayIndex, column.IsVisible, column.Width.Value, column.Width.UnitType)).ToArray(),
            _batchSort, BatchDetailsToggle.IsChecked == true);
    }

    private void ReadBatchSort()
    {
        if (BatchResults.ItemsSource is DataGridCollectionView view && !_batchUsingGameSort)
            _batchSort = view.SortDescriptions.Where(sort => !string.IsNullOrEmpty(sort.PropertyPath))
                .Select(sort => new SavedBatchSort(sort.PropertyPath!, sort.Direction)).ToArray();
    }

    private bool _batchUsingGameSort;

    private void SetBatchItems(BatchRow[] rows)
    {
        ReadBatchSort();
        _batchUsingGameSort = BatchAlphabetical.IsChecked == true;
        BatchGroupFiles.IsEnabled = !_batchUsingGameSort;
        var view = new DataGridCollectionView(rows);
        using (view.DeferRefresh())
        {
            if (BatchGroupFiles.IsChecked == true && !_batchUsingGameSort)
                view.GroupDescriptions.Add(new DataGridPathGroupDescription(nameof(BatchRow.SourceGroupKey)));
            if (!_batchUsingGameSort)
                foreach (var sort in _batchSort) view.SortDescriptions.Add(DataGridSortDescription.FromPath(sort.BindingPath, sort.Direction));
        }
        BatchResults.ItemsSource = view;
    }

    private void ToggleBatchDetails(object? sender, RoutedEventArgs e)
    {
        if (BatchDetailsTabs == null) return;
        var visible = BatchDetailsToggle.IsChecked == true;
        BatchDetailsTabs.IsVisible = visible;
        BatchDetailsSplitter.IsVisible = visible;
        var width = BatchTableLayout.ColumnDefinitions[2].Width;
        if (!visible && width.Value > 0) _batchDetailsWidth = width;
        BatchTableLayout.ColumnDefinitions[2].Width = visible ? _batchDetailsWidth : new GridLength(0);
        BatchDetailsToggle.Content = visible ? "▸" : "◂";
    }

    private GridLength _batchDetailsWidth = new(2, GridUnitType.Star);
}
