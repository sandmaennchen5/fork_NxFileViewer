# Changelog

All notable changes to NxFileViewer will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [4.0.0-beta.2] - 2026-10-07

### Added

- Save the last five batch sessions and resume interrupted scans/checks using source fingerprints; history can be disabled in Settings → Program.
- Support nested ZIP/7z archives up to eight levels and mixed archives containing game packages, firmware archives and firmware folders.
- Add a renaming result table, an optional target directory and relative pattern subfolders, without overwriting occupied targets.
- Add batch naming checks and naming-status/proposed-path/error columns. Individual checks show a status dialog; individual renaming requires Yes/Cancel confirmation.
- Write immediately flushed UTF-8 session logs with redacted key values and configurable retention of 1–100 launches (default 5).

### Changed

- Consolidate navigation into workspace tabs and file-toolbar actions, with matching icons, Start/Info logos and an automatically scrolling Log page.
- Restrict navigation to the active task page and Log during background work; allow cancelling single-file loads.
- Rename Batch integrity check to Batch check and organize Plugins around nicoboss/nsz; installation/update/rollback actions are in Settings → Updates.
- Add combined batch scanning/verification and per-row package actions; reorder filter controls and space wrapped button rows consistently.
- Show full table text in tooltips and place naming columns at the end. Move naming options to Settings → Naming settings and keep both sections open.
- Offer named website/metadata sources with editable custom URL templates.
- Copy current keys to the user's .switch directory with replacement confirmation, retaining source files.
- Standardize README and documentation in English and update instructions for the current interface and Beta 2 packages.

### Fixed

- Compare complete semantic release versions, including beta/RC identifiers and numeric preview ordering; validate the downloaded executable against the full release version.

- Reuse completed batch overview metadata instead of repeatedly decoding NSZ files.
- Retain shared archive extraction while entries are open and recognize firmware sets alongside games, including unavailable-reference results.
- Retain verified plugin downloads across startup failures, preserve GUI fallback runtime files and reuse successful compatibility checks.
- Use temporary ASCII input/key aliases for bundled NSZ executables, restore output names and detect missing output even on exit code 0; log bounded sanitized diagnostics.
- Show conversion phases and CLI progress details during long operations.
- Apply light/dark themes consistently to dialogs, title bars, tab headers and status colors.
- Hide key warnings without a selected file; highlight required-key errors and report permitted missing delta fragments as information.
- Accept Windows-style paths in single-executable application-update ZIPs.
- Refresh localized idle status after language changes (upstream #40) and tolerate malformed optional title ratings (upstream #18).

### Known limitation

- Installed Beta 1 builds require manual installation of Beta 2 once because they still use numeric-only update comparisons. The updated viewer recognizes later beta/RC suffix updates.


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

- NSZ conversion offers replacement, numbering or cancellation for existing targets, and optional source deletion after successful verification. Batch overview selection reuses loaded metadata without repeated NSZ decompression.

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

This release adds batch integrity checks, modernizes the interface and incorporates improvements from community forks.

### Batch integrity checks

- Verify complete folders containing NSP, NSZ, XCI and XCZ files, optionally including subdirectories.
- Display results during verification, including file type, package type, compression, integrity status, errors and full paths.
- Filter errors, preview the current file, report progress and support cancellation.
- Export results as CSV and remember the last batch directory.
- Move valid files to a selected destination while preserving subdirectories.
- Never overwrite existing targets or automatically delete/move invalid files.

### Interface and settings

- Add dark, light and system themes, including dark title bars for the main, settings, renaming and batch windows.
- Save window size and position and improve dark-mode batch presentation.
- Configure Tinfoil title-page and API URLs independently; migrate from tinfoil.media to tinfoil.io.

### NACP improvements

- Support compressed NACP title blocks and up to 32 languages instead of 16.
- Expand displayed NACP information.

### Renaming

- Fix GitHub issue #46.
- Restore missing spaces or separators in online titles and improve normalization of incorrectly combined filenames.

### Dependencies and build

- Update .NET 8-compatible dependencies, ZstdSharp.Port, Microsoft.Extensions packages, the test SDK, xUnit and Coverlet.
- Keep LibHac at 0.19.0 because a fully compatible direct replacement is unavailable.

Thanks to the original NxFileViewer project and all community contributors.
