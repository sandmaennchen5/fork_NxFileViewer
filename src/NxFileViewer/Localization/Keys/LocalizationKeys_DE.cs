using System;
using Emignatik.NxFileViewer.Utils.MVVM.Localization;
using LibHac.Ncm;

namespace Emignatik.NxFileViewer.Localization.Keys;

public class LocalizationKeys_DE : LocalizationKeysBase, ILocalizationKeys
{
    public string Nand_Detected => "NAND-Signaturen erkannt. Die IntegritÃ¤t wurde nicht geprÃ¼ft.";
    public string Nand_OpenSave => "Spielstand öffnen";
    public string Nand_LeaveSave => "Zur Partition";
    public string Nand_ExportSave => "Spielstand exportieren…";
    public string Nand_ExplorerTitle => "Titel";
    public string Nand_ExplorerUserId => "Benutzer-ID";
    public string Nand_NameCandidate => "NAND-Kandidat anhand des Dateinamens. FÃ¼r BestÃ¤tigung und erweiterte Informationen NAND-Informationen abrufen.";
    public string Nand_Installed => "Plugin NxNandManager installiert";
    public string Nand_CustomUpdate => "Eigenen EXE-Pfad leeren und Ã¼bernehmen, um verwaltete Downloads zu verwenden.";
    public string Nand_Updating => "NxNandManager wird heruntergeladen und geprÃ¼ftâ€¦";
    public string Nand_Open => "NAND-Dump Ã¶ffnenâ€¦";
    public string Nand_Info => "NAND-Informationen";
    public string Nand_Export => "Partition exportierenâ€¦";
    public string Nand_SettingsTip => "EXE-Pfad leer lassen fÃ¼r verwaltete Downloads unter Einstellungen â†’ Updates â†’ Plugins â†’ NxNandManager. Eine eigene EXE wird nicht ersetzt.";
    public string Nand_BisKeys => "BIS-Key-Datei (optional; leer = aktive prod.keys von NxFileViewer)";
    public string Nand_Tip => "NAND-Dump Ã¶ffnen (bei geteilten Dumps die erste Datei). Der Export kopiert die Partition im gespeicherten Zustand, ohne EntschlÃ¼sselung. NxNandManager unter Einstellungen â†’ Plugins konfigurieren.";
    public string Nand_NewTarget => "Neue lokale Zieldatei wÃ¤hlen. Vorhandene Dateien kÃ¶nnen nicht ersetzt werden.";
    public string Nand_SourceMissing => "Die NAND-Quelldatei fehlt oder ist keine lokale Datei.";
    public string Nand_NotInstalled => "NxNandManager ist nicht installiert. Installation unter Einstellungen â†’ Updates â†’ Plugins.";
    public string Nand_OpenDrive => "Laufwerk Ã¶ffnenâ€¦";
    public string Nand_Explorer => "NAND Explorer";
    public string Nand_ExplorerUp => "Ãœbergeordneter Ordner";
    public string Nand_ExportFile => "Datei exportierenâ€¦";
    public string Nand_ExplorerFolder => "Ordner";
    public string Nand_DriveTip => @"NAND-Laufwerk auswÃ¤hlen (z. B. \\.\PhysicalDrive2). Es wird nur lesend geÃ¶ffnet. FÃ¼r den Zugriff auf physische Laufwerke kÃ¶nnen Administratorrechte nÃ¶tig sein.";
    public string Nand_KeysMissing => "Die konfigurierte BIS-Key-Datei existiert nicht.";
    public string Nand_ExportFailed => "NxNandManager hat keine gefÃ¼llte Partitionsdatei erzeugt.";
    public string Nand_ExportDone => "Partition exportiert.";
    public string Nand_Cancelled => "Abgebrochen oder Zeitlimit der Informationsabfrage erreicht.";
    public string DataUpdate_Firmware => "Firmware-Hashes";
    public string DataUpdate_Title => "Updates";
    public string DataUpdate_Titles => "TitleDB aktualisieren";
    public string BatchNaming_Unchecked => "Nicht geprÃ¼ft";
    public string DataUpdate_LocalFirmwareVersion => "Lokale Hashlisten bis Firmware {0}.";
    public string DataUpdate_NoLocalFirmware => "Keine lokalen Firmware-Hashlisten installiert.";
    public string DataUpdate_TitleCatalogDate => "TitleDB {0}: lokal aktualisiert am {1}.";
    public string DataUpdate_TitleCatalogMissing => "TitleDB {0}: nicht lokal gespeichert.";
    public string DataUpdate_OnlineFirmwareVersion => "Online-Hashlisten bis Firmware {0}.";
    public string Keys_ExistingValidation => "Vorhandene Datei:";
    public string Keys_IncomingValidation => "Neue Datei:";
    public string Keys_ReplaceDownloaded => "Heruntergeladene Keys Ã¼bernehmen und vorhandene Datei ersetzen?";
    public string Keys_SaveTicketKeys => "Fehlende Ticket-SchlÃ¼ssel in title.keys speichern";
    public string Keys_TicketConflict => "Ticket-SchlÃ¼sselkonflikt fÃ¼r {0} in {1}: vorhandener Eintrag bleibt erhalten.";
    public string Keys_TicketSaved => "Ticket-SchlÃ¼ssel fÃ¼r {0} wurde in {1} gespeichert.";
    public string Tinfoil_StabilityHint => "Tinfoil ist zeitweise nicht erreichbar oder instabil. Bei Fehlern bitte spÃ¤ter erneut versuchen oder eine andere Quelle auswÃ¤hlen.";
    public string DataUpdate_TitleTip => "Aktualisiert die GitHub-TitleDB fÃ¼r die gespeicherte Region und den US-Fallback im Programmordner. Der ausgewÃ¤hlte Titelanbieter bleibt unverÃ¤ndert.";
    public string DataUpdate_FirmwareTip => "Firmware-PrÃ¼fungen laden Online-Hashes weiterhin frisch ohne dauerhaften Cache. Das Offlinepaket wird nur Ã¼ber die separate SchaltflÃ¤che gespeichert; die bisherigen lokalen Hashes werden gesichert.";
    public string DataUpdate_CheckFirmware => "Online-Hashes prÃ¼fen";
    public string DataUpdate_SaveFirmware => "Offline-Hashes aktualisieren";
    public string DataUpdate_Working => "Aktualisierung lÃ¤uftâ€¦";
    public string DataUpdate_TitlesDone => "TitleDB {0} aktualisiert: {1} KatalogeintrÃ¤ge inklusive US-Fallback.";
    public string DataUpdate_FirmwareSaved => "Offline-Hashes aktualisiert: {0} Referenzdateien.";
    public string DataUpdate_FirmwareChecked => "Online-Hashes geprÃ¼ft: {0} Referenzdateien; nichts gespeichert.";
    public string Update_Title => "Programm-Updates";
    public string Update_IncludePrereleases => "Vorabversionen (Pre-Releases) bei Programmupdates berÃ¼cksichtigen";
    public string Update_Prerelease => "Vorabversion";
    public string Update_Auto => "Beim Start nach Programm-Updates suchen";
    public string Update_Check => "Nach Updates suchen";
    public string Update_Install => "Herunterladen und installieren";
    public string Update_Checking => "Suche nach Updatesâ€¦";
    public string Update_Current => "Keine neuere verÃ¶ffentlichte Version verfÃ¼gbar.";
    public string Component_UpdateAvailable => "Update verfÃ¼gbar.";
    public string Component_NotInstalled => "Nicht installiert.";
    public string Component_CustomVersion => "Benutzerdefinierte Version: automatische PrÃ¼fung nicht mÃ¶glich.";
    public string Update_Available => "Version {0} ist verfÃ¼gbar.";
    public string Update_Failed => "Update fehlgeschlagen.";
    public string Update_Confirm => "Version {0} herunterladen und installieren? NxFileViewer wird anschlieÃŸend neu gestartet. Keys, Einstellungen und Plugins bleiben erhalten.";
    public string Update_Downloading => "Update wird heruntergeladen und geprÃ¼ftâ€¦";
    public string Update_Installing => "Update wird installiertâ€¦";
    public string Update_Cancelled => "Update abgebrochen.";
    public string Nsz_Mode => "Kompressionsmodus";
    public string Nsz_ModeAuto => "Automatisch (NSZ: Solid, XCZ: BlÃ¶cke)";
    public string Nsz_ModeSolid => "Solid / Blockless";
    public string Nsz_ModeBlock => "Block-Komprimierung";
    public string Nsz_BlockSize => "BlockgrÃ¶ÃŸe";
    public string Nsz_ModeTip => "Solid komprimiert etwas stÃ¤rker. BlÃ¶cke ermÃ¶glichen schnelle RÃ¼cksprÃ¼nge und zufÃ¤llige Lesezugriffe. Modus und BlockgrÃ¶ÃŸe gelten nur beim Komprimieren.";
    public string Workspace_Plugins => "Plugins";
    public string Workspace_Home => "Start";
    public string Workspace_File => "DateiprÃ¼fung";
    public string Workspace_Menu => "HauptmenÃ¼";
    public string Nsz_Replace => "Ersetzen";
    public string Nsz_Number => "Mit Nummerierung speichern";
    public string TitlePage_Custom => "Eigene";
    public string Info_WithRuntime => "Mit integriertem .NET";
    public string Info_WithoutRuntime => "Ohne integriertes .NET â€” benÃ¶tigt .NET 8 Desktop Runtime";
    public string Info_Description => "NxFileViewer prÃ¼ft und zeigt Nintendo-Switch-Dateien an. UnterstÃ¼tzt NSP, NSZ, XCI, XCZ, NCA sowie ZIP- und 7z-Archive, Firmware-PrÃ¼fung und NSZ-Konvertierung.";
    public string Info_Shortcuts => "TastenkÃ¼rzel";
    public string BatchHistory_Show => "Anzeigen";
    public string BatchHistory_Title => "Letzte 5 StapelprÃ¼fungen";
    public string BatchHistory_Resume => "Fortsetzen";
    public string Dialog_Yes => "Ja";
    public string Dialog_No => "Nein";
    public string Nsz_Cancel => "Abbrechen";
    public string Nsz_DeleteSourcePrompt => "Quelldateien nach erfolgreicher Konvertierung und PrÃ¼fung lÃ¶schen? Bei Fehlern bleiben sie erhalten.";
    public string Nsz_SourceDeleted => "Quelle gelÃ¶scht";
    public string Nsz_SourceDeleteFailed => "Quelle konnte nicht gelÃ¶scht werden";
    public string Nsz_Compress => "Komprimieren und prÃ¼fenâ€¦";
    public string Nsz_Decompress => "Dekomprimieren und prÃ¼fenâ€¦";
    public string Nsz_CompressValid => "Fehlerfreie komprimierenâ€¦";
    public string Nsz_DecompressValid => "Fehlerfreie dekomprimierenâ€¦";
    public string Nsz_Update => "Plugin installieren / aktualisierenâ€¦";
    public string Nsz_Rollback => "Vorherige Version verwenden";
    public string Nsz_SelectDestination => "Zielordner auswÃ¤hlen";
    public string Nsz_PythonRuntimeFailed => "Die externe NSZ-EXE konnte ihre eingebettete Python-DLL nicht laden. Das Programm startet nicht; Keys und Eingabedateien wurden noch nicht geprÃ¼ft. WÃ¤hle unter Einstellungen â†’ Plugin nicoboss/nsz eine andere, auf diesem Rechner lauffÃ¤hige NSZ-CLI.";
    public string Nsz_NotInstalled => "Plugin nicoboss/nsz ist nicht installiert.";
    public string Nsz_Updating => "Plugin nicoboss/nsz wird aktualisiertâ€¦";
    public string Nsz_SourceSize => "OriginalgrÃ¶ÃŸe (Bytes)";
    public string Nsz_OutputSize => "ZielgrÃ¶ÃŸe (Bytes)";
    public string Nsz_Verified => "Ergebnis geprÃ¼ft";
    public string Nsz_Summary => "{0} umgewandelt und geprÃ¼ft; {1} fehlgeschlagen; {2} nicht verarbeitet. Originale bleiben erhalten.";
    public string Nsz_SettingsTip => "Optionaler Pfad zur NSZ-CLI. Leer lassen, um offizielle Releases automatisch zu verwalten. Quelle und Ergebnis werden geprÃ¼ft; Originale bleiben erhalten.";
    public string Nsz_CheckUpdates => "Vor Umwandlung auf stabile Updates prÃ¼fen";
    public string Nsz_Level => "Kompressionsstufe (1â€“22)";
    public string Nsz_OutputExists => "Zieldatei existiert bereits:";
    public string Nsz_SourceInvalid => "IntegritÃ¤tsprÃ¼fung der Quelle fehlgeschlagen:";
    public string Nsz_OutputInvalid => "IntegritÃ¤tsprÃ¼fung des Ergebnisses fehlgeschlagen:";
    public string Nsz_OutputMissing => "NSZ hat die erwartete Zieldatei nicht erstellt.";
    public string Nsz_KeysMissing => "Keine prod.keys-Datei geladen.";
    public string Nsz_Incompatible => "Diese NSZ-CLI unterstÃ¼tzt die benÃ¶tigten Optionen nicht.";
    public string Nsz_OfflineFallback => "Update nicht verfÃ¼gbar; installierte NSZ-Version wird verwendet.";
    public string Firmware_NoReferences => "Firmware-Hashlisten nicht verfÃ¼gbar: GitHub nicht erreichbar und lokale Referenzen fehlen oder sind ungÃ¼ltig.";
    public string Firmware_LoadingOnline => "Firmware-Hashlisten von GitHub ladenâ€¦";
    public string Firmware_OnlineSource => "Hashquelle: GitHub (fÃ¼r diesen PrÃ¼flauf geladen).";
    public string Firmware_OfflineSource => "Hinweis: GitHub nicht verfÃ¼gbar. PrÃ¼fung mit mitgelieferten Hashlisten; neuere Firmware kann fehlen.";
    public string Firmware_BrowseZip => "ZIP / 7z auswÃ¤hlenâ€¦";
    public string Firmware_NcaMatches => "Diese NCA ist in folgenden Firmware-Versionen enthalten:";
    public string Firmware_Versions => "Firmware-Version(en)";
    public string Firmware_Title => "Firmware-PrÃ¼fung";
    public string Firmware_Unknown => "Keine passende Firmware-Hashliste gefunden.";
    public string Firmware_Summary => "{0}/{1} Dateien gÃ¼ltig; fehlend: {2}, verÃ¤ndert: {3}, zusÃ¤tzlich: {4}, doppelt: {5}.";
    public string Firmware_Missing => "Fehlend";
    public string Firmware_Changed => "VerÃ¤ndert (GrÃ¶ÃŸe/SHA-256)";
    public string Firmware_Renamed => "Falsch benannt (Inhalt stimmt Ã¼berein)";
    public string Firmware_RenamedSummary => "Falsch benannt: {0}.";
    public string Firmware_Extra => "ZusÃ¤tzliche NCA";
    public string Firmware_Duplicate => "Doppelter Dateiname";

