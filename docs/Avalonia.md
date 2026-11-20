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
- Shared title/content metadata, NCA SHA-256 checks against filename hash prefixes,
  original NCZ reconstruction and available header-signature checks.
- Batch folder scanning with cancellation and optional checks.
- Local firmware-reference comparison using `Plugins/fwhashes/*.json`.
- Offline rename suggestions, editable previews and confirmed rename application.
- German, English, French and Spanish language selection and light/dark/automatic themes.

Preferences use the separate `NxFileViewerFork.Avalonia.settings.json` beside the
executable. The WPF viewer's community reporting/polls, updater, plugin managers,
Explorer integration and full workspace workflows are not equivalent features of
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
