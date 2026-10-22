# Protokollierung

Die allgemeine Mindeststufe steht in den Einstellungen. Unter Plugins â†’ Protokollstufen der Plugins kann sie fÃ¼r nicoboss/nsz, NxNandManager, MTP und NSC_BUILDER/nscb_rust ersetzt werden. Standard ist â€žAllgemeine Einstellung Ã¼bernehmenâ€œ. â€žAusâ€œ unterdrÃ¼ckt die zugeordneten Pluginmeldungen vollstÃ¤ndig. Die Filter gelten fÃ¼r die Logansicht und die gespeicherte Sitzungsdatei. Der Sitzungskopf mit Version und Startzeit bleibt erhalten.

Beispiel: Allgemein â€žWarningâ€œ, NSZ â€žDebugâ€œ zeigt detaillierte NSZ-Prozessausgaben, wÃ¤hrend das Ã¼brige Programm nur Warnungen und Fehler protokolliert. Die Zuordnung erfolgt anhand der Loggerkategorie des Plugins. Meldungen gemeinsam verwendeter Lese- und IntegritÃ¤tsdienste behalten die allgemeine Stufe, auch wenn ein Plugin den Vorgang gestartet hat. Dialoge und Fortschrittsanzeigen werden nicht ausgeblendet.

- Trace/Debug: technische Details; NSZ-Rohausgaben unter Debug.
- Information: Start, erfolgreicher Abschluss, erfolgreiches Umbenennen, bewusster Abbruch.
- Warning: optionale Logos fehlen oder lassen sich nicht darstellen; EinschrÃ¤nkungen und verfÃ¼gbare Ausweichwege.
- Error: angeforderte Aktion fehlgeschlagen, erforderliche Inhalte fehlen oder eine IntegritÃ¤tsprÃ¼fung schlÃ¤gt fehl.
- Critical: schwerwiegender Ausfall; wird unterstÃ¼tzt und lÃ¶st wie Error die Fehleranzeige aus. Es werden keine normalen Dateifehler kÃ¼nstlich auf Critical hochgestuft.

Eine Mindeststufe schlieÃŸt alle hÃ¶heren Schweregrade ein. â€žNoneâ€œ ist keine auszugebende Meldungsstufe.

## Trace und RAM-Diagnose

Bei allgemeiner Stufe Trace schreibt das Programm sofort und dann alle zehn Sekunden passive RAM-, Heap- und GC-Zähler ins Sitzungslog. In der Stapelansicht werden zusätzlich die Speicherdiagnose, CSV-Messungen an Scan-/Verlaufsereignissen und der manuelle GC-Button freigeschaltet. Ein Trace-Filter nur für ein Plugin aktiviert die globale RAM-Messung nicht. Automatische Messungen erzwingen keine Speicherbereinigung. Details: [Memory diagnostics](Memory-diagnostics.md).

Die sichtbare Logansicht hält höchstens 2.000 Einträge; ältere Einträge bleiben in der Sitzungsdatei. Die Überschrift der aktiven Dateigruppe bleibt bei der Begrenzung erhalten.