    public override bool IsFallback => true;
    public override string DisplayName => "Deutsch";
    public override string CultureName => "de-DE";
    public override string LanguageAuto => "Auto";

    public string FileNotSupported_Log => "Â«{0}Â» Datei wird nicht unterstÃ¼tzt.";
    public string OpenSdCard => "SD-Karte Ã¶ffnen";
    public string OpenFile_Filter => "Nintendo Switch Dateien (*.nsp;*.nsz;*.xci;*.xcz;*.nca;*.nro;*.zip;*.7z;*.bin;*.img;*.00)|*.nsp;*.nsz;*.xci;*.xcz;*.nca;*.nro;*.zip;*.7z;*.bin;*.img;*.00|Switch-Spielpakete (*.nsp;*.nsz;*.xci;*.xcz)|*.nsp;*.nsz;*.xci;*.xcz|Archive (*.zip;*.7z)|*.zip;*.7z|Switch-Inhaltsdateien (*.nca)|*.nca|NAND-Abbilder und geteilte Dumps (*.bin;*.img;*.00)|*.bin;*.img;*.00|Alle Dateien (*.*)|*.*";
    public string MenuItem_File => "Datei";
    public string MenuItem_Open => "Ã–ffnen...";
    public string MenuItem_OpenLast => "Letzte Ã¶ffnen";
    public string MenuItem_Close => "SchlieÃŸen";
    public string MenuItem_Exit => "Beenden";
    public string MenuItem_Tools => "Werkzeuge";
    public string MenuItem_CheckIntegrity => "IntegritÃ¤t prÃ¼fen";
    public string MenuItem_CheckDirectoryIntegrity => "OrdnerintegritÃ¤t prÃ¼fenâ€¦";
    public string BatchNaming_Title => "Benennung";
    public string BatchNaming_Matches => "Passt";
    public string BatchNaming_Differs => "Passt nicht";
    public string BatchNaming_Check => "Benennung prÃ¼fen";
    public string BatchNaming_RenameAll => "Alle abweichenden umbenennen";
    public string BatchNaming_Error => "Benennungsfehler";
    public string BatchIntegrity_Title => "StapelprÃ¼fung";
    public string BatchIntegrity_SelectDirectory => "Ordner mit Switch-Dateien auswÃ¤hlen";
    public string BatchIntegrity_Browse => "Durchsuchenâ€¦";
    public string BatchIntegrity_IncludeSubdirectories => "Unterordner einbeziehen";
    public string BatchTable_Columns => "Spaltenâ€¦";
    public string BatchTable_Search => "Suchen";
    public string BatchTable_All => "Alle";
    public string BatchTable_ResetFilters => "Filter zurÃ¼cksetzen";
    public string BatchTable_ResetSort => "Sortierung zurÃ¼cksetzen";
    public string BatchIntegrity_File => "Datei";
    public string BatchIntegrity_Path => "Pfad";
    public string BatchIntegrity_Error => "Fehler";
    public string BatchIntegrity_NszDataCorrupted => "Der komprimierte NSZ/NCZ-Datenstrom ist beschÃ¤digt oder unvollstÃ¤ndig (Zstandard-Dekomprimierung fehlgeschlagen).";
    public string BatchIntegrity_IntegrityFailed => "Die IntegritÃ¤tsprÃ¼fung konnte nicht vollstÃ¤ndig ausgefÃ¼hrt werden. Details stehen im Protokoll.";
    public string PackageStructure_Filesystem => "Dateisystem";
    public string Signature_Title => "NCA-Signatur";
    public string Signature_Passed => "Bestanden";
    public string Signature_NotPassed => "Nicht bestanden";
    public string Signature_Unchecked => "Nicht geprÃ¼ft";
    public string ToolTip_PackageStructure => "Struktur nach Nx Game Info:\nScene (XCI): Update-, Normal- und Secure-Partition vorhanden.\nKonvertiert (XCI): nur Secure-Partition; typisch fÃ¼r NSP â†’ XCI.\nScene (NSP): legalinfo.xml, nacp.xml, programinfo.xml und cardspec.xml; typisch fÃ¼r BBB-Releases.\nHomebrew (NSP): authoringtoolinfo.xml vorhanden.\nCDN (NSP): Zertifikat (.cert) und Ticket (.tik); typisch fÃ¼r eShop-CDN-Dumps.\nKonvertiert (NSP): ohne Zertifikat und Ticket; typisch fÃ¼r XCI â†’ NSP.\nDateisystem: installierte NAX0-Titel auf der Switch-SD-Karte.\nUnvollstÃ¤ndig: nur NCA-Inhalte. NSZ/XCZ folgen den entsprechenden Paketregeln; NCZ zÃ¤hlt als NCA.";
    public string ToolTip_NcaSignature => "Bestanden: gÃ¼ltige NCA-Signaturen, wie bei offiziellen Titeln erwartet.\nNicht bestanden: mindestens eine ungÃ¼ltige NCA-Signatur; bei Homebrew mÃ¶glich, bei offiziellen Titeln auffÃ¤llig.\nNicht geprÃ¼ft: die NCA-SignaturprÃ¼fung wurde noch nicht vollstÃ¤ndig durchgefÃ¼hrt. Ãœber die IntegritÃ¤tsprÃ¼fung starten.\nDiese Anzeige betrifft NCA-Header-Signaturen; ACID ist eine separate NPDM-Signatur.";
    public string ToolTip_Permission => "Sicher: kein Dateisystem-Servicezugriff oder Bit 0x8000000000000000 nicht gesetzt.\nUnsicher: Dateisystem-Servicezugriff und Bit 0x8000000000000000 gesetzt (EraseMmc).\nGefÃ¤hrlich: Dateisystem-Servicezugriff und Maske 0xffffffffffffffff (alle Berechtigungen).\nUnsicher/GefÃ¤hrlich sollte nur bei Homebrew, nicht bei offiziellen Spielen vorkommen. Nur fÃ¼r Basistitel und Updates verfÃ¼gbar. Diese Einstufung bewertet Berechtigungen und ist keine vollstÃ¤ndige SicherheitsprÃ¼fung.";
    public string ToolTip_AcidSignature => "Signatur des ACID-Abschnitts in main.npdm, unabhÃ¤ngig von der NCA-Header-Signatur.";
    public string PackageStructure_Title => "Paketstruktur";
    public string PackageStructure_Scene => "Scene-Release";
    public string PackageStructure_Cdn => "CDN-Rip";
    public string PackageStructure_Converted => "Konvertiert";
    public string PackageStructure_Homebrew => "Homebrew";
    public string PackageStructure_Incomplete => "UnvollstÃ¤ndig";
    public string PackageStructure_Unknown => "Unbekannt";
    public string FileInfo_FileSize => "DateigrÃ¶ÃŸe";
    public string FileInfo_CompressionRatio => "KompressionsverhÃ¤ltnis";
    public string FileInfo_Uncompressed => "unkomprimiert";
    public string FileInfo_SystemUpdate => "Enthaltenes System-Update (XCI)";
    public string BatchIntegrity_Export => "CSV exportierenâ€¦";
    public string BatchIntegrity_Start => "PrÃ¼fung starten";
    public string BatchIntegrity_OpenSelected => "In DateiprÃ¼fung Ã¶ffnen";
    public string BatchIntegrity_MoveSelected => "Verschiebenâ€¦";
    public string BatchIntegrity_MoveValid => "Fehlerfreie verschiebenâ€¦";
    public string BatchIntegrity_SelectMoveDestination => "Zielordner fÃ¼r fehlerfreie Dateien auswÃ¤hlen";
    public string BatchIntegrity_Moving => "Verschiebe";
    public string MenuItem_Options => "Optionen";
    public string MenuItem_Settings => "Einstellungen";
    public string MenuItem_ReloadKeys => "Keys Neuladen";
    public string MenuItem_OpenTitleWebPage => "Titel Webseite Ã¶ffnen...";
    public string MenuItem_ShowRenameToolWindow => "Umbenennen...";

