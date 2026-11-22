# First converted Avalonia applications — early beta

[Documentation index](README.md) · [Development and publishing](Development.md)

**4.1.1-beta5** includes the first converted Avalonia applications: the viewer and
standalone LogViewer. The conversion is still in progress. **All Avalonia packages
are early beta versions; not all functions are implemented or tested.** They do not
yet provide complete WPF appearance or feature parity.

Beta releases publish these applications for Windows, Linux and macOS. Building a
package for a platform does not mean every function has been tested on that platform.
Windows-specific components and external tools can have additional limitations.
Use the Windows WPF viewer for the existing full application workflows.

## Applications and settings

- Viewer project: `src/NxFileViewer.Avalonia`; Windows executable: `NxFileViewerFork.Avalonia.exe`.
- LogViewer project: `src/NxFileViewer.LogViewer.Avalonia`; Windows executable: `NxFileViewerFork.LogViewer.Avalonia.exe`.
- Viewer preferences are stored separately in `NxFileViewerFork.Avalonia.settings.json`
  beside the executable. Existing WPF preferences can supply the initial settings.
- Keep WPF and Avalonia executable names distinct when testing them in the same folder.

## Build and test publishing

```powershell
dotnet build src/NxFileViewer.Avalonia/NxFileViewer.Avalonia.csproj -c Release
dotnet build src/NxFileViewer.LogViewer.Avalonia/NxFileViewer.LogViewer.Avalonia.csproj -c Release
```

The local `PublishTest.ps1` helper can select WPF/Avalonia projects and copy several
finished executables into a configured test folder with distinct destination names.
The regular `Publish.ps1` Windows release matrix remains independent of Avalonia.
For tags such as `v4.1.1-beta5`, the Release workflow additionally uses
`PublishAvalonia.ps1` on native Windows, Linux and macOS runners:

- Windows: x64, x86 and ARM64.
- Linux: x64 and ARM64.
- macOS: Intel x64 and Apple Silicon ARM64.

Both applications are packaged with and without .NET (28 additional ZIPs). Windows
ZIPs retain the `_x64`, `_x86` and `_arm64` suffixes; Linux and macOS use
`_linux-x64`, `_linux-arm64`, `_osx-x64` and `_osx-arm64`. Native ZIP packaging
preserves executable permissions on Linux/macOS. The macOS packages are unsigned
executable ZIPs, not signed `.app` installers.

The release waits for all three platform builds and verifies every expected ZIP
before publishing. Stable tags skip the Avalonia matrix. To build selected runtimes
locally, for example on Linux:

```powershell
./PublishAvalonia.ps1 -Version 4.1.1-beta5 -RuntimeIdentifiers linux-x64,linux-arm64
```
