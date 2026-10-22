# NxFileViewer Fork v4

![Downloads](https://img.shields.io/endpoint?url=https://sandmaennchen5.github.io/fork_NxFileViewer/badge/downloads.json)

Browse and verify Nintendo Switch packages with a Windows desktop interface based on [LibHac](https://github.com/Thealexbarney/LibHac).

Download application ZIPs from [GitHub Releases](https://github.com/sandmaennchen5/fork_NxFileViewer/releases). Choose x64 or x86, either standard (requires .NET 8 Desktop Runtime) or self-contained (includes .NET, compressed). Application updates preserve the installed architecture and runtime variant; firmware hash references are a separate optional download. See the [changelog](CHANGELOG.md) for release history.

Join the [Discord community](https://discord.gg/tjvhd3jw3d).

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

The built-in solid NCZ reader caches a decoded prefix under `Temp/NCZ` to avoid repeated full decompression on backward reads. The first forward skip still requires decoding the preceding data, and temporary disk usage can approach the payload size. Temporary hash-reader caches are released after each NCA, including cancellation or failure; package closure also releases nested NCZ caches, preventing accumulation across completed batch files. Keep enough free space for the active content. Block NCZ files use bounded block reads and indexed lookup. ZstdSharp.Port remains at 0.8.8. See [NCZ reader notes](docs/NSZ-reader-update.md).

## Homebrew and Switch SD cards

NRO files can be opened individually, in ZIP/7z archives and in batch checks. The overview reads embedded NACP title names, author/publisher, display version, languages, build ID and icon. Embedded assets are optional; NRO files do not need keys and do not receive NCA signature or integrity verification.

Use **Open SD card** in File check to select the SD root, `Nintendo` or `Nintendo/Contents`. Batch check also recognizes these folders. Registered NAX0 contents are opened read-only using `sd_seed` and the SD content keys derived from the current `prod.keys`; a separate `console.keys` file is not needed. The seed must belong to the source console. Keep the original `Nintendo/Contents/registered/...` paths, which are part of NAX0 encryption. Opening an individual NAX0 file within that tree opens its containing SD content set.

SD discovery includes the original `/Nintendo` directory, RAW emuMMC under `/emuMMC/RAW1/Nintendo` through `RAW3`, and file emuMMC under `/emuMMC/SD00/Nintendo` through `SD99`. Select the SD root, `emuMMC`, an individual slot, or its Nintendo/Contents folder. File check offers a selector for the discovered content sets and initially opens the first available set (original Nintendo first). Batch creates a separate NAX0 row per discovered content set, even with recursive scanning disabled. Each source requires its matching seed in the active `prod.keys`.

FAT32 split NCA directories (`00`, `01`, ...) are supported even when copied folders lost their archive attribute. The content tree and title selector expose the installed titles; control metadata without a corresponding Meta NCA is displayed separately. NRO and NAX0 have dedicated batch filters; SD contents cannot be renamed or converted through package actions.

Use **Sphaira (MTP)** in File check or Batch check when Windows shows the Switch as a portable device without a drive letter. Start MTP in Sphaira, connect USB, open the device in the picker, and select microSD, Contents, a supported file or a folder. Choose a cache location with enough space; another local drive or a network folder can be selected. Only supported content is copied, and the Switch is not modified. File check opens the imported content; Batch check fills the source field so the usual scan/verify buttons can be used. For ordinary game folders, File check opens the first supported file; use Batch check for the whole folder.

The source selector next to the Batch source path offers **Auto**, **Regular folder**, and **Switch SD**. Auto recognizes a Switch SD root from `Nintendo`, `emuMMC`, or `hbmenu.nro`; this includes copied card folders. Switch SD forces the configured SD area filter even if those markers are missing, while Regular folder bypasses that filter. The area filter must be enabled in Settings. The choice is saved and also applies to batch MTP imports.

In **Settings → Program**, enable **Switch SD: check selected areas only** to restrict whole-card batch scans to `/switch`, `/Nintendo`, `/emuMMC`, `/tools`, root `hbmenu.nro`, `/DBIsaves`, `/dumps`, and `/nxdt_rw_poc`, each with its own checkbox. Add your own folders or individual files in **Custom areas**, one path per line relative to the SD root (for example `/atmosphere`, `/backup/saves`, `/apps/tool.nro`). These supplement the checkboxes and can be removed by deleting their lines. Nested paths are filtered during MTP traversal as well. Drive/UNC paths, wildcards and parent traversal are rejected. The filter is off by default and all area choices are saved. It applies to ordinary SD folders/drives and batch MTP selection; excluded MTP areas are not copied. Explicitly opened individual files or subfolders are unaffected. An enabled filter with no selected areas scans no content.

The picker defaults to **local cache**. Enable **Read directly (experimental)** to read individual NSP/NSZ, XCI/XCZ or NCA files, or a folder containing those files, without an upfront application-level copy. Both single and batch views support this mode. The Switch must remain connected with MTP active; direct references last only for the current app session and cannot be renamed, moved or converted. Archives, NAND, split NCA and SD structures require cache mode. Unsupported selections report this explicitly. Windows may buffer whole files internally: a small-file seek/read/backward-read test succeeded, while a seek to the end of a 4.03-GiB file stalled and was stopped. The new direct reader was also tested on a connected Switch with a 337,408-byte NCA: no upfront payload copy, successful 64-byte reads and matching data after returning to the start, but the seek/read roundtrip took about 30 seconds. A blocked native seek cannot be cancelled until it returns. Direct mode is therefore experimental and does not guarantee disk-free reading.

The cache preserves SD content paths and split NCA parts; encrypted SD contents still require the matching `sd_seed` in the active `prod.keys`. Copy status shows the current file, byte counts and percentage where the device supplies its length. Cancel removes the incomplete import after the current device read returns. Completed copies remain available during the app session for selection and history; cleanup is attempted on exit. Driver/connection failures can leave cache files behind. MTP supports file/folder transfers, not raw NAND drive access. Transfer logic is covered by simulated-device tests; browsing, a read-only NCA-header transfer and a complete small NCA transfer were also tested on a connected Switch. A complete card import was not performed. MTP Contents views that provide NCAs without the NAX0 layer can use that content directly; raw NAX0 imports still require the matching seed.

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

While reading files, the overview follows the current game package and selects its completed result. Recent batch sessions can be displayed or resumed; Settings → Program → History sets retention from 1 to 100 sessions (default 5). Historical overviews use the normal layout with package/language selection and a notice in place of the logo. They read saved metadata without reopening the source or archive. Reducing retention changes the list immediately; older stored runs are removed on the next history save. Disable history under Settings → Program if desired; existing saved sessions remain available when re-enabled. See [batch history](docs/Batch-history.md).

## Renaming

Configure naming patterns under **Settings → Naming settings**; the Renaming page links to these settings. Pattern and character-replacement sections remain visible.

An optional target directory changes the destination root. Without one, each file's current directory is used. Patterns support relative subdirectories, such as `DLC/{WTitle}.{Ext:L}` or `{WAppTitle}/DLC/{WTitle}.{Ext:L}`. Use the target-directory field for absolute paths; absolute paths and `..` are rejected inside patterns. See [naming patterns and batch naming](docs/Renaming.md).

The result table shows old path, new path, status and errors. Paths use `QUELL::` for the source root and `ZIEL::` for a different destination root; these display markers are retained across interface languages. Simulation creates no folders or files. Actual renaming creates missing destination folders and never overwrites an existing target. Online title failures fall back to local NACP names when available.

In Batch check, Check naming compares physical NSP/NSZ/XCI/XCZ packages with the saved patterns and target directory. Naming columns appear at the end of the table; additional proposed-path/error columns can be enabled. The context-menu check shows a result dialog. Individual renaming previews the paths and requires Yes or Cancel confirmation. Rename all differing files checks eligible rows again against current settings. Firmware and archive members are excluded; naming checks are independent of integrity verification.

## Keys and title sources

The dedicated Settings → Keys tab keeps prod.keys and title.keys sections visible without expanders. These sections show independent paths and validation results for the program directory, %USERPROFILE%/.switch and the custom path when configured. The currently used file is marked; each location has a button to open its file location, replacing the duplicate effective-path section. Results refresh when entering the Keys tab, reloading keys, copying shared keys or changing the custom path. Validation includes structural errors, missing master-key revisions and firmware estimates. Select your own locations or use configured download URLs, including anonymous FTP. A shared IP/hostname field replaces {IP} in both URL templates, for example ftp://{IP}:5000/sdmc:/switch/prod.keys. Download keys beside Copy keys downloads prod.keys and title.keys to their configured custom paths, or to the program directory when a custom path is empty. Failed or cancelled downloads retain existing files; successful downloads refresh validation. Legacy FTP URLs sharing one host migrate to the shared field without changing their ports or remote paths. Reload keys reruns discovery and validation: an explicit settings path takes priority, followed by the program directory and then %USERPROFILE%/.switch. Newly added program-local files are recognized without restarting. Missing required keys are shown on Start and file/batch views.

An explicit settings action copies the current key files to `%USERPROFILE%/.switch` for other applications. Existing files require replacement confirmation; source files remain unchanged. Key values are masked in saved session logs.

Missing key files are never downloaded automatically at startup. Use **Download keys** explicitly when your configured source is available. **Copy current keys to %USERPROFILE%\.switch** optionally supplies a shared location for compatible tools; it keeps the originals and does not synchronize future changes automatically. Hover over a question mark for help, or click it for a resizable, themed help window.

Title-name providers are Tinfoil, GitHub TitleDB, NLib API and Custom. The title website is selected independently from Tinfoil, NX Content or Custom. Presets have fixed URLs; only Custom displays an editable template. See [title providers](docs/Title-providers.md).

## Archives and firmware

ZIP and 7z archives are supported in individual and batch mode, including nested archives up to eight levels. Virtual paths identify the chain, for example `outer.zip::inner.zip::Game.nsp`. Opening an archive extracts all its contents into a shared session under `Temp/ZIP`; nested archives are extracted when processed. Allow enough disk space for the extracted contents. Switching between already loaded entries reuses their files and models. Closing or reopening the archive releases its retained temporary data.

Mixed archives can contain game packages, firmware archives and firmware folders. Firmware sets appear as separate entries and are checked independently; missing reference data does not hide them. Batch file types indicate archive origin, such as `NSP (ZIP)`. Archive members cannot be converted, moved or renamed through package actions.

Firmware verification checks NCA sizes and SHA-256 hashes, identifies the best matching firmware version, and reports missing, changed, additional, duplicate or incorrectly named files. Individual firmware NCAs list matching releases by content hash. Sources are never renamed by firmware verification.

References are fetched from GitHub into memory only when firmware candidates are detected or explicitly opened. Each check requests current references; GitHub directory metadata shares the update cooldown/cache and downloaded manifests are validated against Git blob hashes; ordinary game folders make no firmware-reference request. Optional local `Plugins/fwhashes/*.json` files provide an offline fallback. Extract the separate firmware-hashes ZIP beside `NxFileViewerFork.exe` to install them. See [firmware verification](docs/Firmware-verification.md) and [reference maintenance](fw/README.md).

## NAND plugin

NAND dumps are recognized in **File check**, **Batch check** and ZIP/7z archives, including split dumps. The individual NAND details view and **NAND** workspace provide information and partition export through an optional NxNandManager CLI installation. Install/update it under **Settings → Updates → Plugins → NxNandManager**, with rollback to the previous version. The plugin uses the viewer's active prod.keys when it contains BIS keys. Settings → Plugins accepts a custom EXE override and an optional separate BIS key file. Exports preserve stored encryption and write only to new files; detected NANDs remain Unchecked in batch results. See [NAND plugin setup and limitations](docs/NAND-plugin.md).

## Plugin nicoboss/nsz

The separately installed [nicoboss/nsz](https://github.com/nicoboss/nsz) plugin converts physical NSP/XCI and NSZ/XCZ packages. It uses the viewer's current `prod.keys`. Settings → Plugins → nicoboss/nsz provides compression level, Automatic/Solid/Block mode, block size and a custom executable option. Installation, updates and rollback are under **Settings → Updates → Plugins → nicoboss/nsz**.

Conversion verifies both source and output. Existing targets offer replacement, numbering or cancellation. Optional source deletion occurs only after successful conversion and verification. Progress distinguishes source verification, conversion and output verification, with CLI percentage/speed/ETA when available. Verified downloads survive failed startup checks and are reused; the official GUI package can provide CLI mode for the recognized standalone Python-runtime failure. Temporary ASCII aliases avoid Unicode filename failures in bundled executables. See [plugin setup and conversion](docs/NSZ-plugin.md).

## Updates and local storage

The central update check compares NxFileViewer, installed managed plugins, regional/fallback TitleDB catalogs and firmware hash references; results appear in Settings. Automatic checks share a six-hour metadata cache across restarts; manual checks fetch fresh metadata when GitHub permits. API cooldowns are saved and respected, with retry times and dated cached results shown. Custom plugin executables cannot be version-checked automatically.

Settings → Updates groups application updates, plugin installation/update/rollback, TitleDB refresh and firmware-reference actions. It shows the local TitleDB refresh date for each regional catalog and the highest firmware version covered by installed hash references. Online firmware checks also show the highest available reference version. Automatic application checks are optional and never install without confirmation. Published pre-releases can be included explicitly. Updates verify SHA-256, version and architecture before replacement and restart. See [application updates and compatible release packages](docs/Application-updates.md).

Check online hashes reads firmware references without saving them; Update offline hashes explicitly replaces the local set and retains a backup.

Settings, Plugins, Cache, Temp, History, Logs and Updates are stored beside the executable. The program directory must be writable; there is no AppData import or storage fallback.

Each launch creates a UTF-8 log in `Logs`, with timestamps and severity at the selected logging level. Settings → Program controls retention from 1 to 100 launches (default 5). Older session logs are removed after settings load and when a smaller retention count is applied. Logs are flushed immediately and key values are redacted. The Log tab follows new messages automatically.

## Requirements and development

Windows and the [.NET Desktop Runtime 8](https://dotnet.microsoft.com/download/dotnet/8.0) are required if the downloaded application does not start. Development requires the .NET 8 SDK and optionally Visual Studio 2022 or later.

```powershell
dotnet restore src/NxFileViewer.sln
dotnet build src/NxFileViewer.sln -c Release --no-restore
dotnet test src/NxFileViewer.sln -c Release --no-restore
.\Publish.ps1
```

Publish.ps1 creates x64/x86 application ZIPs, separate standalone LogViewer ZIPs (standard and self-contained), and a separate optional firmware-hashes ZIP. Use `-LogViewerOnly` to publish only the diagnostic viewer or `-SkipLogViewer` to omit it. The [LogViewer](docs/LogViewer.md) opens existing logs with colored file groups, text/severity filters and a RAM/GC filter. The Build workflow exposes these as separate artifacts retained for seven days; attach release ZIPs to GitHub Releases for permanent downloads and application updates. Source paths in Release diagnostics are mapped to `/_/NxFileViewer/`, with debug symbols embedded in the executable. Runtime file paths remain visible.

The ignored `/test/` directory contains personal test data; automated tests under `src/*.Test` are part of the repository.

## Screenshots

Screenshots illustrate the viewer and may show an earlier interface.

![Start](screenshots/Start.png)
![Overview](screenshots/Overview.png)
![Missing Keys](screenshots/Missing-Keys.png)
![Content](screenshots/Content.png)
![Batch Game Overview](screenshots/Batch-Game-Overview.png)
![Batch Context Menu](screenshots/Batch-Context-Menu.png)
![Rename](screenshots/Rename.png)
![Firmware Verification](screenshots/Firmware-Verification.png)
![Firmware Verification2](screenshots/Firmware-Verification2.png)
![Firmware NCA Identification](screenshots/Firmware-NCA-Identification.png)
![NAND Information](screenshots/NAND-Information.png)
![Settings Program](screenshots/Settings-Program.png)
![Settings Keys](screenshots/Settings-Keys.png)
![Settings Naming](screenshots/Settings-Naming.png)
![Settings Updates](screenshots/Settings-Updates.png)
![Settings Plugins](screenshots/Settings-Plugins.png)

## Contributing and credits

Contributions and translations are welcome. See [credits and dependency versions](CREDITS.txt), the [project license](LICENSE), and [third-party license texts](docs/THIRD-PARTY-NOTICES.txt). These notices are embedded in the application and available offline on the Info page. Application ZIPs contain only NxFileViewerFork.exe, nxfileviewer-package.json and LICENSE.txt. The inherited icon attribution is incomplete; see [icon provenance](src/NxFileViewer/Styling/Icons/README.md).

Thanks to [Myster-Tee](https://github.com/Myster-Tee/NxFileViewer)

Thanks to [Thealexbarney](https://github.com/Thealexbarney) for LibHac, [eliboa](https://github.com/eliboa) and [THZoria](https://github.com/THZoria) for NxNandManager, [nicoboss](https://github.com/nicoboss) for NSZ and format guidance, and the Nintendo Switch community.

NAND drive selection enumerates physical devices independently of drive letters and raw-read permissions. Offline/unlettered Hekate UMS devices can be selected by their `\\.\PhysicalDriveN` path; model and capacity are shown when Windows provides them. Refresh the list after connecting. Opening remains read-only and may require running NxFileViewer as administrator. No drive letter, formatting or online-state change is needed for selection.

In **Settings → Program → Windows file associations**, click **Register NxFileViewer Fork** to register `.nsp`, `.nsz`, `.xci`, `.xcz` and `.nro` for the current Windows user. The shared stable ProgID `NxFileViewerFork.File` uses the adjacent native `NxFileViewerFork.Open.exe` launcher, quoted together with the file argument. The main application is `NxFileViewerFork.exe`; the launcher reads its name from an adjacent `NxFileViewerFork.Open.target` reference and is refreshed when the application starts. Package updates preserve file associations. Then choose **NxFileViewer Fork** through Windows **Open with → Choose another app → Always** for each extension you want as default. Existing defaults and original-viewer registrations are preserved. Repeat registration after moving the app to another folder; no administrator rights are required. Registration takes effect immediately, independently of saving or cancelling settings.

Version 4.1.0-beta1 is a pre-release. For manual upgrades from older executable/data paths and the configuration-aware legacy transition, see [application updates](docs/Application-updates.md). New release packages use NxFileViewerFork_v4.1.0-beta1_*.zip.
