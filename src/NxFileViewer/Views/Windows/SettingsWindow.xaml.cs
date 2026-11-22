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
        var language = Emignatik.NxFileViewer.Localization.CommunityLanguage.SelectedLanguage;
        UsageStatisticsOption.Content = language switch
        {
            "de" => "Systembericht deaktivieren",
            "fr" => "Désactiver le rapport système",
            "es" => "Desactivar el informe del sistema",
            _ => "Disable system report"
        };
        UsageStatisticsHint.Text = language switch
        {
            "de" => "Sendet beim Start Programmversion und Systemdetails alle 90 Tage sowie nach einem Upgrade oder einer Änderung der Systemdaten. Zeigt, welche Betriebssysteme und Systemumgebungen verwendet werden. Die App erfasst keine Nutzungsdauer.",
            "fr" => "Au démarrage, envoie la version de l’application et les données système tous les 90 jours, après une mise à niveau ou si ces données changent. Indique les systèmes d’exploitation et les configurations utilisés. L’application ne mesure pas le temps d’utilisation.",
            "es" => "Al iniciar, envía la versión de la aplicación y los datos del sistema cada 90 días, tras actualizar a una versión superior o si cambian esos datos. Indica qué sistemas operativos y configuraciones se usan. La aplicación no mide el tiempo de uso.",
            _ => "Sends the app version and system details at startup every 90 days and after an upgrade or a change in system details. Shows which operating systems and system configurations are in use. The app does not track time spent using it."
        };
    }

    private void RefreshKeyLocations() => (DataContext as SettingsWindowViewModel)?.RefreshKeyLocations();
}
