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
                typeof(BatchIntegrityWindow).GetMethod("ApplyDefaultColumns", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                    .Invoke(batchPage, null);
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
                Assert.Equal(6, ((TabControl)settingsPage.FindName("SettingsSections")).Items.Count);
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
                var packageSettings = Emignatik.NxFileViewer.Services.PackageOperations.PackageEngineConfiguration.Capture(actual);
                var packageActions = App.ServiceProvider.GetRequiredService<Emignatik.NxFileViewer.Services.PackageOperations.PackageActions>();
                Assert.False(packageActions.IsEnabled);
                var packageGroup = Assert.IsType<GroupBox>(pluginPage.FindName("PackageOperationsGroup"));
                packageGroup.GetBindingExpression(UIElement.VisibilityProperty)!.UpdateTarget();
                Assert.Equal(Visibility.Collapsed, packageGroup.Visibility);
                Assert.False(packageActions.MergeNspCommand.CanExecute(null));
                var engineSelector = Assert.IsType<ComboBox>(pluginPage.FindName("PackageEngineSelector"));
                Assert.Equal("EditedSettings.PackageEngine", engineSelector.GetBindingExpression(ComboBox.SelectedValueProperty)!.ParentBinding.Path.Path);
                plugins.EditedSettings.PackageEngine = PackageEngine.Kronos;
                plugins.EditedSettings.RustPackageExecutablePath = @"C:\Plugins\nscb_rust.exe";
                plugins.EditedSettings.KronosPackageDirectory = @"C:\Plugins\Kronos";
                plugins.EditedSettings.NandExecutablePath = "C:\\Plugins\\NxNandManager.exe";
                plugins.EditedSettings.NandBisKeysPath = "C:\\Keys\\bis.keys";
                plugins.ApplySettingsCommand.Execute(null);
                Assert.Equal("C:\\Plugins\\NxNandManager.exe", actual.NandExecutablePath);
                Assert.Equal("C:\\Keys\\bis.keys", actual.NandBisKeysPath);
                Assert.Equal(9, actual.NszCompressionLevel);
                Assert.Equal(PackageEngine.Kronos, actual.PackageEngine);
                Assert.Equal(@"C:\Plugins\nscb_rust.exe", actual.RustPackageExecutablePath);
                Assert.Equal(@"C:\Plugins\Kronos", actual.KronosPackageDirectory);
                Assert.Equal(originalTitleUrl, actual.TitlePageUrl);
                Assert.Equal("https://example.com/general-draft", settings.EditedSettings.TitlePageUrl);
                main.Navigate(WorkspaceSection.Settings);
                settings.ApplySettingsCommand.Execute(null);
                Assert.Equal(PackageEngine.Kronos, actual.PackageEngine);
                Assert.Equal(@"C:\Plugins\Kronos", actual.KronosPackageDirectory);
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
                Assert.Equal(PackageEngine.Kronos, actual.PackageEngine);
                Assert.Equal(@"C:\Plugins\nscb_rust.exe", actual.RustPackageExecutablePath);
                Assert.Equal(@"C:\Plugins\Kronos", actual.KronosPackageDirectory);
                Assert.Equal("https://example.com/general-draft", actual.TitlePageUrl);
                main.Navigate(WorkspaceSection.Plugins);
                Assert.Same(pluginPage, ((TabItem)main.FindName("PluginsTab")).Content);
                plugins.EditedSettings.NszCompressionLevel = 12;
                plugins.CancelSettingsCommand.Execute(null);
                Assert.Equal(9, actual.NszCompressionLevel);
                Assert.Equal(PackageEngine.Kronos, actual.PackageEngine);
                Assert.Equal(@"C:\Plugins\nscb_rust.exe", actual.RustPackageExecutablePath);
                Assert.Equal(@"C:\Plugins\Kronos", actual.KronosPackageDirectory);
                Assert.Equal(9, plugins.EditedSettings.NszCompressionLevel);
                Assert.False(closed);
                actual.NszCompressionLevel = originalLevel;
                actual.NandExecutablePath = originalNandExecutable;
                actual.NandBisKeysPath = originalBisKeys;
                actual.PackageEngine = packageSettings.Engine;
                actual.RustPackageExecutablePath = packageSettings.RustExecutable;
                actual.KronosPackageDirectory = packageSettings.KronosDirectory;
                actual.TitlePageUrl = originalTitleUrl;
                App.ServiceProvider.GetRequiredService<IShowRenameToolWindowCommand>().Execute(null);
                Assert.IsType<RenameToolWindow>(((TabItem)main.FindName("RenameTab")).Content);
                main.Navigate(WorkspaceSection.Updates);
                var updates = Assert.IsType<UpdateCenterView>(((TabItem)main.FindName("UpdatesTab")).Content);
                var updateModel = Assert.IsType<UpdateCenterViewModel>(updates.DataContext);
                var programGroups = Assert.IsType<StackPanel>(settingsPage.FindName("ProgramSettingsGroups"));
                Assert.Equal(9, programGroups.Children.Count);
                foreach (var setting in new[] { "BatchSeparatePackages", "BatchExportSeparatePackages", "BatchSummaryPreservePackageOrder" })
                {
                    var packageCheckBox = Assert.IsType<CheckBox>(settingsPage.FindName(setting + "CheckBox"));
                    Assert.Equal("EditedSettings." + setting, packageCheckBox.GetBindingExpression(CheckBox.IsCheckedProperty)!.ParentBinding.Path.Path);
                }
                var exportColumns = Assert.IsType<CheckBox>(settingsPage.FindName("BatchExportVisibleColumnsCheckBox"));
                Assert.Equal("EditedSettings.BatchExportVisibleColumnsOnly", exportColumns.GetBindingExpression(CheckBox.IsCheckedProperty)!.ParentBinding.Path.Path);
                var exportFormat = Assert.IsType<ComboBox>(settingsPage.FindName("BatchExportFormatComboBox"));
                Assert.Equal("EditedSettings.BatchExportExcel", exportFormat.GetBindingExpression(ComboBox.SelectedValueProperty)!.ParentBinding.Path.Path);
                Assert.Equal(false, Assert.IsType<ComboBoxItem>(exportFormat.Items[0]).Tag);
                Assert.Equal(true, Assert.IsType<ComboBoxItem>(exportFormat.Items[1]).Tag);
                Assert.IsType<GroupBox>(settingsPage.FindName("OptionalFeaturesGroup"));
                var featureSections = Assert.IsType<TabControl>(settingsPage.FindName("SettingsSections"));
                var pluginsTab = Assert.IsType<TabItem>(settingsPage.FindName("PluginsSettingsTab"));
                var optionalTab = Assert.IsType<TabItem>(settingsPage.FindName("OptionalFeaturesSettingsTab"));
                Assert.Equal(featureSections.Items.IndexOf(pluginsTab) + 1, featureSections.Items.IndexOf(optionalTab));
                Assert.Equal(Visibility.Visible, optionalTab.Visibility);
                foreach (var feature in new[] { "NszPlugin", "Nand", "NandPlugin", "Mtp", "Firmware", "PackageOperations" })
                {
                    var checkboxName = feature == "Firmware" ? "EnableFirmwareCheckBox" : "Enable" + feature + "CheckBox";
                    var checkbox = Assert.IsType<CheckBox>(settingsPage.FindName(checkboxName));
                    var property = feature == "Firmware" ? "EnableFirmwareCheck" : "Enable" + feature;
                    Assert.Equal("EditedSettings." + property, checkbox.GetBindingExpression(CheckBox.IsCheckedProperty)!.ParentBinding.Path.Path);
                }
                Assert.All(System.Linq.Enumerable.Cast<UIElement>(programGroups.Children), child => Assert.IsType<GroupBox>(child));
                foreach (var mode in new[] { "Program", "Windows", "BatchDrop" })
                {
                    var autoTip = Assert.IsType<Emignatik.NxFileViewer.Views.CustomControls.TipLabel>(settingsPage.FindName("AutoVerify" + mode + "Tip"));
                    Assert.False(string.IsNullOrWhiteSpace(autoTip.TipText));
                    Assert.True(autoTip.TipText!.Length > 80);
                }
                var autoProgramBox = Assert.IsType<CheckBox>(settingsPage.FindName("AutoVerifyProgramCheckBox"));
                var autoWindowsBox = Assert.IsType<CheckBox>(settingsPage.FindName("AutoVerifyWindowsCheckBox"));
                var autoBatchBox = Assert.IsType<CheckBox>(settingsPage.FindName("AutoVerifyBatchDropCheckBox"));
                Assert.Equal("EditedSettings.VerifyBatchDrops", autoBatchBox.GetBindingExpression(CheckBox.IsCheckedProperty)!.ParentBinding.Path.Path);
                settings.EditedSettings.VerifyBatchDrops = true;
                var historyLimitBox = Assert.IsType<TextBox>(settingsPage.FindName("BatchHistoryLimitBox"));
                Assert.Equal("EditedSettings.BatchHistoryLimit", historyLimitBox.GetBindingExpression(TextBox.TextProperty)!.ParentBinding.Path.Path);
                settings.EditedSettings.BatchHistoryLimit = 12;
                Assert.Equal("EditedSettings.VerifyFilesOpenedInProgram", autoProgramBox.GetBindingExpression(CheckBox.IsCheckedProperty)!.ParentBinding.Path.Path);
                Assert.Equal("EditedSettings.VerifyFilesOpenedFromWindows", autoWindowsBox.GetBindingExpression(CheckBox.IsCheckedProperty)!.ParentBinding.Path.Path);
                settings.EditedSettings.VerifyFilesOpenedInProgram = true;
                settings.EditedSettings.VerifyFilesOpenedFromWindows = false;
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
                Assert.True(actual.VerifyFilesOpenedInProgram);
                Assert.True(actual.VerifyBatchDrops);
                Assert.Equal(12, actual.BatchHistoryLimit);
                Assert.Equal(12, batch.HistoryLimit);
                Assert.False(actual.VerifyFilesOpenedFromWindows);
                settingsManager.SaveSafe();
                using (var savedAuto = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(savedSettingsPath)))
                {
                    Assert.True(savedAuto.RootElement.GetProperty(nameof(IAppSettings.VerifyFilesOpenedInProgram)).GetBoolean());
                    Assert.True(savedAuto.RootElement.GetProperty(nameof(IAppSettings.VerifyBatchDrops)).GetBoolean());
                    Assert.Equal(12, savedAuto.RootElement.GetProperty(nameof(IAppSettings.BatchHistoryLimit)).GetInt32());
                    Assert.False(savedAuto.RootElement.GetProperty(nameof(IAppSettings.VerifyFilesOpenedFromWindows)).GetBoolean());
                }
                actual.BatchHistoryLimit = 5;
                settings.EditedSettings.BatchHistoryLimit = 5;
                actual.VerifyBatchDrops = false;
                settings.EditedSettings.VerifyBatchDrops = false;
                actual.VerifyFilesOpenedInProgram = false;
                settings.EditedSettings.VerifyFilesOpenedInProgram = false;
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
                var detailsToggle = Assert.IsType<System.Windows.Controls.Primitives.ToggleButton>(batchPage.FindName("DetailsToggleButton"));
                var detailsColumn = Assert.IsType<ColumnDefinition>(batchPage.FindName("DetailsPaneColumn"));
                var resultsColumn = Assert.IsType<ColumnDefinition>(batchPage.FindName("ResultsPaneColumn"));
                var detailsSplitter = Assert.IsType<GridSplitter>(batchPage.FindName("DetailsSplitter"));
                var savedDetailsWidth = detailsColumn.Width;
                var savedResultsWidth = resultsColumn.Width;
                Assert.Equal(Visibility.Visible, detailsTabs.Visibility);
                Assert.Equal("▶", detailsToggle.Content);
                detailsColumn.Width = new GridLength(320);
                resultsColumn.Width = new GridLength(1, GridUnitType.Star);
                var selectedBeforeCollapse = batch.SelectedResult;
                var tabBeforeCollapse = batch.SelectedDetailsTabIndex;
                detailsToggle.SetCurrentValue(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, false);
                detailsToggle.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                Assert.Equal(Visibility.Collapsed, detailsTabs.Visibility);
                Assert.Equal(Visibility.Collapsed, detailsSplitter.Visibility);
                Assert.Equal(0, detailsColumn.Width.Value);
                Assert.Equal(Visibility.Visible, detailsToggle.Visibility);
                Assert.Equal("◀", detailsToggle.Content);
                main.Navigate(WorkspaceSection.Home);
                main.Navigate(WorkspaceSection.Batch);
                Assert.Equal(Visibility.Collapsed, detailsTabs.Visibility);
                detailsToggle.SetCurrentValue(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, true);
                detailsToggle.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                Assert.Equal(Visibility.Visible, detailsTabs.Visibility);
                Assert.Equal(Visibility.Visible, detailsSplitter.Visibility);
                Assert.Equal(new GridLength(320), detailsColumn.Width);
                Assert.Equal(new GridLength(1, GridUnitType.Star), resultsColumn.Width);
                Assert.Same(selectedBeforeCollapse, batch.SelectedResult);
                Assert.Equal(tabBeforeCollapse, batch.SelectedDetailsTabIndex);
                Assert.Equal("▶", detailsToggle.Content);
                detailsColumn.Width = savedDetailsWidth;
                resultsColumn.Width = savedResultsWidth;
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
                Assert.Equal(VerticalAlignment.Center, Assert.IsType<TextBox>(batchPage.FindName("BatchSearchBox")).VerticalContentAlignment);
                Assert.Equal(System.Linq.Enumerable.Select(grid.Columns, column => column.SortMemberPath),
                    System.Linq.Enumerable.Select(Emignatik.NxFileViewer.Services.Integrity.BatchResultExport.SelectColumns(false, batch.ColumnLayout), column => column.Id));
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
                var multiDisplayResult = Assert.Single(System.Linq.Enumerable.Cast<Emignatik.NxFileViewer.Services.Integrity.BatchIntegrityResult>(batch.ResultsView));
                grid.Items.Refresh(); grid.UpdateLayout();
                var multiRow = Assert.IsType<DataGridRow>(grid.ItemContainerGenerator.ContainerFromItem(multiDisplayResult));
                var cellPresenter = Assert.IsType<ContentPresenter>(grid.Columns[0].GetCellContent(multiRow));
                cellPresenter.ApplyTemplate();
                var cell = Assert.IsType<DockPanel>(System.Windows.Media.VisualTreeHelper.GetChild(cellPresenter, 0));
                var arrow = System.Linq.Enumerable.Single(System.Linq.Enumerable.OfType<System.Windows.Controls.Primitives.ToggleButton>(cell.Children));
                arrow.SetCurrentValue(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, true);
                arrow.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                grid.UpdateLayout();
                Assert.True(multiDisplayResult.PackagesExpanded);
                Assert.Equal(Visibility.Visible, multiRow.DetailsVisibility);
                Assert.Same(multiDisplayResult, batch.SelectedResult);
                Assert.Same(capturedNsz, batch.PreviewOverview);
                var detailsContent = Assert.IsType<Border>(grid.RowDetailsTemplate.LoadContent());
                var packageGrid = Assert.IsType<DataGrid>(detailsContent.Child);
                packageGrid.ItemsSource = new[] { new Emignatik.NxFileViewer.Services.Integrity.BatchPackageSummary(
                    "Asterix & Obelix Slap Them All! — A long title that must remain readable", "01002B401285E000", "Application", "196608") };
                typeof(BatchIntegrityWindow).GetMethod("PackageGridLoaded", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                    .Invoke(batchPage, new object[] { packageGrid, new RoutedEventArgs() });
                Assert.Equal(17, packageGrid.Columns.Count);
                Assert.DoesNotContain(packageGrid.Columns, column => column.SortMemberPath == "SystemUpdateVersion");
                Assert.All(packageGrid.Columns, column => Assert.True(column.Width.IsAuto));
                Assert.Equal(ScrollBarVisibility.Auto, ScrollViewer.GetHorizontalScrollBarVisibility(packageGrid));
                packageGrid.Measure(new Size(1000, 280));
                packageGrid.Arrange(new Rect(0, 0, 1000, 280));
                packageGrid.UpdateLayout();
                Assert.True(System.Linq.Enumerable.Single(packageGrid.Columns, column => column.SortMemberPath == "Title").ActualWidth > 240);
                Assert.All(System.Linq.Enumerable.Where(packageGrid.Columns, column => column.Visibility == Visibility.Visible), column => Assert.True(column.ActualWidth >= 90));
                arrow.SetCurrentValue(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, false);
                arrow.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                grid.UpdateLayout();
                Assert.False(multiDisplayResult.PackagesExpanded);
                Assert.Equal(Visibility.Collapsed, multiRow.DetailsVisibility);
                var originalSeparate = actual.BatchSeparatePackages;
                var originalExportSeparate = actual.BatchExportSeparatePackages;
                var originalSummaryOrder = actual.BatchSummaryPreservePackageOrder;
                try
                {
                    actual.BatchSeparatePackages = false;
                    actual.BatchExportSeparatePackages = false;
                    actual.BatchSummaryPreservePackageOrder = false;
                    var unorderedSummary = multiResult with
                    {
                        ContentType = "Update / Base", DisplayVersion = "1.0.0 / 12.0.0", SystemVersion = "17.0.0 / 22.0.0",
                        MasterKey = "MasterKey 9 / MasterKey 10", Title = "Zulu / Alpha",
                        Packages = new[] {
                            new Emignatik.NxFileViewer.Services.Integrity.BatchPackageSummary("Zulu", "01", "Update", "0") { DisplayVersion = "1.0.0", SystemVersion = "17.0.0", MasterKey = "MasterKey 9" },
                            new Emignatik.NxFileViewer.Services.Integrity.BatchPackageSummary("Alpha", "02", "Base", "65536") { DisplayVersion = "12.0.0", SystemVersion = "22.0.0", MasterKey = "MasterKey 10" }
                        }
                    };
                    batch.Results[0] = unorderedSummary;
                    var sortedSummary = Assert.Single(System.Linq.Enumerable.Cast<Emignatik.NxFileViewer.Services.Integrity.BatchIntegrityResult>(batch.ResultsView));
                    Assert.Equal("Base / Update", sortedSummary.ContentType);
                    Assert.Equal("12.0.0 / 1.0.0", sortedSummary.DisplayVersion);
                    Assert.Equal("22.0.0 / 17.0.0", sortedSummary.SystemVersion);
                    Assert.Equal("MasterKey 10 / MasterKey 9", sortedSummary.MasterKey);
                    Assert.Equal("Alpha / Zulu", sortedSummary.Title);
                    Assert.Equal(sortedSummary.DisplayVersion, Assert.Single(batch.GetExportRows()).DisplayVersion);
                    actual.BatchSummaryPreservePackageOrder = true;
                    Assert.Same(unorderedSummary, Assert.Single(System.Linq.Enumerable.Cast<Emignatik.NxFileViewer.Services.Integrity.BatchIntegrityResult>(batch.ResultsView)));
                    Assert.Same(unorderedSummary, Assert.Single(batch.GetExportRows()));
                    actual.BatchSummaryPreservePackageOrder = false;
                    Assert.Equal("12.0.0 / 1.0.0", Assert.Single(batch.GetExportRows()).DisplayVersion);
                    batch.Results[0] = multiResult;
                    foreach (var displaySeparate in new[] { false, true })
                        foreach (var exportSeparate in new[] { false, true })
                        {
                            actual.BatchSeparatePackages = displaySeparate;
                            actual.BatchExportSeparatePackages = exportSeparate;
                            var displayed = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Cast<Emignatik.NxFileViewer.Services.Integrity.BatchIntegrityResult>(batch.ResultsView));
                            Assert.Equal(displaySeparate ? 2 : 1, displayed.Length);
                            Assert.Equal(exportSeparate ? 2 : 1, batch.GetExportRows().Count);
                            Assert.Single(batch.Results);
                            Assert.Equal(2, batch.Results[0].Packages.Length);
                            if (displaySeparate)
                            {
                                Assert.EndsWith("::2", displayed[1].FileName);
                                Assert.Equal("Update", displayed[1].Title);
                                Assert.Equal(multiResult.FilePath, displayed[1].FilePath);
                                batch.SelectedResult = displayed[1];
                                Assert.Equal("Update", batch.StoredOverview!.CnmtContainers[0].Titles[0].AppName);
                            }
                        }
                    actual.BatchSeparatePackages = true;
                    batch.SearchText = "::2";
                    Assert.Single(System.Linq.Enumerable.Cast<object>(batch.ResultsView));
                    actual.BatchExportSeparatePackages = true;
                    Assert.Single(batch.GetExportRows());
                    actual.BatchExportSeparatePackages = false;
                    var combinedExport = Assert.Single(batch.GetExportRows());
                    Assert.Equal(0, combinedExport.PackageIndex);
                    Assert.Same(multiResult.Packages, combinedExport.Packages);
                    batch.ResetFiltersCommand.Execute(null);
                    batch.Results[0] = multiResult with { Packages = new[] { multiResult.Packages[0], multiResult.Packages[1] with { Title = "Updated package" } } };
                    var updatedRows = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Cast<Emignatik.NxFileViewer.Services.Integrity.BatchIntegrityResult>(batch.ResultsView));
                    Assert.Equal("Updated package", updatedRows[1].Title);
                    Assert.Equal(2, batch.SelectedResult!.PackageIndex);
                    batch.Results.RemoveAt(0);
                    Assert.Empty(System.Linq.Enumerable.Cast<object>(batch.ResultsView));
                    batch.Results.Add(multiResult);
                    Assert.Equal(2, System.Linq.Enumerable.Count(System.Linq.Enumerable.Cast<object>(batch.ResultsView)));
                }
                finally
                {
                    batch.ResetFiltersCommand.Execute(null);
                    actual.BatchSeparatePackages = originalSeparate;
                    actual.BatchExportSeparatePackages = originalExportSeparate;
                    actual.BatchSummaryPreservePackageOrder = originalSummaryOrder;
                    batch.Results[0] = multiResult;
                }
                var findScroll = typeof(BatchIntegrityWindow).GetMethod("FindScrollViewer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
                var scroll = Assert.IsType<ScrollViewer>(findScroll.Invoke(null, new object[] { grid }));
                Assert.True(scroll.ScrollableWidth > 0);
                Assert.Empty(grid.GroupStyle);
                Assert.All(System.Linq.Enumerable.Where(grid.Columns, column => column.Visibility == Visibility.Visible), column => Assert.True(column.ActualWidth > 0));
                foreach (var localization in Emignatik.NxFileViewer.Localization.LocalizationManager.Instance.RealLocalizations)
                    foreach (var property in localization.Keys.GetType().GetProperties())
                        if (property.GetIndexParameters().Length == 0 && property.PropertyType == typeof(string) && property.GetValue(localization.Keys) is string text)
                        { Assert.DoesNotContain("Ã", text); Assert.DoesNotContain("â€", text); }
                Assert.Equal(new[] { "FileName", "FileType", "PackageType", "Structure", "NcaSignature", "Integrity", "FileSize", "Compression",
                    "CompressionRatio", "SystemUpdateVersion", "ContentType", "TitleId", "VersionNumber", "SystemVersion", "BaseTitleId", "MasterKey",
                    "MinimumApplicationVersion", "Distribution", "BuildId", "IsDemo", "PermissionDisplay",
                    "FileSystemPermissions", "AcidSignatureValidity", "Title", "Publisher", "DisplayVersion", "Languages", "Error",
                    "MissingKeys", "FilePath", "NamingStatus", "ProposedPath", "NamingError", "ConversionStatus", "SourceSize", "OutputSize", "ConvertedPath" },
                    System.Linq.Enumerable.Select(grid.Columns, column => column.SortMemberPath));
                Assert.Equal(new[] { "FileName", "FileType", "PackageType", "Structure", "NcaSignature", "Integrity", "FileSize", "Compression",
                    "ContentType", "TitleId", "VersionNumber", "PermissionDisplay", "Title", "Error" },
                    System.Linq.Enumerable.Select(System.Linq.Enumerable.Where(grid.Columns, column => column.Visibility == Visibility.Visible), column => column.SortMemberPath));
                Assert.Contains(grid.Columns, column => column.SortMemberPath == "TitleId" && column.Visibility == Visibility.Visible);
                Assert.Contains(grid.Columns, column => column.SortMemberPath == "ContentType" && column.Visibility == Visibility.Visible);
                Assert.Contains(grid.Columns, column => column.SortMemberPath == "PermissionDisplay" && column.Visibility == Visibility.Visible);
                var titleColumn = System.Linq.Enumerable.First(grid.Columns, column => column.SortMemberPath == "Title");
                titleColumn.Visibility = Visibility.Visible;
                main.Navigate(WorkspaceSection.Home);
                main.Navigate(WorkspaceSection.Batch);
                Assert.Equal(Visibility.Visible, titleColumn.Visibility);
                var createFileActions = typeof(BatchIntegrityWindow).GetMethod("CreateFileActionsMenu", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
                var previousNszFeature = actual.EnableNszPlugin;
                var previousNandFeature = actual.EnableNand;
                try
                {
                    var featureKeys = Emignatik.NxFileViewer.Localization.LocalizationManager.Instance.Current.Keys;
                    actual.EnableNszPlugin = false;
                    var disabledPluginMenu = Assert.IsType<ContextMenu>(createFileActions.Invoke(batchPage, new object[] { batch, nszResult }));
                    Assert.DoesNotContain(System.Linq.Enumerable.OfType<MenuItem>(disabledPluginMenu.Items), item => Equals(item.Header, featureKeys.Nsz_Compress) || Equals(item.Header, featureKeys.Nsz_Decompress));
                    Assert.Contains(System.Linq.Enumerable.OfType<MenuItem>(disabledPluginMenu.Items), item => Equals(item.Header, featureKeys.BatchTable_OpenExplorer));
                    actual.EnableNszPlugin = true;
                    var enabledPluginMenu = Assert.IsType<ContextMenu>(createFileActions.Invoke(batchPage, new object[] { batch, nszResult }));
                    Assert.Contains(System.Linq.Enumerable.OfType<MenuItem>(enabledPluginMenu.Items), item => Equals(item.Header, featureKeys.Nsz_Compress));
                    Assert.Contains(System.Linq.Enumerable.OfType<MenuItem>(enabledPluginMenu.Items), item => Equals(item.Header, featureKeys.Nsz_Decompress));
                    actual.EnableNand = false;
                    var nandNavigationTab = Assert.IsType<TabItem>(main.FindName("NandTab"));
                    nandNavigationTab.GetBindingExpression(UIElement.VisibilityProperty)!.UpdateTarget();
                    Assert.Equal(Visibility.Collapsed, nandNavigationTab.Visibility);
                    var selectedWorkspaceTab = ((TabControl)main.FindName("WorkspaceTabs")).SelectedItem;
                    main.Navigate(WorkspaceSection.Nand);
                    Assert.Same(selectedWorkspaceTab, ((TabControl)main.FindName("WorkspaceTabs")).SelectedItem);
                    Assert.DoesNotContain(batch.FileTypeFilters, filter => filter.Value == "NAND");
                }
                finally
                {
                    actual.EnableNszPlugin = previousNszFeature;
                    actual.EnableNand = previousNandFeature;
                    Assert.IsType<TabItem>(main.FindName("NandTab")).GetBindingExpression(UIElement.VisibilityProperty)!.UpdateTarget();
                }
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
                var previewServices = new PreviewServices(previewLoader);
                var previewRunner = new ImmediatePreviewRunner();
                var reloadSettings = new AppSettings { EnableBatchHistory = false };
                var reloadBatch = new BatchIntegrityWindowViewModel(
                    App.ServiceProvider.GetRequiredService<Emignatik.NxFileViewer.Services.Prompting.IPromptService>(),
                    previewServices, previewRunner,
                    Microsoft.Extensions.Logging.Abstractions.NullLogger<BatchIntegrityWindowViewModel>.Instance,
                    reloadSettings);
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
                    reloadSettings.EnableBatchHistory = true;
                    var persistedGame = reloadRow with
                    {
                        Title = "Saved title",
                        Packages = new[]
                        {
                            new Emignatik.NxFileViewer.Services.Integrity.BatchPackageSummary("Saved base", "0100000000000000", "Base", "0"),
                            new Emignatik.NxFileViewer.Services.Integrity.BatchPackageSummary("Saved update", "0100000000000800", "Update", "65536")
                            {
                                SystemVersion = "19.0.0", BaseTitleId = "0100000000000000", BuildId = "saved-build",
                                Titles = new[] { new Emignatik.NxFileViewer.Services.Integrity.BatchTitleSummary("German", "Gespeicherter Titel", "Gespeicherter Verlag"),
                                    new Emignatik.NxFileViewer.Services.Integrity.BatchTitleSummary("AmericanEnglish", "Saved English title", "Saved English publisher") }
                            }
                        }
                    };
                    var historyEntry = new Emignatik.NxFileViewer.Services.Integrity.BatchHistoryEntry(Guid.NewGuid(), DateTime.UtcNow,
                        System.IO.Path.GetDirectoryName(reloadPath)!, false, false, false, false, "Completed", new[] { persistedGame }, new());
                    reloadBatch.SelectedHistory = System.Text.Json.JsonSerializer.Deserialize<Emignatik.NxFileViewer.Services.Integrity.BatchHistoryEntry>(
                        System.Text.Json.JsonSerializer.Serialize(historyEntry));
                    batchPage.DataContext = reloadBatch;
                    reloadBatch.LoadHistoryCommand.Execute(null);
                    batchPage.UpdateLayout();
                    var historyOverview = Assert.IsType<Emignatik.NxFileViewer.Views.UserControls.FileOverviewView>(batchPage.FindName("HistoryOverview"));
                    Assert.Same(reloadBatch.StoredOverview, historyOverview.DataContext);
                    var storedPicker = Assert.IsType<ComboBox>(historyOverview.FindName("PackagePicker"));
                    Assert.Equal(2, storedPicker.Items.Count);
                    Assert.Same(reloadBatch.StoredOverview!.CnmtContainers[0], storedPicker.SelectedItem);
                    storedPicker.SelectedIndex = 1;
                    historyOverview.UpdateLayout();
                    var storedPackageView = Assert.IsType<Emignatik.NxFileViewer.Views.UserControls.CnmtContainerView>(historyOverview.FindName("PackageOverview"));
                    var storedPackage = Assert.IsType<Emignatik.NxFileViewer.Views.UserControls.StoredCnmtContainerViewModel>(storedPackageView.DataContext);
                    Assert.Equal("saved-build", storedPackage.BuildID);
                    Assert.Equal("Gespeicherter Titel", storedPackage.SelectedTitle!.AppName);
                    Assert.Equal("Gespeicherter Verlag", storedPackage.SelectedTitle.Publisher);
                    storedPackage.SelectedTitle = storedPackage.Titles[1];
                    Assert.Equal("Saved English title", storedPackage.SelectedTitle.AppName);
                    Assert.Equal("Saved English publisher", storedPackage.SelectedTitle.Publisher);
                    Assert.Null(storedPackage.SelectedTitle.Icon);
                    Assert.Equal(Visibility.Collapsed, storedPackage.LogoVisibility);
                    Assert.Equal(Visibility.Collapsed, reloadBatch.StoredOverview.VerifyControlsVisibility);
                    var historyNotice = Assert.IsType<TextBlock>(storedPackageView.FindName("HistoryLogoNotice"));
                    Assert.Equal(Visibility.Visible, historyNotice.Visibility);
                    Assert.Equal(Emignatik.NxFileViewer.Localization.LocalizationManager.Instance.Current.Keys.BatchHistory_LoadedNotice, historyNotice.Text);
                    Assert.Null(reloadBatch.PreviewOverview);
                    Assert.True(reloadBatch.ShowStoredOverview);
                    Assert.Equal(1, previewLoader.LoadCount); // The existing source must not be reopened.
                    Assert.Equal(2, reloadBatch.StoredPackages.Count);
                    reloadBatch.SelectedStoredPackage = reloadBatch.StoredPackages[1];
                    Assert.Contains(reloadBatch.StoredPackageFields, field => field.Value == "saved-build");
                    Assert.Contains(reloadBatch.StoredPackageFields, field => field.Value == "Gespeicherter Titel");
                    Assert.Contains(reloadBatch.StoredPackageFields, field => field.Value == "Gespeicherter Verlag");
                    var restoredGame = reloadBatch.SelectedResult;
                    reloadBatch.SelectedResult = firmwareResult;
                    reloadBatch.SelectedResult = restoredGame;
                    Assert.Null(reloadBatch.PreviewOverview);
                    Assert.Equal(1, previewLoader.LoadCount);
                    reloadSettings.EnableBatchHistory = false;
                    var secondDropPath = reloadPath + ".nsp";
                    System.IO.File.WriteAllText(secondDropPath, "second dropped source");
                    try
                    {
                        reloadBatch.LoadDroppedSources(new[] { reloadPath, secondDropPath, reloadPath });
                        batchPage.UpdateLayout();
                        Assert.Equal(2, reloadBatch.SourcePaths.Count);
                        Assert.Equal(2, reloadBatch.Results.Count);
                        Assert.Equal(3, previewLoader.LoadCount);
                        Assert.True(reloadBatch.StartCommand.CanExecute(null));
                        Assert.Equal(0, previewServices.VerificationCalls);
                        reloadSettings.VerifyBatchDrops = true;
                        reloadBatch.LoadDroppedSources(new[] { reloadPath, secondDropPath });
                        Assert.Equal(2, previewServices.VerificationCalls);
                        Assert.All(reloadBatch.Results, row => Assert.Equal(Emignatik.NxFileViewer.Models.Overview.NcasIntegrity.Original, row.Integrity));
                        reloadSettings.VerifyBatchDrops = false;
                        reloadSettings.VerifyFilesOpenedInProgram = true;
                        reloadSettings.VerifyFilesOpenedFromWindows = true;
                        reloadBatch.LoadDroppedSources(new[] { reloadPath, secondDropPath });
                        Assert.Equal(2, previewServices.VerificationCalls);
                        reloadBatch.InputDirectory = "changed manually";
                        Assert.Empty(reloadBatch.SourcePaths);
                    }
                    finally { System.IO.File.Delete(secondDropPath); }
                    reloadBatch.ClosePreview();
                    batchPage.DataContext = batch;
                }
                finally { batchPage.DataContext = batch; System.IO.File.Delete(reloadPath); }
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
                var tipWindow = new TipWindow();
                var tipTextBox = Assert.IsType<TextBox>(tipWindow.FindName("TextBox"));
                var tipWorkArea = SystemParameters.WorkArea;
                var keysTip = languages.Current.Keys.SettingsView_ToolTip_Keys;
                Assert.Contains(languages.Current.Keys.Keys_CopyToSwitchHelp, keysTip);
                tipWindow.Configure(keysTip, tipWorkArea, new Point(tipWorkArea.Right - 1, tipWorkArea.Bottom - 1));
                tipWindow.Show(); tipWindow.UpdateLayout();
                Assert.Equal(keysTip, tipTextBox.Text);
                Assert.True(tipWindow.Width > 300);
                Assert.True(tipWindow.Height > 200);
                Assert.True(tipWindow.Left >= tipWorkArea.Left);
                Assert.True(tipWindow.Top >= tipWorkArea.Top);
                Assert.True(tipWindow.Left + tipWindow.Width <= tipWorkArea.Right + 1);
                Assert.True(tipWindow.Top + tipWindow.Height <= tipWorkArea.Bottom + 1);
                Assert.Same(app.FindResource("WindowStyle"), tipWindow.Style);
                if (tipWindow.Height < tipWindow.MaxHeight - 1)
                    Assert.True(tipTextBox.ExtentHeight <= tipTextBox.ViewportHeight + 1);
                var longTip = string.Join("\n", System.Linq.Enumerable.Repeat(keysTip, 20));
                tipWindow.Configure(longTip, new Rect(-1280, 0, 1280, 720), new Point(-10, 700));
                tipWindow.UpdateLayout();
                Assert.Equal(longTip, tipTextBox.Text);
                Assert.True(tipWindow.Left >= -1280);
                Assert.True(tipWindow.Left + tipWindow.Width <= 0);
                Assert.True(tipWindow.Height <= 696);
                Assert.Equal(ScrollBarVisibility.Auto, tipTextBox.VerticalScrollBarVisibility);
                Assert.Equal(ScrollBarVisibility.Disabled, tipTextBox.HorizontalScrollBarVisibility);
                tipWindow.Close();
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
                VerifyXciRepairOpeningWorkflow();
                main.Close();
            }
            catch (Exception ex) { error = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "Workspace test timed out.");
        if (error != null) throw new InvalidOperationException("Workspace UI test failed.", error);
    }
    private static void VerifyXciRepairOpeningWorkflow()
    {
        foreach (var scenario in new[] { "repair", "decline", "cancel", "healthy", "disabled", "failure", "close" })
        {
            var settings = new AppSettings { EnableNszPlugin = scenario != "disabled" };
            settings.InitializeFeatures();
            var loader = new PreviewLoader();
            var repair = new OpeningRepairService { HasCandidate = scenario != "healthy", FailRepair = scenario == "failure" };
            var prompt = new OpeningRepairPrompt { Decision = scenario switch
            {
                "cancel" => new(true),
                "decline" => new(false),
                _ => new(false, "repaired.xci")
            } };
            var opening = new Emignatik.NxFileViewer.Services.FileOpening.FileOpeningService(
                Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance, settings, loader,
                new ImmediatePreviewRunner(), repairService: repair, repairPrompt: prompt);
            if (scenario == "close") prompt.BeforeDecision = opening.SafeClose;
            opening.SafeOpenFile("original.xci").GetAwaiter().GetResult();
            var aborted = scenario is "cancel" or "failure" or "close";
            Assert.Equal(aborted ? 0 : 1, loader.LoadCount);
            Assert.Equal(scenario == "disabled" ? 0 : 1, repair.Inspections);
            Assert.Equal(scenario is "healthy" or "disabled" ? 0 : 1, prompt.Questions);
            Assert.Equal(scenario is "repair" or "failure" ? 1 : 0, repair.Repairs);
            Assert.Equal(scenario == "failure" ? 1 : 0, prompt.Failures);
            if (!aborted)
            {
                Assert.Equal(scenario == "repair" ? "repaired.xci" : "original.xci", opening.OpenedFile!.FilePath);
                Assert.Equal(opening.OpenedFile.FilePath, settings.LastOpenedFile);
            }
            opening.SafeClose();
        }
    }
    private sealed class OpeningRepairService : Emignatik.NxFileViewer.Services.Nsz.IXciRepairService
    {
        public bool HasCandidate, FailRepair;
        public int Inspections, Repairs;
        public Emignatik.NxFileViewer.Services.Nsz.XciRepairCandidate? Inspect(string path,
            Emignatik.NxFileViewer.Services.BackgroundTask.IProgressReporter progress, CancellationToken token)
        {
            Inspections++;
            return HasCandidate ? new(path, null, 0, true, 1, 0) : null;
        }
        public string RepairCopy(Emignatik.NxFileViewer.Services.Nsz.XciRepairCandidate candidate, string destination,
            Emignatik.NxFileViewer.Services.BackgroundTask.IProgressReporter progress, CancellationToken token)
        {
            Repairs++;
            if (FailRepair) throw new System.IO.IOException("Synthetic verification failure.");
            return destination;
        }
    }
    private sealed class OpeningRepairPrompt : Emignatik.NxFileViewer.Services.Nsz.IXciRepairPrompt
    {
        public Emignatik.NxFileViewer.Services.Nsz.XciRepairDecision Decision = new(true);
        public Action? BeforeDecision;
        public int Questions, Failures;
        public Emignatik.NxFileViewer.Services.Nsz.XciRepairDecision Ask(Emignatik.NxFileViewer.Services.Nsz.XciRepairCandidate candidate)
        { Questions++; BeforeDecision?.Invoke(); return Decision; }
        public void ShowFailure(string reason) => Failures++;
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
    private sealed class PreviewVerifier(Action verified) : Emignatik.NxFileViewer.Services.BackgroundTask.RunnableImpl.IVerifyNcasIntegrityRunnable
    {
        private Emignatik.NxFileViewer.Models.Overview.FileOverview? _overview;
        public bool SupportsCancellation => true;
        public bool SupportProgress => true;
        public void Setup(Emignatik.NxFileViewer.Models.Overview.FileOverview overview, bool ignoreMissingDeltaFragments) => _overview = overview;
        public void Run(Emignatik.NxFileViewer.Services.BackgroundTask.IProgressReporter reporter, CancellationToken token)
        { verified(); _overview!.NcasIntegrity = Emignatik.NxFileViewer.Models.Overview.NcasIntegrity.Original; }
    }
    private sealed class PreviewServices(PreviewLoader loader) : IServiceProvider
    {
        public int VerificationCalls { get; private set; }
        public object? GetService(Type type) => type == typeof(Emignatik.NxFileViewer.FileLoading.IFileLoader) ? loader
            : type == typeof(Emignatik.NxFileViewer.Services.BackgroundTask.RunnableImpl.IVerifyDirectoryIntegrityRunnable)
                ? new Emignatik.NxFileViewer.Services.BackgroundTask.RunnableImpl.VerifyDirectoryIntegrityRunnable(loader, this, new AppSettings(),
                    Microsoft.Extensions.Logging.Abstractions.NullLogger<Emignatik.NxFileViewer.Services.BackgroundTask.RunnableImpl.VerifyDirectoryIntegrityRunnable>.Instance)
                : type == typeof(Emignatik.NxFileViewer.Services.BackgroundTask.RunnableImpl.IVerifyNcasIntegrityRunnable)
                    ? new PreviewVerifier(() => VerificationCalls++) : App.ServiceProvider.GetService(type);
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
