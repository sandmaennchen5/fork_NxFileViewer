using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;

namespace NxFileViewer.Avalonia;

public partial class UsageStatisticsConsentWindow : Window
{
    private bool _answered;

    public bool StatisticsEnabled { get; private set; } = true;
    private readonly string _language;

    public UsageStatisticsConsentWindow() : this(false) { }
    public UsageStatisticsConsentWindow(bool enabledByDefault)
    {
        _language = Emignatik.NxFileViewer.Localization.LocalizationManager.Instance.Current.Keys.CultureName.Split('-')[0];
        StatisticsEnabled = enabledByDefault;
        var german = _language == "de";
        InitializeComponent();
        WpfTheme.Apply(this);
        Title = german ? "Systembericht" : "System report";
        Heading.Text = german ? "Systembericht senden?" : "Send system report?";
        Explanation.Text = german
            ? "Der Systembericht zeigt, welche Betriebssysteme und Versionen (z. B. Windows 10 oder Windows 11), Architekturen, .NET-Laufzeiten und Programmversionen im Einsatz sind.\n\nIch freue mich, wenn du den Systembericht aktivierst. So kann ich meine Tests und die Entwicklung gezielt auf die Systeme ausrichten, die ihr im Alltag nutzt. Danke für deine Unterstützung!\n\nDie App erfasst keine Nutzungsdauer. Öffentliche Grafiken zeigen zusammengefasste Zahlen.\n\n" +
              "Übermittelt werden eine lokal aus der Systemkennung berechnete oder dauerhaft gespeicherte Ersatzkennung, Programm- und Build-Version, Betriebssystem und Plattform, Prozess- und Betriebssystemarchitektur, WPF/Avalonia, .NET-Auslieferung, Runtime-Version und Runtime-ID.\n\n" +
              "Der Dienst speichert den letzten Stand und Tagesstände für Verläufe. Die feste Kennung ist intern mit deinen Support-Meldungen verknüpfbar. Die Daten sind daher technisch pseudonym. Die ursprüngliche Windows-Kennung, dein Anzeigename, Dateinamen, persönliche Pfade, Dateiinhalte und Sitzungslogs sind nicht Teil dieser Statistikmeldung.\n\n" +
              "Die Übertragung erfolgt beim Start über HTTPS alle 7 Tage, wenn Systemberichte aktiviert sind. Bei einem Offline-Dienst startet das Programm normal. Die Option ist jederzeit unter Einstellungen → Programm änderbar."
            : "The system report shows which operating systems and versions (such as Windows 10 or Windows 11), architectures, .NET runtimes and app versions are in use.\n\nI appreciate it if you enable system reports. Your report helps me focus testing and development on the systems people actually use. Thank you for your support!\n\nThe app does not track time spent using it. Public charts show aggregate counts.\n\n" +
              "Reports contain an identity derived locally from the system identity or a persistent fallback identity, application and build versions, operating system and platform, process and OS architectures, WPF/Avalonia, .NET distribution, runtime version and runtime ID.\n\n" +
              "The service stores the latest report and daily records for trends. The stable identity can be linked internally to your support reports, so the data is technically pseudonymous. The original Windows ID, display name, filenames, personal paths, file contents and session logs are not part of this report.\n\n" +
              "Reports are sent over HTTPS at startup every 7 days when system reports are enabled. An offline service does not prevent startup. You can change this option under Settings → Program.";
        ClosingHint.Text = german
            ? "Aktiviert lassen oder deaktivieren? Schließen oder Esc behält die bisherige Einstellung bei und speichert diese Auswahl."
            : "Keep statistics enabled or disable them? Closing this window or pressing Esc keeps statistics enabled and saves the choice.";
        KeepEnabledButton.Content = german ? "Aktiviert lassen" : "Keep enabled";
        DisableButton.Content = german ? "Deaktivieren" : "Disable";
        switch (_language)
        {
            case "fr":
                Title = "Rapport système";
                Heading.Text = "Envoyer le rapport système ?";
                Explanation.Text = "Le rapport système indique quels systèmes d’exploitation et versions (par exemple Windows 10 ou Windows 11), architectures, environnements .NET et versions de l’application sont utilisés.\n\nMerci de activer le rapport système si tu le souhaites. Tes données m’aident à orienter mes tests et le développement vers les systèmes que vous utilisez au quotidien. Merci pour ton soutien !\n\nL’application ne mesure pas le temps d’utilisation. Les graphiques publics présentent des chiffres agrégés.\n\nLe rapport contient un identifiant dérivé localement de l’identifiant d’installation de Windows, les versions de l’application et de sa compilation, le système d’exploitation et la plateforme, les architectures du processus et du système, WPF/Avalonia, le mode de distribution de .NET, sa version et son identifiant d’exécution.\n\nLe service conserve le dernier rapport et des relevés journaliers pour suivre l’évolution. L’identifiant stable peut être associé en interne à tes demandes d’assistance. Les données sont donc pseudonymisées. L’identifiant Windows d’origine, ton nom d’affichage, les noms de fichiers, les chemins personnels, le contenu des fichiers et les journaux de session ne sont pas transmis.\n\nL’envoi se fait par HTTPS au démarrage, tous les 7 jours si les rapports système sont activés. Si le service est indisponible, l’application démarre normalement. Tu peux modifier cette option dans Paramètres → Programme.";
                ClosingHint.Text = "Fermer cette fenêtre ou appuyer sur Échap laisse les statistiques activées et enregistre ce choix.";
                KeepEnabledButton.Content = "Laisser activées";
                DisableButton.Content = "Désactiver";
                break;
            case "es":
                Title = "Informe del sistema";
                Heading.Text = "¿Enviar el informe del sistema?";
                Explanation.Text = "El informe del sistema muestra qué sistemas operativos y versiones (por ejemplo, Windows 10 o Windows 11), arquitecturas, entornos de .NET y versiones de la aplicación se usan.\n\nMe alegra que actives el informe del sistema si quieres participar. Tu informe me ayuda a orientar las pruebas y el desarrollo hacia los sistemas que usáis a diario. ¡Gracias por tu apoyo!\n\nLa aplicación no mide el tiempo de uso. Los gráficos públicos muestran cifras agregadas.\n\nEl informe incluye un identificador derivado localmente del identificador de instalación de Windows, las versiones de la aplicación y de la compilación, el sistema operativo y la plataforma, las arquitecturas del proceso y del sistema, WPF/Avalonia, el modo de distribución de .NET, su versión y su identificador de ejecución.\n\nEl servicio guarda el último informe y registros diarios para ver la evolución. El identificador persistente puede vincularse internamente a tus solicitudes de soporte, por lo que los datos son seudónimos. El identificador original de Windows, tu nombre visible, los nombres de archivo, las rutas personales, el contenido de los archivos y los registros de sesión quedan fuera del informe.\n\nEl envío se realiza por HTTPS al iniciar la aplicación cada 7 días si los informes del sistema están activados. Si el servicio no está disponible, la aplicación se inicia con normalidad. Puedes modificar esta opción en Ajustes → Programa.";
                ClosingHint.Text = "Cerrar esta ventana o pulsar Esc mantiene las estadísticas activadas y guarda esta elección.";
                KeepEnabledButton.Content = "Mantener activadas";
                DisableButton.Content = "Desactivar";
                break;
        }
        ClosingHint.Text = _language switch
        {
            "de" => "Bitte wähle Aktivieren oder Deaktivieren. Die App wartet auf deine Antwort und speichert die Auswahl. Du kannst sie später in den Einstellungen ändern.",
            "fr" => "Choisis Activer ou Désactiver. L’application attend ta réponse et mémorise ce choix. Tu peux le modifier dans les paramètres.",
            "es" => "Elige Activar o Desactivar. La aplicación espera tu respuesta y guarda la elección. Puedes cambiarla en Ajustes.",
            _ => "Choose Enable or Disable. The app waits for your answer and saves your choice. You can change it later in Settings."
        };
        KeepEnabledButton.Content = _language switch
        {
            "de" => "Aktivieren", "fr" => "Activer", "es" => "Activar", _ => "Enable"
        };
        KeepEnabledButton.IsDefault = false;
        DisableButton.IsDefault = false;
        Opened += (_, _) =>
        {
            var screen = Screens.ScreenFromWindow(this);
            if (screen != null) MaxHeight = Math.Max(200, screen.WorkingArea.Height / screen.Scaling - 48);
        };
        KeyDown += (_, e) => { if (e.Key == Key.Escape) e.Handled = true; };
        Closing += (_, e) => e.Cancel = !_answered;
    }

    private void KeepEnabled_Click(object sender, RoutedEventArgs e)
    {
        _answered = true;
        StatisticsEnabled = true;
        Close(true);
    }

    private void Disable_Click(object sender, RoutedEventArgs e)
    {
        _answered = true;
        StatisticsEnabled = false;
        Close(false);
    }
}
