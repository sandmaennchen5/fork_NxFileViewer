using System;
using Emignatik.NxFileViewer.Utils.MVVM.Localization;
using LibHac.Ncm;

namespace Emignatik.NxFileViewer.Localization.Keys;

public class LocalizationKeys_DE : LocalizationKeysBase, ILocalizationKeys
{
    public string Info_Credits => "Mitwirkende und Credits";
    public string Info_Licenses => "Lizenztexte";

    public string PluginLog_Title => "Protokollstufen der Plugins";
    public string PluginLog_Inherit => "Allgemeine Einstellung übernehmen";
    public string PluginLog_Off => "Aus";
    public string PluginLog_Tip => "Die Pluginstufe ersetzt die allgemeine Mindeststufe für Meldungen dieses Plugins. Gilt für die Logansicht und die gespeicherte Protokolldatei.";

    public string Update_RollbackConfirm => "Die vorherige Programmversion wiederherstellen? NxFileViewer Fork wird geschlossen und neu gestartet. Nach dem Entfernen alter Versionen ist diese Wiederherstellung nicht mehr verfügbar.";
    public string Update_Cleanup => "Alte Versionen entfernen";
    public string Update_CleanupConfirm => "Folgende alte Versionen bzw. Update-Sicherungen endgültig entfernen?\n\n{0}\n\nDie betreffenden Rückfallversionen stehen danach nicht mehr zur Verfügung. Die aktuelle Version und konfigurierte eigene Programmdateien bleiben erhalten.";
    public string Update_CleanupEmpty => "Keine alten, zur Bereinigung freigegebenen Versionen gefunden. Laufende, kürzlich abgeschlossene oder fehlgeschlagene Programmupdates bleiben erhalten.";
    public string Update_CleanupDone => "{0} alte Version(en) bzw. Update-Sicherung(en) entfernt.";
    public string BatchDetails_Show => "Detailbereich einblenden";
    public string BatchDetails_Hide => "Detailbereich ausblenden";
    public string BatchPackages_SummaryOrder => "Ursprüngliche Paketreihenfolge in Zusammenfassung verwenden";
    public string BatchPackages_SummaryOrderTip => "Deaktiviert (Standard): Die Werte jeder zusammengefassten Spalte werden unabhängig von der Paketreihenfolge sortiert.\n\n- Pakettyp: Base / Update / DLC.\n- Version, Display-Version und minimale Systemversion: höchste zuerst, z. B. 22.0.0 / 17.0.0 / 2.0.0. Versionsbestandteile werden numerisch verglichen.\n- Masterkey: höchste Schlüsselrevision zuerst, z. B. MasterKey 10 / MasterKey 9 / MasterKey 2.\n- Übrige Texte und IDs: alphabetisch aufsteigend (A–Z), ohne Beachtung der Groß-/Kleinschreibung.\n\nJede Spalte wird für sich sortiert. Werte an derselben Position in unterschiedlichen Spalten gehören daher nicht zwingend zum selben Paket.\n\nAktiviert: Die zusammengefassten Werte bleiben wie bisher in der ursprünglichen Reihenfolge der Pakete in der Datei.\n\nGilt auch für zusammengefasste CSV-/Excel-Exporte. Die Reihenfolge und Nummerierung einzelner Paketzeilen (::1, ::2, …) bleibt unverändert.";
    public string BatchPackages_Title => "Multipakete";
    public string BatchPackages_Display => "Multipakete als einzelne Zeilen anzeigen (::1, ::2, …)";
    public string BatchPackages_Export => "Multipakete als einzelne Zeilen exportieren (::1, ::2, …)";
    public string BatchPackages_Tip => "Jede Zeile zeigt die Metadaten eines Pakets. Dateipfad, Dateigröße, Prüfergebnis und Dateiaktionen gelten für die gesamte Containerdatei. Anzeige und Export sind unabhängig einstellbar.";
    public string BatchExport_Title => "Stapelexport";
    public string BatchExport_VisibleOnly => "Nur sichtbare Spalten in Tabellenreihenfolge exportieren (sonst: alle Spalten in Standardreihenfolge)";
    public string BatchExport_Format => "Standardformat für den Export";
    public string BatchExport_NoColumns => "Keine Spalten sichtbar. Vor dem Export mindestens eine Spalte einblenden.";
    public string BatchExport_InvalidExtension => "Bitte einen Dateinamen mit .csv oder .xlsx wählen.";
    public string Package_PublicationRecovery => "Die Veröffentlichung wurde unterbrochen. Die geprüften Dateien bleiben in den folgenden Ordnern erhalten. Ein Ordner mit NXFV-INCOMPLETE.txt ist noch unvollständig.";
    public string Features_PackageOperations => "Zusammenführen / Aufteilen / NSP ↔ XCI aktivieren (experimentell)";
    public string Features_PackageOperationsDisabled => "Diese experimentelle Funktion ist deaktiviert. Unter Einstellungen → Funktionen aktivieren und das Programm neu starten.";
    public string Package_Title => "Zusammenführen / Inhalte aufteilen / NSP ↔ XCI (experimentell)";
    public string Package_Engine => "Paket-Engine";
    public string Package_RustPath => "cxfcxf / nscb_rust — Programmdatei";
    public string Package_KronosPath => "Kronos — portabler Ordner (runtime und ztools)";
    public string Package_Tip => "Einstellungen vor dem Start übernehmen. Pro Titel eine Version auswählen. Quelldateien bleiben erhalten; Ergebnisse landen in einem neuen Ordner. Download-Schaltflächen installieren und aktivieren die Engine direkt. Für die Kompression kann nscb_rust separat gewählt werden.";
    public string Package_MergeNsp => "Zusammenführen → NSP";
    public string Package_MergeXci => "Zusammenführen → XCI";
    public string Package_ConvertNsp => "Konvertieren → NSP";
    public string Package_ConvertXci => "Konvertieren → XCI";
    public string Package_Split => "Inhalte aufteilen → NSP";
    public string Package_SplitXci => "Inhalte aufteilen → XCI";
    public string Package_Destination => "Ausgabeordner auswählen";
    public string Package_Inputs => "Lokale NSP/XCI-Dateien auswählen";
    public string Package_Done => "Ausgabeordner: ";
    public string Package_Missing => "Ausgewählte Engine ist nicht eingerichtet oder unvollständig.";
    public string Package_Invalid => "Ausgabeprüfung fehlgeschlagen. Kein Ergebnis wurde veröffentlicht.";
    public string Package_Ready => "Bereit";
    public string Package_Apply => "Gespeicherte Einstellung: ";

    public string XciRepair_Title => "XCI-Container reparieren";
    public string XciRepair_Inspect => "XCI auf bekannte Layoutfehler prüfen…";
    public string XciRepair_Prompt => "Bei „{0}“ wurde ein bekannter reparierbarer Containerfehler erkannt: {1}\n\nEine reparierte Kopie erstellen und anschließend öffnen? Das Original bleibt unverändert. Die Kopie wird vor dem Speichern vollständig geprüft.";
    public string XciRepair_HeaderIssue => "Der Gamecard-Header passt nicht zum Inhalt.";
    public string XciRepair_LayoutIssue => "Die Eintragsgrößen nach dem Dekomprimieren sind fehlerhaft. Die passende XCZ im selben Ordner ermöglicht eine Korrektur.";
    public string XciRepair_SaveCopy => "Reparierte XCI-Kopie speichern";
    public string XciRepair_Copying => "XCI für die Reparatur kopieren…";
    public string XciRepair_Verifying => "Reparierte XCI prüfen";
    public string XciRepair_NewFileRequired => "Bitte einen neuen Dateinamen mit der Endung .xci wählen. Vorhandene Dateien werden nicht überschrieben.";
    public string XciRepair_SourceChanged => "Die Datei oder ihre XCZ-Quelle wurde seit der Prüfung verändert. Bitte erneut öffnen.";
    public string XciRepair_VerificationFailed => "Die reparierte Kopie hat die Integritätsprüfung nicht bestanden.";
    public string XciRepair_Failed => "Die Reparatur wurde nicht abgeschlossen. Das Original bleibt unverändert.\n\n{0}";
    public string Features_Nand => "NAND-Funktionen aktivieren";
    public string Features_NandDisabled => "NAND-Funktionen sind deaktiviert. In den Einstellungen aktivieren und das Programm neu starten.";
    public string Features_NszPlugin => "NSZ-Plugin aktivieren";
    public string Features_NandPlugin => "NxNandManager-Plugin aktivieren";
    public string Features_Firmware => "Firmware-Prüfung aktivieren";
    public string Features_FirmwareDisabled => "Die Firmware-Prüfung ist deaktiviert. In den Einstellungen aktivieren und das Programm neu starten.";
    public string Features_Title => "Optionale Funktionen";
    public string Features_Plugins => "Plugins aktivieren (NSZ / NxNandManager)";
    public string Features_Mtp => "MTP aktivieren (experimentell)";
    public string Features_Restart => "Änderungen werden nach einem Neustart des Programms wirksam.";
    public string Features_PluginsDisabled => "Plugins sind deaktiviert. In den Einstellungen aktivieren und das Programm neu starten.";

