# Configuration

Documentation for the four configuration files lives alongside them in this directory.

| File | Contents | Documentation |
| --- | --- | --- |
| [default-settings.json](default-settings.json) | General defaults and checkbox states | [Default settings](#default-settings) |
| [naming-presets.json](naming-presets.json) | Naming presets and character replacement | [Naming presets](#naming-presets) |
| [package-manifest.json](package-manifest.json) | Update sources, timeouts and migrations | [Update configuration](Update-sources.md) |
| [external-sources.json](external-sources.json) | Plugin, firmware, Explorer and online-service sources | [External sources](Update-sources.md#external-component-and-online-service-sources) |

General user guides remain under [docs](../../docs/README.md).

## Default settings

`default-settings.json` defines application defaults. It is embedded in the executable
during build/publish; changes take effect after rebuilding. Before publishing,
`Validate-DefaultSettings.ps1` checks for missing/unknown keys, incorrect types,
invalid choices and out-of-range numbers.

- `Settings`: general settings, plugins, updates, verification, compression and appearance.
- `RenamingOptions`: renaming preferences.
- `true` means a checkbox is **checked**; `false` means **unchecked**.
- Selectors use enum names such as `System`, `Auto`, `Rust` and `Information`.
- Keys correspond to properties in `NxFileViewerFork.settings.json`.

## Precedence

1. Embedded defaults are loaded first.
2. Local `NxFileViewerFork.settings.json` values override those defaults, including
   explicitly saved `false` values.
3. Missing properties retain their defaults.
4. **Reset** restores embedded defaults; **Apply** saves the change.

Changing defaults does not overwrite existing saved user values.

## Separate configuration

Naming patterns, external addresses and update-source/migration rules belong in
their respective JSON files, not duplicated in `default-settings.json`.
Personal file paths, window placement, table layouts and recent files remain runtime data.

`EnablePlugins` is the legacy shared plugin switch. Individual
`EnableNszPlugin`/`EnableNandPlugin` defaults apply until individual values have
been saved. A legacy saved shared `false` value disables both unless an individual
saved setting overrides it.

## Common checkboxes

| Key | Meaning |
| --- | --- |
| CheckViewerUpdatesOnStartup | Check for application updates on startup |
| IncludeViewerPrereleases | Include preview application releases |
| EnableNszPlugin / EnableNandPlugin | Enable the NSZ / NAND plugin |
| EnableMtp / EnableNand | Enable MTP / NAND features |
| EnablePackageOperations | Enable package operations |
| EnableFirmwareCheck | Enable firmware verification |
| EnableBatchHistory | Save batch history |
| SaveBatchHistoryLogos | Save icons in batch history |
| VerifyBatchDrops | Verify files dropped into Batch |
| VerifyFilesOpenedInProgram | Verify files opened inside the application |
| VerifyFilesOpenedFromWindows | Verify files opened through Windows |
| RememberWindowPlacement | Remember window position and size |
| IsSimulation | Simulate renaming before making changes |
| IncludeSubdirectories | Include subdirectories |
| ReplaceWhiteSpaceChars | Replace whitespace |
| AutoCloseOpenedFile | Close the currently opened file automatically when renaming |

## Naming presets

`naming-presets.json` uses `SchemaVersion: 2` with five independent sections:
`Application` (games), `Patch` (updates), `Addon` (DLC), `Combined` (same-game
combined packages) and `MultiPackage` (mixed packages / DLC collections).

Each section contains `DefaultPreset` and `Presets`. Each preset defines only `Id`,
localized `Names` and `Pattern`. Use one compact JSON line per preset. IDs must be
unique within a section. `Names` requires an English label (`en`); `manual` is a
reserved ID. The application adds the Manual choice automatically.

`Publish.ps1` validates the catalog before building. Presets are embedded in the
executable; existing saved user patterns retain precedence.

### Character replacement

The `Defaults` section of `naming-presets.json` defines:

- `InvalidFileNameCharsReplacement`: replacement for invalid Windows filename
  characters; default `꞉`.
- `ReplaceWhiteSpaceChars`: replace whitespace by default; default `false`.
- `WhiteSpaceCharsReplacement`: whitespace replacement; default `_`.

Empty replacement strings remove those characters. Invalid Windows filename
characters are obtained from the operating system. Slashes in a naming pattern
act as directory separators. Existing saved renaming settings retain precedence.

The optional NSCB Desktop GUI release source is configured with `RustDesktopRepository` in external-sources.json; its default is cxfcxf/nscb-desktop.


The default-settings.json Hints object documents the UI location of every default.
It is metadata and does not change the setting values. Standard JSON does not
accept # comments. Checkbox defaults for filename whitespace replacement remain
in naming-presets.json, alongside the invalid filename character rules; read-only
metadata checkboxes are not user settings.

BatchSeparatePackages controls only separate content rows (:1, :2, ...). BatchGameSortAlphabetical independently sorts base-game families and temporarily locks column sorting. Archives, subdirectories, folder grouping,
error filtering and the normal column sort also have central defaults.
MergeOutputLocation, SplitOutputLocation, CompressionOutputLocation and
DecompressionOutputLocation accept NextToSource, ActionSubdirectory or Custom.
CustomOutputDirectory supplies the editable custom path. These defaults appear
under Settings > Plugins > General > Default output directories; each action can
override them for its current run. PackageEngine also accepts RustWithKronosBackup
and KronosWithRustBackup.
## Master key validation and firmware mapping

`master-keys.json` is the single source for master-key CRC32 fingerprints and firmware labels.
The hexadecimal property name is the zero-based revision: `"0f"` means `master_key_0f`
(decimal revision 15, the 16th master key). NCA key-generation interpretation remains
in the reader; it is not an independently editable value in this catalog.

```json
"0f": { "Firmware": "16.0.0", "Crc32": 4082108335 }
```

These are fingerprints, not actual key values. To maintain a revision, update its
firmware label and unsigned 32-bit CRC32 in this one entry. Revisions use exactly
two lowercase hexadecimal characters. Duplicate revisions, empty firmware labels
and invalid checksums are rejected. Unknown revisions keep the existing unsupported-key
warning until a catalog entry is added and the application is rebuilt.

The file is embedded at build time as Viewer.MasterKeys.json. No additional file is
required next to the published executable, and editing the repository file affects the
next build. Publish.ps1 runs Validate-MasterKeys.ps1 before publishing. It can also
be run independently with `pwsh -File src/config/Validate-MasterKeys.ps1`.
The optional game-folders preset (Folders by game) groups Application, Patch, Addon and Combined files under {WAppTitle} [{AppId:U}]/ while retaining their default filenames. The base application ID keeps games, updates and DLC in the same game folder. Mixed-game MultiPackage files retain their separate presets because they do not have one unambiguous base game.
