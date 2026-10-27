# User guide

[Documentation index](README.md) · [Project overview](../README.md)

Everyday workflows are described below. Engine setup, naming variables, update formats and diagnostics have their own pages in the [documentation index](README.md).

## Workspace

Start, File check, Batch check, Renaming, NAND, Settings, Log and Info share one main window. Switching tabs retains results and settings drafts. During a background task, navigation is restricted to the active page and Log. Use Cancel to stop the task, including an unfinished single-file load.

Start offers a NAND shortcut and buttons to open installed plugin GUIs. The update notice lists only components with available updates.

The interface supports English, French, German and Spanish, with light, dark and system themes. The first launch starts maximized; saved window placement is respected when enabled. Info lists application details and keyboard shortcuts.

## File check

- Open NSP, NSZ, XCI, XCZ or standalone NCA files, including supported packages inside ZIP/7z archives. Separate file-dialog filters cover game packages, archives, NCA content and NAND images/split dumps.
- Browse the content tree, export files, and save or copy title images.
- View package metadata, languages, firmware requirements, required master keys, compression and security information. Tooltips explain structure, NCA signature results and permission levels; ACID signatures are displayed separately.
- Verify NCA hashes and signatures, compress/decompress supported packages, or open the selected title website from the file toolbar.
- Browse Super NSP/XCI packages and compressed NACP titles with extended language support.
- See missing-key warnings separately from informational messages about unused keys or permitted missing delta fragments. Title-key warnings identify the Rights ID and point to the matching ticket/title.keys; newer master keys in prod.keys do not replace title keys. The batch column uses the short heading Missing keys. New history records retain the diagnostic key types and names, never key values.

**NCA contents only** describes an NSP/NSZ containing just NCA/NCZ files, without tickets, certificates or XML extras. Repacked or converted files can have this layout and still contain all required game data. **Incomplete** under package structure describes missing container markers or an unusual XCI partition layout; the separate integrity result determines missing/modified game content. Existing history entries retain their original classification until the source is explicitly reread.

Settings → Program → Verification provides three independent automatic-verification checkboxes: opening files inside the program (including single-file drag and drop), opening through Windows (double-click, Open with or a startup file argument), and dropping files/folders into batch mode. All default to off. Verification starts after loading and can be cancelled. Completed cached checks are not repeated automatically; batch history display does not start verification.

The built-in solid NCZ reader caches a decoded prefix under `Temp/NCZ` to avoid repeated full decompression on backward reads. The first forward skip still requires decoding the preceding data, and temporary disk usage can approach the payload size. Temporary hash-reader caches are released after each NCA, including cancellation or failure; package closure also releases nested NCZ caches, preventing accumulation across completed batch files. Keep enough free space for the active content. Block NCZ files use bounded block reads and indexed lookup. ZstdSharp.Port remains at 0.8.8. See [NCZ reader notes](NSZ-reader-update.md).

## Batch check

Choose a directory or ZIP/7z archive, or drop multiple files, folders or mixed selections into the window. Multiple selections form one batch; a single file dropped on the batch page also stays in batch mode. Duplicate entries are suppressed. Subdirectories and archives can be included or excluded.

- **Read file list** scans package metadata without running game-package integrity checks.
- **Read file list and verify integrity** combines scanning and verification.
- **Verify integrity of all files** checks the scanned list later, including rows hidden by filters.
- Firmware candidates are identified and verified during scanning.
- Successfully verified physical packages can be compressed, decompressed or moved; relative subdirectories are preserved.
- Right-click a row for individual actions: Open in Explorer appears first and selects the file or outer archive when available, followed by open in File check, verify, convert, move, open the title website, check naming or rename.

The Columns button offers 37 file, package, security, title, naming and conversion fields. Visibility, order and widths are saved automatically across restarts; Reset columns restores the defaults. Expanded package rows follow the package-column selection. Click headers to sort, Shift-click for multiple sort columns, and drag headers to reorder. Combine search, file-type, integrity and naming-status filters with Only errors. Naming filters include matching, differing, unchecked and failed names. The naming actions appear immediately before Cancel. The Games filter includes NSP/NSZ/XCI/XCZ and their archive members. Reset filters and sorting clears both in one action. Resetting filters/sorting or columns requires confirmation; declining or closing the dialog keeps the current settings. CSV export follows the current filtering and sorting and includes all export fields, even hidden columns. Hover over cells or headers to read their full text. Horizontal/vertical scrolling and Shift + mouse wheel support wide tables. Overview metadata is retained for quick selection without reopening NSZ files. If a selected result has no cached overview, its available source is loaded in the background; missing sources retain their stored summary.

Use the arrow beside a multi-package filename to show or hide its contained titles. The optional **Group by folder / archive** checkbox collects files under collapsible source-folder or ZIP/7z archive headings. Groups show their file count; filters and sorting still apply, and individual rows keep their overview and context-menu actions.

