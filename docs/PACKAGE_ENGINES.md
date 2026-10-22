# Paket-Engines

Unter **Plugins → Zusammenführen / Inhalte aufteilen / NSP ↔ XCI** können zwei
Backends parallel eingerichtet werden:

- **cxfcxf / nscb_rust:** Pfad zu `nscb_rust.exe`.
- **Kronos / NSC_BUILDER:** portabler Ordner mit `runtime/python.exe` und
  `ztools/squirrel.py`, beispielsweise das lokal erstellte Kronos-Testpaket.

Nach Auswahl der Engine und ihrer Pfade **Übernehmen** drücken. Der angezeigte
Status und die Aktionsbuttons verwenden die gespeicherten Einstellungen.
Download und automatische Updates dieser Engines sind noch nicht integriert.
Die bestehende NSZ-Anbindung bleibt für Kompression und Dekompression zuständig.

## Aktionen

- Zusammenführen nach NSP oder XCI: mehrere lokale NSP/XCI-Dateien auswählen.
- Konvertieren nach NSP oder XCI: eine lokale NSP/XCI-Datei auswählen.
- Inhalte aufteilen: eine Multi-Content-Datei in einzelne NSP-Dateien aufteilen.
  Dies ist kein FAT32-Dateisplit.

Pro Title-ID darf nur eine Version ausgewählt werden. Die Quelldateien bleiben
unverändert. Der Hintergrundauftrag kann abgebrochen werden und behält die zu
Beginn gewählte Engine, auch wenn später andere Einstellungen gespeichert werden.

Die Engines schreiben zuerst in einen eigenen temporären Arbeitsordner im
gewählten Ausgabeordner. Der Viewer prüft die NCA-Integrität sowie die Menge der
Titel-IDs und Versionen vor und nach der Verarbeitung. Geänderte Signaturen bei
weiterhin gültigen Hashes sind für Repacking zulässig. Fehlende oder beschädigte
Inhalte verhindern die Veröffentlichung. Nur die Paketdateien werden in einem
neuen `NXFV-<Aktion>-<ID>`-Ordner veröffentlicht. Vorhandene Ergebnisse werden
nicht überschrieben. Eine automatische Firmware-/Schlüsselgeneration-Absenkung
ist nicht eingeschaltet.

Kronos erhält eine eigene Kopie seiner Arbeitsdateien und der benötigten
Schlüsseldatei im temporären Auftrag; die Installation selbst wird nicht
umkonfiguriert. Der Arbeitsordner wird nach Abschluss oder Abbruch bereinigt.
Zusätzlicher Platz für die Arbeitskopie und das Ergebnis ist erforderlich.

## Prüfung

Automatisiert geprüft sind Einstellungen/JSON, Erhalt separat gespeicherter
Plugin-Einstellungen, CLI-Parameter, Abbruch vor Start sowie Ablehnung ungültiger
Quellen ohne Änderung der Originaldateien. Der Anwendungsbuild wird geprüft.
Die tatsächlichen Merge-/Split-/Convert-Ergebnisse beider Engines müssen noch
mit repräsentativen NSP/XCI-Dateien und den dazugehörigen Schlüsseln praktisch
verglichen werden.

Bei der Veröffentlichung werden ausschließlich die geprüften Paketdateien einzeln
verschoben; ein Umbenennen des Arbeitsordners ist nicht erforderlich. Vorübergehende
Dateisperren werden kurz wiederholt. Bei einem dauerhaften Fehler bleiben die
geprüften Daten erhalten; die Fehlermeldung nennt die betroffenen Ordner. Eine
teilweise veröffentlichte Ausgabe enthält `NXFV-INCOMPLETE.txt`. Private
Engine-Arbeitsdateien werden weiterhin bereinigt.
