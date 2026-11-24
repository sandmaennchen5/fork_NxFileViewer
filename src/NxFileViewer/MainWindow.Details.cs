using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using System.IO;
using Emignatik.NxFileViewer.FileLoading.QuickFileInfoLoading;
namespace NxFileViewer.Avalonia;
public partial class MainWindow
{
    private sealed record DetailRow(string Name, string Value);
    private Bitmap? _batchIcon;
    private (string Path, string DisplayPath, int PartNumber)? _batchDetailSource;
    private int _batchContentIndex;
    private void BatchSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!ReferenceEquals(e.Source, BatchResults)) return;

        BatchDetails.Children.Clear();
        _batchIcon?.Dispose();
        _batchIcon = null;
        BatchNandTab.IsVisible = BatchResults.SelectedItem is BatchRow { Nand: not null };
        BatchNandInformation.Text = "";
        if (BatchResults.SelectedItem is not BatchRow row)
        {
            BatchDetailPath.Text = "";
            return;
        }

        var source = (row.Path, row.DisplayPath, row.PartNumber);
        if (_batchDetailSource != source) _batchContentIndex = 0;
        _batchDetailSource = source;
        BatchDetailPath.Text = row.DisplayPath;
        if (row.FirmwareArchiveType != null)
        {
            if (_firmwareCancellation == null) FirmwareResult.Text = row.Summary;
            BatchDetailsTabs.SelectedItem = FirmwareTab;
            return;
        }

        if (row.Nand is { } nand)
        {
            BatchNandInformation.Text = nand.Information;
            BatchDetailsTabs.SelectedItem = BatchNandTab;
            return;
        }

        BatchDetailsTabs.SelectedIndex = 0;
        AddBatchOverviewGroup(BatchDetails, _text.FileInfo_Title, 2,
            (_text.Title_FileInfo_FileType, row.ContainerType), (_text.PackageStructure_Title, row.Structure),
            (_text.Signature_Title, row.Signature), (_text.Title_FileInfo_Integrity, row.Integrity),
            (_text.FileInfo_FileSize, row.Size), (_text.Title_FileInfo_Compression, row.Compression),
            (_text.FileInfo_CompressionRatio, row.CompressionRatio));
        if (!string.IsNullOrWhiteSpace(row.MissingKeys))
            BatchDetails.Children.Add(CreateOverviewField(_text.File_MissingKeysHeader, row.MissingKeys));
        var metadataHost = new StackPanel();
        if (row.ShowPackageDetails)
        {
            var options = row.PackageDetails.Select(part => new BatchContentOption(part)).ToArray();
            var choice = new ComboBox { ItemsSource = options };
            ToolTip.SetTip(choice, string.Format(_text.MultiContentPackageToolTip, row.ContainerType));
            choice.MinWidth = 120;
            var selector = AddBatchOverviewGroup(BatchDetails, _text.Packages_Title, 1);
            selector.Children.Add(new StackPanel
            {
                Orientation = global::Avalonia.Layout.Orientation.Horizontal,
                HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
                Spacing = 4,
                Children = { new TextBlock { Text = _text.AvailableContents, VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center }, choice }
            });
            BatchDetails.Children.Add(metadataHost);
            choice.SelectionChanged += (_, _) =>
            {
                if (choice.SelectedItem is BatchContentOption option)
                {
                    _batchContentIndex = choice.SelectedIndex;
                    ShowBatchContent(metadataHost, option.Row);
                }
            };
            choice.SelectedIndex = Math.Clamp(_batchContentIndex, 0, options.Length - 1);
        }
        else
        {
            BatchDetails.Children.Add(metadataHost);
            ShowBatchContent(metadataHost, row);
        }
    }

    private sealed record BatchContentOption(BatchRow Row)
    {
        public override string ToString()
        {
            return $"{Row.PartNumber}. {Row.ContentType} · {Row.TitleId} · {Row.Title}";
        }
    }

    private void ShowBatchContent(StackPanel host, BatchRow row)
    {
        host.Children.Clear();
        _batchIcon?.Dispose();
        _batchIcon = null;
        var content = row.ContentOverride ?? row.Package?.Titles.FirstOrDefault();
        var package = AddBatchOverviewGroup(host, _text.CnmtOverview_Title, 2,
            (_text.CnmtOverview_ContentType, row.ContentType), (_text.CnmtOverview_TitleId, row.TitleId),
            (_text.CnmtOverview_TitleVersion, row.Version), (_text.CnmtOverview_MinimumSystemVersion, row.MinimumSystemVersion),
            (_text.CnmtOverview_BaseTitleId, row.BaseTitleId), (_text.CnmtOverview_MasterKey, row.MasterKey),
            (_text.CnmtOverview_MinimumApplicationVersion, row.MinimumApplicationVersion), (_text.CnmtOverview_Distribution, row.Distribution));
        var build = CreateOverviewField(_text.CnmtOverview_BuildID, row.BuildId);
        package.Children.Add(build);
        if ((row.StoredDemo ?? content?.NacpData?.IsDemo) is { } demo)
            build.Children.Add(new CheckBox { Content = _text.CnmtOverview_IsDemo, IsChecked = demo, IsHitTestVisible = false, Focusable = false });
        AddBatchOverviewGroup(host, _text.Security_Title, 3,
            (_text.Security_Level, row.Security), (_text.Security_FileSystemPermissions, row.Permissions),
            (_text.Security_AcidSignature, row.AcidSignature));

        var languages = row.StoredLanguages ?? GetOverviewLanguages(content);
        var presentation = AddBatchOverviewGroup(host, _text.Presentation_Title, 1,
            (_text.DisplayVersion, row.DisplayVersion));
        if (languages.Length > 0)
        {
            var languageList = new ListBox { ItemsSource = languages, MinWidth = 105 };
            var title = CreateOverviewField(_text.AppTitle, "—");
            var publisher = CreateOverviewField(_text.Publisher, "—");
            var cover = new Image { MaxWidth = 256, MaxHeight = 256, IsVisible = false };
            var metadata = new StackPanel { Spacing = 4, Children = { title, publisher, cover } };
            var columns = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 6 };
            Grid.SetColumn(metadata, 1);
            columns.Children.Add(languageList);
            columns.Children.Add(metadata);
            presentation.Children.Add(columns);
            languageList.SelectionChanged += (_, _) =>
            {
                var selected = languageList.SelectedItem as OverviewLanguage;
                ((TextBox)title.Children[1]).Text = selected?.AppName ?? "—";
                ((TextBox)publisher.Children[1]).Text = selected?.Publisher ?? "—";
                cover.Source = null;
                cover.IsVisible = false;
                _batchIcon?.Dispose();
                _batchIcon = null;
                if (selected?.Icon is not { Length: > 0 } bytes) return;
                try
                {
                    using var stream = new MemoryStream(bytes);
                    _batchIcon = new Bitmap(stream);
                    cover.Source = _batchIcon;
                    cover.IsVisible = true;
                }
                catch (ArgumentException) { }
            };
            languageList.SelectedIndex = 0;
        }
        if (!string.IsNullOrWhiteSpace(row.Error))
            host.Children.Add(CreateOverviewField(_text.BatchIntegrity_Error, row.Error));
    }

    private StackPanel AddBatchOverviewGroup(StackPanel host, string heading, int columns, params (string Name, string Value)[] fields)
    {
        var grid = new UniformGrid { Columns = columns };
        foreach (var field in fields)
            grid.Children.Add(CreateOverviewField(field.Name, field.Value));
        var panel = new StackPanel { Spacing = 4, Children = { new TextBlock { Text = heading, FontWeight = FontWeight.Bold }, grid } };
        var border = new Border { Child = panel };
        border.Classes.Add("overview-group");
        host.Children.Add(border);
        return panel;
    }

    private static StackPanel CreateOverviewField(string name, string value)
    {
        var label = new TextBlock { Text = name, TextTrimming = TextTrimming.CharacterEllipsis };
        label.Classes.Add("overview-label");
        var text = new TextBox { Text = value, IsReadOnly = true };
        text.Classes.Add("overview-value");
        return new StackPanel { Margin = new global::Avalonia.Thickness(2, 0, 2, 2), Children = { label, text } };
    }
    private void EntryPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(Entries).Properties.IsRightButtonPressed) return;
        if (e.Source is global::Avalonia.Visual visual)
        {
            for (var current = visual; current != null; current = global::Avalonia.VisualTree.VisualExtensions.GetVisualParent(current))
                if (current is TreeViewItem item) { Entries.SelectedItem = item.DataContext; break; }
        }
    }
}
