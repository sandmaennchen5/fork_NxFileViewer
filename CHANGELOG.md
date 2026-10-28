# Changelog

All notable changes to NxFileViewer Fork will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [4.1.1-beta1] - 2026-10-28

### Added

- Include an installed modern Explorer MSIX in general update checks and the Start update summary, comparing its independent package version and skipping Explorer release requests when the package is absent.

- Add batch-wide Split and Merge actions for mixed NSP/NSZ/XCI/XCZ inputs. Preserve the source container family or choose NSP/XCI; merge by base game ID or across games. Offer temporary splitting of mixed-game containers when matching additional content is available.
- Add primary/backup engine selections for compression/decompression (nscb_rust and nicoboss/nsz) and package operations (nscb_rust and Kronos). Retry a failed process or invalid output once with the selected backup in an isolated workspace.
- Add output choices beside the source, in an action subdirectory, or in a custom typed/browsed directory. Configure defaults per action under Plugins → General. Successful package and compression summaries can open the generated files in Batch.
- Add independent alphabetical sorting by base game title, keeping base content, updates and DLC together. Temporarily lock column sorting and restore the previous user sorting when disabled.
- Add configurable direct launches for NSZ, NxNandManager, NSCB Desktop and Kronos, with the selected keys. Install NSCB Desktop alongside nscb_rust; launch Kronos through its interactive NSCB.bat/NSCB.exe starter and private Python runtime.
- Centralize master-key CRC32 fingerprints and firmware mappings in src/config/master-keys.json, embedded at build time and validated before publishing.
- Add the Folders by game naming preset for applications, patches, DLC and same-game combined packages, using the base game title and application ID as their shared directory.
- Add an optional German GitHub Wiki alongside the English documentation and append a generated ZIP/runtime explanation to release notes.

### Changed

- Target .NET 10 across the application, log viewer, transition, packaging tools and tests. Standard ZIPs require .NET 10 Desktop Runtime; self-contained ZIPs include it.
- Unify managed plugin storage: active.json contains current/previous paths; each version retains its metadata in installation.json. Automatically migrate legacy states, preserve migration backups and support rollback and old-version cleanup for NSZ, NxNandManager, nscb_rust, Kronos and NSCB Desktop.
- Display installed release versions or a short Kronos commit in Updates. Preserve full commits and available executable/source digests in installation metadata.
- Group plugin settings into General, Compress / Decompress, experimental Merge / Split / Convert, and NxNandManager. Apply the compression feature switch to both engines; offer initial installation beside paths and keep update/rollback/cleanup actions under Updates.
- Keep Show multipackages separately (:1, :2, …) independent of game-family sorting. Persist normal column sorting and document checkbox defaults and UI locations in default-settings.json.
- Extend classic and modern Explorer package actions to compressed and mixed selections. Explorer extension 1.0.4.0 dynamically hides disabled optional actions; subsequent saved feature changes require no new MSIX.
- Publish validated Merge/Split results directly into the selected directory instead of GUID-named result folders, while retaining private staging and recovery data when publication fails.

### Fixed

- Avoid recursive NCA ID formatting with newer Span overload resolution by converting byte spans directly to hexadecimal.
- Honor the selected compression engine for decompression and Explorer actions, including backup selection. Keep source and output integrity checks enabled; remove the automatic stable-plugin-update checkbox before conversion and retain manual updates and on-demand initial installation.
- Restore managed plugin paths after moving the application directory without replacing explicitly selected manual executables.
- Recognize split-capable source containers when Batch displays their contents as separate rows. Decompress compressed Merge/Split inputs when required and report single-content sources before splitting.
- Correct Kronos direct launch to open the interactive console rather than automatically opening its web interface. Allow cleanup of old Kronos versions containing the bundled empty title.keys placeholder while protecting nonempty key files.
- Prevent repeated recursive plugin-directory scans during UI command-state checks and page navigation. Search for cleanup candidates only when requested and perform that search in the background.

