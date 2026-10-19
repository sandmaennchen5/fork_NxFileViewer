# NxFileViewer Fork LogViewer

Eigenständiger Windows-Logviewer für Diagnoseprotokolle. NxFileViewer, Plugins und Schlüsseldateien werden nicht benötigt. ZIP entpacken und `NxFileViewerFork.LogViewer.exe` starten. Die Variante `self-contained` enthält .NET; die Standardvariante benötigt die .NET 8 Desktop Runtime.

- Logdatei über Öffnen / Strg+O, Drag-and-drop oder als erstes Kommandozeilenargument laden.
- Info blau, Warnungen gelb, Fehler und Critical rot; Dateigruppen wie in der Logseite des Hauptprogramms.
- Suche ohne Beachtung der Groß-/Kleinschreibung in Meldungen, Zeitstempeln, Logstufen und zugeordneten Dateipfaden.
- Mindest-Logstufe und optional nur RAM-/GC-Messungen; alle Filter wirken zusammen. Bei ausgefilterten Öffnungsmeldungen bleiben die zugehörigen Dateigruppen sichtbar.
- Mehrzeilige Ausnahmen bleiben ein Eintrag. Der untere Detailbereich zeigt den vollständigen Text und erlaubt Auswahl/Kopieren; Strg+C in der Liste kopiert den ausgewählten Eintrag samt Dateikontext.
- F5 / Neu laden liest den aktuellen Stand einer noch laufenden Sitzung ein. Es gibt keine automatische Überwachung.
- Die Liste ist virtualisiert und hat keine 2.000-Einträge-Grenze. Die Datei wird im Hintergrund eingelesen; ihr Text bleibt für die Suche im RAM. Große Dateien benötigen entsprechend Speicher.

Der Viewer liest `.log`/`.txt` direkt und verändert sie nicht. Die ZIP-Datei ist das portable Programmpaket. Sprache: Deutsch bei deutscher Windows-Anzeigesprache, sonst Englisch.

## Build

`./Publish.ps1` erstellt zusätzlich separate LogViewer-ZIPs für x64/x86, jeweils mit und ohne .NET-Laufzeit. Nur den Viewer bauen:

```powershell
.\Publish.ps1 -LogViewerOnly
```

Mit `-SkipLogViewer` werden diese Zusatzpakete ausgelassen. Dateinamen: `NxFileViewerFork.LogViewer_v<VERSION>[_self-contained]_<ARCH>.zip`.

## English

Extract the standalone ZIP and start `NxFileViewerFork.LogViewer.exe`. Open or drop a `.log`/`.txt` file; Ctrl+O opens, Ctrl+F focuses search and F5 reloads. Search, minimum severity and RAM/GC filters combine. File headings survive filtering and multiline exceptions stay together. Use the lower pane or Ctrl+C in the list to copy a complete entry. The viewer reads files only and does not watch them automatically. Self-contained packages include .NET; standard packages require .NET 8 Desktop Runtime. The visible list is virtualized, but the file text remains in memory for searching.
