# Changelog

All notable changes to NxFileViewer will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [4.0.0-beta.1] - 2026-10-06

- Individual archive entries retain extracted files, loaded content and view models until close/reopen, avoiding repeated ZIP/7z decompression when switching back. Batch types/CSV include archive origin (e.g. NSP (ZIP)), with package/container filtering.

- Firmware verification recognizes incorrectly named NCAs by size and SHA-256, reports actual → expected filenames instead of counting each pair as missing plus extra, and can identify fully renamed sets. Source files are not changed; filename errors remain flagged.

- Application updates optionally include published pre-releases, with persisted opt-in, beta/RC tag support, explicit preview labels, paginated release selection and channel-change invalidation. Stable-only remains the default.

- Added managed 7z archive support in individual/batch mode, automatic single-mode firmware ZIP/7z verification, and SHA-256 identification of individual firmware NCAs including renamed/shared files and all matching firmware versions.

- Batch selection automatically switches to Overview for packages and Firmware verification for firmware results; tabs can still be selected manually.

- Fixed horizontal scrolling with additional batch columns; Shift + mouse wheel scrolls horizontally. Scrollable overview panel, first-start maximized window with saved placement respected, Settings sub-tabs (Program/Updates/Plugins), explicit installed plugin status/path, and phased NSZ progress with CLI percentage/speed/ETA when available.

- Batch table: selectable overview columns (titles, IDs, publisher, versions, firmware, master key, build ID, distribution, languages, size and ratio), column sorting/reordering, combined search/type/integrity filters and reset controls. CSV exports the filtered, sorted results including overview metadata. Column choices persist while navigating within the current session.

- NSZ compression-ratio display caches the reconstructed size instead of rescanning the file tree on each selection. Breadth-first tree traversal uses a queue to avoid quadratic list shifting for packages with many files.

- Batch result selection reuses the overview captured during verification. Switching back to NSZ/ZIP entries never reloads or decompresses the source, and remains available after source files are moved or deleted.

- ZIP package support: individual mode opens NSP/NSZ/XCI/XCZ/NCA entries with an in-window entry selector; batch mode checks each NSP/NSZ/XCI/XCZ entry separately. Standalone NCA loading and signature/hash checks. Temporary extraction stays in the program directory and is removed when closed; archive entries are excluded from conversion and move operations.
- Start page displays prod.keys validation problems with links to settings and key reload.

- Added central Updates page for application, NSZ plugin, GitHub TitleDB and firmware references. Forced TitleDB refresh replaces only validated catalogs and clears cached title responses. Firmware checks remain memory-only; explicit offline updates preserve the previous reference folder as a backup.

- Separate Plugins workspace page with NSZ settings, status and update/rollback controls. Independent plugin/general drafts prevent saving one from overwriting settings saved in the other.

- Optional automatic startup check for regular NxFileViewer GitHub Releases, manual checks and download/install/restart action. SHA-256, EXE version and architecture verification, EXE backup and early-start recovery; keys, settings and plugins are retained.

- NSZ settings: selectable Automatic/Solid/Block compression and block sizes from 16 KiB to 4 GiB. The CLI receives the viewer’s actual prod.keys path; compression options are omitted during decompression.

- Unified main window with Start, File verification, Batch verification/Firmware, Renaming and Settings pages. Menu entries and tabs switch pages while retaining results, settings drafts and running tasks. Settings Apply/Cancel no longer closes a separate window.

- NSZ-Konvertierung: vorhandene Ziele ersetzen, nummerieren oder abbrechen; Quelldateien auf Wunsch erst nach erfolgreicher Prüfung löschen. Die Stapelübersicht folgt der ausgewählten Datei und verwendet bereits geladene Metadaten ohne erneute NSZ-Dekomprimierung.

### Added

- Firmware verification for ZIP archives and folders of loose NCAs using bundled `fw/hashes/*.json` size and SHA-256 references, without keys or extraction.
- Firmware checks fetch current hash references from GitHub only for detected or explicitly selected firmware, with no disk cache and a visible bundled-reference fallback during outages. Repeated firmware checks fetch again; pure game folders make no request.
- Automatic firmware detection in folder integrity checks, firmware version/result details, and CSV export. Missing, changed, extra and duplicate NCA entries fail verification.
- Selectable title metadata sources: Tinfoil, regional GitHub TitleDB with persistent offline cache, and configurable NLib API.
- Firmware mapping for master-key revision 0x16 to 23.0.0. Its CRC32 reference was derived from a local key file and is not an independent upstream confirmation.

- Added validation results for `prod.keys` and `title.keys` to the settings window.
- Added detection and reporting of missing `master_key_XX` revisions in outdated `prod.keys` files.
- Added CRC32-based validation of known master-key revisions through `master_key_16`.
- Added malformed-line detection for key files.
- Added structural validation of Rights ID and title-key pairs in `title.keys`.
- Added an estimate of the newest supported firmware based on the highest valid master-key revision.
- Added a warning when a key file contains a newer master-key revision that this application version cannot validate or map to a firmware.
- Added localized validation messages for English, German, French, and Spanish.
- Added automated tests for key-file validation, firmware mapping, and unknown master-key detection.
- Added anonymous FTP support to the existing HTTP/HTTPS key downloader.
- Added editable default Sphaira FTP locations for `prod.keys` and `title.keys`; customized addresses continue to be stored in the application settings.
- Downloads now use a temporary file and only replace the destination after a successful transfer.
- Added parsing of `main.npdm` from program NCAs.
- Added a program-security assessment based on signed ACID file-system permissions, including safe, unsafe, and dangerous classifications.
- Added the ACID signature state, raw permission mask, and authorized service list to the program details.
- Added base title ID, required master-key revision/key generation, minimum application version for DLC, and NCA distribution type to the title overview.
- Added package-structure detection for Scene releases, CDN rips, converted packages, Homebrew packages, and incomplete NSP/NSZ/XCI/XCZ files.
- Added package file size and an estimated NSZ/XCZ compression ratio with reconstructed uncompressed size.
- Added detection of the system-update version contained in an XCI/XCZ update partition.