While reading files, the overview follows the current game package and selects its completed result. Recent batch sessions can be displayed or resumed; Settings → Program → History sets retention from 1 to 100 sessions (default 5). Historical overviews use the normal layout with package/language selection and a notice in place of the logo. They read saved metadata without reopening the source or archive. Reducing retention changes the list immediately; older stored runs are removed on the next history save. Disable history under Settings → Program if desired; existing saved sessions remain available when re-enabled. See [batch history](Batch-history.md).

### Actions for selected files

Use Ctrl/Shift to select several rows. Right-clicking a selected row keeps the selection. The menu offers actions supported by the selected files, with counts such as Compress (2/5). Each action processes only the eligible subset; multiple rows from the same physical file are processed once.

NSP/XCI package actions include Convert to NSP/XCI, Merge to NSP/XCI for multiple files, and Split to NSP/XCI for packages with multiple contents. Convert and Split process each source separately. Merge sorts inputs by game, update and DLC using actual CNMT metadata. See [package engines](PACKAGE_ENGINES.md).

## Renaming

Configure naming patterns under **Settings → Naming settings**; the Renaming page links to these settings. Pattern and character-replacement sections remain visible.

An optional target directory changes the destination root. Without one, each file's current directory is used. Patterns support relative subdirectories, such as `DLC/{WTitle}.{Ext:L}` or `{WAppTitle}/DLC/{WTitle}.{Ext:L}`. Use the target-directory field for absolute paths; absolute paths and `..` are rejected inside patterns. See [naming patterns and batch naming](Renaming.md).

The result table shows old path, new path, status and errors. Paths use `QUELL::` for the source root and `ZIEL::` for a different destination root; these display markers are retained across interface languages. Simulation creates no folders or files. Actual renaming creates missing destination folders and never overwrites an existing target. Online title failures fall back to local NACP names when available.

In Batch check, Check naming compares physical NSP/NSZ/XCI/XCZ packages with the saved patterns and target directory. Naming columns appear at the end of the table; additional proposed-path/error columns can be enabled. The context-menu check shows a result dialog. Individual renaming previews the paths and requires Yes or Cancel confirmation. Rename all differing files checks eligible rows again against current settings. Firmware and archive members are excluded; naming checks are independent of integrity verification.

## Keys and title sources

The dedicated Settings → Keys tab keeps prod.keys and title.keys sections visible without expanders. These sections show independent paths and validation results for the program directory, %USERPROFILE%/.switch and the custom path when configured. The currently used file is marked; each location has a button to open its file location, replacing the duplicate effective-path section. Results refresh when entering the Keys tab, reloading keys, copying shared keys or changing the custom path. Validation includes structural errors, missing master-key revisions and firmware estimates. Select your own locations or use configured download URLs, including anonymous FTP. A shared IP/hostname field replaces {IP} in both URL templates, for example ftp://{IP}:5000/sdmc:/switch/prod.keys. Download keys beside Copy keys downloads prod.keys and title.keys to their configured custom paths, or to the program directory when a custom path is empty. Failed or cancelled downloads retain existing files; successful downloads refresh validation. Legacy FTP URLs sharing one host migrate to the shared field without changing their ports or remote paths. Reload keys reruns discovery and validation: an explicit settings path takes priority, followed by the program directory and then %USERPROFILE%/.switch. Newly added program-local files are recognized without restarting. Missing required keys are shown on Start and file/batch views.

An explicit settings action copies the current key files to `%USERPROFILE%/.switch` for other applications. Existing files require replacement confirmation; source files remain unchanged. Key values are masked in saved session logs.

Missing key files are never downloaded automatically at startup. Use **Download keys** explicitly when your configured source is available. **Copy current keys to %USERPROFILE%\.switch** optionally supplies a shared location for compatible tools; it keeps the originals and does not synchronize future changes automatically. Hover over a question mark for help, or click it for a resizable, themed help window.

Title-name providers are Tinfoil, GitHub TitleDB, NLib API and Custom. The title website is selected independently from Tinfoil, NX Content or Custom. Presets have fixed URLs; only Custom displays an editable template. See [title providers](Title-providers.md).

## Archives and firmware

ZIP and 7z archives are supported in individual and batch mode, including nested archives up to eight levels. Virtual paths identify the chain, for example `outer.zip::inner.zip::Game.nsp`. Opening an archive extracts all its contents into a shared session under `Temp/ZIP`; nested archives are extracted when processed. Allow enough disk space for the extracted contents. Switching between already loaded entries reuses their files and models. Closing or reopening the archive releases its retained temporary data.

Mixed archives can contain game packages, firmware archives and firmware folders. Firmware sets appear as separate entries and are checked independently; missing reference data does not hide them. Batch file types indicate archive origin, such as `NSP (ZIP)`. Archive members cannot be converted, moved or renamed through package actions.