    public string Packages_Title => "Multi-Paket Datei";
    public string DisplayVersion => "Display Version";
    public string Presentation_Title => "PrÃ¤sentation";
    public string ToolTip_AvailableLanguages => "Titel, Publisher und Icon kÃ¶nnen sich je nach ausgewÃ¤hlter Sprache Ã¤ndern.";
    public string AvailableLanguages => "Sprachen";
    public string AppTitle => "Titel";
    public string Publisher => "Publisher";
    public string Security_Title => "Programmsicherheit";
    public string Security_Level => "Bewertung";
    public string Security_FileSystemPermissions => "Dateisystemrechte";
    public string Security_AcidSignature => "ACID-Signatur";
    public string Security_Safe => "Sicher";
    public string Security_Unsafe => "Unsicher";
    public string Security_Dangerous => "GefÃ¤hrlich";
    public string Security_Unavailable => "Nicht verfÃ¼gbar";
    public string Security_Details => "Erlaubte Dienste ({0}): {1}";

    public string Lng_AmericanEnglish => "Englisch (US)";
    public string Lng_BritishEnglish => "Englisch (UK)";
    public string Lng_CanadianFrench => "FranzÃ¶sisch (Kanada)";
    public string Lng_Dutch => "NiederlÃ¤ndisch";
    public string Lng_French => "FranzÃ¶sisch";
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
    public string SettingsView_Button_Apply => "Ãœbernehmen";
    public string SettingsView_Button_Cancel => "Abbrechen";
    public string SettingsView_Button_Reset => "ZurÃ¼cksetzen";
    public string SettingsView_GroupBoxKeys => "Keys";
    public string SettingsView_Title_KeysEffectiveFilePath => "Effektiver Pfad";
    public string SettingsView_Title_KeysCustomFilePath => "Benutzerdefinierter Pfad";
    public string SettingsView_Title_KeysDownloadUrl => "Download URL";
    public string KeysValidation_MissingFile => "Keine Datei gefunden.";
    public string KeysValidation_ValidEntries => "GÃ¼ltig ({0} EintrÃ¤ge).";
    public string KeysValidation_MissingMasterKeys => "Fehlende Master-Keys: {0}.";
    public string CnmtOverview_BaseTitleId => "Basis-Titel-ID";
    public string CnmtOverview_MasterKey => "BenÃ¶tigter Master-Key";
    public string CnmtOverview_MinimumApplicationVersion => "Minimale Anwendungsversion (DLC)";
    public string CnmtOverview_Distribution => "Distribution";
    public string KeysValidation_InvalidMasterKeys => "UngÃ¼ltige Master-Keys: {0}.";
    public string KeysValidation_InvalidLines => "Fehlerhafte Zeilen: {0}.";
    public string KeysValidation_EmptyFile => "Die Datei enthÃ¤lt keine gÃ¼ltigen EintrÃ¤ge.";
    public string KeysValidation_FirmwareEstimate => "HÃ¶chste gÃ¼ltige Revision: {0} â€” unterstÃ¼tzt Inhalte bis Firmware {1}.";
    public string KeysValidation_UnsupportedMasterKeys => "Neue Master-Key-Revision erkannt: {0}. Diese Programmversion kann sie noch nicht prÃ¼fen oder einer Firmware zuordnen; ein Programm-Update ist erforderlich.";
    public string SettingsView_ToolTip_Keys => """
                                               Keys sind erforderlich, um verschlÃ¼sselte Nintendo-Switch-Dateien (XCI, NSP, ...) zu Ã¶ffnen.
                                               Jede offizielle Nintendo-Switch-Datei ist mit Keys verschlÃ¼sselt, die spezifisch fÃ¼r die Switch-Firmware-Version sind, fÃ¼r die sie erstellt wurde.

                                               Um jede Nintendo-Switch-Datei ohne Fehler zu Ã¶ffnen, stelle sicher, dass du stets eine aktuelle "prod.keys"-Datei mit den Keys aller bestehenden Firmware-Versionen hast.

                                               Die Datei sollte einen Key pro Zeile enthalten, in der Form Â«KEY_NAME = HEXADECIMAL_WERTÂ».
                                               """;
    public string SettingsView_ToolTip_ProdKeys => """
                                                   Diese Datei enthÃ¤lt allgemeine Keys, die von allen Switch-GerÃ¤ten verwendet werden. Sie wird benÃ¶tigt, um verschlÃ¼sselte Titelinhalte zu Ã¶ffnen.
                                                   Das Programm sucht diese Datei in folgender Reihenfolge:
                                                       1. Im Pfad, der durch diese Einstellung definiert ist
                                                       2. Im Verzeichnis des aktuellen Programms
                                                       3. Im Verzeichnis Â«%UserProfile%\\.switchÂ»

                                                   Beim Start kann das Programm die Keydatei automatisch herunterladen, falls keine auf dem System gefunden wird.
                                                   Die Keydatei wird im Verzeichnis der aktuellen Anwendung gespeichert.
                                                   """;

