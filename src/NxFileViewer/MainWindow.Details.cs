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
        var selected = BatchResults.SelectedItem as BatchRow;
        var page = selected?.FirmwareArchiveType != null ? FirmwareTab
            : selected?.Nand != null ? BatchNandTab : selected != null ? BatchOverviewTab : null;
        if (page != null) page.IsVisible = true;
        BatchDetailsTabs.SelectedItem = page;
        BatchOverviewTab.IsVisible = page == BatchOverviewTab;
        FirmwareTab.IsVisible = page == FirmwareTab;
        BatchNandTab.IsVisible = page == BatchNandTab;
        BatchNandPath.Text = selected?.Nand != null ? selected.DisplayPath : "";
        BatchFirmwarePath.Text = selected?.FirmwareArchiveType != null ? selected.DisplayPath : "";
        BatchNandInformation.Text = "";
        if (BatchResults.SelectedItem is not BatchRow row)
        {
            BatchDetailPath.Text = "";
            return;
        }

        var source = (row.Path, row.DisplayPath, row.PartNumber);
        if (_batchDetailSource != source)
        {
            _batchContentIndex = 0;
            if (_preferences.LogMinimumLevel == "TRACE")
                LogFileSection(row.DisplayPath, [("TRACE", row.Package == null ? "Displaying stored batch metadata" : "Displaying batch metadata")]);
        }
        _batchDetailSource = source;
        BatchDetailPath.Text = row.DisplayPath;
        if (row.FirmwareArchiveType != null)
        {
            FirmwareResult.Text = row.Summary;
            BatchDetailsTabs.SelectedItem = FirmwareTab;
            return;
        }

        if (row.Nand is { } nand)
        {
            BatchNandInformation.Text = row.NandDetails ?? nand.Information;
            BatchDetailsTabs.SelectedItem = BatchNandTab;
            return;
        }

        var missingKeys = row.MissingKeys.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (CreateMissingKeysWarning(missingKeys) is { } warning)
            BatchDetails.Children.Add(new ContentControl { ContentTemplate = OverviewMissingKeys.ContentTemplate, Content = warning });
        AddBatchOverviewGroup(BatchDetails, _text.FileInfo_Title, 4,
            (_text.Title_FileInfo_FileType, row.ContainerType), (_text.PackageStructure_Title, row.Structure),
            (_text.Signature_Title, row.Signature), (_text.Title_FileInfo_Integrity, row.Integrity),
            (_text.FileInfo_FileSize, row.Size), (_text.Title_FileInfo_Compression, row.Compression),
            (_text.FileInfo_CompressionRatio, row.CompressionRatio));
        if (!string.IsNullOrWhiteSpace(row.SystemUpdateVersion) && row.SystemUpdateVersion != "—")
            BatchDetails.Children.Add(CreateOverviewField(_text.FileInfo_SystemUpdate, row.SystemUpdateVersion));
        if (row.IsSaveBackup) return;
        var metadataHost = new StackPanel();
        if (row.ShowPackageDetails)
        {
            var options = row.PackageDetails.Select(part => new BatchContentOption(part)).ToArray();
            var choice = new ComboBox { ItemsSource = options };
            var tip = string.Format(_text.MultiContentPackageToolTip, row.ContainerType);
            ToolTip.SetTip(choice, tip);
            choice.MinWidth = 120;
            var selector = AddBatchOverviewGroup(BatchDetails, _text.Packages_Title, 1);
            selector.Children.Add(new StackPanel
            {
                Orientation = global::Avalonia.Layout.Orientation.Horizontal,
                HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
                Spacing = 4,
                Children = { new TextBlock { Text = _text.AvailableContents, VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center }, choice, OverviewHelp(tip) }
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
        if (row.RawFileType == "NRO")
        {
            var homebrew = row.Package?.Homebrew;
            var homebrewLanguages = row.StoredLanguages ?? (homebrew == null ? [] : GetHomebrewLanguages(homebrew));
            var overview = AddBatchOverviewGroup(host, "Homebrew (NRO)", 2,
                (_text.AppTitle, row.Title), (_text.Publisher, row.Publisher),
                (_text.DisplayVersion, row.DisplayVersion), (_text.AvailableLanguages, row.Languages),
                (_text.CnmtOverview_BuildID, row.BuildId));
            if ((ReadBatchArtwork(homebrewLanguages.FirstOrDefault()) ?? homebrew?.Icon) is { Length: > 0 } bytes)
            {
                try
                {
                    using var stream = new MemoryStream(bytes);
                    _batchIcon = new Bitmap(stream);
                    overview.Children.Add(new Image { Source = _batchIcon, Width = 160, Height = 160,
                        HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Left });
                }
                catch (ArgumentException) { }
            }
            return;
        }
        var package = AddBatchOverviewGroup(host, _text.CnmtOverview_Title, 2,
            (_text.CnmtOverview_ContentType, row.ContentType), (_text.CnmtOverview_TitleId, row.TitleId),
            (_text.CnmtOverview_TitleVersion, row.Version), (_text.CnmtOverview_MinimumSystemVersion, row.MinimumSystemVersion),
            (_text.CnmtOverview_BaseTitleId, row.BaseTitleId), (_text.CnmtOverview_MasterKey, row.MasterKey),
            (_text.CnmtOverview_MinimumApplicationVersion, row.MinimumApplicationVersion), (_text.CnmtOverview_Distribution, row.Distribution));
        var build = CreateOverviewField(_text.CnmtOverview_BuildID, row.BuildId);
        if ((row.StoredDemo ?? content?.NacpData?.IsDemo) is { } demo)
        {
            var demoCheck = new CheckBox { Content = _text.CnmtOverview_IsDemo, IsChecked = demo, IsHitTestVisible = false, Focusable = false,
                VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Bottom };
            var buildAndDemo = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 6, Children = { build, demoCheck } };
            Grid.SetColumn(demoCheck, 1);
            package.Children.Add(buildAndDemo);
        }
        else package.Children.Add(build);
        var security = row.Security switch { "Safe" => _text.Security_Safe, "Unsafe" => _text.Security_Unsafe, "Dangerous" => _text.Security_Dangerous, _ => row.Security };
        AddBatchOverviewGroup(host, _text.Security_Title, 3,
            (_text.Security_Level, security), (_text.Security_FileSystemPermissions, row.Permissions),
            (_text.Security_AcidSignature, row.AcidSignature));

        var languages = row.StoredLanguages ?? GetOverviewLanguages(content);
        var presentation = AddBatchOverviewGroup(host, _text.Presentation_Title, 1,
            (_text.DisplayVersion, row.DisplayVersion));
        if (languages.Length > 0)
        {
            var languageList = new ListBox { ItemsSource = languages, MinWidth = 105 };
            var title = CreateOverviewField(_text.AppTitle, "—");
            var publisher = CreateOverviewField(_text.Publisher, "—");
            var cover = new Image { Width = 256, Height = 256, IsVisible = false };
            var metadata = new StackPanel { Spacing = 4, Children = { title, publisher, cover } };
            var columns = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 6 };
            Grid.SetColumn(metadata, 1);
            var languagePanel = new StackPanel { Children =
                { OverviewLabel(_text.AvailableLanguages, _text.ToolTip_AvailableLanguages), languageList } };
            columns.Children.Add(languagePanel);
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
                if (ReadBatchArtwork(selected) is not { Length: > 0 } bytes) return;
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

    private StackPanel CreateOverviewField(string name, string value)
    {
        var tip = name == _text.PackageStructure_Title ? _text.ToolTip_PackageStructure :
            name == _text.Signature_Title ? _text.ToolTip_NcaSignature :
            name == _text.Title_FileInfo_Integrity ? _text.ToolTip_NcasIntegrity :
            name == _text.Security_Level || name == _text.Security_FileSystemPermissions ? _text.ToolTip_Permission :
            name == _text.Security_AcidSignature ? _text.ToolTip_AcidSignature : null;
        var text = new TextBox { Text = value, IsReadOnly = true };
        text.Classes.Add("overview-value");
        if (name == _text.Title_FileInfo_Integrity) ApplyIntegrityColor(text);
        if (name == _text.Security_Level) ApplySecurityColor(text);
        return new StackPanel { Margin = new global::Avalonia.Thickness(2, 0, 2, 2), Children = { OverviewLabel(name, tip), text } };
    }

    private static void SetOverviewColor(TextBox field, bool success, bool warning, bool error)
    {
        field.Classes.Set("overview-success", success);
        field.Classes.Set("overview-warning", warning);
        field.Classes.Set("overview-error", error);
    }

    private void ApplyIntegrityColor(TextBox field) => SetOverviewColor(field,
        field.Text == _text.NcasIntegrity_Original || field.Text == _text.Signature_Passed,
        field.Text == _text.NcasIntegrity_Incomplete || field.Text == _text.NcasIntegrity_Modified,
        field.Text == _text.NcasIntegrity_Error || field.Text == _text.NcasIntegrity_Corrupted);

    private void ApplySecurityColor(TextBox field) => SetOverviewColor(field,
        field.Text == _text.Security_Safe, field.Text == _text.Security_Unsafe, field.Text == _text.Security_Dangerous);

    private static Control OverviewLabel(string name, string? tip)
    {
        var label = new TextBlock { Text = name, TextTrimming = TextTrimming.CharacterEllipsis };
        label.Classes.Add("overview-label");
        if (tip == null) return label;
        ToolTip.SetTip(label, tip);
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        var help = OverviewHelp(tip);
        Grid.SetColumn(help, 1);
        header.Children.Add(label);
        header.Children.Add(help);
        return header;
    }

    private static Border OverviewHelp(string tip)
    {
        var marker = new Border { Child = new TextBlock { Text = "?", FontWeight = FontWeight.Bold,
            HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center } };
        marker.Classes.Add("help-tip");
        ToolTip.SetTip(marker, tip);
        return marker;
    }
}