Firmware verification checks NCA sizes and SHA-256 hashes, identifies the best matching firmware version, and reports missing, changed, additional, duplicate or incorrectly named files. Individual firmware NCAs list matching releases by content hash. Sources are never renamed by firmware verification.

References are fetched from GitHub into memory only when firmware candidates are detected or explicitly opened. Each check requests current references; GitHub directory metadata shares the update cooldown/cache and downloaded manifests are validated against Git blob hashes; ordinary game folders make no firmware-reference request. Optional local `Plugins/fwhashes/*.json` files provide an offline fallback. Extract the separate firmware-hashes ZIP beside `NxFileViewerFork.exe` to install them. See [firmware verification](Firmware-verification.md) and [reference maintenance](../fw/README.md).

## NAND plugin

NAND dumps are recognized in **File check**, **Batch check** and ZIP/7z archives, including split dumps. The individual NAND details view and **NAND** workspace provide information and partition export through an optional NxNandManager CLI installation. Install/update it under **Settings → Updates → Plugins → NxNandManager**, with rollback to the previous version. The plugin uses the viewer's active prod.keys when it contains BIS keys. Settings → Plugins accepts a custom EXE override and an optional separate BIS key file. Exports preserve stored encryption and write only to new files; detected NANDs remain Unchecked in batch results. See [NAND plugin setup and limitations](NAND-plugin.md).

## Plugin nicoboss/nsz

The separately installed [nicoboss/nsz](https://github.com/nicoboss/nsz) plugin converts physical NSP/XCI and NSZ/XCZ packages. It uses the viewer's current `prod.keys`. Settings → Plugins → nicoboss/nsz provides compression level, Automatic/Solid/Block mode, block size and a custom executable option. Installation, updates and rollback are under **Settings → Updates → Plugins → nicoboss/nsz**.

Conversion verifies both source and output. Existing targets offer replacement, numbering or cancellation. Optional source deletion occurs only after successful conversion and verification. Progress distinguishes source verification, conversion and output verification, with CLI percentage/speed/ETA when available. Verified downloads survive failed startup checks and are reused; the official GUI package can provide CLI mode for the recognized standalone Python-runtime failure. Temporary ASCII aliases avoid Unicode filename failures in bundled executables. See [plugin setup and conversion](NSZ-plugin.md).

## Updates and local storage

The central update check compares NxFileViewer, installed managed plugins, regional/fallback TitleDB catalogs and firmware hash references; results appear in Settings. Automatic checks share a six-hour metadata cache across restarts; manual checks fetch fresh metadata when GitHub permits. API cooldowns are saved and respected, with retry times and dated cached results shown. Custom plugin executables cannot be version-checked automatically.

Settings → Updates groups application updates, plugin installation/update/rollback, TitleDB refresh and firmware-reference actions. It shows the local TitleDB refresh date for each regional catalog and the highest firmware version covered by installed hash references. Online firmware checks also show the highest available reference version. Automatic application checks are optional and never install without confirmation. Published pre-releases can be included explicitly. Updates verify SHA-256, version and architecture before replacement and restart. See [application updates and compatible release packages](Application-updates.md).

Check online hashes reads firmware references without saving them; Update offline hashes explicitly replaces the local set and retains a backup.

Settings, Plugins, Cache, Temp, History, Logs and Updates are stored beside the executable. The program directory must be writable; there is no AppData import or storage fallback.

Each launch creates a UTF-8 log in `Logs`, with timestamps and severity at the selected logging level. Settings → Program controls retention from 1 to 100 launches (default 5). Older session logs are removed after settings load and when a smaller retention count is applied. Logs are flushed immediately and key values are redacted. The Log tab follows new messages automatically.

## NAND drive discovery

NAND drive selection enumerates physical devices independently of drive letters and raw-read permissions. Offline/unlettered Hekate UMS devices can be selected by their `\\.\PhysicalDriveN` path; model and capacity are shown when Windows provides them. Refresh the list after connecting. Opening remains read-only and may require running NxFileViewer as administrator. No drive letter, formatting or online-state change is needed for selection.

See [NAND setup](NAND-plugin.md).

## Windows file associations

In **Settings → Program → Windows file associations**, click **Register NxFileViewer Fork** to register `.nsp`, `.nsz`, `.xci`, `.xcz` and `.nro` for the current Windows user. The shared stable ProgID `NxFileViewerFork.File` uses the adjacent native `NxFileViewerFork.Open.exe` launcher, quoted together with the file argument. The main application is `NxFileViewerFork.exe`; the launcher reads its name from an adjacent `NxFileViewerFork.Open.target` reference and is refreshed when the application starts. Package updates preserve file associations. Then choose **NxFileViewer Fork** through Windows **Open with → Choose another app → Always** for each extension you want as default. Existing defaults and original-viewer registrations are preserved. Repeat registration after moving the app to another folder; no administrator rights are required. Registration takes effect immediately, independently of saving or cancelling settings.

See [classic and modern Explorer menus](Explorer-integration.md).
