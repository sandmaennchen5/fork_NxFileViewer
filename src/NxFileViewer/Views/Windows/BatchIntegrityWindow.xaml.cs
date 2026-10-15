using System;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.IO;
using Emignatik.NxFileViewer.FileLoading;
using Emignatik.NxFileViewer.Localization;
using Emignatik.NxFileViewer.Services.Integrity;
using Emignatik.NxFileViewer.Services.Nsz;

namespace Emignatik.NxFileViewer.Views.Windows;

// Embedded workspace page; its view model survives navigation.
public partial class BatchIntegrityWindow : UserControl
{
    private readonly DispatcherTimer _layoutTimer = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private readonly List<WeakReference<DataGrid>> _packageGrids = new();
    private string _defaultLayout = "";
    private bool _applyingLayout;
    private BatchIntegrityWindowViewModel? _layoutModel;
    private static readonly HashSet<string> PackageColumns = new(StringComparer.Ordinal)
    {
        "ContentType", "TitleId", "VersionNumber", "SystemVersion", "BaseTitleId", "MasterKey",
        "MinimumApplicationVersion", "Distribution", "BuildId", "IsDemo",
        "PermissionDisplay", "FileSystemPermissions", "AcidSignatureValidity", "Title", "Publisher", "DisplayVersion", "Languages"
    };
    public BatchIntegrityWindow()
    {
        InitializeComponent();
        _defaultLayout = BatchColumnLayout.Capture(ResultsGrid.Columns);
        _layoutTimer.Tick += (_, _) => FlushColumnLayout();
        foreach (var column in ResultsGrid.Columns)
            foreach (var property in new[] { DataGridColumn.VisibilityProperty, DataGridColumn.DisplayIndexProperty, DataGridColumn.WidthProperty })
                DependencyPropertyDescriptor.FromProperty(property, typeof(DataGridColumn)).AddValueChanged(column, OnColumnLayoutChanged);
        Unloaded += (_, _) => { if (_layoutTimer.IsEnabled) FlushColumnLayout(); };
        DataContextChanged += (_, _) =>
        {
            if (DataContext is BatchIntegrityWindowViewModel model)
            {
                System.ComponentModel.PropertyChangedEventManager.AddHandler(model, OnGroupingChanged, nameof(model.GroupFiles));
                UpdateGrouping(model.GroupFiles);
                if (!ReferenceEquals(_layoutModel, model))
                {
                    if (_layoutTimer.IsEnabled) FlushColumnLayout();
                    _layoutModel = model;
                    _applyingLayout = true;
                    try
                    {
                        BatchColumnLayout.Apply(ResultsGrid.Columns, _defaultLayout);
                        BatchColumnLayout.Apply(ResultsGrid.Columns, model.ColumnLayout);
                    }
                    finally { _applyingLayout = false; }
                    SyncPackageGrids();
                }
            }
        };
    }
    private void OnColumnLayoutChanged(object? sender, EventArgs e)
    {
        if (_applyingLayout || _layoutModel == null) return;
        _layoutTimer.Stop();
        _layoutTimer.Start();
    }
    private void FlushColumnLayout()
    {
        _layoutTimer.Stop();
        if (_layoutModel != null) _layoutModel.ColumnLayout = BatchColumnLayout.Capture(ResultsGrid.Columns);
        SyncPackageGrids();
    }
    private void ResetColumns(object sender, RoutedEventArgs e)
    {
        _applyingLayout = true;
        try { BatchColumnLayout.Apply(ResultsGrid.Columns, _defaultLayout); }
        finally { _applyingLayout = false; }
        FlushColumnLayout();
    }
    private void PackageGridLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not DataGrid grid) return;
        if (!_packageGrids.Any(reference => reference.TryGetTarget(out var target) && ReferenceEquals(target, grid)))
            _packageGrids.Add(new WeakReference<DataGrid>(grid));
        SyncPackageGrid(grid);
    }
    private void SyncPackageGrids()
    {
        _packageGrids.RemoveAll(reference => !reference.TryGetTarget(out _));
        foreach (var reference in _packageGrids)
            if (reference.TryGetTarget(out var grid)) SyncPackageGrid(grid);
    }
    private void SyncPackageGrid(DataGrid grid)
    {
        grid.Columns.Clear();
        grid.CanUserReorderColumns = false;
        foreach (var source in ResultsGrid.Columns.OrderBy(column => column.DisplayIndex).OfType<DataGridTextColumn>())
        {
            if (!PackageColumns.Contains(source.SortMemberPath)) continue;
            var column = new DataGridTextColumn
            {
                Binding = source.Binding, SortMemberPath = source.SortMemberPath,
                Visibility = source.Visibility, Width = DataGridLength.Auto,
                MinWidth = source.SortMemberPath == "Title" ? 240 : 90
            };
            BindingOperations.SetBinding(column, DataGridColumn.HeaderProperty, new Binding(nameof(DataGridColumn.Header)) { Source = source });
            grid.Columns.Add(column);
        }
    }
    private void OnGroupingChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    { if (DataContext is BatchIntegrityWindowViewModel model) UpdateGrouping(model.GroupFiles); }
    private void UpdateGrouping(bool grouped)
    {
        ResultsGrid.GroupStyle.Clear();
        if (grouped) ResultsGrid.GroupStyle.Add((GroupStyle)FindResource("SourceGroupStyle"));
    }

    private void TogglePackages(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Primitives.ToggleButton { DataContext: BatchIntegrityResult result } button)
        {
            result.PackagesExpanded = button.IsChecked == true;
            ResultsGrid.SetCurrentValue(DataGrid.SelectedItemProperty, result);
            if (DataContext is BatchIntegrityWindowViewModel model) model.SelectedResult = result;
        }
        e.Handled = true;
    }

    private void OpenFileActions(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGridRow { Item: BatchIntegrityResult file } row ||
            DataContext is not BatchIntegrityWindowViewModel model) return;
        ResultsGrid.SelectedItem = file;
        var keys = LocalizationManager.Instance.Current.Keys;
        var menu = new ContextMenu { PlacementTarget = row };
        void Add(string title, bool enabled, System.Action action)
        {
            var item = new MenuItem { Header = title, IsEnabled = enabled };
            item.Click += (_, _) => action();
            menu.Items.Add(item);
        }
        Add(keys.BatchIntegrity_OpenSelected, model.CanOpenInSingle(file), () => model.OpenInSingle(file));
        Add(keys.Nsz_Compress, model.CanActOnFile(file, NszOperation.Compress), () => model.ConvertSelected(file, NszOperation.Compress));
        Add(keys.Nsz_Decompress, model.CanActOnFile(file, NszOperation.Decompress), () => model.ConvertSelected(file, NszOperation.Decompress));
        Add(keys.MenuItem_OpenTitleWebPage, model.CanOpenTitle(file), () => model.OpenSelectedTitle(file));
        Add(keys.MenuItem_CheckIntegrity, !file.IsNand && !model.BackgroundTask.IsRunning &&
            (Directory.Exists(file.FilePath) || File.Exists(PackageZip.ArchivePath(file.FilePath))), () => model.VerifySelected(file));
        Add(keys.BatchNaming_Check, model.CanCheckNaming(file), () => model.CheckSelectedNaming(file));
        Add(keys.RenamingTool_Button_Rename, model.CanCheckNaming(file), () => model.RenameSelected(file));
        Add(keys.BatchIntegrity_MoveSelected, model.CanActOnFile(file), () => model.MoveSelected(file));
        row.ContextMenu = menu;
        menu.IsOpen = true;
        e.Handled = true;
    }

    private void OpenColumns(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        button.ContextMenu = CreateColumnsMenu(button);
        button.ContextMenu.IsOpen = true;
    }

    private ContextMenu CreateColumnsMenu(Button button)
    {
        var menu = new ContextMenu { PlacementTarget = button };
        foreach (var column in ResultsGrid.Columns.OrderBy(column => column.DisplayIndex))
        {
            var item = new MenuItem { IsCheckable = true, IsChecked = column.Visibility == Visibility.Visible, StaysOpenOnClick = true };
            item.SetBinding(HeaderedItemsControl.HeaderProperty, new Binding(nameof(DataGridColumn.Header)) { Source = column });
            item.Click += (_, _) => column.Visibility = item.IsChecked ? Visibility.Visible : Visibility.Collapsed;
            menu.Items.Add(item);
        }
        return menu;
    }

    private void ScrollTable(object sender, MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Shift) == 0) return;
        var scroll = FindScrollViewer(ResultsGrid);
        if (scroll == null) return;
        scroll.ScrollToHorizontalOffset(scroll.HorizontalOffset - e.Delta);
        e.Handled = true;
    }
    private static ScrollViewer? FindScrollViewer(DependencyObject root)
    {
        if (root is ScrollViewer scroll) return scroll;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            if (FindScrollViewer(VisualTreeHelper.GetChild(root, i)) is { } found) return found;
        return null;
    }
    private void ResetFiltersAndSort(object sender, RoutedEventArgs e)
    {
        if (DataContext is BatchIntegrityWindowViewModel model) model.ResetFiltersCommand.Execute(null);
        ResultsGrid.Items.SortDescriptions.Clear();
        foreach (var column in ResultsGrid.Columns) column.SortDirection = null;
    }
}
