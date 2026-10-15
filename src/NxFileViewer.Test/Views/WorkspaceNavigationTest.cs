using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Emignatik.NxFileViewer.Commands;
using Emignatik.NxFileViewer.Settings;
using Emignatik.NxFileViewer.Views.Windows;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Views;

[CollectionDefinition("Workspace UI", DisableParallelization = true)]
public sealed class WorkspaceUiCollection { }

[Collection("Workspace UI")]
public sealed class WorkspaceNavigationTest
{
    [Fact]
    public void NavigationKeepsPagesAndDraftsAndSettingsButtonsDoNotCloseMainWindow()
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                app.InitializeComponent();
                var main = new MainWindow();
                app.MainWindow = main;
                var closed = false;
                main.Closed += (_, _) => closed = true;
                var tabs = (TabControl)main.FindName("WorkspaceTabs");
                App.ServiceProvider.GetRequiredService<IShowBatchIntegrityWindowCommand>().Execute(null);
                var batchTab = (TabItem)main.FindName("BatchTab");
                var batchPage = Assert.IsType<BatchIntegrityWindow>(batchTab.Content);
                var batch = Assert.IsType<BatchIntegrityWindowViewModel>(batchPage.DataContext);
                var singleMtp = Assert.IsType<Button>(main.FindName("SphairaSingleButton"));
                Assert.Equal("OpenSphairaCommand", singleMtp.GetBindingExpression(Button.CommandProperty)!.ParentBinding.Path.Path);
                var batchMtp = Assert.IsType<Button>(batchPage.FindName("SphairaBatchButton"));
                Assert.Equal(nameof(BatchIntegrityWindowViewModel.BrowseSphairaCommand), batchMtp.GetBindingExpression(Button.CommandProperty)!.ParentBinding.Path.Path);
                Assert.NotNull(App.ServiceProvider.GetRequiredService<Emignatik.NxFileViewer.Services.Mtp.MtpImportService>());
                var mtpWindow = new SphairaMtpWindow();
                Assert.Equal("Sphaira (MTP)", mtpWindow.Title);
                Assert.False(mtpWindow.DirectRead);
                mtpWindow.Close();
                var nandDevice = new Emignatik.NxFileViewer.Services.Nand.NandDriveChoice(@"\\.\PhysicalDrive40", "SD GPP hekate", 12UL * 1024 * 1024 * 1024);
                var drivePicker = new NandDriveWindow(new[] { nandDevice });
                var deviceCombo = Assert.IsType<ComboBox>(Assert.IsType<StackPanel>(drivePicker.Content).Children[1]);
                deviceCombo.SelectedItem = nandDevice; deviceCombo.Text = nandDevice.DisplayName;
                Assert.Equal(nandDevice.Path, drivePicker.Source);
                deviceCombo.SelectedItem = null; deviceCombo.Text = @"\\.\PhysicalDrive41";
                Assert.Equal(@"\\.\PhysicalDrive41", drivePicker.Source);
                drivePicker.Close();
                var sourcePicker = Assert.IsType<ComboBox>(batchPage.FindName("SdSourceModePicker"));
                Assert.Equal("SdSourceMode", sourcePicker.GetBindingExpression(ComboBox.SelectedValueProperty)!.ParentBinding.Path.Path);
                Assert.Equal(3, sourcePicker.Items.Count);
                var previousMode = batch.SdSourceMode;
                Assert.Equal(SdBatchSourceMode.SwitchSd, Assert.IsType<ComboBoxItem>(sourcePicker.Items[2]).Tag);
                batch.SdSourceMode = SdBatchSourceMode.SwitchSd;
                Assert.Equal(SdBatchSourceMode.SwitchSd, batch.SdSourceMode);
                Assert.Equal(SdBatchSourceMode.SwitchSd, App.ServiceProvider.GetRequiredService<IAppSettings>().SdBatchSourceMode);
                batch.SdSourceMode = previousMode;
                batch.InputDirectory = "retained batch directory";
                App.ServiceProvider.GetRequiredService<IShowSettingsWindowCommand>().Execute(null);
                var settingsPage = Assert.IsType<SettingsWindow>(((TabItem)main.FindName("SettingsTab")).Content);
                var settings = Assert.IsType<SettingsWindowViewModel>(settingsPage.DataContext);
                Assert.Equal(5, ((TabControl)settingsPage.FindName("SettingsSections")).Items.Count);
                Assert.Same(settingsPage.FindName("KeysSettingsTab"), ((TabControl)settingsPage.FindName("SettingsSections")).Items[1]);
                Assert.IsType<GroupBox>(settingsPage.FindName("ProdKeysSettingsGroup"));
                Assert.IsType<GroupBox>(settingsPage.FindName("TitleKeysSettingsGroup"));
                main.NavigateNamingSettings();
                var sections = (TabControl)settingsPage.FindName("SettingsSections");
                Assert.Same(settingsPage.FindName("NamingSettingsTab"), sections.SelectedItem);
                Assert.Equal(2, sections.SelectedIndex);
                var originalPattern = settings.EditedSettings.RenamingOptions.ApplicationPattern;
                settings.EditedSettings.RenamingOptions.ApplicationPattern = "GAME/{Title}.{Ext}";
                main.Navigate(WorkspaceSection.Rename);
                main.NavigateNamingSettings();
                Assert.Equal("GAME/{Title}.{Ext}", settings.EditedSettings.RenamingOptions.ApplicationPattern);
                settings.CancelSettingsCommand.Execute(null);
                Assert.Equal(originalPattern, settings.EditedSettings.RenamingOptions.ApplicationPattern);
                main.Navigate(WorkspaceSection.Settings);
                Assert.IsType<UpdateCenterView>(((ContentControl)settingsPage.FindName("SettingsUpdatesContent")).Content);
                Assert.IsType<PluginSettingsView>(((ContentControl)settingsPage.FindName("SettingsPluginsContent")).Content);
                var originalLevel = settings.EditedSettings.NszCompressionLevel;
                settings.EditedSettings.NszCompressionLevel = 7;
                main.Navigate(WorkspaceSection.Batch);
                Assert.Same(batchPage, batchTab.Content);
                Assert.Equal("retained batch directory", batch.InputDirectory);
                main.Navigate(WorkspaceSection.Settings);
                Assert.Same(settingsPage, ((TabItem)main.FindName("SettingsTab")).Content);
                Assert.Equal(7, settings.EditedSettings.NszCompressionLevel);
                settings.CancelSettingsCommand.Execute(null);
                Assert.False(closed);
                Assert.Equal((int)WorkspaceSection.Home, tabs.SelectedIndex);
                Assert.Equal(originalLevel, settings.EditedSettings.NszCompressionLevel);
                main.Navigate(WorkspaceSection.Settings);
                settings.ApplySettingsCommand.Execute(null);
                Assert.False(closed);
                var actual = App.ServiceProvider.GetRequiredService<IAppSettings>();
                var associationButton = Assert.IsType<Button>(settingsPage.FindName("RegisterForkAssociations"));
                Assert.Equal("RegisterFileAssociationsCommand", associationButton.GetBindingExpression(Button.CommandProperty)!.ParentBinding.Path.Path);
                Assert.NotNull(settings.RegisterFileAssociationsCommand);
                var areaCheckbox = Assert.IsType<CheckBox>(settingsPage.FindName("SdBatchAreasEnabled"));
                Assert.Equal("EditedSettings.SdBatchSelectedAreasOnly", areaCheckbox.GetBindingExpression(CheckBox.IsCheckedProperty)!.ParentBinding.Path.Path);
                var originalSdEnabled = actual.SdBatchSelectedAreasOnly; var originalSdNintendo = actual.SdBatchNintendo; var originalSdCustom = actual.SdBatchCustomPaths;
                var customPathsBox = Assert.IsType<TextBox>(settingsPage.FindName("SdBatchCustomPaths"));
                Assert.Equal("EditedSettings.SdBatchCustomPaths", customPathsBox.GetBindingExpression(TextBox.TextProperty)!.ParentBinding.Path.Path);
                settings.EditedSettings.SdBatchSelectedAreasOnly = true; settings.EditedSettings.SdBatchNintendo = false; settings.EditedSettings.SdBatchCustomPaths = "/backup/saves\n/config/game.nsp";
                settings.ApplySettingsCommand.Execute(null);
                Assert.True(actual.SdBatchSelectedAreasOnly); Assert.False(actual.SdBatchNintendo); Assert.Equal("/backup/saves\n/config/game.nsp", actual.SdBatchCustomPaths);
                actual.SdBatchSelectedAreasOnly = originalSdEnabled; actual.SdBatchNintendo = originalSdNintendo; actual.SdBatchCustomPaths = originalSdCustom;
                settings.CancelSettingsCommand.Execute(null);
                var originalTitleUrl = actual.TitlePageUrl;
                settings.EditedSettings.TitlePageUrl = "https://example.com/general-draft";
                main.Navigate(WorkspaceSection.Plugins);
                var pluginPage = Assert.IsType<PluginSettingsView>(((TabItem)main.FindName("PluginsTab")).Content);
                var plugins = Assert.IsType<SettingsWindowViewModel>(pluginPage.DataContext);
                Assert.NotSame(settings, plugins);
                Assert.True(plugins.PluginSettingsOnly);
                plugins.EditedSettings.NszCompressionLevel = 9;
                var originalNandExecutable = actual.NandExecutablePath;
                var originalBisKeys = actual.NandBisKeysPath;
                plugins.EditedSettings.NandExecutablePath = "C:\\Plugins\\NxNandManager.exe";
                plugins.EditedSettings.NandBisKeysPath = "C:\\Keys\\bis.keys";
                plugins.ApplySettingsCommand.Execute(null);
                Assert.Equal("C:\\Plugins\\NxNandManager.exe", actual.NandExecutablePath);
                Assert.Equal("C:\\Keys\\bis.keys", actual.NandBisKeysPath);
                Assert.Equal(9, actual.NszCompressionLevel);
                Assert.Equal(originalTitleUrl, actual.TitlePageUrl);
                Assert.Equal("https://example.com/general-draft", settings.EditedSettings.TitlePageUrl);
                main.Navigate(WorkspaceSection.Settings);
                settings.ApplySettingsCommand.Execute(null);
                Assert.Equal("C:\\Plugins\\NxNandManager.exe", actual.NandExecutablePath);
                main.Navigate(WorkspaceSection.Nand);
                var nandPage = Assert.IsType<NandView>(((TabItem)main.FindName("NandTab")).Content);
                Assert.IsType<NandViewModel>(nandPage.DataContext);
                main.Navigate(WorkspaceSection.Home);
                main.Navigate(WorkspaceSection.Nand);
                Assert.Same(nandPage, ((TabItem)main.FindName("NandTab")).Content);
                var nandRoot = new Emignatik.NxFileViewer.Models.TreeItems.Impl.NandFileItem("synthetic.bin");
                using var nandFile = new Emignatik.NxFileViewer.Models.NxFile("synthetic.bin", nandRoot,
                    new Emignatik.NxFileViewer.Models.Overview.FileOverview(nandRoot))
                {
                    NandPhysicalPath = "synthetic.bin",
                    NandResult = new Emignatik.NxFileViewer.Services.Nand.NandDetectionResult("RAWNAND", new[] { "SYSTEM" }, false, 512, 1)
                };
                var openedNand = new Emignatik.NxFileViewer.Views.UserControls.OpenedFileViewModel(nandFile, App.ServiceProvider);
                Assert.True(openedNand.IsNand);
                Assert.Equal(3, openedNand.InitialDetailsTabIndex);
                Assert.False(openedNand.Nand!.IsStandalone);
                Assert.Equal("SYSTEM", openedNand.Nand.SelectedPartition);
                Assert.False(batch.CanActOnFile(new Emignatik.NxFileViewer.Services.Integrity.BatchIntegrityResult(
                    "synthetic.bin", "NAND", "NAND", "RAWNAND", "None", Emignatik.NxFileViewer.Models.Overview.NcasIntegrity.Unchecked, null) { IsNand = true }));
                var openedNandPage = new Emignatik.NxFileViewer.Views.UserControls.OpenedFileView { DataContext = openedNand };
                var nandDetails = (TabControl)openedNandPage.Content;
                Assert.IsType<NandView>(((TabItem)nandDetails.Items[3]).Content);
                Assert.Equal(9, actual.NszCompressionLevel);
                Assert.Equal("https://example.com/general-draft", actual.TitlePageUrl);
                main.Navigate(WorkspaceSection.Plugins);
                Assert.Same(pluginPage, ((TabItem)main.FindName("PluginsTab")).Content);
                plugins.EditedSettings.NszCompressionLevel = 12;
                plugins.CancelSettingsCommand.Execute(null);
                Assert.Equal(9, actual.NszCompressionLevel);
                Assert.Equal(9, plugins.EditedSettings.NszCompressionLevel);
                Assert.False(closed);
                actual.NszCompressionLevel = originalLevel;
                actual.NandExecutablePath = originalNandExecutable;
                actual.NandBisKeysPath = originalBisKeys;
                actual.TitlePageUrl = originalTitleUrl;
                App.ServiceProvider.GetRequiredService<IShowRenameToolWindowCommand>().Execute(null);
                Assert.IsType<RenameToolWindow>(((TabItem)main.FindName("RenameTab")).Content);
                main.Navigate(WorkspaceSection.Updates);
                var updates = Assert.IsType<UpdateCenterView>(((TabItem)main.FindName("UpdatesTab")).Content);
                var updateModel = Assert.IsType<UpdateCenterViewModel>(updates.DataContext);
                var originalCheckUpdates = actual.NszCheckUpdates;
                var updateCheckbox = (CheckBox)updates.FindName("NszCheckUpdatesBox");
                var updateBinding = updateCheckbox.GetBindingExpression(CheckBox.IsCheckedProperty)!.ParentBinding;
                Assert.Equal(nameof(UpdateCenterViewModel.NszCheckUpdates), updateBinding.Path.Path);
                Assert.Null(updateBinding.RelativeSource);
                updateModel.NszCheckUpdates = false;
                Assert.False(actual.NszCheckUpdates);
                var settingsManager = App.ServiceProvider.GetRequiredService<IAppSettingsManager>();
                Assert.False(settingsManager.Clone().NszCheckUpdates);
                var savedSettingsPath = System.IO.Path.Combine(Emignatik.NxFileViewer.Utils.PathHelper.CurrentAppDir,
                    AppDomain.CurrentDomain.FriendlyName + ".settings.json");
                using (var saved = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(savedSettingsPath)))
                    Assert.False(saved.RootElement.GetProperty(nameof(IAppSettings.NszCheckUpdates)).GetBoolean());
                settings.ApplySettingsCommand.Execute(null);
                Assert.False(actual.NszCheckUpdates);
                var embeddedUpdates = (UpdateCenterView)((ContentControl)settingsPage.FindName("SettingsUpdatesContent")).Content;
                var embeddedModel = Assert.IsType<UpdateCenterViewModel>(embeddedUpdates.DataContext);
                Assert.False(embeddedModel.NszCheckUpdates);
                embeddedModel.NszCheckUpdates = true;
                Assert.True(actual.NszCheckUpdates);
                Assert.True(updateModel.NszCheckUpdates);
                updateModel.NszCheckUpdates = originalCheckUpdates;
                main.Navigate(WorkspaceSection.Home);
                main.Navigate(WorkspaceSection.Updates);
                Assert.Same(updates, ((TabItem)main.FindName("UpdatesTab")).Content);
                // A completed NSZ preview stays available even without its source file.
                var showPreview = typeof(BatchIntegrityWindowViewModel).GetMethod("ShowPreview",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
                var showCompleted = typeof(BatchIntegrityWindowViewModel).GetMethod("ShowCompletedResult",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
                var nszOverview = new Emignatik.NxFileViewer.Models.Overview.FileOverview(new PreviewItem());
                var nszPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid() + ".nsz");
                var nszFile = new Emignatik.NxFileViewer.Models.NxFile(nszPath, nszOverview.RootItem, nszOverview);
                showPreview.Invoke(batch, new object[] { nszFile });
                nszOverview.NcasIntegrity = Emignatik.NxFileViewer.Models.Overview.NcasIntegrity.Original;
                var nszResult = new Emignatik.NxFileViewer.Services.Integrity.BatchIntegrityResult(nszPath,
                    "NSZ", "NSZ", "Cdn", "Blockless", nszOverview.NcasIntegrity, null);
                showCompleted.Invoke(batch, new object[] { nszResult });
                batch.SelectedResult = nszResult;
                var capturedNsz = Assert.IsType<Emignatik.NxFileViewer.Views.UserControls.FileOverviewViewModel>(batch.PreviewOverview);
                Assert.Equal(nszOverview.NcasIntegrity, capturedNsz.NcasIntegrity);
                var nspOverview = new Emignatik.NxFileViewer.Models.Overview.FileOverview(new PreviewItem());
                var nspPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid() + ".nsp");
                batch.SelectedResult = nszResult with { IsFirmware = true };
                Assert.Equal(1, batch.SelectedDetailsTabIndex);
                showPreview.Invoke(batch, new object[] {
                    new Emignatik.NxFileViewer.Models.NxFile(nspPath, nspOverview.RootItem, nspOverview) });
                Assert.Equal(0, batch.SelectedDetailsTabIndex);
                Assert.Null(batch.SelectedResult);
                Assert.False(batch.ShowStoredOverview);
                Assert.NotSame(capturedNsz, batch.PreviewOverview);
                Assert.Equal(nspOverview.NcasIntegrity, batch.PreviewOverview!.NcasIntegrity);
                var nspResult = nszResult with { FilePath = nspPath, FileType = "NSP", Compression = "None" };
                showCompleted.Invoke(batch, new object[] { nspResult });
                Assert.Equal(nspPath, batch.SelectedResult!.FilePath);
                batch.SelectedResult = nspResult;
                Assert.NotNull(batch.PreviewOverview);
                batch.SelectedResult = nszResult;
                Assert.Same(capturedNsz, batch.PreviewOverview);
                var uncachedResult = nszResult with { FilePath = nszPath + ".missing.nsz" };
                batch.Results.Add(uncachedResult);
                batch.SelectedResult = uncachedResult;
                // Per-file conversion must work directly after scanning, but reject missing keys and incompatible types.
                System.IO.File.WriteAllBytes(nspPath, new byte[] { 0 });
                try
                {
                    var uncheckedNsp = nspResult with { Integrity = Emignatik.NxFileViewer.Models.Overview.NcasIntegrity.Unchecked };
                    Assert.True(batch.CanActOnFile(uncheckedNsp, Emignatik.NxFileViewer.Services.Nsz.NszOperation.Compress));
                    Assert.False(batch.CanActOnFile(uncheckedNsp, Emignatik.NxFileViewer.Services.Nsz.NszOperation.Decompress));
                    Assert.False(batch.CanActOnFile(uncheckedNsp with { HasMissingKeys = true }, Emignatik.NxFileViewer.Services.Nsz.NszOperation.Compress));
                    Assert.False(batch.CanActOnFile(uncheckedNsp with { IsFirmware = true }, Emignatik.NxFileViewer.Services.Nsz.NszOperation.Compress));
                    Assert.True(batch.CanOpenInSingle(uncheckedNsp));
                }
                finally { System.IO.File.Delete(nspPath); }
                Assert.Null(batch.PreviewOverview);
                var detailsTabs = (TabControl)batchPage.FindName("DetailsTabs");
                var detailsBinding = detailsTabs.GetBindingExpression(TabControl.SelectedIndexProperty)!.ParentBinding;
                Assert.Equal(nameof(BatchIntegrityWindowViewModel.SelectedDetailsTabIndex), detailsBinding.Path.Path);
                Assert.Equal(System.Windows.Data.BindingMode.TwoWay, detailsBinding.Mode);
                var firmwareResult = nszResult with { FilePath = "firmware.zip", FileType = "ZIP", IsFirmware = true };
                batch.SelectedResult = firmwareResult;
                Assert.Equal(1, batch.SelectedDetailsTabIndex);
                foreach (var type in new[] { "NSP", "NSZ", "XCI", "XCZ" })
                {
                    batch.SelectedDetailsTabIndex = 1;
                    batch.SelectedResult = nszResult with { FileType = type };
                    Assert.Equal(0, batch.SelectedDetailsTabIndex);
                    batch.SelectedResult = firmwareResult;
                    Assert.Equal(1, batch.SelectedDetailsTabIndex);
                }
                batch.SelectedDetailsTabIndex = 0;
                Assert.Equal(0, batch.SelectedDetailsTabIndex);
                batch.SelectedResult = nszResult;
                batch.SelectedResult = firmwareResult;
                Assert.Equal(1, batch.SelectedDetailsTabIndex);
                Assert.Single(app.Windows);
                var firmwareRoot = new Emignatik.NxFileViewer.Models.TreeItems.Impl.FirmwareFileItem("firmware.7z");
                using (var firmwareFile = new Emignatik.NxFileViewer.Models.NxFile("firmware.7z", firmwareRoot,
                    new Emignatik.NxFileViewer.Models.Overview.FileOverview(firmwareRoot))
                    { FirmwareResult = firmwareResult with { Structure = "2.0.0", FirmwareDetails = "synthetic details" } })
                {
                    var firmwareViewModel = new Emignatik.NxFileViewer.Views.UserControls.OpenedFileViewModel(firmwareFile, App.ServiceProvider);
                    Assert.True(firmwareViewModel.HasFirmwareInfo);
                    Assert.True(firmwareViewModel.IsFirmwareOnly);
                    Assert.Equal(2, firmwareViewModel.InitialDetailsTabIndex);
                    Assert.Equal("2.0.0", firmwareViewModel.FirmwareVersion);
                    Assert.Equal("synthetic details", firmwareViewModel.FirmwareDetails);
                }
                var grid = (DataGrid)batchPage.FindName("ResultsGrid");
                Assert.True(grid.CanUserSortColumns);
                Assert.Equal(ScrollBarVisibility.Auto, ScrollViewer.GetHorizontalScrollBarVisibility(grid));
                Assert.All(grid.Columns, column => Assert.False(column.Width.IsStar));
                grid.Width = 500;
                grid.Height = 200;
                grid.ApplyTemplate();
                grid.Measure(new Size(500, 200));
                grid.Arrange(new Rect(0, 0, 500, 200));
                grid.UpdateLayout();
                batch.Results.Clear();
                var multiResult = nszResult with { Packages = new[] {
                    new Emignatik.NxFileViewer.Services.Integrity.BatchPackageSummary("Game", "0100000000000000", "Application", "0"),
                    new Emignatik.NxFileViewer.Services.Integrity.BatchPackageSummary("Update", "0100000000000800", "Patch", "65536") } };
                batch.Results.Add(multiResult);
                grid.Items.Refresh(); grid.UpdateLayout();
                var multiRow = Assert.IsType<DataGridRow>(grid.ItemContainerGenerator.ContainerFromItem(multiResult));
                var cellPresenter = Assert.IsType<ContentPresenter>(grid.Columns[0].GetCellContent(multiRow));
                cellPresenter.ApplyTemplate();
                var cell = Assert.IsType<DockPanel>(System.Windows.Media.VisualTreeHelper.GetChild(cellPresenter, 0));
                var arrow = System.Linq.Enumerable.Single(System.Linq.Enumerable.OfType<System.Windows.Controls.Primitives.ToggleButton>(cell.Children));
                arrow.SetCurrentValue(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, true);
                arrow.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                grid.UpdateLayout();
                Assert.True(multiResult.PackagesExpanded);
                Assert.Equal(Visibility.Visible, multiRow.DetailsVisibility);
                Assert.Same(multiResult, batch.SelectedResult);
                Assert.Same(capturedNsz, batch.PreviewOverview);
                var detailsContent = Assert.IsType<Border>(grid.RowDetailsTemplate.LoadContent());
                var packageGrid = Assert.IsType<DataGrid>(detailsContent.Child);
                packageGrid.ItemsSource = new[] { new Emignatik.NxFileViewer.Services.Integrity.BatchPackageSummary(
                    "Asterix & Obelix Slap Them All! — A long title that must remain readable", "01002B401285E000", "Application", "196608") };
                Assert.All(packageGrid.Columns, column => Assert.True(column.Width.IsAuto));
                Assert.Equal(ScrollBarVisibility.Auto, ScrollViewer.GetHorizontalScrollBarVisibility(packageGrid));
                packageGrid.Measure(new Size(1000, 280));
                packageGrid.Arrange(new Rect(0, 0, 1000, 280));
                packageGrid.UpdateLayout();
                Assert.True(packageGrid.Columns[0].ActualWidth > 240);
                Assert.All(packageGrid.Columns, column => Assert.True(column.ActualWidth >= 90));
                arrow.SetCurrentValue(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, false);
                arrow.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                grid.UpdateLayout();
                Assert.False(multiResult.PackagesExpanded);
                Assert.Equal(Visibility.Collapsed, multiRow.DetailsVisibility);
                var findScroll = typeof(BatchIntegrityWindow).GetMethod("FindScrollViewer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
                var scroll = Assert.IsType<ScrollViewer>(findScroll.Invoke(null, new object[] { grid }));
                Assert.True(scroll.ScrollableWidth > 0);
                Assert.Empty(grid.GroupStyle);
                Assert.All(System.Linq.Enumerable.Where(grid.Columns, column => column.Visibility == Visibility.Visible), column => Assert.True(column.ActualWidth > 0));
                foreach (var localization in Emignatik.NxFileViewer.Localization.LocalizationManager.Instance.RealLocalizations)
                    foreach (var property in localization.Keys.GetType().GetProperties())
                        if (property.GetIndexParameters().Length == 0 && property.PropertyType == typeof(string) && property.GetValue(localization.Keys) is string text)
                        { Assert.DoesNotContain("Ã", text); Assert.DoesNotContain("â€", text); }
                Assert.Contains(grid.Columns, column => column.SortMemberPath == "TitleId" && column.Visibility == Visibility.Collapsed);
                var titleColumn = System.Linq.Enumerable.First(grid.Columns, column => column.SortMemberPath == "Title");
                titleColumn.Visibility = Visibility.Visible;
                main.Navigate(WorkspaceSection.Home);
                main.Navigate(WorkspaceSection.Batch);
                Assert.Equal(Visibility.Visible, titleColumn.Visibility);
                var makeMenu = typeof(BatchIntegrityWindow).GetMethod("CreateColumnsMenu", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
                var menu = (ContextMenu)makeMenu.Invoke(batchPage, new object[] { new Button() })!;
                Assert.Equal(grid.Columns.Count, menu.Items.Count);
                var titleMenu = (MenuItem)menu.Items[grid.Columns.IndexOf(titleColumn)];
                titleMenu.IsChecked = false;
                titleMenu.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                Assert.Equal(Visibility.Collapsed, titleColumn.Visibility);
                titleMenu.IsChecked = true;
                titleMenu.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                Assert.Equal(Visibility.Visible, titleColumn.Visibility);
                batch.Results.Clear();
                batch.Results.Add(nszResult with { Title = "Zulu", FileSize = 20 });
                batch.Results.Add(nspResult with { Title = "Alpha", FileSize = 3 });
                batch.ResultsView.SortDescriptions.Add(new System.ComponentModel.SortDescription("FileSize", System.ComponentModel.ListSortDirection.Ascending));
                Assert.Equal("Alpha", System.Linq.Enumerable.First(System.Linq.Enumerable.Cast<Emignatik.NxFileViewer.Services.Integrity.BatchIntegrityResult>(batch.ResultsView)).Title);
                batch.SearchText = "zulu";
                Assert.Single(System.Linq.Enumerable.Cast<object>(batch.ResultsView));
                batch.FileTypeFilter = "NSP";
                Assert.Empty(System.Linq.Enumerable.Cast<object>(batch.ResultsView));
                batch.ResetFiltersCommand.Execute(null);
                Assert.Equal(2, System.Linq.Enumerable.Count(System.Linq.Enumerable.Cast<object>(batch.ResultsView)));
                batch.Results.Clear();
                batch.Results.Add(nszResult with { FilePath = @"C:\batch\folder\one.nsz" });
                batch.Results.Add(nszResult with { FilePath = @"C:\batch\folder\two.nsz" });
                batch.Results.Add(nszResult with { FilePath = @"C:\batch\bundle.zip::one.nsp" });
                batch.Results.Add(nszResult with { FilePath = @"C:\batch\bundle.zip::nested/two.nsp" });
                batch.GroupFiles = true;
                Assert.Single(grid.GroupStyle);
                Assert.Equal(2, batch.ResultsView.Groups!.Count);
                Assert.All(System.Linq.Enumerable.Cast<System.Windows.Data.CollectionViewGroup>(batch.ResultsView.Groups), group => Assert.Equal(2, group.ItemCount));
                batch.SearchText = "one";
                Assert.All(System.Linq.Enumerable.Cast<System.Windows.Data.CollectionViewGroup>(batch.ResultsView.Groups), group => Assert.Equal(1, group.ItemCount));
                batch.ResetFiltersCommand.Execute(null);
                batch.GroupFiles = false;
                Assert.Empty(grid.GroupStyle);
                Assert.Null(batch.ResultsView.Groups);
                var previewLoader = new PreviewLoader();
                var previewRunner = new ImmediatePreviewRunner();
                var reloadBatch = new BatchIntegrityWindowViewModel(
                    App.ServiceProvider.GetRequiredService<Emignatik.NxFileViewer.Services.Prompting.IPromptService>(),
                    new PreviewServices(previewLoader), previewRunner,
                    Microsoft.Extensions.Logging.Abstractions.NullLogger<BatchIntegrityWindowViewModel>.Instance,
                    new AppSettings { EnableBatchHistory = false });
                var reloadPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid() + ".nsz");
                System.IO.File.WriteAllText(reloadPath, "synthetic source");
                try
                {
                    var reloadRow = nszResult with { FilePath = reloadPath };
                    reloadBatch.SelectedResult = reloadRow;
                    Assert.NotNull(reloadBatch.PreviewOverview);
                    Assert.Equal(1, previewLoader.LoadCount);
                    reloadBatch.SelectedResult = firmwareResult;
                    reloadBatch.SelectedResult = reloadRow;
                    Assert.NotNull(reloadBatch.PreviewOverview);
                    Assert.Equal(1, previewLoader.LoadCount);
                    reloadBatch.ClosePreview();
                }
                finally { System.IO.File.Delete(reloadPath); }
                var emptyOverview = new Emignatik.NxFileViewer.Views.UserControls.FileOverviewView { DataContext = null };
                var keyWarning = Assert.IsType<Border>(((Grid)emptyOverview.Content).Children[0]);
                Assert.Equal(Visibility.Collapsed, keyWarning.Visibility);
                var languages = Emignatik.NxFileViewer.Localization.LocalizationManager.Instance;
                var originalLanguage = languages.Current;
                try
                {
                    languages.Current = System.Linq.Enumerable.First(languages.RealLocalizations, l => l.CultureName.StartsWith("de"));
                    var runner = new Emignatik.NxFileViewer.Services.BackgroundTask.BackgroundTaskRunner();
                    var statusChanged = false;
                    runner.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(runner.ProgressText)) statusChanged = true; };
                    var germanReady = runner.ProgressText;
                    languages.Current = System.Linq.Enumerable.First(languages.RealLocalizations, l => l.CultureName.StartsWith("en"));
                    Assert.True(statusChanged);
                    Assert.Equal(languages.Current.Keys.Status_Ready, runner.ProgressText);
                    Assert.NotEqual(germanReady, runner.ProgressText);
                }
                finally { languages.Current = originalLanguage; }
                var historyEnabled = actual.EnableBatchHistory;
                try
                {
                    actual.EnableBatchHistory = false;
                    Assert.False(batch.IsHistoryEnabled);
                    Assert.Empty(batch.History);
                    Assert.False(batch.ResumeHistoryCommand.CanExecute(null));
                    Assert.False(batch.LoadHistoryCommand.CanExecute(null));
                    actual.EnableBatchHistory = true;
                    Assert.True(batch.IsHistoryEnabled);
                    // Restoring a history has no live preview cache. Display persisted
                    // game metadata even when the source/archive no longer exists.
                    foreach (var format in new[] { "NSP", "NSZ", "XCI", "XCZ", "NSP (ZIP)", "NSZ (7Z)" })
                    {
                        var savedGame = nszResult with { FilePath = nszPath + ".missing", FileType = format,
                            Title = "Saved game", TitleId = "0100123456789000", Publisher = "Saved publisher", FileSize = 1024 };
                        batch.SelectedHistory = new Emignatik.NxFileViewer.Services.Integrity.BatchHistoryEntry(Guid.NewGuid(), DateTime.UtcNow,
                            "missing history source", false, true, true, false, "Completed", new[] { savedGame }, new());
                        batch.LoadHistoryCommand.Execute(null);
                        Assert.Same(savedGame, batch.SelectedResult);
                        Assert.Null(batch.PreviewOverview);
                        Assert.True(batch.ShowStoredOverview);
                        Assert.Equal(0, batch.SelectedDetailsTabIndex);
                        Assert.Contains(batch.StoredOverviewFields, field => field.Value == "Saved game");
                        Assert.Contains(batch.StoredOverviewFields, field => field.Value == "0100123456789000");
                        batch.SelectedResult = savedGame with { IsNand = true };
                        Assert.False(batch.ShowStoredOverview);
                        batch.SelectedResult = savedGame with { IsFirmware = true };
                        Assert.False(batch.ShowStoredOverview);
                    }
                }
                finally { actual.EnableBatchHistory = historyEnabled; }
                main.Close();
            }
            catch (Exception ex) { error = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "Workspace test timed out.");
        if (error != null) throw new InvalidOperationException("Workspace UI test failed.", error);
    }
    private sealed class PreviewLoader : Emignatik.NxFileViewer.FileLoading.IFileLoader
    {
        public int LoadCount { get; private set; }
        public Emignatik.NxFileViewer.Models.NxFile Load(string path)
        {
            LoadCount++;
            var root = new PreviewItem();
            return new Emignatik.NxFileViewer.Models.NxFile(path, root, new Emignatik.NxFileViewer.Models.Overview.FileOverview(root));
        }
    }
    private sealed class PreviewServices(PreviewLoader loader) : IServiceProvider
    {
        public object? GetService(Type type) => type == typeof(Emignatik.NxFileViewer.FileLoading.IFileLoader) ? loader : App.ServiceProvider.GetService(type);
    }
    private sealed class ImmediatePreviewRunner : Emignatik.NxFileViewer.Services.BackgroundTask.IMainBackgroundTaskRunnerService
    {
        private readonly Emignatik.NxFileViewer.Services.BackgroundTask.BackgroundTaskRunner _reporter = new();
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged { add { } remove { } }
        public string ProgressText => "";
        public double ProgressValue => 0;
        public string ProgressValueText => "";
        public bool IsRunning => false;
        public bool IsIndeterminate => false;
        public System.Windows.Input.ICommand CancelCommand => _reporter.CancelCommand;
        public System.Threading.Tasks.Task RunAsync(Emignatik.NxFileViewer.Services.BackgroundTask.IRunnable runnable)
        { runnable.Run(_reporter, CancellationToken.None); return System.Threading.Tasks.Task.CompletedTask; }
        public System.Threading.Tasks.Task<T> RunAsync<T>(Emignatik.NxFileViewer.Services.BackgroundTask.IRunnable<T> runnable)
            => System.Threading.Tasks.Task.FromResult(runnable.Run(_reporter, CancellationToken.None));
    }
    private sealed class PreviewItem : Emignatik.NxFileViewer.Models.TreeItems.ItemBase
    {
        public PreviewItem() : base(null) { }
        public override string Name => "preview";
        public override string DisplayName => Name;
        public override string Format => "NSZ";
        public override string LibHacTypeName => "preview";
    }
}