    public string SettingsView_ToolTip_TitleKeys => """
                                                    Du kannst optional eine Datei angeben, die spielbezogene Keys enthÃ¤lt.
                                                    Das Programm sucht diese Datei in folgender Reihenfolge:
                                                        1. Im Pfad, der durch diese Einstellung definiert ist
                                                        2. Im Verzeichnis des aktuellen Programms
                                                        3. Im Verzeichnis Â«%UserProfile%\\.switchÂ»

                                                    Beim Start kann das Programm die Keydatei automatisch herunterladen, falls keine auf dem System gefunden wird.
                                                    Die Keydatei wird im Verzeichnis der aktuellen Anwendung gespeichert.
                                                    """;

    public string SettingsView_LogFileRetention => "Logdateien behalten (1â€“100 Starts)";
    public string SettingsView_LogLevel => "Protokollierungsgrad ";
    public string SettingsView_ToolTip_LogLevel => "Der Protokollierungsgrad gibt die minimale Ebene an, die protokolliert werden soll.";
    public string SettingsView_CheckBox_AlwaysReloadKeysBeforeOpen => "Keys immer neu laden bevor eine Datei geÃ¶ffnet wird.";
    public string SettingsView_CheckBox_InjectTicketKeys => "Keys aus Ticket-Dateien (*.tik) injizieren";
    public string SettingsView_Title_Language => "Sprache";
    public string SettingsView_Title_Theme => "Design";
    public string SettingsView_Title_NczOptions => "NSZ/XCZ Einstellungen";

