# Naming patterns and batch naming

Patterns and character replacements are configured under **Settings → Naming settings**. The Renaming page links to this tab. Choose a preset separately for applications, updates, DLC, same-game combined packages (SuperNSP / SuperXCI), and mixed packages / DLC collections. The current pattern remains visible and can be copied. Choose **Manual** to open an editor; a manual pattern also has an **Edit** button. The editor validates variables and modifiers before accepting. Cancel leaves the previous pattern unchanged. Settings Apply/Cancel still controls whether the complete settings draft is saved.

## Editable preset catalog

The developer-facing preset catalog, defaults and replacement characters are documented alongside the configuration files: [src/config](../src/config/README.md#naming-presets).

Existing saved patterns are preserved. A pattern matching a preset selects that entry; other saved patterns show Manual without opening a dialog automatically. Changing the catalog default does not overwrite personal patterns in an existing settings file.

## Variables and manual editing

The **?** button and manual editor list variables allowed for the selected content type and explain their meaning. Patterns use `{Keyword}`; append `:U` for uppercase or `:L` for lowercase, such as `{TitleId:U}` and `{Ext:L}`.

| Variable | Meaning |
| --- | --- |
| `{TitleId}` | Current content title ID |
| `{AppId}` | Corresponding application title ID |
| `{PatchId}` | Corresponding patch ID for an application; otherwise zero |
| `{PatchNum}` | Patch/content revision number |
| `{Title}` | Local title from NACP; applications/updates only, not DLC |
| `{Ext}` | Detected package extension/type |
| `{VerNum}` | Numeric content version |
| `{VerDsp}` | Display version, when available |
| `{WTitle}` | Online title for the current content |
| `{WAppTitle}` | Online title for the corresponding application |

Online names use the existing local fallback where available. The Offline preset avoids online title variables, including in its DLC pattern. Escape literal braces as `\{` and `\}`. The editor checks pattern syntax, not the availability of metadata in a particular package; use simulation to check actual filenames and paths before renaming.

## SuperNSP / SuperXCI

Use the dedicated **Combined packages (SuperNSP / SuperXCI)** selector. Its preset and manual pattern are saved independently in RenamingOptions.SuperPattern. Standalone applications, updates and DLC retain their own selectors and patterns.
It also supports the compressed NSZ/XCZ variants and preserves the detected extension.

Pattern:

    {WAppTitle} [{AppId:U}] [v{PackageVerNum}] ({GameCount}G+{UpdateCount}U+{DlcCount}D).{Ext:L}

Example:

    Pokemon Legends Z-A [0100F43008C44000] [v393216] (1G+1U+1D).nsz

- GameCount: number of base-game CNMT contents in the physical package.
- UpdateCount: number of update CNMT contents.
- DlcCount: number of DLC CNMT contents.
- PackageVerNum: highest numeric base-game/update version; a higher DLC version never replaces it.
  For a standalone DLC, its own version is used.

SuperPattern is used when all contents belong to one application and include a base game or update. Mixed game IDs and DLC-only collections use MultiPattern. Unsupported content types are reported as errors.
Same-game bundles with multiple CNMT contents use SuperPattern, including update + DLC bundles without a base game. Files with exactly one content use their application, update or DLC pattern.
Local names fall back from the online lookup as with existing patterns.
Changing only the combined-package selector never changes standalone filenames. Selecting a count-based pattern explicitly in a standalone selector is still supported.
The existing presets and saved personal patterns retain their values.
Use simulation to preview; selecting a preset does not rename files.

## Mixed game IDs and DLC collections

A separate **Multi-game packages / DLC collections** selector controls MultiPattern.
The default is:

    MultiPaket ({GameCount}G+{UpdateCount}U+{DlcCount}D).{Ext:L}

For example:

    MultiPaket (2G+3U+5D).nsz

- Multiple application IDs are counted and named as a complete package; no game is selected arbitrarily.
- DLC-only collections, including several DLCs for the same game, also use MultiPattern.
- A single standalone DLC still uses AddonPattern.
- Only GameCount, UpdateCount, DlcCount and Ext are allowed in MultiPattern.
  A singular title, title ID or version would misrepresent a mixed package, so those variables are rejected.
- Literal names, relative folders and manual editing are supported as with other patterns.
- Existing saved settings receive the embedded MultiPattern default without changing single-file or SuperPattern settings.

## Target folders and simulation

An optional target directory sets the destination root. Without it, each source file's current directory remains the root. Relative subdirectories can be part of a pattern:

- `DLC/{WTitle}.{Ext:L}`
- `{WAppTitle}/DLC/{WTitle}.{Ext:L}`

Only literal pattern separators create directories; separators in metadata are sanitized. Absolute paths, empty path components and traversal through `..` are rejected. Use the target-directory field for an absolute destination.

Simulation reports proposed changes without moving files or creating directories. Actual renaming creates missing folders and never overwrites occupied targets. Failure leaves the source intact and is reported in the result table and Log.

The table displays old path, proposed/new path, status and error. `QUELL::` marks paths relative to the source root; `ZIEL::` marks a different target root. The target uses `QUELL::` when both roots are the same. These are display labels only; actual filesystem paths are unchanged.

## Batch actions

**Check naming** compares eligible physical NSP/NSZ/XCI/XCZ rows with current saved patterns and the target directory. Naming status, proposed path and naming errors are separate from integrity status; additional columns are available through Columns, at the end of the table.

Right-click **Check naming** shows a result dialog with status and paths. Right-click **Rename** first previews those paths and requires **Yes** or **Cancel**. Closing or cancelling does not rename the file.

**Rename all differing files** checks every eligible row again with current settings and renames only differences. Firmware and archive members are excluded. Existing targets are never overwritten. Online title lookup falls back to local NACP names when available.

Hover over a table cell or header to see its full text. Apply/Cancel/Reset in settings manage the settings draft; editing a pattern does not rename files by itself.

Combined.DefaultPreset selects the default SuperPattern; MultiPackage.DefaultPreset selects the default MultiPattern. Old saved settings missing these properties receive the corresponding embedded defaults without changing existing patterns. Configuration catalogs use SchemaVersion 2; the former shared-profile structure must be converted before publishing.

Old preset catalogs without MultiPattern use the generic MultiPaket count pattern.


## Batch selection actions

Select several rows with Ctrl or Shift. Right-clicking a selected row preserves the selection and opens a menu of batch-capable actions: verify, compress/decompress, check naming, rename and move. Each action shows the number of eligible files, for example Compress (2/5), and acts only on that subset. Single-file actions remain in the single-selection menu. Multiple package rows for the same source file are processed once. Naming changes retain the existing per-file confirmation and move collisions never overwrite an existing file.