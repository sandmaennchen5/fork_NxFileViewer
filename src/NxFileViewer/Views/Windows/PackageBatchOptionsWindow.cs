using System.Windows;
using System.Windows.Controls;
using Emignatik.NxFileViewer.Localization;
using Emignatik.NxFileViewer.Services.PackageOperations;

namespace Emignatik.NxFileViewer.Views.Windows;

public sealed class PackageBatchOptionsWindow : Window
{
    public sealed record Choice<T>(T Value, string Label);
    public ComboBox FormatSelector { get; }
    public ComboBox GroupingSelector { get; }
    public PackageBatchFormat Format => ((Choice<PackageBatchFormat>)FormatSelector.SelectedItem).Value;
    public PackageMergeGrouping Grouping => ((Choice<PackageMergeGrouping>)GroupingSelector.SelectedItem).Value;
    public PackageBatchOptionsWindow(bool merge)
    {
        var keys = LocalizationManager.Instance.Current.Keys;
        Title = merge ? keys.Batch_MergeAllPackages : keys.Batch_SplitAllPackages;
        Width = 540; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Style = Application.Current.TryFindResource("WindowStyle") as Style;
        var panel = new StackPanel { Margin = new Thickness(20) };
        Content = panel;
        GroupingSelector = new ComboBox { DisplayMemberPath = "Label", Margin = new Thickness(0, 0, 0, 16),
            Visibility = merge ? Visibility.Visible : Visibility.Collapsed,
            ItemsSource = new[] {
                new Choice<PackageMergeGrouping>(PackageMergeGrouping.ByGame, keys.Batch_MergeByGame),
                new Choice<PackageMergeGrouping>(PackageMergeGrouping.AcrossGames, keys.Batch_MergeAcrossGames) }, SelectedIndex = 0 };
        panel.Children.Add(GroupingSelector);
        var formats = new System.Collections.Generic.List<Choice<PackageBatchFormat>>();
        if (!merge) formats.Add(new(PackageBatchFormat.KeepSource, keys.Batch_KeepSourceFormat));
        formats.Add(new(PackageBatchFormat.Nsp, merge ? "SuperNSP" : "NSP"));
        formats.Add(new(PackageBatchFormat.Xci, merge ? "SuperXCI" : "XCI"));
        FormatSelector = new ComboBox { DisplayMemberPath = "Label", ItemsSource = formats, SelectedIndex = 0, Margin = new Thickness(0, 0, 0, 20) };
        panel.Children.Add(FormatSelector);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var start = new Button { Content = keys.Batch_StartPackageJob, IsDefault = true, Padding = new Thickness(12, 5, 12, 5), Margin = new Thickness(0, 0, 8, 0) };
        start.Click += (_, _) => DialogResult = true;
        buttons.Children.Add(start);
        buttons.Children.Add(new Button { Content = keys.Nsz_Cancel, IsCancel = true, Padding = new Thickness(12, 5, 12, 5) });
        panel.Children.Add(buttons);
    }
}