# NxFileViewer 4.0.0-beta.1

The batch table's **Columns** button shows or hides additional overview fields. Click column headers to sort (Shift-click for multiple columns); drag headers to reorder. Combine text search, file-type and integrity filters with "Only errors". Search terms match across the already loaded overview data, without reopening NSZ files. Reset buttons clear filters or sorting. CSV export follows the current filters and sorting and includes all overview fields; column visibility affects the table only. Column choices remain in place while switching workspace pages during the session.

ZIP packages can be opened in individual mode (NSP, NSZ, XCI, XCZ and NCA). The first supported entry opens initially; use the entry selector on the File page to switch to another. Standalone NCA files are also supported. Batch checks process all NSP/NSZ/XCI/XCZ entries, each in its own result row; pure NCA ZIPs retain firmware verification. Entries are temporarily extracted under `Temp/ZIP` in the program directory and removed on close. Sufficient free disk space for the selected entry is required. Archive entries cannot be moved or converted through package actions. Nested ZIP archives are not processed.

## Description

View and browse content of Nintendo Switch files.

Download latest version [here](https://github.com/sandmaennchen5/fork_NxFileViewer/releases).

## Features

- Based on [LibHac](https://github.com/Thealexbarney/LibHac)
- Supported files: NSP, NSZ, XCI, XCZ
- Supports Super NSP/XCI
- Browse files content structure
- Export files
- Save or copy title images
- Specify your own keys location
- Searches keys in commonly used locations
- Automatically download keys from an URL defined in the settings
- Supports drag and drop
- Checks real files type (XCI or NSP)
- Detailed log
- User-friendly and responsive interface
- Application ZIPs plus a separate optional firmware-hashes ZIP
- TitleDB cache in `Cache/TitleDB` and automatically cleaned temporary files in `Temp`, all next to the executable
- Verify integrity (hash and signature)
- Batch integrity check for complete folders containing NSP, NSZ, XCI, and XCZ files
- Live batch results with file type, package type, compression, integrity status, and error details
- Preview the overview of the currently checked file directly in the batch window
- Filter the batch list to show faulty files only and export the results as CSV
- Move successfully verified files to a selected destination while preserving subdirectories
- Existing destination files are never overwritten and faulty files are never deleted automatically
- Displays missing keys
- Opens title URL
- Configurable Tinfoil title page and API URLs
- Selectable Tinfoil, GitHub TitleDB and NLib title metadata, with local-title fallback during outages
- Key-file validation, firmware revision estimates and anonymous FTP key downloads
- Firmware ZIP/folder verification using SHA-256, automatic detection in batch checks and detailed failure reports
- Solid NCZ prefix caching to speed up backward/random reads without repeated full decompression
- Multiple interface languages (English, French, German, and Spanish)
- Supports compressed NACP title blocks and up to 32 NACP title languages
- Advanced files renaming
- Improved online title normalization when renaming files
- Dark, light, and system themes, including themed window title bars
- Remembers the main window size, position, and the last batch directory
- Full support of NSZ and XCZ files (compressed with [NSZ](https://github.com/nicoboss/nsz/) tool from **nicoboss**).

### Batch integrity check

Open **Tools → Check folder integrity** to verify all supported Switch files in a directory. Subdirectories can be included optionally. Results are added to the table as soon as each file has been checked, while the overview panel displays information about the file currently being processed.

The batch window supports cancellation, live progress reporting, faulty-file filtering, and CSV export. After the check, files reported as original and valid can be moved to a selected destination. The original folder structure is retained, existing destination files are skipped, and invalid files remain untouched.

### Firmware verification

Open **Tools → Firmware verification** and choose a folder or **Select firmware ZIP…**. Folder integrity checks also recognize firmware, optionally in subdirectories. The table shows the inferred version in the Structure column; select its row and open the Firmware verification tab for missing, changed, extra or duplicate NCA details. Only complete matches pass. Firmware entries are excluded from moving verified game packages.

Current references are loaded from GitHub into memory for each detected or explicitly selected firmware check, without a disk cache. Pure game folders make no GitHub request. References from [`fw/hashes`](fw/hashes) are provided in a separate optional firmware-hashes ZIP as an offline fallback, shown in the result details. Extract this add-on into the directory containing `NxFileViewer.exe`, so the files are located at `fw/hashes/*.json`. ZIP contents are hashed directly without extraction or keys; repacked ZIPs can pass. Loose NCA files and ZIPs containing NCA entries are recognized as candidates even without local hash lists. Missing hash sources affect only firmware results, while game package checks continue. See [firmware verification](docs/Firmware-verification.md) and [firmware reference maintenance](fw/README.md).

### Title information and NCZ loading

Choose Tinfoil, GitHub TitleDB or NLib in **Settings → Miscellaneous → Title name source**. GitHub catalogs are cached for offline use; renaming falls back to local NACP names if online requests fail. See [title providers](docs/Title-providers.md).

Solid NCZ decoding reuses a temporary decoded prefix for backward reads. First-time forward skips still require decoding and temporary disk usage can approach the payload size. See [NCZ reader changes](docs/NSZ-reader-update.md). ZstdSharp remains 0.8.8 and compressed NACP support is retained.

## Screenshots

### NSZ conversion plugin

Use **Tools → NSZ** to compress/decompress an opened NSP, NSZ, XCI or XCZ, with source and output integrity checks. Folder checks offer corresponding actions for valid files. The official CLI can be installed/updated independently; source deletion after successful verification is optional; existing targets can be replaced or saved with numbering. See [NSZ plugin setup and workflow](docs/NSZ-plugin.md).

![Overview](./screenshots/Overview.png)

![Content](./screenshots/Content.png)

![Content](./screenshots/Rename.png)

![Settings](./screenshots/Settings.png)

## Requirements

If application doesn't start, please install the *.NET Desktop Runtime 8* which can be downloaded from the official Microsoft website [here](https://dotnet.microsoft.com/download/dotnet/8.0).

## Contribute

Feel free to contribute to this project to make this program better.

I designed the application so that it can be easily localized in several languages.
If you want this app in your language, send me your translations ;).

## Development

### Requirements

 - Microsoft Visual Studio 2022+

### Build and tests

```powershell
dotnet build src/NxFileViewer.sln --no-restore
dotnet test src/NxFileViewer.sln --no-restore
```

Restore dependencies first on a fresh checkout. The local `/test/` folder is ignored and contains only personal test data; automated tests under `src/*.Test` remain part of the repository. See [4.0.0-beta.1 release notes](CHANGELOG.md).

### GitHub Actions downloads

After a push to master, the Build workflow provides the x64 and x86 application ZIPs and the optional firmware-hashes ZIP as three separate artifacts. Each download is the original `NxFileViewer_v<version>_x64.zip` or `_x86.zip`, plus `NxFileViewer_v<version>_firmware-hashes.zip`, without an additional ZIP wrapper. Artifacts are retained for seven days; permanent downloads belong in GitHub Releases.

Release builds map source paths to `/_/NxFileViewer/` in diagnostics and embed debug symbols. Local builds and GitHub Actions therefore retain source filenames and line numbers without exposing the checkout location. Runtime paths for opened files remain visible in logs.

NSZ plugins are installed in `Plugins/NSZ` next to the executable. Move this subfolder together with the application; plugin state uses relative paths. No AppData import or fallback is performed. Settings, plugins, caches and application temporary files stay in the program directory, which must be writable.

### Publishing

Run the PowerShell script below.

```PowerShell
.\Publish.ps1
```

## Credits

- Special thanks to [Thealexbarney](https://github.com/Thealexbarney) for his powerful and easy to use [LibHac](https://github.com/Thealexbarney/LibHac) library.
- Special thanks to [nicoboss](https://github.com/nicoboss/) who took a lot of time to explain me the [NSZ](https://github.com/nicoboss/nsz) format and many other things.
- Thanks to all the Switch scene :)

- NSZ-Konvertierung: vorhandene Ziele ersetzen, nummerieren oder abbrechen; Quelldateien auf Wunsch erst nach erfolgreicher Prüfung löschen. Die Stapelübersicht folgt der ausgewählten Datei.

### Single-window workspace

The start page, file verification, batch verification (including firmware), renaming and settings share one main window. Switch with the Main menu or the tabs. Switching retains batch results, running operations and settings drafts. Apply saves settings and returns to Start; Cancel discards the draft without closing the application. Existing menu commands and shortcuts navigate to the embedded pages. File pickers and confirmation prompts remain dialogs.

NSZ Plugin settings include Automatic, Solid/Blockless and Block compression modes, with a block-size selector (default 1 MiB). NSZ uses the same detected `prod.keys` as the viewer.

### Application updates

The viewer checks regular GitHub Releases on startup (optional in Settings). Use **Application updates → Download and install** for a newer version. The update verifies the matching x64/x86 ZIP, backs up and replaces the EXE, then restarts. Keys, settings and plugins remain in the program directory. See [update workflow and compatible release assets](docs/Application-updates.md).

Plugin settings have their own **Plugins** page, reachable from Start, Main menu, Options or its tab. NSZ settings and plugin update/rollback actions are grouped there. General and plugin settings use separate drafts; applying one preserves changes already saved in the other.

### Update center

The **Updates** tab, Start page and Main menu provide one page for application updates, NSZ plugin update/rollback, GitHub TitleDB refresh and firmware-hash actions. TitleDB refresh downloads the saved region and US fallback even when the existing catalog is fresh, then invalidates cached title responses. Firmware online checks do not persist data. **Update offline hashes** explicitly saves a validated reference set under `fw/hashes` and retains the previous folder as a backup.

The batch table uses horizontal and vertical scroll bars for optional columns; Shift + mouse wheel scrolls horizontally. The overview panel scrolls vertically. On first launch, or when remembering window placement is disabled, the application starts maximized. A saved window size/state is respected.

Settings groups Program, Updates and Plugins into nested tabs. Existing menu shortcuts select the matching tab. Installed NSZ status explicitly includes the installation label, version (or custom executable label), and executable path.

ZIP and 7z are supported in individual and batch mode. NCA-only firmware archives open their version/integrity details automatically; individual firmware NCAs list all matching firmware versions by content hash. See [firmware verification](docs/Firmware-verification.md).

Application updates optionally include pre-releases: enable the option under Settings → Updates and apply it before checking. Stable releases remain the default.

Already viewed ZIP/7z entries keep their extracted files and loaded content until the archive is closed or reopened. Batch file types include the archive origin, such as NSP (ZIP) or NSZ (7z); filters accept either package type or archive type.
