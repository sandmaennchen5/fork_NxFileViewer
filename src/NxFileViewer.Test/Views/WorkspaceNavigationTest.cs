using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using NxFileViewer.Avalonia;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Views;

[Collection("Avalonia desktop")]
public sealed class WorkspaceNavigationTest
{
    [Fact]
    public Task NavigationRetainsInputsAndCancellingSettingsDiscardsOnlySettingsDraft() =>
        AvaloniaWorkspace.RunAsync(() =>
        {
            var window = new MainWindow();
            var closed = false;
            window.Closed += (_, _) => closed = true;
            try
            {
                var workspace = window.FindControl<TabControl>("Workspace")!;
                var settingsPages = window.FindControl<TabControl>("SettingsPages")!;
                var batch = window.FindControl<TabItem>("BatchTab")!;
                var batchContent = batch.Content;
                var batchPath = window.FindControl<TextBox>("BatchInputPath")!;
                var rename = window.FindControl<TabItem>("RenameTab")!;
                var renamePath = window.FindControl<TextBox>("RenameOutputPath")!;
                var settings = window.FindControl<TabItem>("SettingsTab")!;
                var level = window.FindControl<TextBox>("CompressionLevelSetting")!;
                var originalLevel = level.Text;

                ClickNavigation(window.FindControl<TabItem>("HomeTab")!, "BatchTab");
                Assert.Same(batch, workspace.SelectedItem);
                batchPath.Text = "batch-draft";
                ClickNavigation(window.FindControl<TabItem>("HomeTab")!, "RenameTab");
                Assert.Same(rename, workspace.SelectedItem);
                renamePath.Text = "rename-draft";

                ClickNavigation(rename, "NamingSettingsTab");
                Assert.Same(settings, workspace.SelectedItem);
                Assert.Same(window.FindControl<TabItem>("NamingSettingsTab"), settingsPages.SelectedItem);
                ClickNavigation(window.FindControl<TabItem>("HomeTab")!, "PluginsTab");
                Assert.Same(window.FindControl<TabItem>("PluginsTab"), settingsPages.SelectedItem);
                level.Text = "7";
                ClickNavigation(window.FindControl<TabItem>("HomeTab")!, "BatchTab");
                Assert.Same(batchContent, batch.Content);
                Assert.Equal("batch-draft", batchPath.Text);
                ClickNavigation(window.FindControl<TabItem>("HomeTab")!, "SettingsTab");
                Assert.Same(window.FindControl<TabItem>("ProgramSettingsTab"), settingsPages.SelectedItem);
                Assert.Equal("7", level.Text);

                Click(window.FindControl<Button>("CancelSettingsButton")!);
                Assert.Equal(originalLevel, level.Text);
                Assert.Equal("rename-draft", renamePath.Text);
                Assert.Equal("batch-draft", batchPath.Text);
                Assert.False(closed);
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken);

    [Fact]
    public Task OpeningAndClosingSinglePackageLeavesBatchDraftIntact() =>
        AvaloniaWorkspace.RunAsync(async () =>
        {
            var path = System.IO.Path.Combine(ViewerDataDirectory.Root, "fixture.nsp");
            var package = new byte[16];
            "PFS0"u8.CopyTo(package);
            System.IO.File.WriteAllBytes(path, package);
            var window = new MainWindow();
            try
            {
                var workspace = window.FindControl<TabControl>("Workspace")!;
                var batch = window.FindControl<TabItem>("BatchTab")!;
                var file = window.FindControl<TabItem>("FileTab")!;
                var batchPath = window.FindControl<TextBox>("BatchInputPath")!;
                var close = window.FindControl<Button>("FileCloseButton")!;
                batchPath.Text = "batch draft";
                workspace.SelectedItem = batch;

                await window.OpenFileAsync(path);
                Assert.Same(file, workspace.SelectedItem);
                Assert.Contains("fixture.nsp", window.Title);
                Assert.True(close.IsEnabled);
                var entries = window.FindControl<TreeView>("Entries")!;
                Assert.Single(entries.Items);

                Click(close);
                Assert.False(close.IsEnabled);
                Assert.Empty(entries.Items);
                Assert.DoesNotContain("fixture.nsp", window.Title);
                workspace.SelectedItem = batch;
                Assert.Equal("batch draft", batchPath.Text);
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken);

    private static void ClickNavigation(TabItem tab, string destination) =>
        Click(tab.GetLogicalDescendants().OfType<Button>().First(button => Equals(button.Tag, destination)));

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
}
