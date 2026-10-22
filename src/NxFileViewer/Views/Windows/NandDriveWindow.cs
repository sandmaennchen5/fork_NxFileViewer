using System;
using System.Collections.Generic;
using System.IO;
using Emignatik.NxFileViewer.Services.Nand;
using System.Windows;
using System.Windows.Controls;
using Emignatik.NxFileViewer.Localization;

namespace Emignatik.NxFileViewer.Views.Windows;

public sealed class NandDriveWindow : Window
{
    private readonly ComboBox _source = new() { IsEditable = true, DisplayMemberPath = nameof(NandDriveChoice.DisplayName), Margin = new Thickness(0, 8, 0, 12) };
    public string Source => _source.SelectedItem is NandDriveChoice drive && _source.Text == drive.DisplayName ? drive.Path : _source.Text.Trim();
    public NandDriveWindow(IReadOnlyList<NandDriveChoice>? drives = null)
    {
        var keys = LocalizationManager.Instance.Current.Keys;
        Title = keys.Nand_OpenDrive; Width = 540; SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; ResizeMode = ResizeMode.NoResize;
        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(new TextBlock { Text = keys.Nand_DriveTip, TextWrapping = TextWrapping.Wrap });
        foreach (var drive in drives ?? NandDriveDiscovery.List()) _source.Items.Add(drive);
        panel.Children.Add(_source);
        var refresh = new Button { Content = keys.Nand_DriveRefresh, HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(10, 4, 10, 4) };
        refresh.Click += (_, _) => {
            var previous = Source; _source.Items.Clear();
            foreach (var drive in NandDriveDiscovery.List()) _source.Items.Add(drive);
            _source.Text = previous;
        };
        panel.Children.Add(refresh);
        var button = new Button { Content = keys.Nand_OpenDrive, Padding = new Thickness(12, 5, 12, 5), HorizontalAlignment = HorizontalAlignment.Right, IsDefault = true };
        button.Click += (_, _) =>
        {
            if (!Services.Nand.NandExplorer.IsDevice(Source)) return;
            DialogResult = true;
        };
        panel.Children.Add(button); Content = panel;
    }
}