    public string Nand_Detected => "NAND-Signaturen erkannt. Die Integrität wurde nicht geprüft.";
    public string Nand_OpenSave => "Spielstand öffnen";
    public string Nand_LeaveSave => "Zur Partition";
    public string Nand_ExportSave => "Spielstand exportieren…";
    public string Nand_ExplorerTitle => "Titel";
    public string Nand_ExplorerUserId => "Benutzer-ID";
    public string Nand_NameCandidate => "NAND-Kandidat anhand des Dateinamens. Für Bestätigung und erweiterte Informationen NAND-Informationen abrufen.";
    public string Nand_Installed => "Plugin NxNandManager installiert";
    public string Nand_CustomUpdate => "Eigenen EXE-Pfad leeren und übernehmen, um verwaltete Downloads zu verwenden.";
    public string Nand_Updating => "NxNandManager wird heruntergeladen und geprüft…";
    public string Nand_Open => "NAND-Dump öffnen…";
    public string Nand_Info => "NAND-Informationen";
    public string Nand_Export => "Partition exportieren…";
    public string Nand_SettingsTip => "EXE-Pfad leer lassen für verwaltete Downloads unter Einstellungen → Updates → Plugins → NxNandManager. Eine eigene EXE wird nicht ersetzt.";
    public string Nand_BisKeys => "BIS-Key-Datei (optional; leer = aktive prod.keys von NxFileViewer Fork)";
    public string Nand_Tip => "NAND-Dump öffnen (bei geteilten Dumps die erste Datei). Der Export kopiert die Partition im gespeicherten Zustand, ohne Entschlüsselung. NxNandManager unter Einstellungen → Plugins konfigurieren.";
    public string Nand_NewTarget => "Neue lokale Zieldatei wählen. Vorhandene Dateien können nicht ersetzt werden.";
    public string Nand_SourceMissing => "Die NAND-Quelldatei fehlt oder ist keine lokale Datei.";
    public string Nand_NotInstalled => "NxNandManager ist nicht installiert. Installation unter Einstellungen → Updates → Plugins.";
    public string Nand_OpenDrive => "Laufwerk öffnen…";
    public string Nand_Explorer => "NAND Explorer";
    public string Nand_ExplorerUp => "Übergeordneter Ordner";
    public string Nand_ExportFile => "Datei exportieren…";
    public string Nand_ExplorerFolder => "Ordner";
    public string Nand_DriveTip => @"NAND-Laufwerk auswählen (z. B. \\.\PhysicalDrive2). Es wird nur lesend geöffnet. Für den Zugriff auf physische Laufwerke können Administratorrechte nötig sein.";
    public string Nand_KeysMissing => "Die konfigurierte BIS-Key-Datei existiert nicht.";
    public string Nand_ExportFailed => "NxNandManager hat keine gefüllte Partitionsdatei erzeugt.";
    public string Nand_ExportDone => "Partition exportiert.";
    public string Nand_Cancelled => "Abgebrochen oder Zeitlimit der Informationsabfrage erreicht.";
    public string DataUpdate_Firmware => "Firmware-Hashes";
    public string DataUpdate_Title => "Updates";
    public string DataUpdate_Titles => "TitleDB aktualisieren";
    public string BatchNaming_Unchecked => "Nicht geprüft";
    public string DataUpdate_LocalFirmwareVersion => "Lokale Hashlisten bis Firmware {0}.";
    public string DataUpdate_NoLocalFirmware => "Keine lokalen Firmware-Hashlisten installiert.";
    public string DataUpdate_TitleCatalogDate => "TitleDB {0}: lokal aktualisiert am {1}.";
    public string DataUpdate_TitleCatalogMissing => "TitleDB {0}: nicht lokal gespeichert.";
    public string DataUpdate_OnlineFirmwareVersion => "Online-Hashlisten bis Firmware {0}.";
    public string Keys_ExistingValidation => "Vorhandene Datei:";
    public string Keys_IncomingValidation => "Neue Datei:";
    public string Keys_ReplaceDownloaded => "Heruntergeladene Keys übernehmen und vorhandene Datei ersetzen?";
    public string Keys_SaveTicketKeys => "Fehlende Ticket-Schlüssel in title.keys speichern";
    public string Keys_TicketConflict => "Ticket-Schlüsselkonflikt für {0} in {1}: vorhandener Eintrag bleibt erhalten.";
    public string Keys_TicketSaved => "Ticket-Schlüssel für {0} wurde in {1} gespeichert.";
    public string Tinfoil_StabilityHint => "Tinfoil ist zeitweise nicht erreichbar oder instabil. Bei Fehlern bitte später erneut versuchen oder eine andere Quelle auswählen.";
    public string DataUpdate_TitleTip => "Aktualisiert die GitHub-TitleDB für die gespeicherte Region und den US-Fallback im Programmordner. Der ausgewählte Titelanbieter bleibt unverändert.";
    public string DataUpdate_FirmwareTip => "Firmware-Prüfungen laden Online-Hashes weiterhin frisch ohne dauerhaften Cache. Das Offlinepaket wird nur über die separate Schaltfläche gespeichert; die bisherigen lokalen Hashes werden gesichert.";
    public string DataUpdate_CheckFirmware => "Online-Hashes prüfen";
    public string DataUpdate_SaveFirmware => "Offline-Hashes aktualisieren";
    public string DataUpdate_Working => "Aktualisierung läuft…";
    public string DataUpdate_TitlesDone => "TitleDB {0} aktualisiert: {1} Katalogeinträge inklusive US-Fallback.";
    public string DataUpdate_FirmwareSaved => "Offline-Hashes aktualisiert: {0} Referenzdateien.";
    public string DataUpdate_FirmwareChecked => "Online-Hashes geprüft: {0} Referenzdateien; nichts gespeichert.";
    public string Update_Title => "Programm-Updates";
    public string Update_IncludePrereleases => "Vorabversionen (Pre-Releases) bei Programmupdates berücksichtigen";
    public string Update_Prerelease => "Vorabversion";
    public string Update_Auto => "Beim Start nach Programm-Updates suchen";
    public string Update_Check => "Nach Updates suchen";
    public string Update_Install => "Herunterladen und installieren";
    public string Update_RateLimited => "GitHub-Abfragelimit erreicht. Erneut möglich ab {0:g}.";
    public string Update_CachedAt => "Gespeichertes Ergebnis vom {0:g}.";
    public string Update_Checking => "Suche nach Updates…";
    public string Update_Current => "Keine neuere veröffentlichte Version verfügbar.";
    public string Component_UpdateAvailable => "Update verfügbar.";
    public string Component_NotInstalled => "Nicht installiert.";
    public string Component_CustomVersion => "Benutzerdefinierte Version: automatische Prüfung nicht möglich.";
    public string Update_Available => "Version {0} ist verfügbar.";
    public string Update_Failed => "Update fehlgeschlagen.";
    public string Update_Confirm => "Version {0} herunterladen und installieren? NxFileViewer Fork wird anschließend neu gestartet. Keys, Einstellungen und Plugins bleiben erhalten.";
    public string Update_Downloading => "Update wird heruntergeladen und geprüft…";
    public string Update_Installing => "Update wird installiert…";
    public string Update_Cancelled => "Update abgebrochen.";
    public string Nsz_Engine => "Engine für Kompression / Dekompression";
    public string Nsz_Mode => "Kompressionsmodus";
    public string Nsz_ModeAuto => "Automatisch (NSZ: Solid, XCZ: Blöcke)";
    public string Nsz_ModeSolid => "Solid / Blockless";
    public string Nsz_ModeBlock => "Block-Komprimierung";
    public string Nsz_BlockSize => "Blockgröße";
    public string Nsz_ModeTip => "Solid komprimiert etwas stärker. Blöcke ermöglichen schnelle Rücksprünge und zufällige Lesezugriffe. Modus und Blockgröße gelten nur beim Komprimieren.";
    public string Workspace_Plugins => "Plugins";
    public string Workspace_Home => "Start";
    public string Workspace_File => "Dateiprüfung";
    public string Workspace_Menu => "Hauptmenü";
    public string Nsz_Replace => "Ersetzen";
    public string Nsz_Number => "Mit Nummerierung speichern";
    public string TitlePage_Custom => "Eigene";
    public string Info_WithRuntime => "Mit integriertem .NET";
    public string Info_WithoutRuntime => "Ohne integriertes .NET — benötigt .NET 10 Desktop Runtime";
    public string Info_Description => "NxFileViewer Fork prüft und zeigt Nintendo-Switch-Dateien an. Unterstützt NSP, NSZ, XCI, XCZ, NCA sowie ZIP- und 7z-Archive, Firmware-Prüfung und NSZ-Konvertierung.";
    public string Info_Shortcuts => "Tastenkürzel";
    public string BatchHistory_Show => "Anzeigen";
    public string BatchHistory_LoadedNotice => "Aus Verlauf geladen.\nDas Logo wurde nicht gespeichert.";
    public string BatchHistory_Title => "Letzte Stapelprüfungen";
    public string BatchHistory_SaveLogos => "Logos mit im Verlauf speichern";
    public string BatchHistory_Enable => "Stapelprüfungen im Verlauf speichern";
    public string BatchHistory_Limit => "Anzahl gespeicherter Stapelprüfungen (1–100)";
    public string BatchHistory_Resume => "Fortsetzen";
    public string Dialog_Yes => "Ja";
    public string Dialog_No => "Nein";
    public string Nsz_Cancel => "Abbrechen";
    public string Nsz_DeleteSourcePrompt => "Quelldateien nach erfolgreicher Konvertierung und Prüfung löschen? Bei Fehlern bleiben sie erhalten.";
    public string Nsz_SourceDeleted => "Quelle gelöscht";
    public string Nsz_SourceDeleteFailed => "Quelle konnte nicht gelöscht werden";
    public string Nsz_Compress => "Komprimieren und prüfen…";
    public string Nsz_Decompress => "Dekomprimieren und prüfen…";
    public string Nsz_CompressValid => "Fehlerfreie komprimieren…";
    public string Nsz_DecompressValid => "Fehlerfreie dekomprimieren…";
    public string Nsz_Update => "Plugin installieren / aktualisieren…";
    public string Nsz_Rollback => "Vorherige Version verwenden";
    public string Nsz_SelectDestination => "Zielordner auswählen";
    public string Nsz_PythonRuntimeFailed => "Die externe NSZ-EXE konnte ihre eingebettete Python-DLL nicht laden. Das Programm startet nicht; Keys und Eingabedateien wurden noch nicht geprüft. Wähle unter Einstellungen → Plugin nicoboss/nsz eine andere, auf diesem Rechner lauffähige NSZ-CLI.";
    public string Nsz_NotInstalled => "Plugin nicoboss/nsz ist nicht installiert.";
    public string Nsz_Updating => "Plugin nicoboss/nsz wird aktualisiert…";
    public string Nsz_SourceSize => "Originalgröße (Bytes)";
    public string Nsz_OutputSize => "Zielgröße (Bytes)";
    public string Nsz_Verified => "Ergebnis geprüft";
    public string Nsz_Summary => "{0} umgewandelt und geprüft; {1} fehlgeschlagen; {2} nicht verarbeitet. Originale bleiben erhalten.";
    public string Nsz_SettingsTip => "Optionaler Pfad zur NSZ-CLI. Leer lassen, um offizielle Releases automatisch zu verwalten. Quelle und Ergebnis werden geprüft; Originale bleiben erhalten.";
    public string Nsz_CheckUpdates => "Vor Umwandlung auf stabile Updates prüfen";
    public string Nsz_Level => "Kompressionsstufe (1–22)";
    public string Nsz_OutputExists => "Zieldatei existiert bereits:";
    public string Nsz_SourceInvalid => "Integritätsprüfung der Quelle fehlgeschlagen:";
    public string Nsz_OutputInvalid => "Integritätsprüfung des Ergebnisses fehlgeschlagen:";
    public string Nsz_OutputMissing => "NSZ hat die erwartete Zieldatei nicht erstellt.";
    public string Nsz_KeysMissing => "Keine prod.keys-Datei geladen.";
    public string Nsz_Incompatible => "Diese NSZ-CLI unterstützt die benötigten Optionen nicht.";
    public string Nsz_OfflineFallback => "Update nicht verfügbar; installierte NSZ-Version wird verwendet.";
    public string Firmware_NoReferences => "Firmware-Hashlisten nicht verfügbar: GitHub nicht erreichbar und lokale Referenzen fehlen oder sind ungültig.";
    public string Firmware_LoadingOnline => "Firmware-Hashlisten von GitHub laden…";
    public string Firmware_OnlineSource => "Hashquelle: GitHub (für diesen Prüflauf geladen).";
    public string Firmware_OfflineSource => "Hinweis: GitHub nicht verfügbar. Prüfung mit mitgelieferten Hashlisten; neuere Firmware kann fehlen.";
    public string Firmware_BrowseZip => "ZIP / 7z auswählen…";
    public string Firmware_NcaMatches => "Diese NCA ist in folgenden Firmware-Versionen enthalten:";
    public string Firmware_Versions => "Firmware-Version(en)";
    public string Firmware_Title => "Firmware-Prüfung";
    public string Firmware_Unknown => "Keine passende Firmware-Hashliste gefunden.";
    public string Firmware_Summary => "{0}/{1} Dateien gültig; fehlend: {2}, verändert: {3}, zusätzlich: {4}, doppelt: {5}.";
    public string Firmware_Missing => "Fehlend";
    public string Firmware_Changed => "Verändert (Größe/SHA-256)";
    public string Firmware_Renamed => "Falsch benannt (Inhalt stimmt überein)";
    public string Firmware_RenamedSummary => "Falsch benannt: {0}.";
    public string Firmware_Extra => "Zusätzliche NCA";
    public string Firmware_Duplicate => "Doppelter Dateiname";

