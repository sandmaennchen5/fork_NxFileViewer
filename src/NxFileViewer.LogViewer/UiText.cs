using System.Globalization;
namespace NxFileViewer.LogViewer;
public static class UiText
{
    private static bool German => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "de";
    public static string Open => German ? "Logdatei öffnen …" : "Open log file …";
    public static string Reload => German ? "Neu laden" : "Reload";
    public static string Search => German ? "Suchen" : "Search";
    public static string Minimum => German ? "Ab Logstufe" : "Minimum level";
    public static string All => German ? "Alle" : "All";
    public static string Memory => German ? "Nur RAM / GC" : "RAM / GC only";
    public static string General => German ? "Sitzung / allgemein" : "Session / general";
    public static string Empty => German ? "Logdatei öffnen oder hierher ziehen. Strg+O: Öffnen · Strg+F: Suchen · F5: Neu laden" : "Open or drop a log file here. Ctrl+O: Open · Ctrl+F: Search · F5: Reload";
    public static string Loading => German ? "Logdatei wird gelesen …" : "Reading log file …";
    public static string Filtering => German ? "Filter wird angewendet …" : "Applying filter …";
    public static string Error => German ? "Logdatei konnte nicht gelesen werden" : "Could not read log file";
    public static string Copy => German ? "Eintrag kopieren" : "Copy entry";
    public static string Details => German ? "Ausgewählter Eintrag (vollständiger Text)" : "Selected entry (full text)";
    public static string Count(int shown, int total) => German ? $"{shown:N0} von {total:N0} Einträgen" : $"{shown:N0} of {total:N0} entries";
}
