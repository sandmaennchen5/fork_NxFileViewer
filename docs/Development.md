# Development and publishing

[Documentation index](README.md) · [Project overview](../README.md)

## Build

Use Windows and the .NET 10 SDK; Visual Studio 2026 or later is optional.
Run SDK commands from `src` so `src/global.json` selects the SDK.
`src/Directory.Build.props` supplies shared build settings.

From the repository root:

```powershell
cd src
dotnet restore NxFileViewer.sln
dotnet build NxFileViewer.sln -c Release --no-restore
```

## Tests

The test projects provide xUnit executable runners. From `src`, build to a shared
output directory and then run both executables:

```powershell
$testOutput = [IO.Path]::GetFullPath((Join-Path $PWD '../work/tests/'))
dotnet build NxFileViewer.sln -c Release "-p:OutDir=$testOutput"
& (Join-Path $testOutput 'NxFileViewer.Test.exe') -noLogo -noColor
& (Join-Path $testOutput 'LibHac.NSZ.Test.exe') -noLogo -noColor
```

Automated tests live under `src/*.Test`. The ignored `/test/` directory contains
personal test data.

Download-badge tests run without network access from the repository root:

```powershell
python -B -m unittest discover -s .github/scripts -p test_update_download_badge.py
```

## Full release packages

Use the explicit release mode from the **repository root**:

```powershell
.\Publish.ps1 -ReleasePackages
```