    public override bool IsFallback => true;
    public override string DisplayName => "Deutsch";
    public override string CultureName => "de-DE";
    public override string LanguageAuto => "Auto";

    public string FileNotSupported_Log => "«{0}» Datei wird nicht unterstützt.";
    public string OpenSdCard => "SD-Karte öffnen";
    public string OpenFile_Filter => "Nintendo Switch Dateien (*.nsp;*.nsz;*.xci;*.xcz;*.nca;*.nro;*.zip;*.7z;*.bin;*.img;*.00)|*.nsp;*.nsz;*.xci;*.xcz;*.nca;*.nro;*.zip;*.7z;*.bin;*.img;*.00|Switch-Spielpakete (*.nsp;*.nsz;*.xci;*.xcz)|*.nsp;*.nsz;*.xci;*.xcz|Archive (*.zip;*.7z)|*.zip;*.7z|Switch-Inhaltsdateien (*.nca)|*.nca|NAND-Abbilder und geteilte Dumps (*.bin;*.img;*.00)|*.bin;*.img;*.00|Alle Dateien (*.*)|*.*";
    public string MenuItem_File => "Datei";
    public string MenuItem_Open => "Öffnen...";
    public string MenuItem_OpenLast => "Letzte öffnen";
    public string MenuItem_Close => "Schließen";
    public string MenuItem_Exit => "Beenden";
    public string MenuItem_Tools => "Werkzeuge";
    public string MenuItem_CheckIntegrity => "Integrität prüfen";
    public string MenuItem_CheckDirectoryIntegrity => "Ordnerintegrität prüfen…";
    public string BatchNaming_Title => "Benennung";
    public string BatchNaming_Matches => "Passt";
    public string BatchNaming_Differs => "Passt nicht";
    public string BatchNaming_Check => "Benennung prüfen";
    public string BatchNaming_RenameAll => "Alle abweichenden umbenennen";
    public string BatchNaming_Error => "Benennungsfehler";
    public string BatchIntegrity_Title => "Stapelprüfung";
    public string BatchIntegrity_SelectDirectory => "Ordner mit Switch-Dateien auswählen";
    public string BatchIntegrity_Browse => "Durchsuchen…";
    public string BatchIntegrity_IncludeSubdirectories => "Unterordner einbeziehen";
    public string BatchTable_Games => "Spiele";
    public string BatchTable_Type => "Typ";
    public string BatchTable_ConfirmResetFiltersAndSort => "Alle Such- und Filtereinstellungen sowie die Sortierung der Stapeltabelle zurücksetzen?";
    public string BatchTable_ConfirmResetColumns => "Sichtbarkeit, Reihenfolge und Breite aller Spalten auf die Standardwerte zurücksetzen? Das gespeicherte Spaltenlayout wird ersetzt.";
    public string BatchTable_OpenExplorer => "Im Explorer öffnen";
    public string BatchTable_ResetColumns => "Spalten zurücksetzen";
    public string BatchTable_ConversionStatus => "Umwandlungsstatus";
    public string BatchTable_OutputPath => "Ausgabepfad";
    public string BatchTable_Columns => "Spalten…";
    public string BatchTable_Search => "Suchen";
    public string BatchTable_All => "Alle";
    public string BatchTable_ResetFiltersAndSort => "Filter und Sortierung zurücksetzen";
    public string BatchTable_ResetFilters => "Filter zurücksetzen";
    public string BatchTable_ResetSort => "Sortierung zurücksetzen";
    public string BatchIntegrity_File => "Datei";
    public string BatchIntegrity_Path => "Pfad";
    public string BatchIntegrity_Error => "Fehler";
    public string BatchIntegrity_NszDataCorrupted => "Der komprimierte NSZ/NCZ-Datenstrom ist beschädigt oder unvollständig (Zstandard-Dekomprimierung fehlgeschlagen).";
    public string BatchIntegrity_IntegrityFailed => "Die Integritätsprüfung konnte nicht vollständig ausgeführt werden. Details stehen im Protokoll.";
    public string PackageStructure_Filesystem => "Dateisystem";
    public string Signature_Title => "NCA-Signatur";
    public string Signature_Passed => "Bestanden";
    public string Signature_NotPassed => "Nicht bestanden";
    public string Signature_Unchecked => "Nicht geprüft";
    public string ToolTip_PackageStructure => "Struktur nach Nx Game Info:\nScene (XCI): Update-, Normal- und Secure-Partition vorhanden.\nKonvertiert (XCI): nur Secure-Partition; typisch für NSP → XCI.\nScene (NSP): legalinfo.xml, nacp.xml, programinfo.xml und cardspec.xml; typisch für BBB-Releases.\nHomebrew (NSP): authoringtoolinfo.xml vorhanden.\nCDN (NSP): Zertifikat (.cert) und Ticket (.tik); typisch für eShop-CDN-Dumps.\nKonvertiert (NSP): NCA/NCZ-Inhalte und weitere Dateien, ohne Ticket/Zertifikat und ohne Scene-/Homebrew-Merkmale. Dies ist eine Struktureinstufung und kein Nachweis der Herkunft.\nDateisystem: installierte NAX0-Titel auf der Switch-SD-Karte.\nNur NCA-Inhalte: Das Paket enthält ausschließlich .nca/.ncz-Dateien, ohne Ticket, Zertifikat oder zusätzliche XML-Dateien. Das kann bei neu gepackten oder konvertierten Paketen vorkommen und beweist nicht, dass Spieldaten fehlen.\nUnvollständig: keine NCA/NCZ-Inhalte, Ticket ohne Zertifikat (oder umgekehrt) oder ein XCI-Partitionsaufbau außerhalb der Scene-/Konvertiert-Regeln. Dies beschreibt die Containerstruktur. Ob Spieldaten fehlen oder verändert sind, zeigt die Integritätsprüfung.\nNSZ/XCZ folgen den entsprechenden Paketregeln; NCZ zählt als NCA.";
    public string ToolTip_NcaSignature => "Bestanden: gültige NCA-Signaturen, wie bei offiziellen Titeln erwartet.\nNicht bestanden: mindestens eine ungültige NCA-Signatur; bei Homebrew möglich, bei offiziellen Titeln auffällig.\nNicht geprüft: die NCA-Signaturprüfung wurde noch nicht vollständig durchgeführt. Über die Integritätsprüfung starten.\nDiese Anzeige betrifft NCA-Header-Signaturen; ACID ist eine separate NPDM-Signatur.";
    public string ToolTip_Permission => "Sicher: kein Dateisystem-Servicezugriff oder Bit 0x8000000000000000 nicht gesetzt.\nUnsicher: Dateisystem-Servicezugriff und Bit 0x8000000000000000 gesetzt (EraseMmc).\nGefährlich: Dateisystem-Servicezugriff und Maske 0xffffffffffffffff (alle Berechtigungen).\nUnsicher/Gefährlich sollte nur bei Homebrew, nicht bei offiziellen Spielen vorkommen. Nur für Basistitel und Updates verfügbar. Diese Einstufung bewertet Berechtigungen und ist keine vollständige Sicherheitsprüfung.";
    public string ToolTip_AcidSignature => "Die ACID-Signatur ist ein digitales Siegel für den Berechtigungsbereich in main.npdm. Dort stehen Vorgaben dazu, auf welche Systemdienste und Dateisystemfunktionen das Programm zugreifen darf.\n\nBedeutung der Anzeige:\n- Valid: Die Signatur passt zu den geprüften ACID-Daten und dem verwendeten Prüfschlüssel.\n- Invalid: Die Signaturprüfung ist fehlgeschlagen. Die Daten können verändert worden sein oder nicht zur Signatur passen. Das allein beweist keine Schadsoftware.\n- Unchecked: Die Signatur wurde nicht geprüft. Daraus lässt sich keine Aussage über ihre Gültigkeit ableiten.\n- Leeres Feld: Für diese Anzeige sind keine ACID-Informationen verfügbar.\n\nUnterschied zu den anderen Prüfungen:\n- ACID-Signatur: prüft den signierten Berechtigungsbereich des Programms.\n- NCA-Signatur: prüft den NCA-Header.\n- Integrity: prüft die Dateiinhalte anhand ihrer Prüfsummen.\n\nEine gültige ACID-Signatur bedeutet nicht automatisch, dass sämtliche Dateiinhalte unverändert sind oder das Programm ungefährlich ist.";
    public string PackageStructure_Title => "Paketstruktur";
    public string PackageStructure_Scene => "Scene-Release";
    public string PackageStructure_Cdn => "CDN-Rip";
    public string PackageStructure_Converted => "Konvertiert";
    public string PackageStructure_Homebrew => "Homebrew";
    public string PackageStructure_NcaOnly => "Nur NCA-Inhalte";
    public string PackageStructure_Incomplete => "Unvollständig";
    public string PackageStructure_Unknown => "Unbekannt";
    public string FileInfo_FileSize => "Dateigröße";
    public string FileInfo_CompressionRatio => "Kompressionsverhältnis";
    public string FileInfo_Uncompressed => "unkomprimiert";
    public string FileInfo_SystemUpdate => "Enthaltenes System-Update (XCI)";
    public string BatchIntegrity_Export => "Exportieren…";
    public string BatchIntegrity_Start => "Prüfung starten";
    public string BatchIntegrity_OpenSelected => "In Dateiprüfung öffnen";
    public string BatchIntegrity_MoveSelected => "Verschieben…";
    public string BatchIntegrity_MoveValid => "Fehlerfreie verschieben…";
    public string BatchIntegrity_SelectMoveDestination => "Zielordner für fehlerfreie Dateien auswählen";
    public string BatchIntegrity_Moving => "Verschiebe";
    public string MenuItem_Options => "Optionen";
    public string MenuItem_Settings => "Einstellungen";
    public string MenuItem_ReloadKeys => "Keys Neuladen";
    public string MenuItem_OpenTitleWebPage => "Titel Webseite öffnen...";
    public string MenuItem_ShowRenameToolWindow => "Umbenennen...";

