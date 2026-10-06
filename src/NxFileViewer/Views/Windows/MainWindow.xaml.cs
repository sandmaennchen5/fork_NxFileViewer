using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace Emignatik.NxFileViewer.Views.Windows;

public enum WorkspaceSection { Home, File, Batch, Rename, Settings, Plugins, Updates }

public partial class MainWindow : Window
{
    private BatchIntegrityWindowViewModel? _batch;
    private SettingsWindowViewModel? _settings;
    private RenameToolWindowViewModel? _rename;
    private SettingsWindowViewModel? _plugins;

    public MainWindow()
    {
        InitializeComponent();
        Closed += (_, _) =>
        {
            _batch?.ClosePreview();
            App.ServiceProvider.GetService<Emignatik.NxFileViewer.Services.FileOpening.IFileOpeningService>()?.SafeClose();
        };
    }

    public void Navigate(WorkspaceSection section)
    {
        if (section is WorkspaceSection.Plugins or WorkspaceSection.Updates)
        {
            EnsurePage(section);
            EnsurePage(WorkspaceSection.Settings);
            WorkspaceTabs.SelectedIndex = (int)WorkspaceSection.Settings;
            ((SettingsWindow)SettingsTab.Content).SettingsSections.SelectedIndex = section == WorkspaceSection.Updates ? 1 : 2;
            return;
        }
        WorkspaceTabs.SelectedIndex = (int)section;
        EnsurePage(section);
        if (section == WorkspaceSection.Settings) ((SettingsWindow)SettingsTab.Content).SettingsSections.SelectedIndex = 0;
    }

    private void EnsurePage(WorkspaceSection section)
    {
        // Resolve lazily after App has created the main window. Page instances stay
        // attached to their tabs, keeping results, drafts and running tasks intact.
        switch (section)
        {
            case WorkspaceSection.Updates when UpdatesTab.Content == null:
                UpdatesTab.Content = new UpdateCenterView { DataContext = App.ServiceProvider.GetRequiredService<UpdateCenterViewModel>() };
                break;
            case WorkspaceSection.Batch when _batch == null:
                _batch = App.ServiceProvider.GetRequiredService<BatchIntegrityWindowViewModel>();
                BatchTab.Content = new BatchIntegrityWindow { DataContext = _batch };
                break;
            case WorkspaceSection.Settings when _settings == null:
                _settings = App.ServiceProvider.GetRequiredService<SettingsWindowViewModel>();
                _settings.EditingCompleted = () => Navigate(WorkspaceSection.Home);
                _plugins ??= App.ServiceProvider.GetRequiredService<SettingsWindowViewModel>();
                _plugins.PluginSettingsOnly = true;
                _plugins.EditingCompleted = () => Navigate(WorkspaceSection.Home);
                var settingsPage = new SettingsWindow { DataContext = _settings };
                settingsPage.SettingsUpdatesContent.Content = new UpdateCenterView { DataContext = App.ServiceProvider.GetRequiredService<UpdateCenterViewModel>() };
                settingsPage.SettingsPluginsContent.Content = new PluginSettingsView { DataContext = _plugins };
                SettingsTab.Content = settingsPage;
                break;
            case WorkspaceSection.Plugins when PluginsTab.Content == null:
                _plugins ??= App.ServiceProvider.GetRequiredService<SettingsWindowViewModel>();
                _plugins.PluginSettingsOnly = true;
                _plugins.EditingCompleted = () => Navigate(WorkspaceSection.Home);
                PluginsTab.Content = new PluginSettingsView { DataContext = _plugins };
                break;
            case WorkspaceSection.Rename when _rename == null:
                _rename = App.ServiceProvider.GetRequiredService<RenameToolWindowViewModel>();
                _rename.EditingCompleted = () => Navigate(WorkspaceSection.Home);
                RenameTab.Content = new RenameToolWindow { DataContext = _rename };
                break;
        }
    }

    private void WorkspaceSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ReferenceEquals(e.OriginalSource, WorkspaceTabs) && WorkspaceTabs.SelectedIndex >= 0)
            EnsurePage((WorkspaceSection)WorkspaceTabs.SelectedIndex);
    }

    private void NavigateHome(object sender, RoutedEventArgs e) => Navigate(WorkspaceSection.Home);
    private void NavigateFile(object sender, RoutedEventArgs e) => Navigate(WorkspaceSection.File);
    private void NavigateBatch(object sender, RoutedEventArgs e) => Navigate(WorkspaceSection.Batch);
    private void NavigateRename(object sender, RoutedEventArgs e) => Navigate(WorkspaceSection.Rename);
    private void NavigateUpdates(object sender, RoutedEventArgs e) => Navigate(WorkspaceSection.Updates);
    private void NavigatePlugins(object sender, RoutedEventArgs e) => Navigate(WorkspaceSection.Plugins);
    private void NavigateSettings(object sender, RoutedEventArgs e) => Navigate(WorkspaceSection.Settings);
}
