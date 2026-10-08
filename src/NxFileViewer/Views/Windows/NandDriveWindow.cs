using System.IO;
using System.Windows;
using System.Windows.Controls;
using Emignatik.NxFileViewer.Localization;

namespace Emignatik.NxFileViewer.Views.Windows;

public sealed class NandDriveWindow : Window
{
    private readonly ComboBox _source = new() { IsEditable = true, Margin = new Thickness(0, 8, 0, 12) };
    public string Source => _source.Text.Trim();
    public NandDriveWindow()
    {
        var keys = LocalizationManager.Instance.Current.Keys;
        Title = keys.Nand_OpenDrive; Width = 540; SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; ResizeMode = ResizeMode.NoResize;
        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(new TextBlock { Text = keys.Nand_DriveTip, TextWrapping = TextWrapping.Wrap });
        foreach (var drive in DriveInfo.GetDrives()) _source.Items.Add(@"\\.\" + drive.Name.TrimEnd('\\'));
        // Windows physical-device names can also be entered directly.
        for (var index = 0; index < 32; index++)
        {
            var device = @"\\.\PhysicalDrive" + index;
            try { using var probe = new Services.Nand.NandExplorer(device); _source.Items.Add(device); }
            catch (System.Exception ex) when (ex is IOException or System.UnauthorizedAccessException) { }
        }
        panel.Children.Add(_source);
        var button = new Button { Content = keys.Nand_OpenDrive, Padding = new Thickness(12, 5, 12, 5), HorizontalAlignment = HorizontalAlignment.Right, IsDefault = true };
        button.Click += (_, _) =>
        {
            if (!Services.Nand.NandExplorer.IsDevice(Source)) return;
            DialogResult = true;
        };
        panel.Children.Add(button); Content = panel;
    }
}