## [4.1.0] - 2026-10-22

### Added

- Add Sphaira MTP browsing and read-only imports for file and batch checks, with a selectable local/network cache, progress, cancellation and cleanup. Offer experimental direct reads for supported packages and NCA files; cache mode remains required for archives, NAND and SD structures, and Windows may buffer direct reads internally.
- Read NRO homebrew metadata and icons in individual, archive and batch views. Recognize confirmed and likely legacy JKSV save backups separately from game packages; homebrew and save backups do not receive NCA integrity checks.
- Open installed SD-card NAX0 content with the matching sd_seed and active prod.keys. Support FAT32 split content, original Nintendo and RAW/file emuMMC content sets, a content-set selector and dedicated batch filters. Exclude SD content from package rename/conversion actions.
- Configure Switch-SD batch areas and custom nested folders/files. Apply the filter before MTP copying and offer automatic, regular-folder and explicit Switch-SD source selection.
- Browse NAND FAT32 partitions read-only from dumps, split dumps and physical Windows devices. Export individual files, resolve title names through local TitleDB and show readable save/NCA title IDs and save user IDs.
- Browse recognized SaveFS containers and export individual files or complete saves, preserving empty folders. Use new timestamped destinations and clean staged output on cancellation or failure.
- Add 37 configurable batch columns with saved visibility, order and widths; Games filtering; optional folder/archive grouping; and a collapsible details pane that remembers its width.
- Expand multi-package rows and optionally list/export each contained package independently as file.nsp::1, ::2, etc., with its own metadata. Keep actions and paths tied to the source container and retain package details in history.
- Export CSV or native Excel (.xlsx), optionally using only visible columns in table order. Excel preserves title IDs as text and provides a frozen header and filters; file names and full paths use separate columns.
- Support mixed multi-file/folder drops with duplicate suppression, cancellation and resumable history. Add three independent opt-in automatic-verification settings for in-app opening, Windows opening and batch drops.
- Make batch history retention configurable from 1 to 100 runs. Display stored metadata with the live overview layout, package/language selection and a history notice, without reopening sources.
- Add Open in Explorer as the first batch context-menu action, selecting the source file or outer archive when available.
- Add current-user Windows file associations for NSP/NSZ/XCI/XCZ/NRO through an embedded native launcher, preserving existing defaults and refreshing installed launchers when needed.
- Add application rollback with confirmation and restart. Offer confirmed cleanup of old managed plugin versions and successful application-update directories while protecting active/custom files, keys, linked or locked directories and pending/failed updates.
- Add a standalone LogViewer with file groups, multiline exceptions, text/severity/RAM filters, virtualized rendering and reload. Publish separate x64/x86 standard and self-contained ZIPs; support Publish.ps1 -LogViewerOnly.
- Add offline Credits and license texts to Info, including library versions and verified per-file icon provenance. Existing artwork remains unchanged; individual third-party icon authors/licenses remain unverified.

### Changed

- Publish NxFileViewerFork.exe with a stable package identity and full package updates. Main application ZIPs contain only NxFileViewerFork.exe, nxfileviewer-package.json and LICENSE.txt; other notices are embedded in the executable.
- Store TitleDB, GitHub update metadata and firmware references under Plugins/TitleDB, Plugins/CacheUpdates and Plugins/fwhashes. Provide optional old-name transition ZIPs that preserve architecture/runtime variant and user files; only the explicit transition migrates old data/settings paths. The transition follows IncludeViewerPrereleases from the existing configuration (fork file first, legacy file otherwise), selecting the newest compatible stable/beta release when enabled and stable releases otherwise.
- Keep Explorer setup files inside their setup ZIPs. Clean unused Explorer DLL versions after menu changes and startup while protecting active registrations, loaded/locked files and unexpected contents; retry deferred cleanup later.
- Move keys to a dedicated Settings tab, keep both key sections visible and refresh locations on entry. Organize Program settings into focused sections with hover/click help.
- Distinguish external NxNandManager/NSZ GUI launches on Start and supply active keys through an isolated temporary environment, respecting the NAND BIS-key override.
- Sort combined package values independently of package order by default, with an option to retain source order. Sort versions numerically and show all contained content types and the highest known permission risk without treating missing program metadata as safe.
- Require confirmation before resetting batch filters/sorting or column layout. Improve overview field order, column help, Integrity/ACID explanations, and monitor-aware resizable themed help windows.
- Make merge, split and NSP/XCI conversion experimental opt-in features, disabled by default and enabled in Settings → Features after restart.
- Use integrated Trace as the sole switch for passive RAM/GC and batch CSV diagnostics. Write new CSVs to Logs/Diagnostics in the application folder; leave previous AppData captures in place.
- Remove the legacy memory-diagnostic launcher, comparison experiments, unused preview/navigation bookkeeping, the unused renaming-page logger and redundant fields, wrappers and Win32 declarations. Move the remaining test-only logger into the test project.

