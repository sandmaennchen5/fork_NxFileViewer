using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace Emignatik.NxFileViewer.Views.Windows;

// Embedded workspace page; its view model survives navigation.
public partial class BatchIntegrityWindow : UserControl
{
    public BatchIntegrityWindow() => InitializeComponent();

    private void OpenColumns(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        button.ContextMenu = CreateColumnsMenu(button);
        button.ContextMenu.IsOpen = true;
    }

    private ContextMenu CreateColumnsMenu(Button button)
    {
        var menu = new ContextMenu { PlacementTarget = button };
        foreach (var column in ResultsGrid.Columns)
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
    private void ResetSort(object sender, RoutedEventArgs e)
    {
        ResultsGrid.Items.SortDescriptions.Clear();
        foreach (var column in ResultsGrid.Columns) column.SortDirection = null;
    }
}
