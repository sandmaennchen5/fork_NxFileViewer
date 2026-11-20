using System.Windows.Controls;

namespace Emignatik.NxFileViewer.Views.Windows;

// Embedded workspace page; its view model survives navigation.
public partial class SettingsWindow : UserControl
{
    public SettingsWindow()
    {
        InitializeComponent();
        RefreshCommunityText();
        Emignatik.NxFileViewer.Localization.CommunityLanguage.Observe(this, view => view.RefreshCommunityText());
        Loaded += (_, _) => RefreshKeyLocations();
        SettingsSections.SelectionChanged += (_, args) => {
            if (ReferenceEquals(args.OriginalSource, SettingsSections) && SettingsSections.SelectedItem == KeysSettingsTab)
                RefreshKeyLocations();
        };
    }

    private void RefreshCommunityText()
    {
        var statisticsGerman = Emignatik.NxFileViewer.Localization.CommunityLanguage.IsGerman;
        UsageStatisticsOption.Content = statisticsGerman ? "Anonyme Nutzungsstatistik senden" : "Send anonymous usage statistics";
        UsageStatisticsHint.Text = statisticsGerman
            ? "Sendet beim Start höchstens täglich Kennung, Version und Systemdetails, zusätzlich nach einem Versionswechsel. Die feste Kennung ist intern mit deinen Support-Meldungen verknüpfbar."
            : "Sends identity, version and system details at startup at most daily, plus after a version change. The stable identity can be linked internally to your support reports.";
    }

    private void RefreshKeyLocations() => (DataContext as SettingsWindowViewModel)?.RefreshKeyLocations();
}