    public string SettingsView_ToolTip_NczBlockLessCompression => """
                                                                  NSZ- oder XCZ-Dateien bestehen aus NCZ-Dateien, die NCA-komprimierte Dateien sind.
                                                                  NCZ-Dateien kÃ¶nnen ohne die Blockkomprimierungsmethode komprimiert werden, was effizienten zufÃ¤lligen Lesezugriff unmÃ¶glich macht.
                                                                  Daher ist es bei groÃŸen Dateien, bei denen ein kleiner Teil am Ende gelesen werden muss, notwendig, den gesamten Stream zu dekomprimieren, um den gewÃ¼nschten Teil zu erreichen.
                                                                  GroÃŸe Dateien kÃ¶nnen daher lange zum Ã–ffnen benÃ¶tigen.
                                                                  Verwende vorzugsweise die Blockkomprimierung fÃ¼r groÃŸe Dateien.
                                                                  Beachte, dass es, wenn du das Ã–ffnen von blocklos komprimierten NCZ-Dateien nicht zulÃ¤sst, keine Auswirkungen auf die IntegritÃ¤tsprÃ¼ffunktionen hat.
                                                                  """;

    public string SettingsView_CheckBox_NczOpenBlocklessCompression => "Ã–ffne NCZ-komprimierte Dateien ohne Blockkompression.";
    public string SettingsView_Title_Integrity => "IntegritÃ¤t";
    public string SettingsView_CheckBox_IgnoreMissingDeltaFragments => "Fehlende Delta-Fragmente ignorieren";
    public string SettingsView_ToolTip_IgnoreMissingDeltaFragments => $"""
                                                                      Patch-Dateien kÃ¶nnen vollstÃ¤ndige Update-Dateien und inkrementelle Update-Dateien (bekannt als {ContentType.DeltaFragment}) enthalten.
                                                                      Diese Fragmente sind nicht zwingend erforderlich, um eine Anwendung zu aktualisieren, und werden manchmal absichtlich entfernt.
                                                                      Aktiviere diese Option, wenn du fehlende {ContentType.DeltaFragment} bei der IntegritÃ¤tsprÃ¼fung ignorieren mÃ¶chtest.
                                                                      """;

    public string SettingsView_Miscellaneous => "Sonstiges";
    public string SettingsView_ToolTip_OpenKeysLocation => "Keydateipfad Ã¶ffnen";
    public string SettingsView_ToolTip_BrowseKeys => "Suchen...";
    public string SettingsView_ToolTip_DownloadKeys => "Von bestimmter URL herunterladen.";

    public string BrowseKeysFile_ProdTitle => "WÃ¤hle \"prod\" Keydatei";
    public string BrowseKeysFile_TitleTitle => "WÃ¤hle \"title\" Keydatei";
    public string BrowseKeysFile_Filter => "Keydateien (*.keys)|*.keys|Switch-Spielpakete (*.nsp;*.nsz;*.xci;*.xcz)|*.nsp;*.nsz;*.xci;*.xcz|Archive (*.zip;*.7z)|*.zip;*.7z|Switch-Inhaltsdateien (*.nca)|*.nca|NAND-Abbilder und geteilte Dumps (*.bin;*.img;*.00)|*.bin;*.img;*.00|Alle Dateien (*.*)|*.*";

    public string SuspiciousFileExtension => "Dateierweiterung Â«{0}Â» scheint ungÃ¼ltig zu sein, Â«{1}Â» oder Â«{2}Â» wurde erwartet.";
    public string DragMeAFile => "Ziehe eine unterstÃ¼tzte Datei hierher :)";
    public string MultipleFilesDragAndDropNotSupported => "Mehrere Dateien per Drag & Drop werden nicht unterstÃ¼tzt, nur die erste Datei wird geÃ¶ffnet.";

    public string CnmtOverview_Title => "Paketinformationen";
    public string CnmtOverview_TitleId => "TitelID";
    public string CnmtOverview_ContentType => "Typ";
    public string CnmtOverview_TitleVersion => "Version";
    public string CnmtOverview_MinimumSystemVersion => "Minimale Systemversion";
    public string CnmtOverview_BuildID => "BuildID";
    public string CnmtOverview_BuildID_NotAvailableBecauseSectionIsSparse => "Nicht verfÃ¼gbar (Inhalt spÃ¤rlich)";
    public string CnmtOverview_IsDemo => "Demo";

    public string ContextMenu_SaveImage => "Speichern...";
    public string CopyTitleImageError => "Fehler beim Kopieren der Spieldatei: {0}";
    public string SaveTitleImageError => "Konnte die Spieldatei nicht speichern: {0}";

    public string SaveDialog_Title => "Speichern unter";
    public string SaveDialog_ImageFilter => "Spieldatei";
    public string SaveDialog_AnyFileFilter => "Datei";
    public string SaveFile_Error => "Fehler beim speichern der Datei: {0}";

    public string ContextMenu_CopyImage => "Kopieren";

    public string TabOverview => "Ãœbersicht";
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

    public string LoadingError_DiskFull => "Nicht genÃ¼gend Speicherplatz zum Laden oder Entpacken der Datei. Bitte schaffen Sie freien Speicherplatz auf dem Laufwerk des temporÃ¤ren Ordners und versuchen Sie es erneut. Der Ladevorgang wurde abgebrochen.";
    public string LoadingError_Failed => "Fehler beim laden der Datei Â«{0}Â»: {1}";
    public string LoadingError_FailedToCheckIfXciPartitionExists => "Fehler beim ÃœberprÃ¼fen, ob die XCI Partition existiert: {0}";
    public string LoadingError_FailedToOpenXciPartition => "XCI Partition konnte nicht geÃ¶ffnet werden: {0}";
    public string LoadingError_FailedToLoadXciContent => "XCI Partition konnte nicht geladen werden: {0}";
    public string LoadingError_FailedToOpenPartitionFile => "Fehler beim Ã¶ffnen der Partitionsdatei: {0}";
    public string LoadingError_FailedToLoadNcaFile => "NCA Datei konnt nicht geladen werden: {0}";
    public string LoadingError_FailedToLoadPartitionFileSystemContent => "Fehler beim Laden der Inhalte des Partition-Dateisystems: {0}";
    public string LoadingError_FailedToCheckIfSectionCanBeOpened => "Fehler beim ÃœberprÃ¼fen, ob die Sektion geÃ¶ffnet werden kann.: {0}";
    public string LoadingError_FailedToOpenNcaSectionFileSystem => "NCA Sektionsinhalt konnte nicht geladen werden Â«{0}Â»: {1}";
    public string LoadingError_FailedToLoadSectionContent => "Sektionsinhalt konnte nicht geladen werden: {0}";
    public string LoadingError_FailedToGetFileSystemDirectoryEntries => "Fehler beim Abrufen der VerzeichniseintrÃ¤ge des Dateisystems: {0}";
    public string LoadingError_FailedToOpenNacpFile => "Fehler beim Ã¶ffnen der NACP Datei: {0}";
    public string LoadingError_FailedToLoadNacpFile => "Fehler beim laden der NACP Datei: {0}";
    public string LoadingError_FailedToOpenCnmtFile => "Fehler beim Ã¶ffnen der CNMT Datei: {0}";
    public string LoadingError_FailedToLoadCnmtFile => "Fehler beim laden der CNMT Datei: {0}";
    public string LoadingError_FailedToLoadNcaContent => "Fehler beim laden des NCA Inhalts: {0}";
    public string LoadingError_FailedToLoadDirectoryContent => "Fehler beim Laden des Verzeichnisinhalts: {0}";
    public string LoadingError_FailedToLoadIcon_Log => "Fehler beim laden des Icons: {0}";
    public string LoadingError_NcaFileMissing_Log => "NCA-Eintrag Â«{0}Â» vom Typ Â«{1}Â» fehlt.";
    public string LoadingError_NoCnmtFound_Log => "Kein CNMT Eintrag gefunden!";
    public string LoadingError_NacpFileMissing_Log => "NACP Datei Â«{0}Â» nicht gefunden!";
    public string LoadingError_NcaMissingSection_Log => "NCA mit Inhalts-Typ Â«{0}Â» fehlt eine Sektion vom Typ Â«{0}Â».";
    public string LoadingError_MainFileMissing_Log => "Datei Â«{0}Â» nicht gefunden!";
    public string LoadingError_IconMissing_Log => "Erwartete Icondatei Â«{0}Â» fehlt.";
    public string LoadingError_XciSecurePartitionNotFound_Log => "Sichere Partition der XCI nicht gefunden!";
    public string LoadingError_FailedToGetNcaSectionFsHeader => "Fehler beim Abrufen des NCA-Dateisystem-Headers von Sektion Â«{0}Â»: {1}";
    public string LoadingError_FailedToOpenMainFile => "Fehler beim Ã¶ffnen der Hauptdatei: {0}";
    public string LoadingError_FailedToLoadMainFile => "Fehler beim laden der Hauptdatei: {0}";
    public string LoadingError_FailedToOpenNpdmFile => "main.npdm konnte nicht geÃ¶ffnet werden: {0}";
    public string LoadingError_FailedToLoadNpdmFile => "main.npdm konnte nicht ausgewertet werden: {0}";
    public string LoadingError_FailedToLoadTicketFile => "Fehler beim laden der Ticket Datei: {0}";
    public string LoadingError_FailedToLoadTitleIdKey => "Failed to load TitleID key from ticket file Â«{0}Â»: {1}";
    public string LoadingError_NczBlocklessCompressionDisabled => "Das Ã–ffnen von NCZ Dateien ohne Blockkompression ist in den Einstellungen deaktiviert.";