### Fixed

- Solid NCZ reads now cache the decoded prefix in a temporary file, avoiding complete decompression restarts on backward/random reads and improving the likely bottleneck behind issue #52.
- Block NCZ decoding respects compressed block boundaries; block lookup, short reads, EOF handling, zero-length reads and disabled caching are corrected or covered by regression tests.
- Online rename failures (including HTTP 503) fall back to the local NACP title when available.
- Unrecognized additional key names are reported as localized informational notices; malformed key data remains a warning.

- Prevented a double release of the LibHac `main.npdm` file while loading program-security information.
- Added a defensive size limit so malformed NPDM files fail safely instead of destabilizing file loading.
- Snapshot NPDM security data inside the protected parser block so UI bindings cannot trigger delayed parser failures.
- Batch preview errors no longer replace otherwise valid package and integrity results with `Unknown`.
- Exception details and stack traces are now included in the application log for actionable diagnostics.
- Fixed a crash when the program-security fields were displayed by explicitly using one-way WPF bindings for read-only values.
- Batch integrity results now include the underlying NCA error and identify broken NSZ/NCZ Zstandard streams as corrupted or incomplete compressed data.
- Batch integrity results and CSV exports now include the detected package structure.

### Notes

- ZstdSharp.Port remains at 0.8.8; compressed NACP titles remain supported.
- Firmware references ship with the application and work offline. ZIPs repacked with different compression are valid if all NCA contents match. Local `/test/` data is excluded from Git.
- Validation: solution build and 94 automated tests (including nine firmware tests). Real firmware archives were not used for these tests.

- The firmware shown for `prod.keys` is the newest content firmware supported by its keys. The exact firmware on which the file was dumped cannot be determined from the key file.
- `title.keys` can be checked for valid structure, but title-key values cannot be compared against a universal list of expected values.

## [3.0.3]

Dieses Release erweitert NxFileViewer um eine Stapelprüfung, modernisiert die Oberfläche und übernimmt Verbesserungen aus verschiedenen Community-Forks.
Neue Stapel-Integritätsprüfung
- Ganze Ordner mit Nintendo-Switch-Dateien prüfen
- Unterstützt NSP, NSZ, XCI und XCZ
- Unterordner optional einbeziehen
- Ergebnisse erscheinen während der laufenden Prüfung
- Anzeige von:
  - Dateityp
  - Pakettyp
  - Komprimierung
  - Integritätsstatus
  - Fehlerbeschreibung
  - vollständigem Dateipfad
- Filter „Nur fehlerhafte anzeigen“
- Aktuell geprüfte Datei mit eigener Übersicht rechts
- Fortschrittsanzeige und Statusleiste
- Prüfung kann abgebrochen werden
- Ergebnisse als CSV exportieren
- Zuletzt verwendeten Stapelordner speichern
- Fehlerfreie Dateien in einen auswählbaren Zielordner verschieben
- Unterordnerstruktur bleibt beim Verschieben erhalten
- Vorhandene Zieldateien werden nicht überschrieben
- Fehlerhafte Dateien werden niemals automatisch gelöscht oder verschoben
Oberfläche und Einstellungen
- Dark-, Light- und System-Theme
- Dunkle Titelleisten für Hauptfenster, Einstellungen, Umbenennen und Stapelprüfung
- Fensterposition und Fenstergröße werden gespeichert
- Verbesserte Darstellung des Stapelfensters im Dark Mode
- Tinfoil-Titelseiten-URL und Tinfoil-API-URL separat konfigurierbar
- Aktualisierung von tinfoil.media auf tinfoil.io
NACP-Verbesserungen
- Unterstützung komprimierter NACP-Titelblöcke
- Unterstützung von bis zu 32 Sprachen statt bisher 16
- Erweiterte Anzeige von NACP-Informationen
Umbenennen
- GitHub-Issue #46 behoben
- Fehlende Leerzeichen bzw. Trennzeichen in online abgerufenen Titeln werden beim Umbenennen korrigiert
- Normalisierung fehlerhaft zusammengesetzter Dateinamen verbessert
Abhängigkeiten und Build
- Projekt auf aktuelle .NET-8-kompatible Pakete aktualisiert
- ZstdSharp.Port aktualisiert
- Microsoft.Extensions-Pakete aktualisiert
- Test-SDK, xUnit und Coverlet aktualisiert
- LibHac bleibt vorerst auf Version 0.19.0, da kein vollständig kompatibler direkter Ersatz verfügbar ist

Vielen Dank an das ursprüngliche NxFileViewer-Projekt und alle Community-Mitwirkenden.
