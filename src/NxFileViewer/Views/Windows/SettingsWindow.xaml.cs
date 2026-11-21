using System;
using System.Linq;
using System.Windows.Controls;
using Emignatik.NxFileViewer.Services;

namespace Emignatik.NxFileViewer.Views.Windows;

// Embedded workspace page; its view model survives navigation.
public partial class SettingsWindow : UserControl
{
    public SettingsWindow()
    {
        InitializeComponent();
        foreach (var source in TitleDbSources.All)
            TitleDbRepositoryChoice.Items.Add(new ComboBoxItem { Tag = source.Repository, Content = TitleDbSources.Label(source, null) });
        RefreshCommunityText();
        Emignatik.NxFileViewer.Localization.CommunityLanguage.Observe(this, view => view.RefreshCommunityText());
        Loaded += (_, _) => RefreshKeyLocations();
        SettingsSections.SelectionChanged += (_, args) => {
            if (ReferenceEquals(args.OriginalSource, SettingsSections) && SettingsSections.SelectedItem == KeysSettingsTab)
                RefreshKeyLocations();
        };
    }

    private async void TitleDbRepositoryOpened(object? sender, EventArgs e)
    {
        if (DataContext is not SettingsWindowViewModel model) return;
        var region = model.EditedSettings.TitleDbRegion;
        var items = TitleDbRepositoryChoice.Items.OfType<ComboBoxItem>().ToArray();
        foreach (var item in items)
        {
            var source = TitleDbSources.Resolve(item.Tag as string);
            item.Content = TitleDbSources.Label(source, TitleDbSources.CachedDate(source, region));
        }
        await System.Threading.Tasks.Task.WhenAll(items.Select(async item =>
        {
            var source = TitleDbSources.Resolve(item.Tag as string);
            var date = await TitleDbSources.UpdateDateAsync(source, region);
            if (region == model.EditedSettings.TitleDbRegion)
                item.Content = TitleDbSources.Label(source, date);
        }));
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