### Fixed

- Correct NCZ section lengths before offset 0x4000, including firmware sections at 0xC00. Repair known NSZ 5.0.0 oversized HFS0 slots only in staged outputs after every affected NCA hash matches; preserve extra files, refresh hashes and reject unknown corruption.
- Verify NCAs across all XCI partitions, including non-game XCZ content during conversion, so a valid Secure partition cannot hide broken firmware contents. Preserve source files and managed plugin binaries.
- Rebuild decompressed XCI layouts on 512-byte boundaries, updating Secure/LimArea addresses, valid-data end and root hash. Preserve available certificate/prefix bytes and validate rebuilt headers; missing Nintendo signatures/certificates cannot be recreated.
- Release solid NCZ caches after temporary integrity readers and package closure. Surface disk-full paths and interrupt affected batches instead of continuing with misleading hash failures; active solid-compressed content still needs temporary disk space.
- Reuse archive extraction and recursive lists across loads/batch entries, releasing retained sessions on completion/cancellation. Detect ZIP/7z by signature, support nested archives with misleading extensions and show extraction/copy progress.
- Distinguish NCA-only containers from incomplete structures in all four languages and live/history/export views. Structure classification does not establish missing game data; old history keeps its recorded classification until reread.
- Distinguish missing title keys from system/console keys, include Rights IDs and retain diagnostic key names/types in new history without key values. Allow warnings to be dismissed without squeezing overview details.
- Restore UTF-8 localization, ungrouped table visibility, reliable package expansion/selection, stable result hashes, automatic package-column sizing and vertically centered batch search text.
- Restore overview loading when cached models are missing, retain stored summaries for missing sources/errors and prevent delayed results from replacing newer selections.
- List offline/unlettered physical NAND devices without an initial read probe, including Hekate UMS sources; show available model/capacity and explain permissions. Synchronize partition selectors, load new roots automatically and leave SaveFS on partition changes.
- Start key downloads only through the explicit Settings action, never automatically at startup. Persist the NSZ stable-update preference immediately on both update pages.
- Share GitHub metadata requests and a six-hour persistent cache across app/plugins, honor API cooldowns, and show retry times and dated cached results. Manual checks request fresh metadata when permitted.
- Open source locations after the context menu closes and grant Explorer foreground permission.

## [4.0.0] - 2026-10-07

### Added

