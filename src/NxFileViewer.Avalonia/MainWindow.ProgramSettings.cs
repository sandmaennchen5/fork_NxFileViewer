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
    private CheckBox SharedCheck(string label, string property, string? tip = null)
    {
        var check = new CheckBox { Content = label, Margin = new Thickness(0, 3) };
        check.Bind(CheckBox.IsCheckedProperty, new Binding(property) { Source = _editedSharedSettings, Mode = BindingMode.TwoWay });
        if (tip != null) ToolTip.SetTip(check, tip);
        return check;
    }
    private TextBox SharedText(string property, bool multiline = false)
    {
        var input = new TextBox { AcceptsReturn = multiline, TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap };
        input.Bind(TextBox.TextProperty, new Binding(property) { Source = _editedSharedSettings, Mode = BindingMode.TwoWay });
        return input;
    }
    private StackPanel ProgramGroup(string heading)
    {
        var panel = new StackPanel { Spacing = 4, Margin = new Thickness(8) };
        panel.Children.Add(new TextBlock { Text = heading, FontWeight = FontWeight.Bold, Margin = new Thickness(0,0,0,4) });
        var border = new Border { Child = panel }; border.Classes.Add("overview-group"); ProgramSettingsHost.Children.Add(border); return panel;
    }
    private static void ProgramLabel(Panel panel, string label) => panel.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0,4,0,0) });
    private EventHandler<global::Avalonia.Interactivity.RoutedEventArgs>? _programHistoryChanged;
    private void AddSystemReportControls(StackPanel general)
    {
        var language = _text.CultureName.Split('-')[0];
        var option = new CheckBox { IsChecked = !_editedSharedSettings.SendAnonymousUsageStatistics, Margin = new Thickness(0,4,0,4) };
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
            "de" => "Sendet beim Start Programmversion und Systemdetails alle 90 Tage sowie nach einem Upgrade oder einer Änderung der Systemdaten. Zeigt, welche Betriebssysteme und Systemumgebungen verwendet werden. Die App erfasst keine Nutzungsdauer.",
            "fr" => "Au démarrage, envoie la version de l’application et les données système tous les 90 jours, après une mise à niveau ou si ces données changent. Indique les systèmes d’exploitation et les configurations utilisés. L’application ne mesure pas le temps d’utilisation.",
            "es" => "Al iniciar, envía la versión de la aplicación y los datos del sistema cada 90 días, tras actualizar a una versión superior o si cambian esos datos. Indica qué sistemas operativos y configuraciones se usan. La aplicación no mide el tiempo de uso.",
            _ => "Sends the app version and system details at startup every 90 days and after an upgrade or a change in system details. Shows which operating systems and system configurations are in use. The app does not track time spent using it."
        };
        option.IsCheckedChanged += (_, _) => { _editedSharedSettings.SendAnonymousUsageStatistics = option.IsChecked != true; _editedSharedSettings.UsageStatisticsChoiceConfirmed = true; _editedSharedSettings.UsageStatisticsChoiceVersion = Emignatik.NxFileViewer.Services.UsageStatisticsService.BuildVersion; };
        general.Children.Add(new Separator { Margin = new Thickness(0,8) }); general.Children.Add(option); general.Children.Add(hint);
    }
    private void BuildProgramSettings()
    {
        foreach (var control in new Control[] { LanguageChoice, ThemeChoice, TitleDbRepositoryChoice, RefreshTitleDbButton, TitleDbStatus, VerifyOnOpenSetting, AutoVerifyBatchDropSetting, HistoryEnabledSetting, HistoryLimitSetting, ExportVisibleSetting, LogLevelSetting, LogRetentionSetting }) DetachProgramControl(control);
        ProgramSettingsHost.Children.Clear();
        var general = ProgramGroup(_text.Settings_GroupGeneral);
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*") };
        var language = new StackPanel { Spacing = 4 }; ProgramLabel(language, _text.SettingsView_Title_Language); language.Children.Add(LanguageChoice); LanguageChoice.MinWidth = 100; LanguageChoice.HorizontalAlignment = HorizontalAlignment.Left;
        var theme = new StackPanel { Spacing = 4 }; ProgramLabel(theme, _text.SettingsView_Title_Theme); theme.Children.Add(ThemeChoice); ThemeChoice.MinWidth = 100; ThemeChoice.HorizontalAlignment = HorizontalAlignment.Left;
        Grid.SetColumn(theme, 1); grid.Children.Add(language); grid.Children.Add(theme); general.Children.Add(grid); AddSystemReportControls(general);
        var verification = ProgramGroup(_text.Workspace_File);
        ProgramLabel(verification, _text.Settings_AutoVerifyTitle); verification.Children.Add(VerifyOnOpenSetting);
        verification.Children.Add(SharedCheck(_text.Settings_AutoVerifyWindows, nameof(AppSettings.VerifyFilesOpenedFromWindows), _text.Settings_AutoVerifyWindowsTip));
        verification.Children.Add(AutoVerifyBatchDropSetting); verification.Children.Add(new Separator { Margin = new Thickness(0,8) });
        verification.Children.Add(SharedCheck(_text.SettingsView_CheckBox_IgnoreMissingDeltaFragments, nameof(AppSettings.IgnoreMissingDeltaFragments), _text.SettingsView_ToolTip_IgnoreMissingDeltaFragments));
        verification.Children.Add(SharedCheck(_text.SettingsView_CheckBox_NczOpenBlocklessCompression, nameof(AppSettings.OpenBlocklessCompressionNCZ), _text.SettingsView_ToolTip_NczBlockLessCompression));
        var packages = ProgramGroup(_text.BatchPackages_Title);
        packages.Children.Add(SharedCheck(_text.BatchPackages_Export, nameof(AppSettings.BatchExportSeparatePackages)));
        packages.Children.Add(SharedCheck(_text.BatchPackages_SummaryOrder, nameof(AppSettings.BatchSummaryPreservePackageOrder), _text.BatchPackages_SummaryOrderTip));
        packages.Children.Add(new TextBlock { Text = _text.BatchPackages_Tip, TextWrapping = TextWrapping.Wrap });
        var export = ProgramGroup(_text.BatchExport_Title); export.Children.Add(ExportVisibleSetting);
        ProgramLabel(export, _text.BatchExport_Format);
        var format = new ComboBox { ItemsSource = new[] { "CSV (.csv)", "Excel (.xlsx)" }, SelectedIndex = _editedSharedSettings.BatchExportExcel ? 1 : 0, HorizontalAlignment = HorizontalAlignment.Left, MinWidth = 140 };
        format.SelectionChanged += (_, _) => _editedSharedSettings.BatchExportExcel = format.SelectedIndex == 1; export.Children.Add(format);
        var history = ProgramGroup(_text.Settings_GroupHistory); history.Children.Add(HistoryEnabledSetting);
        var logos = SharedCheck(_text.BatchHistory_SaveLogos, nameof(AppSettings.SaveBatchHistoryLogos)); history.Children.Add(logos);
        ProgramLabel(history, _text.BatchHistory_Limit); history.Children.Add(HistoryLimitSetting);
        var sd = ProgramGroup(_text.SdBatch_Title); var areas = SharedCheck(_text.SdBatch_Enable, nameof(AppSettings.SdBatchSelectedAreasOnly), _text.SdBatch_Tip); sd.Children.Add(areas);
        var paths = new WrapPanel();
        foreach (var (label, property) in new[] { ("/switch", nameof(AppSettings.SdBatchSwitch)), ("/Nintendo", nameof(AppSettings.SdBatchNintendo)), ("/emuMMC", nameof(AppSettings.SdBatchEmuMmc)), ("/tools", nameof(AppSettings.SdBatchTools)), ("hbmenu.nro", nameof(AppSettings.SdBatchHbmenu)), ("/DBIsaves", nameof(AppSettings.SdBatchDbiRootSaves)), ("/dumps", nameof(AppSettings.SdBatchDumps)), ("/nxdt_rw_poc", nameof(AppSettings.SdBatchNxdtRwPoc)) })
        { var check = SharedCheck(label, property); check.Margin = new Thickness(0,0,16,8); paths.Children.Add(check); }
        sd.Children.Add(paths);
        var custom = SharedText(nameof(AppSettings.SdBatchCustomPaths), true); custom.Height = 100;
        var customPanel = new StackPanel { Spacing = 4 }; customPanel.Children.Add(custom); customPanel.Children.Add(new TextBlock { Text = _text.SdBatch_CustomTip, TextWrapping = TextWrapping.Wrap });
        var expand = new Expander { Header = _text.SdBatch_Custom, Content = customPanel }; sd.Children.Add(expand);
        void UpdateAreas() { paths.IsEnabled = expand.IsEnabled = areas.IsChecked == true; logos.IsEnabled = HistoryLimitSetting.IsEnabled = HistoryEnabledSetting.IsChecked == true; }
        areas.IsCheckedChanged += (_, _) => UpdateAreas(); if (_programHistoryChanged != null) HistoryEnabledSetting.IsCheckedChanged -= _programHistoryChanged; _programHistoryChanged = (_, _) => UpdateAreas(); HistoryEnabledSetting.IsCheckedChanged += _programHistoryChanged; UpdateAreas();
        var titles = ProgramGroup(_text.Settings_GroupTitleInfo);
        ProgramLabel(titles, _text.SettingsView_TitlePageUrl); titles.Children.Add(SharedText(nameof(AppSettings.TitlePageUrl)));
        ProgramLabel(titles, _text.SettingsView_TitleInfoProvider);
        var provider = new ComboBox { ItemsSource = new[] { "Tinfoil", "TitleDB (GitHub)", "NLib API", _text.TitlePage_Custom }, SelectedIndex = (int)_editedSharedSettings.TitleInfoProvider, HorizontalAlignment = HorizontalAlignment.Left, MinWidth = 220 };
        ToolTip.SetTip(provider, _text.Tinfoil_StabilityHint); provider.SelectionChanged += (_, _) => { if (provider.SelectedIndex >= 0) _editedSharedSettings.TitleInfoProvider = (TitleInfoProvider)provider.SelectedIndex; }; titles.Children.Add(provider);
        ProgramLabel(titles, _text.SettingsView_TitleInfoApiUrl); titles.Children.Add(SharedText(nameof(AppSettings.TitleInfoApiUrl)));
        ProgramLabel(titles, _text.SettingsView_TitleDbRepository); titles.Children.Add(TitleDbRepositoryChoice); titles.Children.Add(RefreshTitleDbButton); titles.Children.Add(TitleDbStatus);
        ProgramLabel(titles, _text.SettingsView_TitleDbRegion); titles.Children.Add(SharedText(nameof(AppSettings.TitleDbRegion)));
        var logging = ProgramGroup(_text.Settings_GroupLogging); ProgramLabel(logging, _text.SettingsView_LogLevel); logging.Children.Add(LogLevelSetting); ProgramLabel(logging, _text.SettingsView_LogFileRetention); logging.Children.Add(LogRetentionSetting);
    }
}