    public string LoadingInfo_TitleIdKeySuccessfullyInjected => "TitleID Key Â«{0}={1}Â» aus der Ticket-Datei Â«{2}Â» wurde erfolgreich zu den Keys hinzugefÃ¼gt.";
    public string LoadingWarning_TitleIdKeyReplaced => "TitleID Key Â«{0}={1}Â» aus der Ticket-Datei Â«{2}Â» wurde als Ersatz fÃ¼r den bestehenden TitleID Key Â«{0}={2}Â» in den Keys verwendet.";
    public string LoadingDebug_TitleIdKeyAlreadyExists => "TitleID Key Â«{0}={1}Â» aus der Ticket-Datei Â«{2}Â» war bereits in den Keys vorhanden.";

    public string KeysFileUsed => "Die Datei Â«{0}Â» wurde verwendet: {1}";
    public string NoneKeysFile => "[keine]";

    public string Status_DownloadingFile => "Lade Datei Â«{0}Â» herunter...";
    public string Log_DownloadingFileFromUrl => "Lade Â«{0}Â» von der URL Â«{1}Â» herunter...";
    public string Log_FileSuccessfullyDownloaded => "Datei Â«{0}Â» wurde erfolgreich heruntergeladen.";
    public string Log_FailedToDownloadFileFromUrl => "Fehler beim Herunterladen von Â«{0}Â» von der URL Â«{1}Â»: {2}";

    public string ToolTip_PatchNumber => "Patchnummer {0}";
    public string Log_OpeningFile => "=====> {0} <=====";
    public string MainModuleIdTooltip => "Auch bekannt als Â«BuildIDÂ» (oder BID).";
    public string ATaskIsAlreadyRunning => "Ein Task wird bereits ausgefÃ¼hrt...";
    public string FileInfo_Title => "Datei";
    public string Title_FileInfo_FileType => "Typ";
    public string Title_FileInfo_Compression => "Komprimierung";
    public string Title_FileInfo_Integrity => "IntegritÃ¤t";
    public string ToolTip_NcasIntegrity => $"""
                                           Die IntegritÃ¤tsprÃ¼fung besteht darin, die IntegritÃ¤t jeder NCA (oder NCZ) zu Ã¼berprÃ¼fen.

                                           Das Ergebnis der IntegritÃ¤tsprÃ¼fung kann wie folgt aussehen:
                                           {NcasIntegrity_NoNca}: Keine NCA-Datei gefunden.
                                           {NcasIntegrity_Unchecked}: IntegritÃ¤t nicht geprÃ¼ft.
                                           {NcasIntegrity_InProgress}: IntegritÃ¤tsprÃ¼fung lÃ¤uft.
                                           {NcasIntegrity_Original}: Alle NCAs sind original (Signatur und Hash in Ordnung).
                                           {NcasIntegrity_Incomplete}: Alle NCAs sind original, aber einige fehlen.
                                           {NcasIntegrity_Modified}: Mindestens eine NCA ist modifiziert (Signatur nicht in Ordnung, aber Hash in Ordnung).
                                           {NcasIntegrity_Corrupted}: Mindestens eine NCA ist beschÃ¤digt (Hash ungÃ¼ltig).
                                           {NcasIntegrity_Error}: Ein Fehler ist wÃ¤hrend der IntegritÃ¤tsprÃ¼fung aufgetreten.

                                           Details zu jeder analysierten NCA findest du im Tab Â«InhaltÂ».
                                           """;

    public string AvailableContents => "Inhalt:";
    public string MultiContentPackageToolTip => "Das aktuelle Paket enthÃ¤lt mehrere Inhalte (Â«{0}Â» erkannt).";

    public string NcasIntegrity_Error_NcaMissing => "Die IntegritÃ¤t der NCA Â«{0}Â» kann nicht Ã¼berprÃ¼ft werden, NCA fehlt.";
    public string NcasIntegrity_Error_Log => "Fehler bei der ÃœberprÃ¼fung der NCA-IntegritÃ¤t: {0}";
    public string NcaIntegrity_GetOriginalNcaError => "Fehler beim Abrufen der originalen NCA: {0}";
    public string NcaIntegrity_GetOriginalNcaError_Log => "Fehler beim Abrufen der originalen NCA aus der NCA Â«{0}Â»: {1}";

    public string NcaHeaderSignature_Valid_Log => "Die Header-Signatur der NCA Â«{0}Â» ist gÃ¼ltig.";
    public string NcaHeaderSignature_Invalid => "Die ÃœberprÃ¼fung der NCA-Header-Signatur ist mit dem Status Â«{0}Â» fehlgeschlagen.";
    public string NcaHeaderSignature_Invalid_Log => "Die ÃœberprÃ¼fung der Header-Signatur der NCA Â«{0}Â» ist mit dem Status Â«{1}Â» fehlgeschlagen.";
    public string NcaHeaderSignature_Error => "Fehler bei der ÃœberprÃ¼fung der NCA-Header-Signatur: {0}.";
    public string NcaHeaderSignature_Error_log => "Fehler bei der ÃœberprÃ¼fung der Signatur des NCA-Headers Â«{0}Â»: {1}";

