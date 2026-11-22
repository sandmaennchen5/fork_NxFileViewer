# First converted Avalonia applications — early beta

[Documentation index](README.md) · [Development and publishing](Development.md)

**4.1.1** includes the first converted Avalonia applications: the viewer and
standalone LogViewer. The conversion is still in progress. **All Avalonia packages
are early beta versions; not all functions are implemented or tested.** They do not
yet provide complete WPF appearance or feature parity.

Prereleases publish the Avalonia viewer for Windows, Linux and macOS; the LogViewer remains available through local builds and Build artifacts. Building a
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

Local builds can package both applications with and without .NET (28 ZIPs). GitHub releases publish only the viewer (14 Avalonia ZIPs), without standalone LogViewer packages. Windows ZIPs retain
`_x64`, `_x86` and `_arm64`; Linux/macOS use `_linux-x64`, `_linux-arm64`, `_osx-x64`
and `_osx-arm64`. Native Unix ZIP packaging preserves executable permissions.
The macOS packages are unsigned executable ZIPs, not signed `.app` installers.

Beta-tag releases build on native Windows/Linux/macOS runners using the common
internal publisher, wait for every platform and verify expected ZIPs before release
publication. Stable tags retain WPF releases. For a targeted headless build:

```powershell
./Publish.ps1 -AvaloniaViewer -AvaloniaRuntimeIdentifiers linux-x64,linux-arm64 -WithDotNet
```

## Application updates

New Avalonia configurations enable prerelease checks by default. An explicitly saved
prerelease preference is retained. The update page selects the matching OS,
architecture and self-contained/framework-dependent package, verifies its SHA-256,
then installs it after the current process exits and restarts the application.
File operations must finish before installing an update. The app verifies that the
new main window opens; installation/restart failures restore backed-up program
files and restart the previous intact executable. Update backups and diagnostics
are retained under `Updates/avalonia-*/` beside the application. Settings, keys and
plugins are outside the set of files an update may replace. The install folder
must be writable by the user. Debug/non-single-file builds use manual updates.

The current ZIP and executable names retain `Avalonia`. A later transition to
`NxFileViewerFork_v...zip` / `NxFileViewerFork` is supported only with both a
`nxfileviewer-packages.json` release index and an internal Avalonia package manifest.
An unmarked generic Windows ZIP can belong to WPF and is never offered as an
Avalonia update. Executable-name collisions with another installation are refused.
The publishing and release workflows retain existing ZIP names and additionally
upload checksum/identity sidecars. Already published ZIPs are not rewritten;
their GitHub SHA-256 digest is required if no sidecar is available.

## Linux launchers

Extract the complete Linux ZIP into a writable folder. Both executables and the
included `start.sh` and `install-shortcut.sh` carry executable permission bits,
including ZIPs built on Windows. A desktop that permits executable files to run
can launch the executable directly with a double-click. File-manager policy varies.

For a menu/desktop launcher, start the viewer once and choose **Info → Create Linux
launcher**, or run `sh ./install-shortcut.sh` in the extracted folder. This creates
a `.desktop` entry with the absolute path to `start.sh`, so paths containing spaces
work and later app updates can keep the same launcher. Some desktops require
**Allow Launching** once for a new desktop shortcut. After that, double-clicking it
starts the application without a terminal. Keep the application folder in place;
recreate the shortcut after moving it. Framework-dependent packages still require
.NET 10; self-contained packages bundle the runtime.

## Updater validation

The build workflow runs `AvaloniaUpdaterTest` for release selection, numeric beta
versions, ZIP/path validation, platform checks, checksums, user-file preservation
and rollback. Packaging tests verify every platform and both runtime variants,
including Linux executable bits and checksum sidecars. Release publication checks
that every Avalonia ZIP has matching metadata before making the release visible.

On Windows, the optional GUI worker smoke test takes a published single-file
viewer and uses an isolated temporary installation, with reporting/polls/network
checks disabled. It opens and closes only its own test window:

```powershell
./.github/scripts/Test-AvaloniaUpdateWorker.ps1 -ExecutablePath /path/to/NxFileViewerFork.Avalonia.exe
./.github/scripts/Test-AvaloniaUpdateWorker.ps1 -ExecutablePath /path/to/NxFileViewerFork.Avalonia.exe -SimulateFailure
```

This tests the real process handoff and main-window acknowledgement; failure mode
also tests restoring and restarting the previous executable. Linux/macOS GUI
restart behavior still needs native end-to-end testing.

Releases publish one `nxfileviewer-packages.json` containing the names, frameworks and SHA-256 checksums of all published ZIPs. Per-ZIP metadata remains an internal build artifact and is not uploaded to releases. Older per-ZIP release markers remain supported by the updater.