    public string Packages_Title => "Multi-Paket Datei";
    public string DisplayVersion => "Display Version";
    public string Presentation_Title => "Präsentation";
    public string ToolTip_AvailableLanguages => "Titel, Publisher und Icon können sich je nach ausgewählter Sprache ändern.";
    public string AvailableLanguages => "Sprachen";
    public string AppTitle => "Titel";
    public string Publisher => "Publisher";
    public string Security_Title => "Programmsicherheit";
    public string Security_Level => "Bewertung";
    public string Security_FileSystemPermissions => "Dateisystemrechte";
    public string Security_AcidSignature => "ACID-Signatur";
    public string Security_Safe => "Sicher";
    public string Security_Unsafe => "Unsicher";
    public string Security_Dangerous => "Gefährlich";
    public string Security_Unavailable => "Nicht verfügbar";
    public string Security_Details => "Erlaubte Dienste ({0}): {1}";

    public string Lng_AmericanEnglish => "Englisch (US)";
    public string Lng_BritishEnglish => "Englisch (UK)";
    public string Lng_CanadianFrench => "Französisch (Kanada)";
    public string Lng_Dutch => "Niederländisch";
    public string Lng_French => "Französisch";
    public string Lng_German => "Deutsch";
    public string Lng_Italian => "Italienisch";
    public string Lng_Japanese => "Japanisch";
    public string Lng_Korean => "Koreanisch";
    public string Lng_LatinAmericanSpanish => "Spanisch (Lateinamerika)";
    public string Lng_Portuguese => "Portugiesisch";
    public string Lng_Russian => "Russisch";
    public string Lng_SimplifiedChinese => "Chinesisch (vereinfacht)";
    public string Lng_Spanish => "Spanisch";
    public string Lng_TraditionalChinese => "Chinesisch (traditionell)";
    public string Lng_BrazilianPortuguese => "Portugiesisch (Brasilien)";
    public string Lng_Unknown => "Unbekannt";

    public string SettingsView_Title => "Einstellungen";
    public string SettingsView_Button_Apply => "Übernehmen";
    public string SettingsView_Button_Cancel => "Abbrechen";
    public string SettingsView_Button_Reset => "Zurücksetzen";
    public string SettingsView_GroupBoxKeys => "Keys";
    public string SettingsView_Title_KeysEffectiveFilePath => "Effektiver Pfad";
    public string SettingsView_Title_KeysCustomFilePath => "Benutzerdefinierter Pfad";
    public string SettingsView_Title_KeysDownloadUrl => "Download URL";
    public string KeysValidation_MissingFile => "Keine Datei gefunden.";
    public string KeysValidation_ValidEntries => "Gültig ({0} Einträge).";
    public string KeysValidation_MissingMasterKeys => "Fehlende Master-Keys: {0}.";
    public string CnmtOverview_BaseTitleId => "Basis-Titel-ID";
    public string CnmtOverview_MasterKey => "Benötigter Master-Key";
    public string CnmtOverview_MinimumApplicationVersion => "Minimale Anwendungsversion (DLC)";
    public string CnmtOverview_Distribution => "Distribution";
    public string KeysValidation_InvalidMasterKeys => "Ungültige Master-Keys: {0}.";
    public string KeysValidation_InvalidLines => "Fehlerhafte Zeilen: {0}.";
    public string KeysValidation_EmptyFile => "Die Datei enthält keine gültigen Einträge.";
    public string KeysValidation_FirmwareEstimate => "Höchste gültige Revision: {0} — unterstützt Inhalte bis Firmware {1}.";
    public string KeysValidation_UnsupportedMasterKeys => "Neue Master-Key-Revision erkannt: {0}. Diese Programmversion kann sie noch nicht prüfen oder einer Firmware zuordnen; ein Programm-Update ist erforderlich.";
    public string SettingsView_ToolTip_Keys => """
                                               Keys sind erforderlich, um verschlüsselte Nintendo-Switch-Dateien (XCI, NSP, ...) zu öffnen.
                                               Jede offizielle Nintendo-Switch-Datei ist mit Keys verschlüsselt, die spezifisch für die Switch-Firmware-Version sind, für die sie erstellt wurde.

                                               Um jede Nintendo-Switch-Datei ohne Fehler zu öffnen, stelle sicher, dass du stets eine aktuelle "prod.keys"-Datei mit den Keys aller bestehenden Firmware-Versionen hast.

                                               Die Datei sollte einen Key pro Zeile enthalten, in der Form «KEY_NAME = HEXADECIMAL_WERT».
                                               """ + "\n\n" + Keys_CopyToSwitchHelp;
    public string SettingsView_ToolTip_ProdKeys => """
                                                   Diese Datei enthält allgemeine Keys, die von allen Switch-Geräten verwendet werden. Sie wird benötigt, um verschlüsselte Titelinhalte zu öffnen.
                                                   Das Programm sucht diese Datei in folgender Reihenfolge:
                                                       1. Im Pfad, der durch diese Einstellung definiert ist
                                                       2. Im Verzeichnis des aktuellen Programms
                                                       3. Im Verzeichnis «%UserProfile%\\.switch»

                                                   Keydateien werden nur über die Download-Schaltflächen im Reiter Keys heruntergeladen.
                                                   Beim Programmstart werden keine Keys automatisch heruntergeladen.
                                                   """;

    public string SettingsView_ToolTip_TitleKeys => """
                                                    Du kannst optional eine Datei angeben, die spielbezogene Keys enthält.
                                                    Das Programm sucht diese Datei in folgender Reihenfolge:
                                                        1. Im Pfad, der durch diese Einstellung definiert ist
                                                        2. Im Verzeichnis des aktuellen Programms
                                                        3. Im Verzeichnis «%UserProfile%\\.switch»

                                                    Keydateien werden nur über die Download-Schaltflächen im Reiter Keys heruntergeladen.
                                                    Beim Programmstart werden keine Keys automatisch heruntergeladen.
                                                    """;

    public string SettingsView_LogFileRetention => "Logdateien behalten (1–100 Starts)";
    public string SettingsView_LogLevel => "Protokollierungsgrad ";
    public string SettingsView_ToolTip_LogLevel => "Der Protokollierungsgrad gibt die minimale Ebene an, die protokolliert werden soll.";
    public string SettingsView_CheckBox_AlwaysReloadKeysBeforeOpen => "Keys immer neu laden bevor eine Datei geöffnet wird.";
    public string SettingsView_CheckBox_InjectTicketKeys => "Keys aus Ticket-Dateien (*.tik) injizieren";
    public string SettingsView_Title_Language => "Sprache";
    public string SettingsView_Title_Theme => "Design";
    public string SettingsView_Title_NczOptions => "NSZ/XCZ Einstellungen";

    public string SettingsView_ToolTip_NczBlockLessCompression => """
                                                                  NSZ- oder XCZ-Dateien bestehen aus NCZ-Dateien, die NCA-komprimierte Dateien sind.
                                                                  NCZ-Dateien können ohne die Blockkomprimierungsmethode komprimiert werden, was effizienten zufälligen Lesezugriff unmöglich macht.
                                                                  Daher ist es bei großen Dateien, bei denen ein kleiner Teil am Ende gelesen werden muss, notwendig, den gesamten Stream zu dekomprimieren, um den gewünschten Teil zu erreichen.
                                                                  Große Dateien können daher lange zum Öffnen benötigen.
                                                                  Verwende vorzugsweise die Blockkomprimierung für große Dateien.
                                                                  Beachte, dass es, wenn du das Öffnen von blocklos komprimierten NCZ-Dateien nicht zulässt, keine Auswirkungen auf die Integritätsprüffunktionen hat.
                                                                  """;