    public string NcaHash_VerificationStart_Log => ">>> Die Hash-ÃœberprÃ¼fung der NCAs wird gestartet...";
    public string NcaHash_VerificationEnd_Log => ">>> Die Hash-ÃœberprÃ¼fung der NCAs ist abgeschlossen.";
    public string NcaHash_NcaItem_CantExtractHashFromName => "Fehler beim Extrahieren des erwarteten Hashs aus dem NCA-Namen.";
    public string NcaHash_CantExtractHashFromName_Log => "Fehler beim Extrahieren des erwarteten Hashs aus dem NCA-Namen Â«{0}Â».";
    public string NcaHash_Valid_Log => "Der Hash der NCA Â«{0}Â» ist gÃ¼ltig.";
    public string NcaHash_NcaItem_Invalid => "Hash ist ungÃ¼ltig.";
    public string NcaHash_Invalid_Log => "Der Hash der NCA Â«{0}Â» ist ungÃ¼ltig.";
    public string NcaHash_NcaItem_Exception => "Fehler bei der ÃœberprÃ¼fung des Hashs: {0}";
    public string NcaHash_Exception_Log => "Fehler bei der ÃœberprÃ¼fung des Hashs der NCA Â«{0}Â»: {1}";
    public string NcaHash_ProgressText => "Hashing der NCA {0}/{1}...";

    public string CancelAction => "Abbrechen";
    public string Status_Ready => "Bereit.";
    public string LoadingFile_PleaseWait => "Lade, bitte warten...";

    public string NcasIntegrity_NoNca => "Kein NCA";
    public string NcasIntegrity_Unchecked => "Nicht Ã¼berprÃ¼ft";
    public string NcasIntegrity_InProgress => "In Bearbeitung";
    public string NcasIntegrity_Original => "Original";
    public string NcasIntegrity_Incomplete => "UnvollstÃ¤ndig";
    public string NcasIntegrity_Modified => "Modifiziert";
    public string NcasIntegrity_Corrupted => "Korrupt";
    public string NcasIntegrity_Error => "Fehler";
    public string NcasIntegrity_Unknown => "Unbekannt";

    public string Status_SavingFile => "Speichere Datei Â«{0}Â»...";

    public string KeysLoading_Starting_Log => ">>> Lade Keys...";
    public string KeysLoading_Successful_Log => ">>> Keys erfolgreich geladen.";
    public string KeysLoading_UnusedKey_Log => "Hinweis: ZusÃ¤tzlicher SchlÃ¼ssel Â«{0}Â» wird von dieser Programmversion nicht verwendet.";
    public string KeysLoading_Error => "Keys konnten nicht geladen werden: {0}.";
    public string WarnNoProdKeysFileFound => "Keine Â«prod.keysÂ» Datei gefunden.";
    public string InvalidSetting_KeysFileNotFound => "In den Einstellungen definierte Keydatei Â«{0}Â» existiert nicht.";
    public string InvalidSetting_BufferSizeInvalid => "In den Einstellungen definierte PuffergrÃ¶ÃŸe Â«{0}Â» ist ungÃ¼ltig, Wert sollte grÃ¶ÃŸer 0 sein.";
    public string InvalidSetting_LanguageNotFound => "In den Einstellungen definierte Sprache Â«{0}Â» existiert nicht.";

    public string ToolTip_KeyMissing => "Key Â«{0}Â» vom Typ Â«{1}Â» fehlt.";

    public string MenuItem_CopyTextToClipboard => "Kopieren";
    public string ContextMenu_OpenFileLocation => "Verzeichnis Ã¶ffnen...";
    public string OpenFileLocation_Failed_Log => "Fehler beim Ã–ffnen des Speicherorts der Datei Â«{0}Â»: {1}";
    public string SettingsView_TitlePageUrl => "Titel Seiten-URL";
    public string SettingsView_TitleInfoApiUrl => "Titelinfo-API-URL";
    public string SettingsView_TitleInfoProvider => "Quelle fÃ¼r Titelnamen";
    public string SettingsView_TitleDbRegion => "TitleDB-Region / Sprache";
    public string SettingsView_TitleDbCacheTip => "TitleDB wird lokal gespeichert und tÃ¤glich aktualisiert. Bei AusfÃ¤llen wird der gespeicherte Stand verwendet. Fehlende Titel werden zusÃ¤tzlich in US.en gesucht.";
    public string BatchIntegrity_FileType => "Dateityp";
    public string BatchIntegrity_PackageType => "Pakettyp";
    public string BatchIntegrity_ShowOnlyErrors => "Nur fehlerhafte anzeigen";
    public string OpenTitleWebPage_Failed => "Fehler beim Ã–ffnen der Titel Webseite: {0}";

    public string Log_DownloadFileCanceled => "Herunterladen abgebrochen.";
    public string Log_SaveToDirCanceled => "Speichern des Verzeichnisses abgebrochen.";
    public string Log_SaveFileCanceled => "Speichern der Datei abgebrochen.";
    public string Log_SaveStorageCanceled => "Speicherung des Speichers abgebrochen.";
    public string Log_NcasIntegrityCanceled => "IntegritÃ¤tsprÃ¼fung der NCAs abgebrochen.";

    public string RenamingTool_TargetDirectory => "Zielordner (leer = bisheriger Ordner)";
    public string RenamingTool_FolderTip => "Unterordner im Muster mit / angeben, z. B. DLC/{WTitle}.{Ext:L} oder {WAppTitle}/DLC/{WTitle}.{Ext:L}.";
    public string RenamingTool_OldName => "Alter Name";
    public string RenamingTool_NewName => "Neuer Name";
    public string RenamingTool_StatusError => "Fehler";
    public string RenamingTool_StatusUnchanged => "UnverÃ¤ndert";
    public string RenamingTool_StatusSimulation => "Simulation";
    public string RenamingTool_StatusRenamed => "Umbenannt";
    public string RenamingTool_WindowTitle => "Umbenennungswerkzeug";
    public string RenamingTool_Patterns => "Muster";
    public string RenamingTool_ApplicationPattern => "Anwendungsmuster";
    public string RenamingTool_PatchPattern => "Patch Muster";
    public string RenamingTool_AddonPattern => "Add-on Muster";
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
           Die Simulation zeigt vollstÃ¤ndige Pfade und erstellt keine Ordner.
           Beim Umbenennen werden fehlende Ordner erstellt.
           Vorhandene Zieldateien werden nicht Ã¼berschrieben.

         SchlÃ¼sselwort-Syntax:
            {<Keyword>[:<Format>]}

         Das Format ist optional und kann sein:
         - U: GroÃŸbuchstaben
         - L: Kleinbuchstaben

         Beispiele:
           {Title} => Der original Titel
           {Title:U} => Der Titel in GroÃŸbuchstaben

         UnterstÃ¼tzte SchlÃ¼sselwÃ¶rter:
           â€¢ TitleID:
              - Die Inhalts-ID.
           â€¢ AppID:
              - Die ID der entsprechenden {{nameof(ContentMetaType.Application)}} (fÃ¼r {{nameof(ContentMetaType.Application)}} Inhalte ist dieser Wert gleich der {TitleID}).
           â€¢ PatchId:
              - Wenn der Inhalt eine {{nameof(ContentMetaType.Application)}} ist, entspricht dieser Wert der ID des entsprechenden {{nameof(ContentMetaType.Patch)}} Inhalts, andernfalls ist der Wert null.
           â€¢ PatchNum:
              - Wenn der Inhalt eine {{nameof(ContentMetaType.Application)}} ist, ist der Wert in der Regel 0.
              - Wenn der Inhalt ein {{nameof(ContentMetaType.Patch)}} ist, entspricht der Wert der Patch-Nummer.
              - Wenn der Inhalt ein {{nameof(ContentMetaType.AddOnContent)}} ist, entspricht der Wert der Add-On-Patch-Nummer.
           â€¢ Title:
              - Der erste Titel aus der Liste der deklarierten Titel.
              - Dieser Wert existiert nur fÃ¼r Inhalte vom Typ {{nameof(ContentMetaType.Application)}} oder {{nameof(ContentMetaType.Patch)}}, jedoch nicht fÃ¼r {{nameof(ContentMetaType.AddOnContent)}}.
           â€¢ Ext:
              - Die Erweiterung, die dem erkannten Dateityp entspricht.
           â€¢ VerNum:
              - Die Versionsnummer des Inhalts.
           â€¢ VerDsp:
              - Die angezeigte Version.
           â€¢ WTitle:
              - Der Titel des Inhalts, der aus dem Internet abgerufen wurde.
           â€¢ WAppTitle:
              - Der Titel der entsprechenden {{nameof(ContentMetaType.Application)}}, der aus dem Internet abgerufen wurde.

