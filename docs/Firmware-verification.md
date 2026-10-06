# Firmware-Prüfung

Included in NxFileViewer 3.0.4.

Unter Werkzeuge → Ordnerintegrität prüfen einen Ordner wählen oder **Firmware-ZIP auswählen…** verwenden. Die Ordnerprüfung erkennt lose Firmware-NCAs und Firmware-ZIPs auch in Unterordnern, wenn diese Option aktiviert ist. Die Ergebniszeile zeigt Firmware, die erkannte Version in der Struktur-Spalte und den Integritätsstatus. Der Reiter Firmware-Prüfung enthält Details; der CSV-Export enthält diese ebenfalls.

Referenzen sind die im Repository vorhandenen `fw/hashes/*.json` (Quelle: https://github.com/sandmaennchen5/fork_NxFileViewer/tree/master/fw/hashes). Sie werden bei Entwicklungsbuilds nach `fw/hashes` kopiert und beim Veröffentlichen als separates, optionales Hashlisten-ZIP angeboten und dienen als Offline-Rückfall. Wenn lose NCA-Dateien oder ZIPs mit NCA-Einträgen gefunden werden oder ausdrücklich ein ZIP ausgewählt wird, lädt die Prüfung alle aktuellen JSON-Referenzen von GitHub. Reine Spieleordner lösen keine Abfrage aus. Die Referenzen bleiben nur im Arbeitsspeicher dieses Prüflaufs: kein Festplatten-Cache. Jeder Firmware-Prüflauf fragt erneut ab. Änderungen während des Downloads werden anhand der Git-Blob-Hashes erkannt; bei Fehlern oder Zeitüberschreitung werden vollständig die mitgelieferten Listen verwendet. Der Firmware-Reiter zeigt die verwendete Quelle bzw. den Offline-Hinweis. Auch neue Dateinamen werden als Kandidaten erkannt. Lokale Hashlisten sind optional: Fehlen sie und ist GitHub nicht verfügbar, werden nur die betreffenden Firmware-Kandidaten als nicht prüfbar gemeldet; Spielepakete werden weiter geprüft. Die x64-/x86-Anwendungs-ZIPs enthalten keine Hashlisten. Das Zusatz-ZIP `NxFileViewer_v<version>_firmware-hashes.zip` in den Ordner der EXE entpacken: Es legt `fw/hashes/*.json` an und passt zu beiden Architekturen. GitHub Actions bietet alle drei ZIPs einzeln an.

Alle NCA-Dateien werden anhand ihrer Größe und SHA-256 geprüft. ZIPs werden direkt gelesen, ohne Entpacken. Ein anderes ZIP-Kompressionsformat ändert das Ergebnis nicht. Fehlende, veränderte, zusätzliche NCAs und doppelte ZIP-Dateinamen führen zu einem Fehler. Nicht-NCA-Dateien (z. B. README) werden ignoriert. ZIP-Unterordner werden über die Dateinamen zugeordnet; lose Dateien werden je Ordner geprüft, nicht über mehrere Ordner zusammengemischt.

Versionen werden anhand der Dateien erkannt. Bei unvollständiger Firmware ist die angezeigte Version die am besten passende Referenz; sie ist kein Nachweis einer vollständigen Version. Nur ein vollständiger Treffer wird als Original angezeigt. Identische Referenzen werden gemeinsam angezeigt. Unbekannte ZIPs können ausdrücklich geprüft werden, liefern aber keinen positiven Nachweis. Automatische Ordnererkennung benötigt mindestens einen bekannten Firmware-Dateinamen. Firmware-Ergebnisse sind vom Verschieben gültiger Spielepakete ausgeschlossen.

Tests: vollständige Ordner, neu gepackte ZIPs mit Unterordnern, fehlende/veränderte/zusätzliche NCAs, doppelte Dateinamen, unbekannte Dateien, Abbruch und Verfügbarkeit der mitgelieferten Referenzen. Es wurden synthetische Dateien verwendet, keine echten Firmware-Archive.

## Firmware hashes on the Updates page

**Check online hashes** loads and validates the current GitHub reference set in memory without saving it. The result shows its reference-file count. It does not check firmware files themselves; use Batch verification for that. Firmware checks continue fetching fresh online data for each check.

**Update offline hashes** is an explicit manual action that downloads and validates the complete set before replacing `fw/hashes` next to the program. The existing folder is retained as `fw/hashes-backup-<id>`. Download/validation failures and cancellation before publication retain the current folder. Keys and plugins are untouched. Offline references are still only a fallback when online loading fails. No automatic firmware cache or AppData fallback is introduced.

## Single-file mode and 7z

Opening an NCA-only ZIP or 7z in individual mode verifies the complete set against firmware references and opens the Firmware verification tab. The matched version(s), matching/missing/changed/extra/duplicate counts and reference source are displayed. Incomplete sets show the nearest candidate and discrepancies, rather than claiming a complete firmware match. References are fetched from GitHub into memory only for firmware candidates and individual NCAs, with bundled/offline fallback when available. Missing references produce an explicit unchecked result.

Opening an individual NCA compares its SHA-256 and size to reference entries. Renamed files are recognized by content. Shared files list all matching firmware versions: a single NCA cannot establish which one it originally came from, nor verify a complete firmware installation. Recognized firmware NCAs require no prod.keys. Unknown NCAs retain the ordinary LibHac loading path and show the firmware lookup result separately.

ZIP and 7z package archives expose NSP/NSZ/XCI/XCZ/NCA entries in individual mode; batch mode checks all NSP/NSZ/XCI/XCZ entries and NCA-only firmware archives. Solid 7z firmware is streamed sequentially without extraction. Package extraction uses unique temporary directories beside the executable and removes them when closed. Password-protected and multipart archives have no dedicated input workflow.

7z support uses [SharpCompress 0.50.3](https://github.com/adamhathcock/sharpcompress/tree/0.50.3), under its MIT license; an external 7-Zip installation is unnecessary. Existing ZstdSharp.Port 0.8.8 remains unchanged.

## Incorrect NCA filenames

Firmware set verification pairs missing reference filenames with additional NCAs only when size and SHA-256 both match. It reports the actual name → expected name as an incorrect filename, removing the pair from missing/extra counts. Each physical file is used at most once; a duplicate copy cannot satisfy multiple missing files. Version selection also considers these content matches, so a fully renamed set can still be identified. Filename similarity alone never establishes a match.

Incorrect filenames remain an integrity error because the firmware set still needs its expected filenames. Verification does not rename or modify the source. Changed content, genuinely missing/extra NCAs and duplicate archive basenames remain errors.

## Open archive lifetime

In individual mode, each visited ZIP/7z package keeps its extracted file and loaded model for the lifetime of the open archive. Returning to that entry reuses the loaded model and does not reread or decompress the archive. Entries are extracted on first selection, rather than eagerly unpacking the entire archive. Overview/content view models are also reused for the same loaded file.

Closing the file, opening a different file/archive, explicitly reopening the archive, or closing the program disposes retained package handles and removes their temporary directories. A failed entry load preserves the currently open archive and its retained entries. Closing during an unfinished load prevents the completed load from reopening the file. Changes made to the source archive while it is open become visible after closing and reopening it.

Batch package rows include archive origin in the file type, such as `NSP (ZIP)` or `NSZ (7z)`, including failed checks and CSV export. The type filter matches either the package type (NSP/NSZ/XCI/XCZ) or the container (ZIP/7z). Batch overview selection continues to reuse completed metadata.