    public string SettingsView_CheckBox_NczOpenBlocklessCompression => "Öffne NCZ-komprimierte Dateien ohne Blockkompression.";
    public string SettingsView_Title_Integrity => "Integrität";
    public string SettingsView_CheckBox_IgnoreMissingDeltaFragments => "Fehlende Delta-Fragmente ignorieren";
    public string SettingsView_ToolTip_IgnoreMissingDeltaFragments => $"""
                                                                      Patch-Dateien können vollständige Update-Dateien und inkrementelle Update-Dateien (bekannt als {ContentType.DeltaFragment}) enthalten.
                                                                      Diese Fragmente sind nicht zwingend erforderlich, um eine Anwendung zu aktualisieren, und werden manchmal absichtlich entfernt.
                                                                      Aktiviere diese Option, wenn du fehlende {ContentType.DeltaFragment} bei der Integritätsprüfung ignorieren möchtest.
                                                                      """;

    public string SettingsView_Miscellaneous => "Sonstiges";
    public string SettingsView_ToolTip_OpenKeysLocation => "Keydateipfad öffnen";
    public string SettingsView_ToolTip_BrowseKeys => "Suchen...";
    public string SettingsView_ToolTip_DownloadKeys => "Von bestimmter URL herunterladen.";

    public string BrowseKeysFile_ProdTitle => "Wähle \"prod\" Keydatei";
    public string BrowseKeysFile_TitleTitle => "Wähle \"title\" Keydatei";
    public string BrowseKeysFile_Filter => "Keydateien (*.keys)|*.keys|Switch-Spielpakete (*.nsp;*.nsz;*.xci;*.xcz)|*.nsp;*.nsz;*.xci;*.xcz|Archive (*.zip;*.7z)|*.zip;*.7z|Switch-Inhaltsdateien (*.nca)|*.nca|NAND-Abbilder und geteilte Dumps (*.bin;*.img;*.00)|*.bin;*.img;*.00|Alle Dateien (*.*)|*.*";

    public string SuspiciousFileExtension => "Dateierweiterung «{0}» scheint ungültig zu sein, «{1}» oder «{2}» wurde erwartet.";
    public string DragMeAFile => "Ziehe eine unterstützte Datei hierher :)";
    public string MultipleFilesDragAndDropNotSupported => "Mehrere Dateien per Drag & Drop werden nicht unterstützt, nur die erste Datei wird geöffnet.";

    public string CnmtOverview_Title => "Paketinformationen";
    public string CnmtOverview_TitleId => "TitelID";
    public string CnmtOverview_ContentType => "Typ";
    public string CnmtOverview_TitleVersion => "Version";
    public string CnmtOverview_MinimumSystemVersion => "Minimale Systemversion";
    public string CnmtOverview_BuildID => "BuildID";
    public string CnmtOverview_BuildID_NotAvailableBecauseSectionIsSparse => "Nicht verfügbar (Inhalt spärlich)";
    public string CnmtOverview_IsDemo => "Demo";

    public string ContextMenu_SaveImage => "Speichern...";
    public string CopyTitleImageError => "Fehler beim Kopieren der Spieldatei: {0}";
    public string SaveTitleImageError => "Konnte die Spieldatei nicht speichern: {0}";

    public string SaveDialog_Title => "Speichern unter";
    public string SaveDialog_ImageFilter => "Spieldatei";
    public string SaveDialog_AnyFileFilter => "Datei";
    public string SaveFile_Error => "Fehler beim speichern der Datei: {0}";

    public string ContextMenu_CopyImage => "Kopieren";

    public string TabOverview => "Übersicht";
    public string TabContent => "Inhalt";
    public string GroupBoxStructure => "Struktur";
    public string GroupBoxProperties => "Eigenschaften";

    public string ContextMenu_ShowItemErrors => "Zeige Fehler...";
    public string ContextMenu_SaveSectionItem => "Sektion speichern...";
    public string ContextMenu_SaveDirectoryItem => "Verzeichnis speichern...";
    public string ContextMenu_SaveFileItem => "Datei speichern...";
    public string ContextMenu_SavePartitionFileItem => "Partitionsdatei speichern...";
    public string ContextMenu_SaveNcaFileRaw => "NCA RAW speichern...";
    public string ContextMenu_SaveNcaFilePlaintext => "NCA Klartext speichern...";

    public string SettingsLoadingError => "Einstellungen konnten nicht geladen werden: {0}";
    public string SettingsSavingError => "Einstellungen konnten nicht gespeichert werden: {0}";

    public string LoadingError_DiskFull => "Nicht genügend Speicherplatz zum Laden oder Entpacken der Datei. Bitte schaffen Sie freien Speicherplatz auf dem Laufwerk des temporären Ordners und versuchen Sie es erneut. Der Ladevorgang wurde abgebrochen.";
    public string LoadingError_Failed => "Fehler beim laden der Datei «{0}»: {1}";
    public string LoadingError_FailedToCheckIfXciPartitionExists => "Fehler beim Überprüfen, ob die XCI Partition existiert: {0}";
    public string LoadingError_FailedToOpenXciPartition => "XCI Partition konnte nicht geöffnet werden: {0}";
    public string LoadingError_FailedToLoadXciContent => "XCI Partition konnte nicht geladen werden: {0}";
    public string LoadingError_FailedToOpenPartitionFile => "Fehler beim öffnen der Partitionsdatei: {0}";
    public string LoadingError_FailedToLoadNcaFile => "NCA Datei konnt nicht geladen werden: {0}";
    public string LoadingError_FailedToLoadPartitionFileSystemContent => "Fehler beim Laden der Inhalte des Partition-Dateisystems: {0}";
    public string LoadingError_FailedToCheckIfSectionCanBeOpened => "Fehler beim Überprüfen, ob die Sektion geöffnet werden kann.: {0}";
    public string LoadingError_FailedToOpenNcaSectionFileSystem => "NCA Sektionsinhalt konnte nicht geladen werden «{0}»: {1}";
    public string LoadingError_FailedToLoadSectionContent => "Sektionsinhalt konnte nicht geladen werden: {0}";
    public string LoadingError_FailedToGetFileSystemDirectoryEntries => "Fehler beim Abrufen der Verzeichniseinträge des Dateisystems: {0}";
    public string LoadingError_FailedToOpenNacpFile => "Fehler beim öffnen der NACP Datei: {0}";
    public string LoadingError_FailedToLoadNacpFile => "Fehler beim laden der NACP Datei: {0}";
    public string LoadingError_FailedToOpenCnmtFile => "Fehler beim öffnen der CNMT Datei: {0}";
    public string LoadingError_FailedToLoadCnmtFile => "Fehler beim laden der CNMT Datei: {0}";
    public string LoadingError_FailedToLoadNcaContent => "Fehler beim laden des NCA Inhalts: {0}";
    public string LoadingError_FailedToLoadDirectoryContent => "Fehler beim Laden des Verzeichnisinhalts: {0}";
    public string LoadingError_FailedToLoadIcon_Log => "Fehler beim laden des Icons: {0}";
    public string LoadingError_NcaFileMissing_Log => "NCA-Eintrag «{0}» vom Typ «{1}» fehlt.";
    public string LoadingError_NoCnmtFound_Log => "Kein CNMT Eintrag gefunden!";
    public string LoadingError_NacpFileMissing_Log => "NACP Datei «{0}» nicht gefunden!";
    public string LoadingError_NcaMissingSection_Log => "NCA mit Inhalts-Typ «{0}» fehlt eine Sektion vom Typ «{0}».";
    public string LoadingError_MainFileMissing_Log => "Datei «{0}» nicht gefunden!";
    public string LoadingError_IconMissing_Log => "Erwartete Icondatei «{0}» fehlt.";
    public string LoadingError_XciSecurePartitionNotFound_Log => "Sichere Partition der XCI nicht gefunden!";
    public string LoadingError_FailedToGetNcaSectionFsHeader => "Fehler beim Abrufen des NCA-Dateisystem-Headers von Sektion «{0}»: {1}";
    public string LoadingError_FailedToOpenMainFile => "Fehler beim öffnen der Hauptdatei: {0}";
    public string LoadingError_FailedToLoadMainFile => "Fehler beim laden der Hauptdatei: {0}";
    public string LoadingError_FailedToOpenNpdmFile => "main.npdm konnte nicht geöffnet werden: {0}";
    public string LoadingError_FailedToLoadNpdmFile => "main.npdm konnte nicht ausgewertet werden: {0}";
    public string LoadingError_FailedToLoadTicketFile => "Fehler beim laden der Ticket Datei: {0}";
    public string LoadingError_FailedToLoadTitleIdKey => "Failed to load TitleID key from ticket file «{0}»: {1}";
    public string LoadingError_NczBlocklessCompressionDisabled => "Das Öffnen von NCZ Dateien ohne Blockkompression ist in den Einstellungen deaktiviert.";

    public string LoadingInfo_TitleIdKeySuccessfullyInjected => "TitleID Key «{0}={1}» aus der Ticket-Datei «{2}» wurde erfolgreich zu den Keys hinzugefügt.";
    public string LoadingWarning_TitleIdKeyReplaced => "TitleID Key «{0}={1}» aus der Ticket-Datei «{2}» wurde als Ersatz für den bestehenden TitleID Key «{0}={2}» in den Keys verwendet.";
    public string LoadingDebug_TitleIdKeyAlreadyExists => "TitleID Key «{0}={1}» aus der Ticket-Datei «{2}» war bereits in den Keys vorhanden.";

    public string KeysFileUsed => "Die Datei «{0}» wurde verwendet: {1}";
    public string NoneKeysFile => "[keine]";

    public string Status_DownloadingFile => "Lade Datei «{0}» herunter...";
    public string Log_DownloadingFileFromUrl => "Lade «{0}» von der URL «{1}» herunter...";
    public string Log_FileSuccessfullyDownloaded => "Datei «{0}» wurde erfolgreich heruntergeladen.";
    public string Log_FailedToDownloadFileFromUrl => "Fehler beim Herunterladen von «{0}» von der URL «{1}»: {2}";

