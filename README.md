# NxFileViewer 4.0.0-beta.2

Browse and verify Nintendo Switch packages with a Windows desktop interface based on [LibHac](https://github.com/Thealexbarney/LibHac).

Download application ZIPs from [GitHub Releases](https://github.com/sandmaennchen5/fork_NxFileViewer/releases). Choose x64 or x86; firmware hash references are a separate optional download. See the [changelog](CHANGELOG.md) for release history.

## Workspace

Start, File check, Batch check, Renaming, Settings, Log and Info share one main window. Switching tabs retains results and settings drafts. During a background task, navigation is restricted to the active page and Log. Use Cancel to stop the task, including an unfinished single-file load.

The interface supports English, French, German and Spanish, with light, dark and system themes. The first launch starts maximized; saved window placement is respected when enabled. Info lists application details and keyboard shortcuts.

## File check

- Open NSP, NSZ, XCI, XCZ or standalone NCA files, including supported packages inside ZIP/7z archives.
- Browse the content tree, export files, and save or copy title images.
- View package metadata, languages, firmware requirements, required master keys, compression and security information.
- Verify NCA hashes and signatures, compress/decompress supported packages, or open the selected title website from the file toolbar.
- Browse Super NSP/XCI packages and compressed NACP titles with extended language support.
- See missing-key warnings separately from informational messages about unused keys or permitted missing delta fragments.

The built-in solid NCZ reader caches a decoded prefix under `Temp/NCZ` to avoid repeated full decompression on backward reads. The first forward skip still requires decoding the preceding data, and temporary disk usage can approach the payload size. Block NCZ files use bounded block reads and indexed lookup. ZstdSharp.Port remains at 0.8.8. See [NCZ reader notes](docs/NSZ-reader-update.md).

## Archives and firmware

ZIP and 7z archives are supported in individual and batch mode, including nested archives up to eight levels. Virtual paths identify the chain, for example `outer.zip::inner.zip::Game.nsp`. Opening an archive extracts all its contents into a shared session under `Temp/ZIP`; nested archives are extracted when processed. Allow enough disk space for the extracted contents. Switching between already loaded entries reuses their files and models. Closing or reopening the archive releases its retained temporary data.

Mixed archives can contain game packages, firmware archives and firmware folders. Firmware sets appear as separate entries and are checked independently; missing reference data does not hide them. Batch file types indicate archive origin, such as `NSP (ZIP)`. Archive members cannot be converted, moved or renamed through package actions.

Firmware verification checks NCA sizes and SHA-256 hashes, identifies the best matching firmware version, and reports missing, changed, additional, duplicate or incorrectly named files. Individual firmware NCAs list matching releases by content hash. Sources are never renamed by firmware verification.

References are fetched from GitHub into memory only when firmware candidates are detected or explicitly opened. Each check fetches current references; ordinary game folders make no firmware-reference request. Optional local `fw/hashes/*.json` files provide an offline fallback. Extract the separate firmware-hashes ZIP beside `NxFileViewer.exe` to install them. See [firmware verification](docs/Firmware-verification.md) and [reference maintenance](fw/README.md).

## Batch check

Choose a directory or ZIP/7z archive. Subdirectories and archives can be included or excluded.

- **Read file list** scans package metadata without running game-package integrity checks.
- **Read file list and verify integrity** combines scanning and verification.
- **Verify integrity of all files** checks the scanned list later, including rows hidden by filters.
- Firmware candidates are identified and verified during scanning.
- Successfully verified physical packages can be compressed, decompressed or moved; relative subdirectories are preserved.
- Right-click a row for individual actions: open in File check, verify, convert, move, open the title website, check naming or rename.

The Columns button shows or hides additional overview fields. Click headers to sort, Shift-click for multiple sort columns, and drag headers to reorder. Combine search, file-type and integrity filters with Only errors. CSV export follows the current filtering and sorting and includes all overview fields. Hover over cells or headers to read their full text. Horizontal/vertical scrolling and Shift + mouse wheel support wide tables. Overview metadata is retained for quick selection without reopening NSZ files.

The last five batch sessions can be displayed or resumed. Disable history under Settings → Program if desired; existing saved sessions remain available when re-enabled. See [batch history](docs/Batch-history.md).

## Renaming

Configure naming patterns under **Settings → Naming settings**; the Renaming page links to these settings. Pattern and character-replacement sections remain visible.

An optional target directory changes the destination root. Without one, each file's current directory is used. Patterns support relative subdirectories, such as `DLC/{WTitle}.{Ext:L}` or `{WAppTitle}/DLC/{WTitle}.{Ext:L}`. Use the target-directory field for absolute paths; absolute paths and `..` are rejected inside patterns. See [naming patterns and batch naming](docs/Renaming.md).

The result table shows old path, new path, status and errors. Paths use `QUELL::` for the source root and `ZIEL::` for a different destination root; these display markers are retained across interface languages. Simulation creates no folders or files. Actual renaming creates missing destination folders and never overwrites an existing target. Online title failures fall back to local NACP names when available.

In Batch check, Check naming compares physical NSP/NSZ/XCI/XCZ packages with the saved patterns and target directory. Naming columns appear at the end of the table; additional proposed-path/error columns can be enabled. The context-menu check shows a result dialog. Individual renaming previews the paths and requires Yes or Cancel confirmation. Rename all differing files checks eligible rows again against current settings. Firmware and archive members are excluded; naming checks are independent of integrity verification.

## Keys and title sources

Settings shows the actual `prod.keys` and `title.keys` files in use, structural validation results, missing master-key revisions and firmware estimates. Select your own locations or use configured download URLs, including anonymous FTP. Missing required keys are shown on Start and file/batch views.

An explicit settings action copies the current key files to `%USERPROFILE%/.switch` for other applications. Existing files require replacement confirmation; source files remain unchanged. Key values are masked in saved session logs.

Title-name providers are Tinfoil, GitHub TitleDB, NLib API and Custom. The title website is selected independently from Tinfoil, NX Content or Custom. Presets have fixed URLs; only Custom displays an editable template. See [title providers](docs/Title-providers.md).

## Plugin nicoboss/nsz

The separately installed [nicoboss/nsz](https://github.com/nicoboss/nsz) plugin converts physical NSP/XCI and NSZ/XCZ packages. It uses the viewer's current `prod.keys`. Settings → Plugins → nicoboss/nsz provides compression level, Automatic/Solid/Block mode, block size and a custom executable option. Installation, updates and rollback are under **Settings → Updates → Plugins → nicoboss/nsz**.

Conversion verifies both source and output. Existing targets offer replacement, numbering or cancellation. Optional source deletion occurs only after successful conversion and verification. Progress distinguishes source verification, conversion and output verification, with CLI percentage/speed/ETA when available. Verified downloads survive failed startup checks and are reused; the official GUI package can provide CLI mode for the recognized standalone Python-runtime failure. Temporary ASCII aliases avoid Unicode filename failures in bundled executables. See [plugin setup and conversion](docs/NSZ-plugin.md).

## Updates and local storage

Settings → Updates groups application updates, plugin installation/update/rollback, TitleDB refresh and firmware-reference actions. Automatic application checks are optional and never install without confirmation. Published pre-releases can be included explicitly. Updates verify SHA-256, version and architecture before replacement and restart. See [application updates and compatible release packages](docs/Application-updates.md).

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

Publish.ps1 creates x64/x86 application ZIPs and a separate optional firmware-hashes ZIP. The Build workflow exposes these as separate artifacts retained for seven days; attach release ZIPs to GitHub Releases for permanent downloads and application updates. Source paths in Release diagnostics are mapped to `/_/NxFileViewer/`, with debug symbols embedded in the executable. Runtime file paths remain visible.

The ignored `/test/` directory contains personal test data; automated tests under `src/*.Test` are part of the repository.

## Screenshots

Screenshots illustrate the viewer and may show an earlier interface.

![Overview](screenshots/Overview.png)
![Content](screenshots/Content.png)
![Renaming](screenshots/Rename.png)
![Settings](screenshots/Settings.png)

## Contributing and credits

Contributions and translations are welcome.

Thanks to [Thealexbarney](https://github.com/Thealexbarney) for LibHac, [nicoboss](https://github.com/nicoboss) for NSZ and format guidance, and the Nintendo Switch community.
