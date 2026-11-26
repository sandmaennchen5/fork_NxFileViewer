using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Emignatik.NxFileViewer.Test.Views;
using NxFileViewer.Avalonia;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services.KeysManagement;

[Collection("Avalonia desktop")]
public sealed class KeyLocationStatusTest
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ReportsDefaultAndCustomLocationsWithIndependentValidation(bool titleKeys) =>
        AvaloniaWorkspace.RunAsync(() =>
        {
            var name = titleKeys ? "title.keys" : "prod.keys";
            var program = Path.Combine(ViewerDataDirectory.Root, name);
            var custom = Path.Combine(ViewerDataDirectory.Root, "custom.keys");
            File.WriteAllText(program, "invalid synthetic line");
            File.WriteAllText(custom, titleKeys
                ? "00000000000000000000000000000012 = 00000000000000000000000000000000"
                : "header_key = " + new string('0', 64));
            var window = new MainWindow();
            try
            {
                var locations = window.FindControl<StackPanel>(titleKeys ? "TitleKeyLocations" : "ProdKeyLocations")!;
                var customPath = window.FindControl<TextBox>(titleKeys ? "TitleKeysPath" : "SettingsKeysPath")!;
                var customStatus = window.FindControl<TextBlock>(titleKeys ? "TitleKeyValidationText" : "KeyValidationText")!;
                customPath.Text = "";
                customPath.RaiseEvent(new RoutedEventArgs(InputElement.LostFocusEvent));
                Assert.Equal(2, locations.Children.Count);
                var programPanel = locations.Children[0];
                Assert.Equal(program, programPanel.GetLogicalDescendants().OfType<TextBox>().Single().Text);
                Assert.Contains(programPanel.GetLogicalDescendants().OfType<TextBlock>(), label => label.Classes.Contains("key-invalid"));
                Assert.False(customStatus.IsVisible);

                customPath.Text = custom;
                customPath.RaiseEvent(new RoutedEventArgs(InputElement.LostFocusEvent));
                Assert.True(customStatus.IsVisible);
                Assert.Contains(Emignatik.NxFileViewer.Localization.LocalizationManager.Instance.Current.Keys.Keys_InUse, customStatus.Text);
                Assert.Equal(custom, MainWindow.ResolveKeyFile(customPath.Text, name));
                Assert.Equal(2, locations.Children.Count);
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken);
}