    public string ToolTip_PatchNumber => "Patchnummer {0}";
    public string Log_OpeningFile => "=====> {0} <=====";
    public string MainModuleIdTooltip => "Auch bekannt als «BuildID» (oder BID).";
    public string ATaskIsAlreadyRunning => "Ein Task wird bereits ausgeführt...";
    public string FileInfo_Title => "Datei";
    public string Title_FileInfo_FileType => "Typ";
    public string Title_FileInfo_Compression => "Komprimierung";
    public string Title_FileInfo_Integrity => "Integrität";
    public string ToolTip_NcasIntegrity => $"""
                                           Die Integritätsprüfung besteht darin, die Integrität jeder NCA (oder NCZ) zu überprüfen.

                                           Das Ergebnis der Integritätsprüfung kann wie folgt aussehen:
                                           {NcasIntegrity_NoNca}: Keine NCA-Datei gefunden.
                                           {NcasIntegrity_Unchecked}: Integrität nicht geprüft.
                                           {NcasIntegrity_InProgress}: Integritätsprüfung läuft.
                                           {NcasIntegrity_Original}: Alle NCAs sind original (Signatur und Hash in Ordnung).
                                           {NcasIntegrity_Incomplete}: Alle NCAs sind original, aber einige fehlen.
                                           {NcasIntegrity_Modified}: Mindestens eine NCA ist modifiziert (Signatur nicht in Ordnung, aber Hash in Ordnung).
                                           {NcasIntegrity_Corrupted}: Mindestens eine NCA ist beschädigt (Hash ungültig).
                                           {NcasIntegrity_Error}: Ein Fehler ist während der Integritätsprüfung aufgetreten.

                                           Details zu jeder analysierten NCA findest du im Tab «Inhalt».
                                           """;

    public string AvailableContents => "Inhalt:";
    public string MultiContentPackageToolTip => "Das aktuelle Paket enthält mehrere Inhalte («{0}» erkannt).";

    public string NcasIntegrity_Error_NcaMissing => "Die Integrität der NCA «{0}» kann nicht überprüft werden, NCA fehlt.";
    public string NcasIntegrity_Error_Log => "Fehler bei der Überprüfung der NCA-Integrität: {0}";
    public string NcaIntegrity_GetOriginalNcaError => "Fehler beim Abrufen der originalen NCA: {0}";
    public string NcaIntegrity_GetOriginalNcaError_Log => "Fehler beim Abrufen der originalen NCA aus der NCA «{0}»: {1}";

    public string NcaHeaderSignature_Valid_Log => "Die Header-Signatur der NCA «{0}» ist gültig.";
    public string NcaHeaderSignature_Invalid => "Die Überprüfung der NCA-Header-Signatur ist mit dem Status «{0}» fehlgeschlagen.";
    public string NcaHeaderSignature_Invalid_Log => "Die Überprüfung der Header-Signatur der NCA «{0}» ist mit dem Status «{1}» fehlgeschlagen.";
    public string NcaHeaderSignature_Error => "Fehler bei der Überprüfung der NCA-Header-Signatur: {0}.";
    public string NcaHeaderSignature_Error_log => "Fehler bei der Überprüfung der Signatur des NCA-Headers «{0}»: {1}";

    public string NcaHash_VerificationStart_Log => ">>> Die Hash-Überprüfung der NCAs wird gestartet...";
    public string NcaHash_VerificationEnd_Log => ">>> Die Hash-Überprüfung der NCAs ist abgeschlossen.";
    public string NcaHash_NcaItem_CantExtractHashFromName => "Fehler beim Extrahieren des erwarteten Hashs aus dem NCA-Namen.";
    public string NcaHash_CantExtractHashFromName_Log => "Fehler beim Extrahieren des erwarteten Hashs aus dem NCA-Namen «{0}».";
    public string NcaHash_Valid_Log => "Der Hash der NCA «{0}» ist gültig.";
    public string NcaHash_NcaItem_Invalid => "Hash ist ungültig.";
    public string NcaHash_Invalid_Log => "Der Hash der NCA «{0}» ist ungültig.";
    public string NcaHash_NcaItem_Exception => "Fehler bei der Überprüfung des Hashs: {0}";
    public string NcaHash_Exception_Log => "Fehler bei der Überprüfung des Hashs der NCA «{0}»: {1}";
    public string NcaHash_ProgressText => "Hashing der NCA {0}/{1}...";

    public string CancelAction => "Abbrechen";
    public string Status_Ready => "Bereit.";
    public string LoadingFile_PleaseWait => "Lade, bitte warten...";

    public string NcasIntegrity_NoNca => "Kein NCA";
    public string NcasIntegrity_Unchecked => "Nicht überprüft";
    public string NcasIntegrity_InProgress => "In Bearbeitung";
    public string NcasIntegrity_Original => "Original";
    public string NcasIntegrity_Incomplete => "Unvollständig";
    public string NcasIntegrity_Modified => "Modifiziert";
    public string NcasIntegrity_Corrupted => "Korrupt";
    public string NcasIntegrity_Error => "Fehler";
    public string NcasIntegrity_Unknown => "Unbekannt";

    public string Status_SavingFile => "Speichere Datei «{0}»...";

    public string KeysLoading_Starting_Log => ">>> Lade Keys...";
    public string KeysLoading_Successful_Log => ">>> Keys erfolgreich geladen.";
    public string KeysLoading_UnusedKey_Log => "Hinweis: Zusätzlicher Schlüssel «{0}» wird von dieser Programmversion nicht verwendet.";
    public string KeysLoading_Error => "Keys konnten nicht geladen werden: {0}.";
    public string WarnNoProdKeysFileFound => "Keine «prod.keys» Datei gefunden.";
    public string InvalidSetting_KeysFileNotFound => "In den Einstellungen definierte Keydatei «{0}» existiert nicht.";
    public string InvalidSetting_BufferSizeInvalid => "In den Einstellungen definierte Puffergröße «{0}» ist ungültig, Wert sollte größer 0 sein.";
    public string InvalidSetting_LanguageNotFound => "In den Einstellungen definierte Sprache «{0}» existiert nicht.";

    public string ToolTip_KeyMissing => "Key «{0}» vom Typ «{1}» fehlt.";

    public string MenuItem_CopyTextToClipboard => "Kopieren";
    public string ContextMenu_OpenFileLocation => "Verzeichnis öffnen...";
    public string OpenFileLocation_Failed_Log => "Fehler beim Öffnen des Speicherorts der Datei «{0}»: {1}";
    public string SettingsView_TitlePageUrl => "Titel Seiten-URL";
    public string SettingsView_TitleInfoApiUrl => "Titelinfo-API-URL";
    public string SettingsView_TitleInfoProvider => "Quelle für Titelnamen";
    public string SettingsView_TitleDbRegion => "TitleDB-Region / Sprache";
    public string SettingsView_TitleDbCacheTip => "TitleDB wird lokal gespeichert und täglich aktualisiert. Bei Ausfällen wird der gespeicherte Stand verwendet. Fehlende Titel werden zusätzlich in US.en gesucht.";
    public string BatchIntegrity_FileType => "Dateityp";
    public string BatchIntegrity_PackageType => "Pakettyp";
    public string BatchIntegrity_ShowOnlyErrors => "Nur fehlerhafte anzeigen";
    public string OpenTitleWebPage_Failed => "Fehler beim Öffnen der Titel Webseite: {0}";

    public string Log_DownloadFileCanceled => "Herunterladen abgebrochen.";
    public string Log_SaveToDirCanceled => "Speichern des Verzeichnisses abgebrochen.";
    public string Log_SaveFileCanceled => "Speichern der Datei abgebrochen.";
    public string Log_SaveStorageCanceled => "Speicherung des Speichers abgebrochen.";
    public string Log_NcasIntegrityCanceled => "Integritätsprüfung der NCAs abgebrochen.";