- Add an optional NxNandManager plugin with verified managed downloads, manual updates and rollback, custom EXE/BIS-key settings, and a NAND workspace for dump information and partition export. It can reuse BIS keys from the viewer's active prod.keys; CLI work is cancellable, and staged exports preserve encryption and never overwrite existing files.
- Recognize NAND signatures and named encrypted-partition candidates in individual/batch mode and ZIP/7z archives, retaining split-dump siblings during extraction. Show NAND details and exclude these entries from NCA verification and game-package actions.
- Support ZIP/7z archives in individual and batch mode, including nested archives up to eight levels and mixed game/firmware contents. Individual mode offers an entry selector and retains extraction/content models until close or reopen. Batch types and CSV identify archive origin; archive members are excluded from conversion, move and rename actions.
- Verify firmware folders and archives by NCA size and SHA-256 without keys. Identify firmware versions and renamed/shared individual NCAs; report missing, changed, additional, duplicate and incorrectly named files without changing sources. Renamed files show actual → expected names instead of being counted as both missing and extra.
- Fetch current firmware references from GitHub for detected or explicitly selected firmware, with local offline fallback. Explicit offline updates retain the previous reference folder as a backup; ordinary game scans make no firmware-reference request.
- Add a central update check for NxFileViewer, installed managed plugins, regional/fallback TitleDB catalogs and firmware hash references. Forced TitleDB refresh validates catalogs before replacement and clears cached title responses; local refresh dates and local/online firmware versions are displayed.
- Add optional automatic startup checks and verified application download/install/restart, with SHA-256, full product-version and architecture checks, backup and early-start recovery. Preserve keys, settings and plugins. Published pre-releases require explicit opt-in; stable-only remains the default.
- Publish standard and compressed self-contained x64/x86 ZIPs, corresponding GitHub Actions artifacts and a separate optional firmware-hashes ZIP. Updates preserve architecture and runtime variant; Info displays both.
- Add descriptive file-dialog filters for game packages, ZIP/7z archives, NCA content and NAND images/split dumps.
- Support standalone NCA loading and signature/hash checks. Add package-structure detection for Scene releases, CDN rips, converted, Homebrew and incomplete packages, plus file size, reconstructed compression ratio and the system-update version in XCI/XCZ update partitions.
- Parse main.npdm and display permission levels, raw filesystem masks, authorized services and ACID signature state. Classifications consider filesystem service access and are shown for base titles and updates. Localized tooltips explain structure, permissions and separate NCA header signature results following Nx Game Info.
- Extend the title overview with base title ID, required master-key revision/key generation, minimum application version for DLC and NCA distribution type.
- Add selectable batch overview columns, sorting/reordering, combined search/type/integrity/naming filters and reset controls. CSV exports the filtered, sorted results with metadata and naming diagnostics; column choices persist during the current session.
- Save the last five batch sessions and resume interrupted work using source fingerprints; history can be disabled in Settings → Program. Historical game overviews show persisted metadata even when package/archive sources are unavailable.
- Add renaming and batch naming result tables, naming checks, proposed paths/status/error columns, an optional target directory and relative pattern subfolders without overwriting occupied targets. Individual checks show a status dialog; individual renaming requires Yes/Cancel confirmation.
- Add NSZ Automatic/Solid/Block modes and block sizes from 16 KiB to 4 GiB. Conversion verifies source and output, uses the viewer's active prod.keys, offers replacement/numbering/cancellation for existing output, and optionally deletes sources only after successful verification.
- Validate prod.keys and title.keys for malformed lines, missing master-key revisions, known master-key CRC32 values through master_key_16, and structurally valid Rights ID/title-key pairs. Estimate the newest supported content firmware and report revisions that cannot yet be validated or mapped.
- Display key validation separately for program-local, .switch and custom files, with active-file indicators and location buttons. Start shows prod.keys problems with Settings and Reload keys actions. Messages support English, German, French and Spanish.
- Add anonymous FTP support and editable Sphaira FTP presets to the HTTP/HTTPS key downloader, with a shared IP/hostname and {IP} URL templates. Downloads can target custom paths or the program folder and compare existing/incoming validation before replacement.
- Optionally save missing ticket keys to title.keys without duplicates or overwriting conflicts; disabled by default.
- Offer Tinfoil, regional GitHub TitleDB with offline cache, NLib API and custom title metadata sources, with independently selectable title websites.
- Write immediately flushed UTF-8 session logs with exception details, redacted key values and configurable retention of 1–100 launches (default 5).

### Changed

