using System;
using Emignatik.NxFileViewer.Services.PackageOperations;
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
    private DispatcherOperation? _pendingSelectionScroll;
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
        Loaded += (_, _) => ScrollSelectedResultIntoView();
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
                PropertyChangedEventManager.AddHandler(model, OnGameSortingChanged, nameof(model.GameSortAlphabetical));
                OnGameSortingChanged(model, new PropertyChangedEventArgs(nameof(model.GameSortAlphabetical)));
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
    private void ResultsSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // SelectionChanged from nested package grids bubbles through the main grid.
        if (ReferenceEquals(e.OriginalSource, ResultsGrid)) ScrollSelectedResultIntoView();
    }

    private void ScrollSelectedResultIntoView()
    {
        if (_pendingSelectionScroll?.Status == DispatcherOperationStatus.Pending) _pendingSelectionScroll.Abort();
        if (ResultsGrid.SelectedItem is not BatchIntegrityResult selected) return;
        // Wait until bindings and row replacement have finished; only follow the latest selection.
        _pendingSelectionScroll = Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            if (!ReferenceEquals(ResultsGrid.SelectedItem, selected) || !ResultsGrid.Items.Contains(selected)) return;
            if (DataContext is BatchIntegrityWindowViewModel { GroupFiles: true })
            {
                ResultsGrid.UpdateLayout();
                foreach (var group in Descendants<GroupItem>(ResultsGrid))
                    if (group.Content is CollectionViewGroup items && items.Items.Contains(selected))
                        Descendants<Expander>(group).FirstOrDefault()?.SetCurrentValue(Expander.IsExpandedProperty, true);
                ResultsGrid.UpdateLayout();
            }
            // No column argument: keep the horizontal scroll position and keyboard focus.
            ResultsGrid.ScrollIntoView(selected);
        }));
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }
    private GridLength _expandedResultsWidth = new(3, GridUnitType.Star);
    private GridLength _expandedDetailsWidth = new(2, GridUnitType.Star);

    private void ToggleDetailsPane(object sender, RoutedEventArgs e)
    {
        var expanded = DetailsToggleButton.IsChecked == true;
        if (expanded == (DetailsTabs.Visibility == Visibility.Visible)) return;
        if (expanded)
        {
            ResultsPaneColumn.Width = _expandedResultsWidth;
            DetailsPaneColumn.Width = _expandedDetailsWidth;
            DetailsSplitterColumn.Width = new GridLength(31);
            DetailsSplitter.Visibility = Visibility.Visible;
            DetailsTabs.Visibility = Visibility.Visible;
        }
        else
        {
            _expandedResultsWidth = ResultsPaneColumn.Width;
            _expandedDetailsWidth = DetailsPaneColumn.Width;
            DetailsTabs.Visibility = Visibility.Collapsed;
            DetailsSplitter.Visibility = Visibility.Collapsed;
            DetailsPaneColumn.Width = new GridLength(0);
            DetailsSplitterColumn.Width = new GridLength(26);
            ResultsPaneColumn.Width = new GridLength(1, GridUnitType.Star);
        }
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
    private void PrepareExport(object sender, RoutedEventArgs e) => FlushColumnLayout();
    private void ResetColumns(object sender, RoutedEventArgs e)
    {
        var keys = LocalizationManager.Instance.Current.Keys;
        if (ThemedDialog.Confirm(keys.BatchTable_ResetColumns, keys.BatchTable_ConfirmResetColumns, false) != MessageBoxResult.Yes) return;
        ApplyDefaultColumns();
    }
    private void ApplyDefaultColumns()
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
        if (!ResultsGrid.SelectedItems.Contains(file)) ResultsGrid.SelectedItem = file;
        var selected = BatchIntegrityWindowViewModel.DistinctFiles(ResultsGrid.SelectedItems.Cast<BatchIntegrityResult>());
        var menu = selected.Length > 1 ? CreateSelectionActionsMenu(model, selected) : CreateFileActionsMenu(model, file);
        menu.PlacementTarget = row;
        row.ContextMenu = menu;
        menu.IsOpen = true;
        e.Handled = true;
    }

    private ContextMenu CreateSelectionActionsMenu(BatchIntegrityWindowViewModel model, BatchIntegrityResult[] files)
    {
        var keys = LocalizationManager.Instance.Current.Keys;
        var menu = new ContextMenu();
        void Add(string title, Func<BatchIntegrityResult, bool> canAct, Action<BatchIntegrityResult[]> action)
        {
            var eligible = files.Where(canAct).ToArray();
            if (eligible.Length == 0) return;
            var item = new MenuItem { Header = $"{title} ({eligible.Length}/{files.Length})" };
            item.Click += (_, _) => action(eligible);
            menu.Items.Add(item);
        }
        Add(keys.MenuItem_CheckIntegrity, model.CanVerifySelected, model.VerifySelection);
        if (model.NszPluginActive)
        {
            Add(keys.Nsz_Compress, file => model.CanActOnFile(file, NszOperation.Compress), selected => model.ConvertSelection(selected, NszOperation.Compress));
            Add(keys.Nsz_Decompress, file => model.CanActOnFile(file, NszOperation.Decompress), selected => model.ConvertSelection(selected, NszOperation.Decompress));
        }
        Add(keys.BatchNaming_Check, model.CanCheckNaming, selected => model.CheckSelectionNaming(selected, false));
        Add(keys.RenamingTool_Button_Rename, model.CanCheckNaming, selected => model.CheckSelectionNaming(selected, true));
        Add(keys.BatchIntegrity_MoveSelected, file => model.CanActOnFile(file), model.MoveSelection);
        AddPackageActions(menu, model, files, true);
        return menu;
    }
    private void AddPackageActions(ContextMenu menu, BatchIntegrityWindowViewModel model, BatchIntegrityResult[] files, bool multiple)
    {
        if (!model.PackageOperationsActive) return;
        var eligible = files.Where(model.CanPackageFile).ToArray();
        if (eligible.Length == 0) return;
        var keys = LocalizationManager.Instance.Current.Keys;
        menu.Items.Add(new Separator());
        void Add(string label, PackageOperation operation, string type, BatchIntegrityResult[] inputs)
        {
            if (inputs.Length == 0 || operation == PackageOperation.Merge && inputs.Length < 2) return;
            var item = new MenuItem {
                Header = multiple ? $"{label} ({inputs.Length}/{files.Length})" : label,
                IsEnabled = model.CanRunPackageActions
            };
            item.Click += (_, _) => model.RunPackageSelection(inputs, operation, type);
            menu.Items.Add(item);
        }
        Add(keys.Package_ConvertNsp, PackageOperation.Convert, "nsp", eligible);
        Add(keys.Package_ConvertXci, PackageOperation.Convert, "xci", eligible);
        if (multiple)
        {
            Add(keys.Package_MergeNsp, PackageOperation.Merge, "nsp", eligible);
            Add(keys.Package_MergeXci, PackageOperation.Merge, "xci", eligible);
        }
        var combined = eligible.Where(file => file.HasMultiplePackagesInSource).ToArray();
        Add(keys.Package_Split, PackageOperation.Split, "nsp", combined);
        Add(keys.Package_SplitXci, PackageOperation.Split, "xci", combined);
    }
    private ContextMenu CreateFileActionsMenu(BatchIntegrityWindowViewModel model, BatchIntegrityResult file)
    {
        var keys = LocalizationManager.Instance.Current.Keys;
        var menu = new ContextMenu();
        void Add(string title, bool enabled, System.Action action)
        {
            var item = new MenuItem { Header = title, IsEnabled = enabled };
            item.Click += (_, _) => action();
            menu.Items.Add(item);
        }
        Add(keys.BatchTable_OpenExplorer, model.CanOpenLocation(file), () => model.OpenLocation(file));
        Add(keys.BatchIntegrity_OpenSelected, model.CanOpenInSingle(file), () => model.OpenInSingle(file));
        if (model.NszPluginActive)
        {
            Add(keys.Nsz_Compress, model.CanActOnFile(file, NszOperation.Compress), () => model.ConvertSelected(file, NszOperation.Compress));
            Add(keys.Nsz_Decompress, model.CanActOnFile(file, NszOperation.Decompress), () => model.ConvertSelected(file, NszOperation.Decompress));
        }
        Add(keys.MenuItem_OpenTitleWebPage, model.CanOpenTitle(file), () => model.OpenSelectedTitle(file));
        Add(keys.MenuItem_CheckIntegrity, !file.IsNand && !model.BackgroundTask.IsRunning &&
            (Directory.Exists(file.FilePath) || File.Exists(PackageZip.ArchivePath(file.FilePath))), () => model.VerifySelected(file));
        Add(keys.BatchNaming_Check, model.CanCheckNaming(file), () => model.CheckSelectedNaming(file));
        Add(keys.RenamingTool_Button_Rename, model.CanCheckNaming(file), () => model.RenameSelected(file));
        Add(keys.BatchIntegrity_MoveSelected, model.CanActOnFile(file), () => model.MoveSelected(file));
        AddPackageActions(menu, model, new[] { file }, false);
        return menu;
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
    private void OnGameSortingChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (DataContext is not BatchIntegrityWindowViewModel model) return;
        foreach (var column in ResultsGrid.Columns)
            column.SortDirection = model.GameSortAlphabetical ? null :
                model.ResultsView.SortDescriptions.Where(s => s.PropertyName == column.SortMemberPath || s.PropertyName == column.SortMemberPath + "SortKey")
                    .Select(s => (ListSortDirection?)s.Direction).FirstOrDefault();
    }
    private void SortVersionColumn(object sender, DataGridSortingEventArgs e)
    {
        if (DataContext is BatchIntegrityWindowViewModel { GameSortAlphabetical: true }) { e.Handled = true; return; }
        Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
        {
            if (ReferenceEquals(sender, ResultsGrid) && DataContext is BatchIntegrityWindowViewModel model && !model.GameSortAlphabetical)
                model.SaveStandardSorting();
        }));
        if (e.Column.SortMemberPath is not ("SystemVersion" or "SystemUpdateVersion" or "DisplayVersion")) return;
        if (sender is not DataGrid grid || grid.ItemsSource == null) return;
        var view = CollectionViewSource.GetDefaultView(grid.ItemsSource);
        if (!view.CanSort) return;
        e.Handled = true;
        var property = e.Column.SortMemberPath + "SortKey";
        var direction = e.Column.SortDirection == ListSortDirection.Ascending
            ? ListSortDirection.Descending : ListSortDirection.Ascending;
        using (view.DeferRefresh())
        {
            if ((Keyboard.Modifiers & ModifierKeys.Shift) == 0)
            {
                view.SortDescriptions.Clear();
                foreach (var column in grid.Columns) column.SortDirection = null;
            }
            for (var i = view.SortDescriptions.Count - 1; i >= 0; i--)
                if (view.SortDescriptions[i].PropertyName == property)
                    view.SortDescriptions.RemoveAt(i);
            view.SortDescriptions.Add(new SortDescription(property, direction));
            e.Column.SortDirection = direction;
        }
    }
    private void ResetFiltersAndSort(object sender, RoutedEventArgs e)
    {
        var keys = LocalizationManager.Instance.Current.Keys;
        if (ThemedDialog.Confirm(keys.BatchTable_ResetFiltersAndSort, keys.BatchTable_ConfirmResetFiltersAndSort, false) != MessageBoxResult.Yes) return;
        if (DataContext is BatchIntegrityWindowViewModel model) model.ResetFiltersCommand.Execute(null);
        if (DataContext is BatchIntegrityWindowViewModel { GameSortAlphabetical: true }) return;
        ResultsGrid.Items.SortDescriptions.Clear();
        if (DataContext is BatchIntegrityWindowViewModel resetModel) resetModel.SaveStandardSorting();
        foreach (var column in ResultsGrid.Columns) column.SortDirection = null;
    }
}