    public string RenamingTool_TargetDirectory => "Zielordner (leer = bisheriger Ordner)";
    public string RenamingTool_FolderTip => "Unterordner im Muster mit / angeben, z. B. DLC/{WTitle}.{Ext:L} oder {WAppTitle}/DLC/{WTitle}.{Ext:L}.";
    public string RenamingTool_OldName => "Alter Name";
    public string RenamingTool_NewName => "Neuer Name";
    public string RenamingTool_StatusError => "Fehler";
    public string RenamingTool_StatusUnchanged => "Unverändert";
    public string RenamingTool_StatusSimulation => "Simulation";
    public string RenamingTool_StatusRenamed => "Umbenannt";
    public string RenamingTool_WindowTitle => "Umbenennungswerkzeug";
    public string Naming_Manual => "Manuell";
    public string Naming_Edit => "Bearbeiten…";
    public string Naming_Variables => "Mögliche Variablen";
    public string Naming_AllowedVariables => "Für dieses Muster erlaubte Variablen:";
    public string RenamingTool_Patterns => "Muster";
    public string RenamingTool_ApplicationPattern => "Anwendungsmuster";
    public string RenamingTool_PatchPattern => "Patch Muster";
    public string RenamingTool_AddonPattern => "Add-on Muster";
    public string RenamingTool_SuperPattern => "Kombinierte Pakete (SuperNSP / SuperXCI)";
    public string RenamingTool_MultiPattern => "Multi-Pakete / DLC-Sammlungen";
    public string RenamingTool_ToolTip_MultiPattern => "Für mehrere Spiel-IDs oder reine DLC-Sammlungen. Erlaubt: GameCount, UpdateCount, DlcCount und Ext. Beispiel: MultiPaket (2G+3U+5D).nsz";
    public string RenamingTool_ToolTip_SuperPattern => "Für ein Spiel mit Grundspiel und/oder Updates sowie optionalen DLCs. Gemischte Spiel-IDs und reine DLC-Sammlungen verwenden das Multi-Muster.";
    public string RenamingTool_InputPath => "Eingabepfad";
    public string RenamingTool_FileFilters => "Filter";
    public string RenamingTool_ToolTip_Patterns =>
        $$"""
         Zielordner und Unterordner:
           Ein leerer Zielordner verwendet den bisherigen Ordner der Datei.
           Unterordner im Muster mit / trennen, beispielsweise:
             DLC/{WTitle}.{Ext:L}
             {WAppTitle}/DLC/{WTitle}.{Ext:L}
           Nur relative Unterordner verwenden, keine absoluten Pfade oder .. .
           Die Simulation zeigt vollständige Pfade und erstellt keine Ordner.
           Beim Umbenennen werden fehlende Ordner erstellt.
           Vorhandene Zieldateien werden nicht überschrieben.

         Schlüsselwort-Syntax:
            {<Keyword>[:<Format>]}

         Das Format ist optional und kann sein:
         - U: Großbuchstaben
         - L: Kleinbuchstaben

         Beispiele:
           {Title} => Der original Titel
           {Title:U} => Der Titel in Großbuchstaben

         Unterstützte Schlüsselwörter:
           GameCount = Anzahl Grundspiele (G), UpdateCount = Anzahl Updates (U), DlcCount = Anzahl DLCs (D).
           PackageVerNum = höchste enthaltene Spiel-/Update-Version; DLC-Versionen werden dafür nicht verwendet.

           • TitleID:
              - Die Inhalts-ID.
           • AppID:
              - Die ID der entsprechenden {{nameof(ContentMetaType.Application)}} (für {{nameof(ContentMetaType.Application)}} Inhalte ist dieser Wert gleich der {TitleID}).
           • PatchId:
              - Wenn der Inhalt eine {{nameof(ContentMetaType.Application)}} ist, entspricht dieser Wert der ID des entsprechenden {{nameof(ContentMetaType.Patch)}} Inhalts, andernfalls ist der Wert null.
           • PatchNum:
              - Wenn der Inhalt eine {{nameof(ContentMetaType.Application)}} ist, ist der Wert in der Regel 0.
              - Wenn der Inhalt ein {{nameof(ContentMetaType.Patch)}} ist, entspricht der Wert der Patch-Nummer.
              - Wenn der Inhalt ein {{nameof(ContentMetaType.AddOnContent)}} ist, entspricht der Wert der Add-On-Patch-Nummer.
           • Title:
              - Der erste Titel aus der Liste der deklarierten Titel.
              - Dieser Wert existiert nur für Inhalte vom Typ {{nameof(ContentMetaType.Application)}} oder {{nameof(ContentMetaType.Patch)}}, jedoch nicht für {{nameof(ContentMetaType.AddOnContent)}}.
           • Ext:
              - Die Erweiterung, die dem erkannten Dateityp entspricht.
           • VerNum:
              - Die Versionsnummer des Inhalts.
           • VerDsp:
              - Die angezeigte Version.
           • WTitle:
              - Der Titel des Inhalts, der aus dem Internet abgerufen wurde.
           • WAppTitle:
              - Der Titel der entsprechenden {{nameof(ContentMetaType.Application)}}, der aus dem Internet abgerufen wurde.

         Verwende  \{ oder \}, um die Zeichen { oder } zu schreiben.
         """;
    public string RenamingTool_ToolTip_BasePattern => $"Das Muster, das für Inhalte des Typs {nameof(ContentMetaType.Application)} verwendet werden soll.";
    public string RenamingTool_ToolTip_PatchPattern => $"Das Muster, das für Inhalte des Typs {nameof(ContentMetaType.Patch)} verwendet werden soll.";
    public string RenamingTool_ToolTip_AddonPattern => $"Das Muster, das für Inhalte des Typs {nameof(ContentMetaType.AddOnContent)} verwendet werden soll.";
    public string RenamingTool_Button_Cancel => "Abbrechen";
    public string RenamingTool_Button_Rename => "Umbenennen";
    public string RenamingTool_GroupBoxInput => "Eingabe";
    public string RenamingTool_GroupBoxNamingSettings => "Benennungseinstellungen";
    public string RenamingTool_BrowseDirTitle => "Verzeichnis auswählen";
    public string RenamingTool_GroupBoxOutput => "Ausgabe";
    public string RenamingTool_Miscellaneous => "Sonstiges";
    public string RenamingTool_InvalidWindowsCharReplacement => "Ersetze unzulässige Windows-Zeichen durch";
    public string RenamingTool_ReplaceWhiteSpaceChars => "Ersetze Leerzeichen-Zeichen";
    public string RenamingTool_ReplaceWhiteSpaceCharsWith => "Ersetze Leerzeichen-Zeichen durch";
    public string RenamingTool_Simulation => "Simulation";
    public string RenamingTool_AutoCloseOpenedFile => "Geöffnete Datei automatisch schließen";
    public string RenamingTool_IncludeSubDirectories => "Unterverzeichnisse einbeziehen";
    public string RenamingTool_ContentTypeNotSupported => "Inhaltstyp «{0}» wird nicht unterstützt.";
    public string RenamingTool_SuperPackageNotSupported => "Nicht unterstützte Inhaltstypen im kombinierten Paket.";
    public string RenamingTool_LogNbFilesToRename => ">>> {0} Datei(en) zum Umbenennen...";
    public string RenamingTool_LogSimulationMode => $"[SIMULATION] ";
    public string RenamingTool_LogFileRenamed => $"• {{0}}Datei wurde umbenannt von{Environment.NewLine}\t«{{1}}» zu{Environment.NewLine}\t«{{2}}».";
    public string RenamingTool_LogFileAlreadyNamedProperly => "• {0}«{1}» ist bereits korrekt benannt.";
    public string RenamingTool_LogFailedToRenameFile => "• {0}«{1}»Umbenennen fehlgeschlagen: {2}";
    public string RenamingTool_LogRenamingFailed => "Umbenennen fehlgeschlagen: {0}";
    public string RenamingTool_BadInvalidFileNameCharReplacement => "Die Ersetzungszeichenfolge «{0}» (für ungültige Dateinamenzeichen) darf das ungültige Zeichen «{1}» nicht enthalten.";

