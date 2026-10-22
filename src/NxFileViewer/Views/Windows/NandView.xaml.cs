using System.Windows.Controls;

namespace Emignatik.NxFileViewer.Views.Windows;

public partial class NandView : UserControl
{
    public NandView() => InitializeComponent();
    private void SelectExplorerFolder(object sender, System.Windows.RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is NandViewModel model && e.NewValue is NandExplorerFolder folder && model.ExplorerPath != folder.Path)
            model.LoadExplorer(folder.Path);
    }
    private void OpenExplorerFolder(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (DataContext is NandViewModel model && model.SelectedExplorerEntry is { IsDirectory: true } entry)
            model.LoadExplorer(entry.Path);
        else if (DataContext is NandViewModel saveModel && saveModel.OpenSaveCommand.CanExecute(null))
            saveModel.OpenSaveCommand.Execute(null);
    }
}
