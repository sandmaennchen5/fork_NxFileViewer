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

Use `./Publish.ps1 -Profile 'Test Win'` for Windows x64 test executables, without
.NET or ZIPs, copied to the configured test folder with distinct WPF/Avalonia names.
The predefined **Avalonia** profile is the entry point for all Avalonia packages:

```powershell
./Publish.ps1 -Profile Avalonia        # GUI with this profile and 10-second countdown
./Publish.ps1 -NoGui -Profile Avalonia # Build immediately
```

Select architectures, .NET variants, ZIP/program-file output and optional test copies
in the GUI, then save the profile. The default Avalonia preset includes:

- Windows: x64, x86 and ARM64.
- Linux: x64 and ARM64.
- macOS: Intel x64 and Apple Silicon ARM64.

Both applications are packaged with and without .NET (28 ZIPs). Windows ZIPs retain
`_x64`, `_x86` and `_arm64`; Linux/macOS use `_linux-x64`, `_linux-arm64`, `_osx-x64`
and `_osx-arm64`. Native Unix ZIP packaging preserves executable permissions.
The macOS packages are unsigned executable ZIPs, not signed `.app` installers.

Beta-tag releases build on native Windows/Linux/macOS runners using the common
internal publisher, wait for every platform and verify expected ZIPs before release
publication. Stable tags retain WPF releases. For a targeted headless build:

```powershell
./Publish.ps1 -AvaloniaViewer -AvaloniaRuntimeIdentifiers linux-x64,linux-arm64 -WithDotNet
```
