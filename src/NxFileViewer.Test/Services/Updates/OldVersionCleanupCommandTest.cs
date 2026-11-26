using System.Threading.Tasks;
using Avalonia.Controls;
using Emignatik.NxFileViewer.Test.Views;
using NxFileViewer.Avalonia;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services.Updates;

[Collection("Avalonia desktop")]
public sealed class OldVersionCleanupCommandTest
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task OpeningPluginSettingsNeverEnumeratesVersionsBeforeCleanupClick(bool supported) =>
        AvaloniaWorkspace.RunAsync(() =>
        {
            var window = new MainWindow();
            try
            {
                var panel = window.CreateManagedPluginSection("Test engine", "owner/test", "1.0", false,
                    (_, _) => Task.CompletedTask, false, () => { },
                    () => throw new System.InvalidOperationException("Cleanup scanned plugin files before a click."),
                    _ => Assert.Fail("Cleanup deleted files before a click."), () => { }, supportsManagement: supported);
                var actions = Assert.IsType<WrapPanel>(panel.Children[2]);
                var cleanup = Assert.IsType<Button>(actions.Children[2]);
                Assert.Equal(supported, cleanup.IsVisible);
                Assert.True(cleanup.IsEnabled);
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken);
}