It builds Viewer and LogViewer ZIPs for Windows, Linux and macOS, in standard and self-contained variants. It also produces firmware references, the update descriptor and optional Explorer packages. Use -LogViewerOnly to select the LogViewer or -SkipLogViewer to omit it. See [Explorer integration](Explorer-integration.md#publishing) for signing options.

Windows Viewer ZIPs include NxFileViewerFork.exe, nxfileviewer-viewer.json, nxfileviewer-package.json and LICENSE.txt.

The Build workflow provides separate artifacts retained for seven days. Release
ZIPs belong in GitHub Releases for permanent downloads and application updates.
See [update packages and release automation](Application-updates.md).

## Individual executables

Select any of these switches to build only the requested EXEs:

| Program | x64 | x86 | ARM64 |
| --- | --- | --- | --- |
| Viewer | `-ViewerX64` | `-ViewerX86` | `-ViewerArm64` |
| LogViewer | `-LogViewerX64` | `-LogViewerX86` | `-LogViewerArm64` |
| Legacy transition updater | `-TransitionX64` | `-TransitionX86` | Not built |

Append `WithRuntime` to any selector for a self-contained variant. Selectors can
be combined:

```powershell
.\Publish.ps1 -ViewerX64
.\Publish.ps1 -ViewerX64 -LogViewerX86WithRuntime
.\Publish.ps1 -TransitionX64WithRuntime -OutputDirectory Publish-Test
```

These modes write EXEs into versioned output subfolders. They create no ZIPs,
release descriptors, firmware archives or Explorer packages. Without `WithRuntime`,
the target machine needs .NET 10 Desktop Runtime for the selected architecture.

## Development versions and local test copies

Publish commands read the version from the Viewer project or an explicit -Version. The Explorer extension has an independent version. Configure a local executable-copy target in the publish profile for testing.

## Publish menu, saved configuration and CI

`./Publish.ps1` is the common entry point. Without selectors it opens the Windows
GUI and loads `publish.ps1.conf` beside the script, when present; otherwise it uses both viewer
and LogViewer applications, all seven targets and both .NET variants. The `.conf`
file is validated JSON, never an executable PowerShell script. Use the built-in
profiles or save your own selections through the GUI; no example file is needed.
Local configuration is ignored by Git.

```powershell
./Publish.ps1                       # GUI; auto-build after 10 seconds without input
./Publish.ps1 -Gui                  # Explicit GUI, also with the 10-second countdown
./Publish.ps1 -NoGui                # Run saved/default settings immediately, without GUI
./Publish.ps1 -Avalonia             # Both Avalonia applications, configured variants
./Publish.ps1 -All                  # Reset this run to the default complete application set
./Publish.ps1 -ConfigFile my.conf    # Alternative JSON configuration
./Publish.ps1 -GitHub               # Full unattended CI artifact build; ignore .conf
./Publish.ps1 -ViewerX64            # Viewer x64 EXE-only selector
./Publish.ps1 --ViewerX64           # Same selector, double-dash spelling
./Publish.ps1 -AvaloniaViewer -AvaloniaRuntimeIdentifiers linux-x64 -WithDotNet
```

The menu configures applications, OS/architectures, with/without .NET, ZIP or
executable-only output, firmware references and a specific executable-copy variant.
Select optional Explorer, transition and update-descriptor packages in the Extras section.
**Profil speichern** saves the current named profile without building; **Bauen** builds the current selection;
**Abbrechen** closes without saving or building. After the window appears, a visible
10-second countdown automatically starts a build with the loaded configuration or
defaults if there is no input. A keyboard action, click, wheel action or change to an
input stops the autostart for that window; use **Bauen** to start manually afterward.
Closing the window cancels the timer. The GUI requires Windows; use `-NoGui` for
headless configured builds. Explicit application selectors and `-GitHub` do not open
the menu. Command
line options override saved configuration for that run; individual legacy selectors
and release/CI modes retain their own selection rules.

GitHub Build runs Publish.ps1 -GitHub and publishes Avalonia Viewer and LogViewer artifacts, firmware hashes, update metadata and legacy transition packages. The separate Release workflow builds Avalonia packages for tags and can generate migration aliases for existing WPF installations.

## Publish locally on Linux and macOS

Install PowerShell 7 (`pwsh`) and the .NET 10 SDK. Open a terminal in the
repository root, enter `pwsh`, then run the commands for your platform below.
These commands use the headless publisher and write to `Publish/` by default.
Use `-OutputDirectory <path>` to change the output directory.

### Linux

```powershell
# ZIP with the Linux executable
./Publish.ps1 -NoGui -AvaloniaViewer -AvaloniaRuntimeIdentifiers linux-x64 -WithDotNet

# AppImage and its ZIP, built on Linux with appimagetool on PATH
./Publish.ps1 -NoGui -AvaloniaViewer -AvaloniaRuntimeIdentifiers linux-x64 -WithDotNet -AppImage
```

Use `linux-arm64` for ARM64. AppImage packaging requires `appimagetool` on the
Linux build host. The repository's CI installs it with
[`Install-AppImageTool.sh`](../.github/scripts/Install-AppImageTool.sh).

### macOS

```powershell
# DMG containing the app bundle, built on macOS
./Publish.ps1 -NoGui -AvaloniaViewer -AvaloniaRuntimeIdentifiers osx-arm64 -WithDotNet -Dmg

# ZIP containing the app bundle, built on macOS
./tools/Publish-Applications.ps1 -Framework Avalonia -Applications Viewer -RuntimeIdentifiers osx-arm64 -RuntimeVariants $true -NativePackage App -ExecutableOnly -SkipFirmwareHashes -NoCopy
```

Use `osx-x64` for Intel Macs. The native packager uses macOS tools including
`hdiutil` for the DMG, `ditto` for the app ZIP and `iconutil` for the app icon.
The `-Dmg` command creates a direct DMG alongside the regular executable ZIP
and the legacy DMG ZIP. Distribute the direct `.dmg` for installing the app bundle.

Use `-WithoutDotNet` with `Publish.ps1` to build a package that uses the
installed runtime. Use `-AvaloniaLogViewer` for the LogViewer or
`-Applications LogViewer` with the app ZIP command. For installing the resulting
packages, see the [installation guide](Installation.md).

## Named selection profiles

`publish.ps1.conf` can contain multiple named profiles. The GUI profile selector
immediately loads an existing profile when switched, including keyboard selection.
Enter a new name and choose **Profil speichern** to save the current selection
separately. The independent **Startprofil (ohne -Profile)** selector saves which
profile to use on future runs without `-Profile`. Saving or loading another profile
does not change that start profile. An explicit `-Profile` takes precedence for
that run. Changing either selector counts as input and stops the ten-second autostart.

The built-in profiles are **Release** and **Prerelease**. Existing profiles with WPF selections are converted to their Avalonia counterparts when loaded. A flat schema-1 configuration loads as **Standard** and is saved in the schema-2 profile format when edited.

## Executable-only output, test copies and size reports

Configure these profile fields through the GUI or JSON:

- `CreateZipArchives`: `false` writes only the executable in each variant folder.
- `CopyExecutables` / `CopyDirectory`: enable optional copies to the test folder.
- `CopyRuntimeIdentifier` / `CopyWithDotNet`: choose one built variant to copy.
- `Applications`, `RuntimeIdentifiers`, `WithDotNet` and `WithoutDotNet`: select builds.

Close running test applications before replacing their executables. Executable-only
mode omits firmware ZIPs and outputs program executables without installer or
package metadata. Unix builds use extensionless program files.

Every successful run writes `publish-sizes-v<Version>.txt` to the output folder. The report lists each
variant's executable, unpacked output and ZIP sizes in MB, with two decimal places
(1 MB = 1,000,000 bytes). Tabs and padding align the longest filename/value.
`-` marks ZIPs not created. Firmware ZIPs are included when enabled.

## Local publishing from network drives

The unified publisher supports Windows PowerShell 5.1 and PowerShell 7 on Windows.
Windows packaging uses `Compress-Archive`; Linux/macOS Avalonia packaging uses
native `zip` to retain executable permissions.

Local Release builds skip the SDK's optional Git-metadata queries because reading
Git refs on SMB shares or mapped drives can fail with an invalid-parameter error.
This does not modify the repository. CI retains source-control metadata, and an
explicit `-p:EnableSourceControlManagerQueries=true` overrides the local default.

## Editable defaults and native components

- [Application and naming defaults](../src/config/README.md): embedded at build time; saved user settings take precedence.
- [External sources](../src/config/Update-sources.md): service addresses and package update configuration.
- [Explorer integration](Explorer-integration.md): native x64/x86/ARM64 extension and package build.
- [Download badge](../.github/Download-badge.md): viewer-only release counters and scheduled updates.
- [LogViewer](LogViewer.md) and [logging](Logging.md): diagnostics and saved logs.

Release diagnostics map source paths to `/_/NxFileViewer/`, with debug symbols
embedded in the executable. Runtime file paths remain visible.

## Contributing and attribution

Contributions and translations are welcome. See [credits and dependency versions](../CREDITS.txt),
the [project license](../LICENSE), [third-party notices](THIRD-PARTY-NOTICES.txt)
and [icon provenance](Icon-provenance.md). License notices are embedded in the application and available offline on the Info page.

Based on [Myster-Tee/NxFileViewer](https://github.com/Myster-Tee/NxFileViewer) and
[LibHac](https://github.com/Thealexbarney/LibHac). Thanks also to
[eliboa](https://github.com/eliboa), [THZoria](https://github.com/THZoria),
[nicoboss](https://github.com/nicoboss) and the Nintendo Switch community.