- Consolidate navigation into one window with Start, File check, Batch check, Renaming, NAND, Settings, Log and Info tabs. Retain results and drafts while switching pages; Settings Apply/Cancel no longer closes a separate window.
- Place NAND after Renaming and add a Start shortcut with a RAM-style icon. The second Start button row opens installed NxNandManager/NSZ GUI builds without downloading them. Start lists only available updates; Settings shows one complete update-check result.
- Organize Settings into Program, Naming settings, Updates and Plugins sections. Keep plugin/general drafts independent; installation/update/rollback actions are under Settings → Updates. Group the three key/ticket checkboxes beside the download controls.
- Restrict navigation to the active page and Log during background work, support cancelling unfinished single-file loads, and make Log follow new messages automatically. Start maximized on first launch while respecting saved placement.
- Combine batch scanning/verification and per-row actions; arrange filter checkboxes and action buttons consistently. Automatically select the appropriate Overview/Firmware/NAND details tab, while allowing manual tab selection. Add horizontal scrolling with Shift + mouse wheel and full-text cell/header tooltips.
- Move naming options to Settings → Naming settings, keep pattern/replacement sections visible, and remove the renaming output divider. Restore tinfoil.media presets with availability hints while preserving custom URLs.
- Copy active key files to .switch with replacement confirmation and validation comparisons, retaining source files. Remove empty program subdirectories on exit while preserving files, links and private test/development directories.
- Cache reconstructed compressed sizes and use queue-based tree traversal to avoid repeated scans and quadratic list shifting. Update README and supporting documentation for the final interface and release variants.

### Fixed

- Follow the current game package during active scans and select its completed result. Reuse captured overviews without reopening/decompressing completed packages; batch preview errors no longer invalidate otherwise valid results.
- Avoid classifying an NSP with only a ticket or only a certificate as converted.
- Rediscover and validate key locations on explicit reload, including newly added program-local files. Preserve existing key files on transfer failure, cancellation or declined replacement.
- Compare complete semantic versions, including beta/RC identifiers and numeric preview ordering; invalidate offers when the channel changes and verify the full downloaded product version.
- Accept Windows-style paths in single-executable application-update ZIPs.
- Retain shared archive extraction while entries are open and recognize firmware alongside games even when references are unavailable.
- Retain verified plugin downloads after startup failures, preserve NSZ GUI fallback runtime files and reuse successful compatibility checks.
- Use temporary ASCII input/key aliases for bundled NSZ executables, restore output names, detect missing output even after exit code 0 and log bounded sanitized diagnostics. Show conversion phases and CLI percentage/speed/ETA when available.
- Cache solid NCZ decoded prefixes to avoid full decompression restarts on backward/random reads (issue #52). Correct block NCZ boundaries, lookup, short reads, EOF, zero-length reads and disabled-cache handling.
- Report underlying NCA errors and identify broken NSZ/NCZ Zstandard streams as corrupted or incomplete compressed data.
- Prevent double release of main.npdm, bound malformed NPDM sizes and snapshot security data inside protected parsing. Use one-way bindings for read-only security fields to avoid UI crashes.
- Apply themes consistently to dialogs, title bars, tabs and status colors. Hide key warnings without a selected file; distinguish required-key errors from permitted missing delta fragments and unknown additional key names.
- Fall back to local NACP titles after online renaming failures, including HTTP 503.
- Refresh localized idle status after language changes (upstream #40) and tolerate malformed optional title ratings (upstream #18).

### Notes

- Firmware hash references are a separate optional download, not bundled into application ZIPs. Installed local references support offline verification. Repacked firmware archives remain valid when their NCA contents match.
- ZstdSharp.Port remains at 0.8.8; compressed NACP titles remain supported.
- The firmware estimate for prod.keys describes supported content firmware, not the exact firmware on which the keys were dumped. The master_key_16 mapping to 23.0.0 uses a locally derived CRC32 reference rather than independent upstream confirmation.
- title.keys supports structural validation; title-key values cannot be checked against a universal expected-value list.

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