    public string Exception_UnexpectedDelimiter => "Unerwarteter Trenner {0} an Position {1} gefunden, verwende stattdessen {2}{0}.";
    public string Exception_EndDelimiterMissing => "End-Trenner {0} fehlt.";
    public string FileRenaming_PatternKeywordUnknown => "Schlüsselwort «{0}» ist unbekannt, erlaubte Schlüsselwörter sind «{1}».";
    public string FileRenaming_EmptyPatternNotAllowed => "Muster darf nicht leer sein.";
    public string FileRenaming_PatternKeywordNotAllowed => "Schlüsselwort «{0}» ist für Muster vom Typ «{1}» nicht erlaubt.";
    public string FileRenaming_StringOperatorUnknown => "Operator «{0}» wird nicht erkannt, erlaubte Operatoren sind «{1}».";
    public string FileRenaming_EmptyDirectoryNotAllowed => "Eingabeverzeichnis darf nicht leer sein.";
    public string Window_Tip_Title => "Hinweis";
    public string Nsz_Installed => "Plugin nicoboss/nsz installiert";
    public string Nsz_CustomExecutable => "Benutzerdefinierte EXE";
    public string Settings_AutoVerifyBatchDrop => "Beim Ablegen in die Stapelprüfung";
    public string Settings_AutoVerifyBatchDropTip => "Gilt für mehrere Dateien, Ordner und gemischte Auswahlen sowie einzelne Dateien im Reiter Stapelprüfung. Aktiviert Einlesen und Integritätsprüfung statt nur Einlesen. Die Prüfung kann abgebrochen werden.";
    public string Settings_AutoVerifyProgram => "Beim Öffnen im Programm";
    public string Settings_AutoVerifyProgramTip => "Gilt für „Datei öffnen“ und einzelne per Drag-and-drop geöffnete Dateien. Die Integritätsprüfung startet nach dem Einlesen und kann abgebrochen werden. Stapelprüfung und History behalten ihre eigenen Abläufe.";
    public string Settings_AutoVerifyWindows => "Beim Öffnen über Windows";
    public string Settings_AutoVerifyWindowsTip => "Gilt für Doppelklick und „Öffnen mit“ über die Windows-Dateizuordnung sowie einen Dateipfad beim Programmstart. Diese Einstellung ist unabhängig vom Öffnen innerhalb des Programms.";
    public string Settings_GroupGeneral => "Allgemein";
    public string Settings_GroupHistory => "Verlauf";
    public string Settings_GroupTitleInfo => "Titelinformationen";
    public string Settings_GroupLogging => "Protokollierung";
    public string Settings_AutoVerifyTitle => "Automatische Integritätsprüfung";
    public string BatchHistory_RetentionTip => "Die Anzahl begrenzt sofort die Anzeige. Beim nächsten Speichern eines Stapellaufs bleiben nur die neuesten Einträge bis zur eingestellten Grenze erhalten. Geprüfte Dateien werden nicht gelöscht.";
    public string Settings_Program => "Programm";
    public string Nsz_PhaseSource => "Quelldatei prüfen";
    public string Nsz_PhaseLayout => "XCZ-Partitionsaufbau prüfen und korrigieren…";
    public string Nsz_PhaseOutput => "Ergebnis prüfen";
    public string Nsz_PhasePublish => "Ergebnis übernehmen";
    public string Batch_IncludeArchives => "ZIP / 7z einbeziehen";
    public string Batch_Scan => "Dateiliste einlesen";
    public string Batch_VerifyAll => "Alle auf Integrität prüfen";
    public string File_SaveBackupSuspected => "Spielstand-Backup (vermutet)";
    public string Mtp_Title => "Sphaira (MTP)";
    public string Mtp_Tip => "Switch per USB verbinden und MTP in Sphaira starten. Gerät öffnen, dann microSD, Contents oder eine Datei bzw. einen Ordner auswählen. Unterstützte Inhalte werden in den gewählten Zwischenspeicher kopiert; die Switch bleibt unverändert. Für SD-Inhalte sind die passenden Keys erforderlich.";
    public string Mtp_Devices => "Geräte aktualisieren";
    public string Mtp_Select => "Auswahl übernehmen";
    public string Mtp_SelectContent => "Bitte auf dem Gerät eine Datei oder einen Inhaltsordner auswählen.";
    public string Mtp_Listing => "Sphaira (MTP): Ordnerinhalt lesen…";
    public string Mtp_NoFiles => "Keine unterstützten Dateien in dieser Auswahl gefunden.";
    public string Mtp_NoDevice => "Keine Einträge gefunden. MTP-Verbindung prüfen oder Geräte aktualisieren.";
    public string Mtp_Copying => "Sphaira (MTP): Kopieren {0} — Datei {1}/{2} — {3:N0}/{4:N0} Bytes";
    public string Mtp_Cache => "Zwischenspeicher für die lokale Kopie";
    public string Archive_Reading => "Archivinhalt lesen: {0}";
    public string Archive_Extracting => "Entpacken / Kopieren: {0} — Datei {1}/{2} — {3:N0}/{4:N0} Bytes";
    public string Batch_GroupFiles => "Nach Ordner / Archiv gruppieren";
    public string Batch_MultiPackageDetails => "Enthaltene Pakete anzeigen oder ausblenden";
    public string File_SaveBackup => "Spielstand-Backup";
    public string File_MissingKeysHeader => "Fehlende Schlüssel";
    public string File_MissingTitleKeysHeading => "Titel-Schlüssel fehlen (Ticket / title.keys)";
    public string File_MissingTitleKeyEntry => "Titel-Schlüssel fehlt · Rights-ID: {0}";
    public string File_MissingTitleKeysHelp => "Titel-Schlüssel gehören zur genannten Rights-ID und stammen aus dem passenden Ticket (.tik) oder aus title.keys. Ein neuerer Master-Key in prod.keys ersetzt sie nicht. Prüfe das passende Ticket und gegebenenfalls die Einstellung zur Übernahme von Schlüsseln aus Tickets.";
    public string File_MissingSystemKeysHelp => "Für fehlende System- oder Konsolenschlüssel prüfe die konkret genannten Bezeichnungen in den verwendeten Schlüsseldateien (z. B. prod.keys). Eine hohe Master-Key-Version allein garantiert nicht, dass jeder benötigte Schlüssel vorhanden ist.";
    public string File_MissingKeys => "Erforderliche Schlüssel fehlen. Einige Inhalte können nicht vollständig gelesen werden.";
    public string File_CopyMissingKeys => "Fehlende Schlüsselnamen kopieren";
    public string Keys_ProgramFolder => "Programmordner";
    public string Keys_SharedFolder => "Benutzerprofil (.switch)";
    public string Keys_InUse => "Wird verwendet";
    public string Keys_DownloadAll => "Keys herunterladen";
    public string Keys_DownloadHost => "Download-IP / Hostname";
    public string Keys_DownloadHostTip => "{IP} in den Download-URLs wird durch diese Adresse ersetzt. Ziel: benutzerdefinierter Pfad, falls gesetzt, sonst Programmordner.";
    public string Keys_CopyToSwitchHelp => @"Optional: Kopiert die aktuell verwendete prod.keys und, falls vorhanden, title.keys nach %USERPROFILE%\.switch. Programme und weitere NxFileViewer Fork-Installationen, die diesen gemeinsamen Suchort unterstützen, können die Dateien dort verwenden. Für portable Installationen reichen die Keys im eigenen Programmordner aus; eine zusätzliche Kopie bietet keine weiteren Entschlüsselungsmöglichkeiten. Es werden Kopien erstellt, die Originale bleiben erhalten. Spätere Änderungen werden nicht automatisch synchronisiert. Vor dem Ersetzen vorhandener Zieldateien wird nachgefragt.";
    public string Keys_CopyToSwitch => @"Aktuelle Keys nach %USERPROFILE%\.switch kopieren";
    public string Keys_ReplaceShared => "Vorhandene Keys ersetzen? Die Quelldateien bleiben erhalten.";
    public string Keys_SharedCopied => "Keys sind im gemeinsamen .switch-Ordner verfügbar.";
    public string Batch_ScanAndVerify => "Dateiliste einlesen und Integrität prüfen";
    public string Mtp_Direct => "Direkt lesen (experimentell; sonst lokaler Cache)";
    public string Mtp_DirectTip => "Ohne vorherige Kopie für NSP/NSZ, XCI/XCZ und einzelne NCA. Windows kann intern puffern; Sprünge können lange dauern und währenddessen nicht abgebrochen werden. Für Archive, NAND und SD-Strukturen den lokalen Cache verwenden.";
    public string Mtp_DirectUnsupported => "Diese Auswahl benötigt den lokalen Cache (Archiv, NAND oder geteilte NCA). Bitte Direkt lesen deaktivieren.";
    public string SdBatch_Title => "Switch-SD: Bereiche für die Stapelprüfung";
    public string SdBatch_Enable => "Nur ausgewählte Bereiche prüfen";
    public string SdBatch_Tip => "Gilt beim Öffnen einer ganzen Switch-SD-Karte, auch über MTP. Nicht ausgewählte Bereiche werden übersprungen und über MTP nicht kopiert. Einzelne geöffnete Dateien oder Unterordner bleiben unabhängig prüfbar.";
    public string SdBatch_Custom => "Eigene Bereiche (ein Pfad pro Zeile)";
    public string SdBatch_CustomTip => "Zusätzlich zu den Checkboxen. Ordner oder Dateien relativ zur SD-Wurzel, z. B. /atmosphere, /backup/saves oder /apps/tool.nro. Zum Entfernen die Zeile löschen. Keine Laufwerkspfade, Platzhalter oder .. verwenden.";
    public string SdBatch_InvalidPath => "Ungültiger SD-Pfad:";
    public string SdBatch_SourceAuto => "Automatisch erkennen";
    public string SdBatch_SourceFolder => "Normaler Ordner";
    public string SdBatch_SourceSd => "Switch-SD";
    public string SdBatch_SourceTip => "Quelltyp für die Stapelprüfung, auch über MTP. Automatisch: Nintendo, emuMMC oder hbmenu.nro in der Wurzel. Switch-SD: SD-Bereichsfilter unabhängig vom Inhalt anwenden. Normaler Ordner: keinen SD-Bereichsfilter anwenden. Der Bereichsfilter muss in den Einstellungen aktiviert sein.";
    public string Nand_DriveRefresh => "Laufwerke aktualisieren";
    public string Nand_DriveAccessDenied => "Der Datenträger wurde erkannt, aber Windows verweigert den Lesezugriff. NxFileViewer Fork als Administrator starten und das Laufwerk erneut auswählen. Es wird nur lesend geöffnet.";
    public string Associations_Title => "Windows-Dateizuordnungen";
    public string Associations_Remove => "Dateizuordnung entfernen";
    public string Associations_Removed => "Die Dateizuordnungs-Registrierung von NxFileViewer Fork wurde entfernt. Falls er die Standard-App war, wähle in Windows über Öffnen mit eine andere App aus. Die Explorer-Kontextmenüs können getrennt entfernt werden.";
    public string Explorer_Title => "Explorer-Kontextmenü";
    public string Explorer_Tip => "Unter NxFileViewer Fork: im Batch oder File öffnen (ohne Hashprüfung) und prüfen (mit Hashprüfung). Bei mehreren Dateien oder Ordnern nur Batch. Auch bei geschlossenem Programm. Klassisch nur für deinen Benutzer, ohne Adminrechte; unter Windows 11 über Weitere Optionen anzeigen. Nach einem Ordnerwechsel erneut hinzufügen. Änderungen wirken sofort.";
    public string Explorer_AddClassic => "Klassisches Menü hinzufügen / aktualisieren";
    public string Explorer_RemoveClassic => "Klassisches Menü entfernen";
    public string Explorer_ModernTip => "Lädt das passende Explorer-Setup von GitHub und prüft dessen Version unabhängig von der App. Beim ersten Mal Zertifikat und Adminabfrage bestätigen. Windows kann das Menü auch klassisch anzeigen; eine zusätzliche klassische Registrierung wird nicht angelegt. Vorabversionen folgen der Update-Einstellung. Eine lokale MSIX bleibt für Offline-Installationen auswählbar.";
    public string Explorer_AddModern => "Modernes Menü installieren / aktualisieren";
    public string Explorer_RemoveModern => "Modernes Menü entfernen";
    public string Explorer_Local => "Lokale MSIX auswählen…";
    public string Explorer_Trusting => "Bitte Zertifikat im geöffneten Fenster bestätigen.";
    public string Explorer_NoPackage => "Kein passendes Explorer-Setup in den GitHub-Releases gefunden. Bei Bedarf Vorabversionen für Updates aktivieren oder eine lokale MSIX auswählen.";
    public string Explorer_Done => "Die Explorer-Integration wurde aktualisiert. Falls das Menü noch unverändert ist, Explorer oder Windows neu starten.";
    public string Associations_Register => "NxFileViewer Fork registrieren";
    public string Associations_Tip => "Für .nsp, .nsz, .xci, .xcz und .nro wird eine eigene Fork-Kennung mit dem aktuellen EXE-Pfad registriert. Danach in Windows über Öffnen mit → Andere App auswählen → Immer den NxFileViewer Fork wählen. Nach einem Ordnerwechsel erneut registrieren. Die Schaltfläche wirkt sofort.";
    public string Associations_Done => "NxFileViewer Fork wurde für .nsp, .nsz, .xci, .xcz und .nro registriert. Wähle ihn jetzt über Öffnen mit → Andere App auswählen → Immer als Standard-App aus.";
    public string Home_PluginLaunchTitle => "Plugins direkt starten";
    public string Home_PluginLaunchTip => "Öffnet das installierte Programm in einem eigenen Fenster.";
    public string Home_NandDescription => "NAND, Partitionen und Spielstände verwalten";
    public string Home_NszDescription => "Spieldateien komprimieren und dekomprimieren";
    public string Home_LaunchExternal => "Extern öffnen ↗";
    public string Home_NandLaunchTip => "Startet NxNandManager separat mit den konfigurierten Keys. Nur verfügbar, wenn installiert und keine Hintergrundaufgabe läuft.";
    public string Home_NszLaunchTip => "Startet die NSZ-Oberfläche separat mit den aktiven prod.keys. Benötigt eine installierte NSZ-GUI-Version und eine freie Hintergrundverarbeitung.";
}
