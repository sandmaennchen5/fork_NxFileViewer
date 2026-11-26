# Avalonia applications

[Documentation index](README.md) · [Installation and data paths](Installation.md) · [Development and publishing](Development.md)

The Viewer and LogViewer use Avalonia. Release packages target Windows, Linux and macOS. Windows Explorer integration and some external tools require Windows.

## Applications and settings

- Viewer project: `src/NxFileViewer/NxFileViewer.csproj`; Windows executable: `NxFileViewerFork.exe`.

Release ZIPs use `NxFileViewerFork_v<version>_<runtime>-with-net.zip` or `NxFileViewerFork_v<version>_<runtime>-without-net.zip`. ZIPs containing a native AppImage or DMG add `-appimage` or `-dmg` before `.zip`.

For a release that switches installed WPF copies to Avalonia, run `Publish.ps1 -Version <version> -Extras WpfMigration` after building the Avalonia Windows ZIPs. This creates compatibility aliases for x64, x86 and ARM64 and writes the update manifests. The legacy WPF updater selects the alias and reads the new entry point from its package manifest. Run `-Extras UpdateManifest` without migration aliases for later releases once that upgrade path is no longer needed.
- LogViewer project: `src/NxFileViewer.LogViewer`; Windows executable: `NxFileViewerFork.LogViewer.exe`.
- Viewer preferences are stored in `NxFileViewerFork.settings.json`. Shared settings use the WPF layout and Avalonia-specific preferences are stored under `AvaloniaPreferences`. Avalonia reads the previous settings filenames for migration.
  beside the executable. Existing WPF preferences can supply the initial settings.

## Data directory on Windows and Linux

The Viewer stores data in `NxFileViewerFork-Daten` next to the executable or
AppImage. Existing installations with program data directly beside the
executable keep using that directory until the new data folder exists.

In **Settings → Updates**, **Migrate application data** appears when eligible
data is found. It copies missing files and keeps the originals and existing
destination files. Restart the Viewer after migration. On Linux, the previous
data folders in the user's data directory are also considered as sources.


## Build and test publishing

```powershell
dotnet build src/NxFileViewer/NxFileViewer.csproj -c Release
dotnet build src/NxFileViewer.LogViewer/NxFileViewer.LogViewer.csproj -c Release
```

Use `./Publish.ps1 -Profile 'Test Win'` for Windows x64 test executables, without
.NET or ZIPs, copied to the test folder configured for the selected framework.
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
The native macOS build provides unsigned app bundles in direct DMGs and App ZIPs.

Beta-tag releases build on native Windows/Linux/macOS runners using the common
internal publisher, wait for every platform and verify expected ZIPs before release
publication. For a targeted headless build:

```powershell
./Publish.ps1 -AvaloniaViewer -AvaloniaRuntimeIdentifiers linux-x64,linux-arm64 -WithDotNet
```

## Application updates

The Viewer checks stable releases by default. Enable prereleases in the update
settings to include them. The update page selects the matching OS,
architecture and self-contained/framework-dependent package, verifies its SHA-256,
then installs it after the current process exits and restarts the application.
File operations must finish before installing an update. The app verifies that the
new main window opens; installation/restart failures restore backed-up program
files and restart the previous intact executable. Update backups and diagnostics
are retained under `Updates/update-*/` beside the application. Settings, keys and
plugins are outside the set of files an update may replace. The install folder
must be writable by the user. Debug/non-single-file builds use manual updates.

On Linux, an AppImage uses the same update page. It selects the matching standalone
AppImage from the release, verifies its SHA-256, and replaces the running AppImage
at its existing path. Desktop shortcuts and the adjacent `NxFileViewerFork-Daten`
directory continue to work. The parent directory must be writable.

macOS releases provide the app bundle in both a direct `.dmg` and an `.app.zip`.
The in-app updater prefers the matching DMG and uses the ZIP when the release
offers no matching DMG. It verifies the download, mounts the disk image read-only
when applicable, and replaces the bundle at its original path. If the updated app
fails to open, the previous bundle is restored. Copy the app out of a mounted DMG
to a writable location before updating.

The current ZIP and executable names retain `Avalonia`. A later transition to
`NxFileViewerFork_v...zip` / `NxFileViewerFork` is supported only with both a
`nxfileviewer-packages.json` release index and an internal Avalonia package manifest.
An unmarked generic Windows ZIP can belong to WPF and is never offered as an
Avalonia update. Executable-name collisions with another installation are refused.
The publishing and release workflows retain existing ZIP names and additionally
upload checksum/identity sidecars. Already published ZIPs are not rewritten;
their GitHub SHA-256 digest is required if no sidecar is available.

## Linux launchers

Extract the Linux ZIP into a writable folder and launch the executable directly.
If your archive tool drops executable permissions, run chmod +x NxFileViewerFork
in the extracted folder. For the LogViewer, use NxFileViewerFork.LogViewer.

In the Viewer, open **Settings → Program → Shortcuts** to create a launcher
in the applications menu, on the desktop, or in both places. This also works
when the Viewer runs from an AppImage. Some desktop environments require
**Allow Launching** once for a new desktop shortcut. Recreate the shortcut
after moving the application.


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
./.github/scripts/Test-AvaloniaUpdateWorker.ps1 -ExecutablePath /path/to/NxFileViewerFork.exe
./.github/scripts/Test-AvaloniaUpdateWorker.ps1 -ExecutablePath /path/to/NxFileViewerFork.exe -SimulateFailure
```

This tests the real process handoff and main-window acknowledgement; failure mode
also tests restoring and restarting the previous executable. Linux/macOS GUI
restart behavior still needs native end-to-end testing.

Releases publish one `nxfileviewer-packages.json` containing the names, frameworks and SHA-256 checksums of all published ZIPs. Per-ZIP metadata remains an internal build artifact and is not uploaded to releases. Older per-ZIP release markers remain supported by the updater.