         Verwende  \{ oder \}, um die Zeichen { oder } zu schreiben.
         """;
    public string RenamingTool_ToolTip_BasePattern => $"Das Muster, das fÃ¼r Inhalte des Typs {nameof(ContentMetaType.Application)} verwendet werden soll.";
    public string RenamingTool_ToolTip_PatchPattern => $"Das Muster, das fÃ¼r Inhalte des Typs {nameof(ContentMetaType.Patch)} verwendet werden soll.";
    public string RenamingTool_ToolTip_AddonPattern => $"Das Muster, das fÃ¼r Inhalte des Typs {nameof(ContentMetaType.AddOnContent)} verwendet werden soll.";
    public string RenamingTool_Button_Cancel => "Abbrechen";
    public string RenamingTool_Button_Rename => "Umbenennen";
    public string RenamingTool_GroupBoxInput => "Eingabe";
    public string RenamingTool_GroupBoxNamingSettings => "Benennungseinstellungen";
    public string RenamingTool_BrowseDirTitle => "Verzeichnis auswÃ¤hlen";
    public string RenamingTool_GroupBoxOutput => "Ausgabe";
    public string RenamingTool_Miscellaneous => "Sonstiges";
    public string RenamingTool_InvalidWindowsCharReplacement => "Ersetze unzulÃ¤ssige Windows-Zeichen durch";
    public string RenamingTool_ReplaceWhiteSpaceChars => "Ersetze Leerzeichen-Zeichen";
    public string RenamingTool_ReplaceWhiteSpaceCharsWith => "Ersetze Leerzeichen-Zeichen durch";
    public string RenamingTool_Simulation => "Simulation";
    public string RenamingTool_AutoCloseOpenedFile => "GeÃ¶ffnete Datei automatisch schlieÃŸen";
    public string RenamingTool_IncludeSubDirectories => "Unterverzeichnisse einbeziehen";
    public string RenamingTool_ContentTypeNotSupported => "Inhaltstyp Â«{0}Â» wird nicht unterstÃ¼tzt.";
    public string RenamingTool_SuperPackageNotSupported => "Super-Paket wird nicht unterstÃ¼tzt.";
    public string RenamingTool_LogNbFilesToRename => ">>> {0} Datei(en) zum Umbenennen...";
    public string RenamingTool_LogSimulationMode => $"[SIMULATION] ";
    public string RenamingTool_LogFileRenamed => $"â€¢ {{0}}Datei wurde umbenannt von{Environment.NewLine}\tÂ«{{1}}Â» zu{Environment.NewLine}\tÂ«{{2}}Â».";
    public string RenamingTool_LogFileAlreadyNamedProperly => "â€¢ {0}Â«{1}Â» ist bereits korrekt benannt.";
    public string RenamingTool_LogFailedToRenameFile => "â€¢ {0}Â«{1}Â»Umbenennen fehlgeschlagen: {2}";
    public string RenamingTool_LogRenamingFailed => "Umbenennen fehlgeschlagen: {0}";
    public string RenamingTool_BadInvalidFileNameCharReplacement => "Die Ersetzungszeichenfolge Â«{0}Â» (fÃ¼r ungÃ¼ltige Dateinamenzeichen) darf das ungÃ¼ltige Zeichen Â«{1}Â» nicht enthalten.";

    public string Exception_UnexpectedDelimiter => "Unerwarteter Trenner {0} an Position {1} gefunden, verwende stattdessen {2}{0}.";
    public string Exception_EndDelimiterMissing => "End-Trenner {0} fehlt.";
    public string FileRenaming_PatternKeywordUnknown => "SchlÃ¼sselwort Â«{0}Â» ist unbekannt, erlaubte SchlÃ¼sselwÃ¶rter sind Â«{1}Â».";
    public string FileRenaming_EmptyPatternNotAllowed => "Muster darf nicht leer sein.";
    public string FileRenaming_PatternKeywordNotAllowed => "SchlÃ¼sselwort Â«{0}Â» ist fÃ¼r Muster vom Typ Â«{1}Â» nicht erlaubt.";
    public string FileRenaming_StringOperatorUnknown => "Operator Â«{0}Â» wird nicht erkannt, erlaubte Operatoren sind Â«{1}Â».";
    public string FileRenaming_EmptyDirectoryNotAllowed => "Eingabeverzeichnis darf nicht leer sein.";
    public string Window_Tip_Title => "Hinweis";
    public string Nsz_Installed => "Plugin nicoboss/nsz installiert";
    public string Nsz_CustomExecutable => "Benutzerdefinierte EXE";
    public string Settings_Program => "Programm";
    public string Nsz_PhaseSource => "Quelldatei prÃ¼fen";
    public string Nsz_PhaseOutput => "Ergebnis prÃ¼fen";
    public string Nsz_PhasePublish => "Ergebnis Ã¼bernehmen";
    public string Batch_IncludeArchives => "ZIP / 7z einbeziehen";
    public string Batch_Scan => "Dateiliste einlesen";
    public string Batch_VerifyAll => "Alle auf IntegritÃ¤t prÃ¼fen";
    public string File_SaveBackupSuspected => "Spielstand-Backup (vermutet)";
    public string Batch_MultiPackageDetails => "Enthaltene Pakete anzeigen oder ausblenden";
    public string File_SaveBackup => "Spielstand-Backup";
    public string File_MissingKeys => "Passende SchlÃ¼ssel fehlen. Inhalte kÃ¶nnen nicht vollstÃ¤ndig gelesen werden. Bitte prod.keys / title.keys prÃ¼fen.";
    public string File_CopyMissingKeys => "Fehlende SchlÃ¼sselnamen kopieren";
    public string Keys_ProgramFolder => "Programmordner";
    public string Keys_SharedFolder => "Benutzerprofil (.switch)";
    public string Keys_InUse => "Wird verwendet";
    public string Keys_DownloadAll => "Keys herunterladen";
    public string Keys_DownloadHost => "Download-IP / Hostname";
    public string Keys_DownloadHostTip => "{IP} in den Download-URLs wird durch diese Adresse ersetzt. Ziel: benutzerdefinierter Pfad, falls gesetzt, sonst Programmordner.";
    public string Keys_CopyToSwitch => @"Aktuelle Keys nach %USERPROFILE%\.switch kopieren";
    public string Keys_ReplaceShared => "Vorhandene Keys ersetzen? Die Quelldateien bleiben erhalten.";
    public string Keys_SharedCopied => "Keys sind im gemeinsamen .switch-Ordner verfÃ¼gbar.";
    public string Batch_ScanAndVerify => "Dateiliste einlesen und IntegritÃ¤t prÃ¼fen";
}
