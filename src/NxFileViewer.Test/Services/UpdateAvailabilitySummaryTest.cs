using System.Threading.Tasks;
using Avalonia.Controls;
using Emignatik.NxFileViewer.Test.Views;
using NxFileViewer.Avalonia;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services;

[Collection("Avalonia desktop")]
public sealed class UpdateAvailabilitySummaryTest
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public Task HomeDistinguishesViewerInstallActionFromAddOnNotice(bool viewer, bool addOn) =>
        AvaloniaWorkspace.RunAsync(() =>
        {
            var window = new MainWindow();
            try
            {
                window.SetHomeApplicationUpdate(viewer, "4.2.0 available", null, "Install");
                window.FindControl<TextBlock>("UpdateFirmwareAvailable")!.Classes.Set("update-available", addOn);
                window.RefreshHomeUpdateNotice();
                Assert.Equal(viewer || addOn, window.FindControl<Control>("HomeUpdatePanel")!.IsVisible);
                Assert.Equal(viewer || addOn, window.FindControl<Button>("HomeUpdatesButton")!.Classes.Contains("update-available"));
                Assert.Equal(viewer, window.FindControl<Button>("HomeInstallUpdate")!.IsVisible);
                Assert.Equal(addOn, window.FindControl<TextBlock>("HomeExtraUpdateText")!.IsVisible);
                if (viewer) Assert.Contains("NxFileViewer: 4.2.0", window.FindControl<TextBlock>("HomeUpdateText")!.Text);
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken);
}
