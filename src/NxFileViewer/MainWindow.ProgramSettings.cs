using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Emignatik.NxFileViewer.Settings;
namespace NxFileViewer.Avalonia;
public partial class MainWindow
{
    private static void DetachProgramControl(Control control)
    {
        if (control.Parent is Panel panel) panel.Children.Remove(control);
        else if (control.Parent is ContentControl content) content.Content = null;
        else if (control.Parent is Decorator decorator) decorator.Child = null;
    }
    private CheckBox SharedCheck(string label, string property)
    {
        var check = new CheckBox { Content = label, Margin = new Thickness(0, 1) };
        check.Bind(CheckBox.IsCheckedProperty, new Binding(property) { Source = _editedSharedSettings, Mode = BindingMode.TwoWay });
        return check;
    }
    private static Control WithSettingsTip(Control control, string tip)
    {
        var icon = new Border
        {
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = "?", FontWeight = FontWeight.Bold,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            }
        };
        icon.Classes.Add("help-tip");
        ToolTip.SetTip(icon, tip);
        return new StackPanel { Orientation = Orientation.Horizontal, Children = { control, icon } };
    }
    private TextBox SharedText(string property, bool multiline = false)
    {
        var input = new TextBox { AcceptsReturn = multiline, TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap };
        input.Bind(TextBox.TextProperty, new Binding(property) { Source = _editedSharedSettings, Mode = BindingMode.TwoWay });
        return input;
    }
    private StackPanel ProgramGroup(string heading, string? tip = null)
    {
        var panel = new StackPanel { Spacing = 3, Margin = new Thickness(5, 0, 5, 6) };
        var title = new TextBlock { Text = heading, FontWeight = FontWeight.Bold, Margin = new Thickness(0,0,0,4) };
        panel.Children.Add(tip == null ? title : WithSettingsTip(title, tip));
        var border = new Border { Child = panel }; border.Classes.Add("overview-group"); ProgramSettingsHost.Children.Add(border); return panel;
    }
    private static void ProgramLabel(Panel panel, string label, string? tip = null)
    {
        var caption = new TextBlock { Text = label, Margin = new Thickness(0, 4, 0, 0) };
        caption.Classes.Add("overview-label");
        panel.Children.Add(tip == null ? caption : WithSettingsTip(caption, tip));
    }
    private EventHandler<global::Avalonia.Interactivity.RoutedEventArgs>? _programHistoryChanged;
    private sealed class InvertedSystemReportConverter : global::Avalonia.Data.Converters.IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => value is not true;
        public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => value is not true;
    }

    private void AddSystemReportControls(StackPanel general)
    {
        var language = _text.CultureName.Split('-')[0];
        var option = new CheckBox { Margin = new Thickness(0,4,0,4) };
        option.Bind(CheckBox.IsCheckedProperty, new Binding(nameof(AppSettings.SendAnonymousUsageStatistics))
        {
            Source = _editedSharedSettings,
            Mode = BindingMode.TwoWay,
            Converter = new InvertedSystemReportConverter()
        });
        var hint = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,8) };
        option.Content = language switch
        {
            "de" => "Systembericht deaktivieren",
            "fr" => "Désactiver le rapport système",
            "es" => "Desactivar el informe del sistema",
            _ => "Disable system report"
        };
        hint.Text = language switch
        {
            "de" => "Aktivierte Systemberichte senden Programmversion und Systemdetails beim ersten Start nach jeweils 7 Tagen. Standardreports bei Erststart, App-Updates sowie Architektur- oder .NET-Paketwechseln werden unabhängig von dieser Option gesendet. Die App erfasst keine Nutzungsdauer.",
            "fr" => "Les rapports système activés sont envoyés au premier démarrage après 7 jours. Les rapports standard au premier lancement, après une mise à jour ou un changement d’architecture ou de distribution .NET sont indépendants de cette option. L’application ne mesure pas le temps d’utilisation.",
            "es" => "Los informes del sistema activados se envían al primer inicio después de 7 días. Los informes estándar al primer inicio, al actualizar la aplicación o al cambiar la arquitectura o distribución de .NET son independientes de esta opción. La aplicación no mide el tiempo de uso.",
            _ => "Enabled system reports send app version and system details at the first startup after 7 days. Standard reports on first launch, app updates and architecture or .NET distribution changes are independent of this option. The app does not track time spent using it."
        };
        option.IsCheckedChanged += (_, _) => { _editedSharedSettings.UsageStatisticsChoiceConfirmed = true; _editedSharedSettings.UsageStatisticsChoiceVersion = Emignatik.NxFileViewer.Services.UsageStatisticsService.BuildVersion; };
        general.Children.Add(new Separator { Margin = new Thickness(0,8) }); general.Children.Add(option); general.Children.Add(hint);
    }
    private void BuildProgramSettings()
    {
        foreach (var control in new Control[] { LanguageChoice, ThemeChoice, TitleDbRepositoryChoice, RefreshTitleDbButton, TitleDbStatus, VerifyOnOpenSetting, AutoVerifyBatchDropSetting, HistoryEnabledSetting, HistoryLimitSetting, ExportVisibleSetting, LogLevelSetting, LogRetentionSetting }) DetachProgramControl(control);
        ProgramSettingsHost.Children.Clear();
        InitializeExplorerRegistration();
        var general = ProgramGroup(_text.Settings_GroupGeneral);
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*") };
        var language = new StackPanel { Spacing = 4 }; ProgramLabel(language, _text.SettingsView_Title_Language); language.Children.Add(LanguageChoice); LanguageChoice.MinWidth = 100; LanguageChoice.HorizontalAlignment = HorizontalAlignment.Left;
        var theme = new StackPanel { Spacing = 4 }; ProgramLabel(theme, _text.SettingsView_Title_Theme); theme.Children.Add(ThemeChoice); ThemeChoice.MinWidth = 100; ThemeChoice.HorizontalAlignment = HorizontalAlignment.Left;
        Grid.SetColumn(theme, 1); grid.Children.Add(language); grid.Children.Add(theme); general.Children.Add(grid); AddSystemReportControls(general);
        var verification = ProgramGroup(_text.Workspace_File);
        ProgramLabel(verification, _text.Settings_AutoVerifyTitle);
        verification.Children.Add(WithSettingsTip(VerifyOnOpenSetting, _text.Settings_AutoVerifyProgramTip));
        verification.Children.Add(WithSettingsTip(SharedCheck(_text.Settings_AutoVerifyWindows, nameof(AppSettings.VerifyFilesOpenedFromWindows)), _text.Settings_AutoVerifyWindowsTip));
        verification.Children.Add(WithSettingsTip(AutoVerifyBatchDropSetting, _text.Settings_AutoVerifyBatchDropTip));
        verification.Children.Add(new Separator { Margin = new Thickness(0,8) });
        verification.Children.Add(WithSettingsTip(SharedCheck(_text.SettingsView_CheckBox_IgnoreMissingDeltaFragments, nameof(AppSettings.IgnoreMissingDeltaFragments)), _text.SettingsView_ToolTip_IgnoreMissingDeltaFragments));
        verification.Children.Add(WithSettingsTip(SharedCheck(_text.SettingsView_CheckBox_NczOpenBlocklessCompression, nameof(AppSettings.OpenBlocklessCompressionNCZ)), _text.SettingsView_ToolTip_NczBlockLessCompression));
        var packages = ProgramGroup(_text.BatchPackages_Title);
        packages.Children.Add(SharedCheck(_text.BatchPackages_Export, nameof(AppSettings.BatchExportSeparatePackages)));
        packages.Children.Add(WithSettingsTip(SharedCheck(_text.BatchPackages_SummaryOrder, nameof(AppSettings.BatchSummaryPreservePackageOrder)), _text.BatchPackages_SummaryOrderTip));
        packages.Children.Add(new TextBlock { Text = _text.BatchPackages_Tip, TextWrapping = TextWrapping.Wrap });
        var export = ProgramGroup(_text.BatchExport_Title); export.Children.Add(ExportVisibleSetting);
        ProgramLabel(export, _text.BatchExport_Format);
        var format = new ComboBox { ItemsSource = new[] { "CSV (.csv)", "Excel (.xlsx)" }, SelectedIndex = _editedSharedSettings.BatchExportExcel ? 1 : 0, HorizontalAlignment = HorizontalAlignment.Left, MinWidth = 140 };
        format.SelectionChanged += (_, _) => _editedSharedSettings.BatchExportExcel = format.SelectedIndex == 1; export.Children.Add(format);
        var history = ProgramGroup(_text.Settings_GroupHistory, _text.BatchHistory_RetentionTip); history.Children.Add(HistoryEnabledSetting);
        var logos = SharedCheck(_text.BatchHistory_SaveLogos, nameof(AppSettings.SaveBatchHistoryLogos)); history.Children.Add(logos);
        ProgramLabel(history, _text.BatchHistory_Limit); history.Children.Add(HistoryLimitSetting);
        var sd = ProgramGroup(_text.SdBatch_Title); var areas = SharedCheck(_text.SdBatch_Enable, nameof(AppSettings.SdBatchSelectedAreasOnly));
        sd.Children.Add(WithSettingsTip(areas, _text.SdBatch_Tip));
        var paths = new WrapPanel();
        foreach (var (label, property) in new[] { ("/switch", nameof(AppSettings.SdBatchSwitch)), ("/Nintendo", nameof(AppSettings.SdBatchNintendo)), ("/emuMMC", nameof(AppSettings.SdBatchEmuMmc)), ("/tools", nameof(AppSettings.SdBatchTools)), ("hbmenu.nro", nameof(AppSettings.SdBatchHbmenu)), ("/DBIsaves", nameof(AppSettings.SdBatchDbiRootSaves)), ("/dumps", nameof(AppSettings.SdBatchDumps)), ("/nxdt_rw_poc", nameof(AppSettings.SdBatchNxdtRwPoc)) })
        { var check = SharedCheck(label, property); check.Margin = new Thickness(0,0,16,8); paths.Children.Add(check); }
        sd.Children.Add(paths);
        var custom = SharedText(nameof(AppSettings.SdBatchCustomPaths), true); custom.Height = 100;
        var customPanel = new StackPanel { Spacing = 4 }; customPanel.Children.Add(custom); customPanel.Children.Add(new TextBlock { Text = _text.SdBatch_CustomTip, TextWrapping = TextWrapping.Wrap });
        var expand = new global::Avalonia.Controls.Primitives.ToggleButton();
        expand.Classes.Add("neutral-disclosure");
        SetDisclosureToggle(expand, _text.SdBatch_Custom);
        sd.Children.Add(expand);
        sd.Children.Add(customPanel);
        expand.IsCheckedChanged += (_, _) => customPanel.IsVisible = expand.IsChecked == true;
        customPanel.IsVisible = false;
        void UpdateAreas() { paths.IsEnabled = expand.IsEnabled = customPanel.IsEnabled = areas.IsChecked == true; logos.IsEnabled = HistoryLimitSetting.IsEnabled = HistoryEnabledSetting.IsChecked == true; }
        areas.IsCheckedChanged += (_, _) => UpdateAreas(); if (_programHistoryChanged != null) HistoryEnabledSetting.IsCheckedChanged -= _programHistoryChanged; _programHistoryChanged = (_, _) => UpdateAreas(); HistoryEnabledSetting.IsCheckedChanged += _programHistoryChanged; UpdateAreas();
        ProgramSettingsHost.Children.Add(ExplorerRegistrationPanel);
        var titles = ProgramGroup(_text.Settings_GroupTitleInfo);
        var sources = Emignatik.NxFileViewer.Services.ExternalSources.Current;
        var pageUrl = _editedSharedSettings.TitlePageUrl;
        var pageSource = new ComboBox
        {
            ItemsSource = new[] { "Tinfoil", "NX-Content", _text.TitlePage_Custom },
            SelectedIndex = pageUrl == sources.TitlePageUrl ? 0 : pageUrl == sources.NxContentPageUrl ? 1 : 2,
            HorizontalAlignment = HorizontalAlignment.Left,
            MinWidth = 220
        };
        var customPageUrl = new TextBox
        {
            Text = pageSource.SelectedIndex == 2 ? pageUrl : "",
            PlaceholderText = "https://tinfoil.media/Title/{TitleId}"
        };
        void UpdateTitlePageSource()
        {
            customPageUrl.IsVisible = pageSource.SelectedIndex == 2;
            _editedSharedSettings.TitlePageUrl = pageSource.SelectedIndex switch
            {
                0 => sources.TitlePageUrl,
                1 => sources.NxContentPageUrl,
                _ => customPageUrl.Text?.Trim() ?? ""
            };
        }
        pageSource.SelectionChanged += (_, _) => UpdateTitlePageSource();
        customPageUrl.TextChanged += (_, _) => UpdateTitlePageSource();
        ProgramLabel(titles, _text.SettingsView_TitlePageUrl);
        titles.Children.Add(pageSource);
        titles.Children.Add(customPageUrl);
        UpdateTitlePageSource();
        ProgramLabel(titles, _text.SettingsView_TitleInfoProvider, _text.Tinfoil_StabilityHint);
        var provider = new ComboBox { ItemsSource = new[] { "Tinfoil", "TitleDB (GitHub)", "NLib API", _text.TitlePage_Custom }, SelectedIndex = (int)_editedSharedSettings.TitleInfoProvider, HorizontalAlignment = HorizontalAlignment.Left, MinWidth = 220 };
        ToolTip.SetTip(provider, _text.Tinfoil_StabilityHint);
        var customApi = new StackPanel { Spacing = 3, IsVisible = _editedSharedSettings.TitleInfoProvider == TitleInfoProvider.Custom };
        ProgramLabel(customApi, _text.SettingsView_TitleInfoApiUrl);
        customApi.Children.Add(SharedText(nameof(AppSettings.TitleInfoApiUrl)));
        provider.SelectionChanged += (_, _) =>
        {
            if (provider.SelectedIndex < 0) return;
            _editedSharedSettings.TitleInfoProvider = (TitleInfoProvider)provider.SelectedIndex;
            customApi.IsVisible = _editedSharedSettings.TitleInfoProvider == TitleInfoProvider.Custom;
        };
        titles.Children.Add(provider);
        titles.Children.Add(customApi);
        ProgramLabel(titles, _text.SettingsView_TitleDbRepository); titles.Children.Add(TitleDbRepositoryChoice); titles.Children.Add(RefreshTitleDbButton); titles.Children.Add(TitleDbStatus);
        ProgramLabel(titles, _text.SettingsView_TitleDbRegion);
        titles.Children.Add(CreateTitleDbRegionChoice());
        var logging = ProgramGroup(_text.Settings_GroupLogging); ProgramLabel(logging, _text.SettingsView_LogLevel, _text.SettingsView_ToolTip_LogLevel); logging.Children.Add(LogLevelSetting); ProgramLabel(logging, _text.SettingsView_LogFileRetention); logging.Children.Add(LogRetentionSetting);
    }
}
