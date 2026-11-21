# Experimental Avalonia frontends

[Documentation index](README.md) · [Development and publishing](Development.md)

The **4.1.1-beta3** source includes separate Avalonia viewer and LogViewer projects.
They are experimental frontends, not a replacement with full WPF feature parity.
The supported release/publishing matrix remains the Windows WPF application unless
an Avalonia build is selected explicitly. Linux/macOS are not claimed as tested or
published release targets merely because Avalonia is cross-platform.

## Viewer

Project: `src/NxFileViewer.Avalonia`; executable: `NxFileViewerFork.Avalonia.exe`.

Implemented areas include:

- File selection and drag/drop, package signatures and NSP/NSZ/XCI/XCZ/NRO inspection.
- WPF-style overview/content tabs with cover art, languages, build ID and program permissions.
- Shared title/content metadata, NCA SHA-256 checks against filename hash prefixes,
  original NCZ reconstruction and available header-signature checks.
- Batch folder and ZIP/7z scanning with cancellation, optional checks and verification of an existing list.
  Includes type/integrity/search/error filters, alphabetical sorting, column selection/reset,
  selected-file details, visible-column CSV/JSON export and a retained source history.
  History entries are re-read and never restore old integrity results as current verification.
- Batch name checks, rename proposals transferred to the simulation/review page, and moving
  verified unchanged originals without overwriting existing files.
- Staged batch compression/decompression through Rust or NSZ, and Rust split/merge/NSP/XCI
  conversion. Sources and outputs are verified; title versions and reconstructed entry SHA-256
  digests must agree before publication. This stricter path can reject engine output that changes
  ticket or other ancillary content; real-game end-to-end coverage is still limited.
- Local firmware-reference comparison using `Plugins/fwhashes/*.json`.
- Folder-based rename previews with target folder, filters, simulation, cancellation and a result/error table.
- Separate persisted naming patterns for applications, updates, DLCs, combined packages and collections.
  Existing local WPF TitleDB files supply title names without an online lookup.
- A NAND workspace using the shared read-only partition/FAT reader, BIS keys and file export.
  Physical-drive discovery and save-data browsing are still missing from this frontend.
- Tree context actions and a property table showing NCA header details.
- An integrated session log with level filters, independent from the standalone LogViewer.
- German, English, French and Spanish language selection and light/dark/automatic themes.
- WPF-style settings sub-tabs, plugin sub-sections, naming help, key validation,
  title-key selection, ticket injection, compression controls, optional/direct-start switches,
  batch history/export options and session-log level/retention settings.
- Info includes the report link, configured poll web overview, Discord, cached WPF support ID,
  credits, licenses and shortcuts. An explicit refresh registers/loads the support ID through
  the same service as WPF. Native reporting and poll forms are still WPF workflows.
- Online version checks for Avalonia releases and external tools, with startup/prerelease options.
  Release links are available; update installation and rollback are not implemented.
- Discovery of existing WPF plugin paths and managed plugin versions in the same application folder.

Preferences use the separate `NxFileViewerFork.Avalonia.settings.json` beside the
executable. The WPF viewer's community reporting/polls, updater, plugin managers,
Explorer integration, MTP/SD source controls and full workspace workflows are not equivalent features of
this frontend. Use the WPF viewer for its documented full feature set.

## LogViewer

Project: `src/NxFileViewer.LogViewer.Avalonia`; executable:
`NxFileViewerFork.LogViewer.Avalonia.exe`. It shares the log document/parser and
UI text definitions with the WPF LogViewer and has its own Avalonia interface.

## Build and test publishing

```powershell
dotnet build src/NxFileViewer.Avalonia/NxFileViewer.Avalonia.csproj -c Release
dotnet build src/NxFileViewer.LogViewer.Avalonia/NxFileViewer.LogViewer.Avalonia.csproj -c Release
```

The local `PublishTest.ps1` helper can select WPF/Avalonia projects and copy several
finished executables into a configured test folder with distinct destination names.
The regular `Publish.ps1` Windows release matrix does not imply Avalonia ZIP releases.
