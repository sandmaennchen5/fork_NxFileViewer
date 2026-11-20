using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace Emignatik.NxFileViewer.Views.Windows;

public partial class UsageStatisticsConsentWindow : Window
{
    public bool StatisticsEnabled { get; private set; } = true;
    private readonly DispatcherTimer _closeTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly Stopwatch _visibleTime = new();
    private readonly bool _german;

    public UsageStatisticsConsentWindow(bool german)
    {
        _german = german;
        InitializeComponent();
        App.ServiceProvider.GetService<Emignatik.NxFileViewer.Styling.Theme.IThemeService>()?.RegisterWindow(this);
        Title = german ? "Nutzungsstatistik" : "Usage statistics";
        Heading.Text = german ? "Anonyme Nutzungsstatistik senden?" : "Send anonymous usage statistics?";
        Explanation.Text = german
            ? "Die Statistik ist standardmäßig aktiviert. Sie hilft dem Entwickler zu erkennen, welche Programmversionen, Betriebssysteme und Auslieferungen tatsächlich verwendet werden, um Updates, Kompatibilität und Tests zu priorisieren. Zusammengefasste Zahlen werden als öffentliche Statistiken und Grafiken angezeigt.\n\n" +
              "Übermittelt werden eine aus der Windows-Installationskennung lokal berechnete Kennung, Programm- und Build-Version, Betriebssystem und Plattform, Prozess- und Betriebssystemarchitektur, WPF/Avalonia, .NET-Auslieferung, Runtime-Version und Runtime-ID.\n\n" +
              "Der Dienst speichert den letzten Stand und Tagesstände für Verläufe. Die feste Kennung ist intern mit deinen Support-Meldungen verknüpfbar. Die Daten sind daher technisch pseudonym. Die ursprüngliche Windows-Kennung, dein Anzeigename, Dateinamen, persönliche Pfade, Dateiinhalte und Sitzungslogs sind nicht Teil dieser Statistikmeldung.\n\n" +
              "Die Übertragung erfolgt beim Start über HTTPS höchstens einmal pro UTC-Tag sowie nach einem Build-Versionswechsel. Bei einem Offline-Dienst startet das Programm normal. Die Option ist jederzeit unter Einstellungen → Programm änderbar."
            : "Usage statistics are enabled by default. They help the developer understand which application versions, operating systems and distributions are used, to prioritize updates, compatibility and testing. Aggregate counts are published as statistics and charts.\n\n" +
              "Reports contain an identity derived locally from the Windows installation ID, application and build versions, operating system and platform, process and OS architectures, WPF/Avalonia, .NET distribution, runtime version and runtime ID.\n\n" +
              "The service stores the latest report and daily records for trends. The stable identity can be linked internally to your support reports, so the data is technically pseudonymous. The original Windows ID, display name, filenames, personal paths, file contents and session logs are not part of this report.\n\n" +
              "Reports are sent over HTTPS at startup at most once per UTC day, plus after a build version change. An offline service does not prevent startup. You can change this option under Settings → Program.";
        ClosingHint.Text = german
            ? "Aktiviert lassen oder deaktivieren? Schließen oder Esc lässt die Statistik aktiviert und speichert diese Auswahl."
            : "Keep statistics enabled or disable them? Closing this window or pressing Esc keeps statistics enabled and saves the choice.";
        KeepEnabledButton.Content = german ? "Aktiviert lassen" : "Keep enabled";
        DisableButton.Content = german ? "Deaktivieren" : "Disable";
        UpdateCountdown(10);
        ContentRendered += (_, _) =>
        {
            if (_visibleTime.IsRunning) return;
            _visibleTime.Start();
            _closeTimer.Start();
        };
        _closeTimer.Tick += (_, _) =>
        {
            var remaining = Math.Max(0, 10 - (int)_visibleTime.Elapsed.TotalSeconds);
            UpdateCountdown(remaining);
            if (remaining == 0) KeepEnabled_Click(this, new RoutedEventArgs());
        };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape) return;
            e.Handled = true;
            KeepEnabled_Click(this, new RoutedEventArgs());
        };
        Closed += (_, _) => { _closeTimer.Stop(); _visibleTime.Stop(); };

    }

    private void UpdateCountdown(int seconds)
    {
        CountdownText.Text = _german
            ? $"Schließt in {seconds} Sekunden automatisch und lässt die Statistik aktiviert."
            : $"Closes automatically in {seconds} seconds and keeps statistics enabled.";
    }

    private void KeepEnabled_Click(object sender, RoutedEventArgs e)
    {
        StatisticsEnabled = true;
        DialogResult = true;
    }

    private void Disable_Click(object sender, RoutedEventArgs e)
    {
        StatisticsEnabled = false;
        DialogResult = false;
    }
}
