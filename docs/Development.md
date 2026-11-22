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
.\Publish.ps1 -ReleasePackages -SkipAvalonia
```

It builds x64/x86/ARM64 WPF viewer and standalone LogViewer ZIPs, plus x64/x86
legacy transition ZIPs, in standard and self-contained variants, with firmware hashes, the update descriptor and
optional Explorer packages. `-LogViewerOnly` selects the LogViewer packages;
`-SkipLogViewer` omits them. Explorer signing and packaging options are described
in [Explorer integration](Explorer-integration.md#publishing).

Application ZIPs contain NxFileViewerFork.exe, nxfileviewer-package.json and LICENSE.txt.

The Build workflow provides separate artifacts retained for seven days. Release
ZIPs belong in GitHub Releases for permanent downloads and application updates.
See [update packages and release automation](Application-updates.md).

## Individual executables

Select any of these switches to build only the requested EXEs:

| Program | x64 | x86 | ARM64 |
| --- | --- | --- | --- |
| WPF viewer | `-ViewerX64` | `-ViewerX86` | `-ViewerArm64` |
| WPF LogViewer | `-LogViewerX64` | `-LogViewerX86` | `-LogViewerArm64` |
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

## beta5 development version and local test copies

The viewer, WPF/Avalonia LogViewers, Avalonia viewer and transition project use
`4.1.1-beta5` for direct builds. Publish/release commands still take their version
from the main viewer project or an explicit `-Version`; the Explorer package keeps
its independent version. Tag `v4.1.1-beta5` or `4.1.1-beta5` creates a prerelease when
published through the release workflow. Preparing local files does not create a tag
or publish a release.

Use `./Publish.ps1 -Profile 'Test Win'` for local test executables. This profile
builds the four Windows x64 applications without .NET, creates no ZIPs and copies
the executables to `C:\tmp\NxFileViewerTest`. Close running test executables before
replacement. Configure the profile in the GUI to change these choices. See
[first converted Avalonia applications (early beta)](Avalonia.md).

## Publish menu, saved configuration and CI

`./Publish.ps1` is the common entry point. Without selectors it opens the Windows
GUI and loads `publish.ps1.conf` beside the script, when present; otherwise it uses all four viewer
and LogViewer applications, all seven targets and both .NET variants. The `.conf`
file is validated JSON, never an executable PowerShell script. Use the built-in
profiles or save your own selections through the GUI; no example file is needed.
Local configuration is ignored by Git.

```powershell
./Publish.ps1                       # GUI; auto-build after 10 seconds without input
./Publish.ps1 -Gui                  # Explicit GUI, also with the 10-second countdown
./Publish.ps1 -NoGui                # Run saved/default settings immediately, without GUI
./Publish.ps1 -Wpf                  # Both WPF applications, configured variants
./Publish.ps1 -Avalonia             # Both Avalonia applications, configured variants
./Publish.ps1 -Wpf -Avalonia         # Both frameworks
./Publish.ps1 -All                  # Reset this run to the default complete application set
./Publish.ps1 -ConfigFile my.conf    # Alternative JSON configuration
./Publish.ps1 -GitHub               # Full unattended CI artifact build; ignore .conf
./Publish.ps1 -ViewerX64            # Existing WPF x64 EXE-only selector
./Publish.ps1 --ViewerX64           # Same selector, double-dash spelling
./Publish.ps1 -AvaloniaViewer -AvaloniaRuntimeIdentifiers linux-x64 -WithDotNet
```

The menu configures applications, OS/architectures, with/without .NET, ZIP or
executable-only output, firmware references and a specific executable-copy variant.
The optional release-extras checkbox adds Explorer, transition and update-descriptor
packages through the established full release builder. It requires both WPF apps,
all Windows architectures, both .NET variants, ZIPs and firmware hashes, with test
copying disabled.
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

GitHub **Build** runs `Publish.ps1 -GitHub` for pushes to `master`/`beta` and pull
requests to `master`. It builds the full existing WPF release artifact set (including
transition and Explorer packages), plus every Avalonia package. It never opens a
menu, reads local `.conf` files or copies programs into a local test folder.

**Release** remains a separate version-tag workflow. Its Windows job uses
`-ReleasePackages -SkipAvalonia` for the established WPF release packages. Beta tags
additionally build Avalonia on native Windows/Linux/macOS runners, explicitly
ignoring editable local target/copy defaults. Stable tags keep WPF packages only.

## Named selection profiles

`publish.ps1.conf` can contain multiple named profiles. The GUI profile selector
immediately loads an existing profile when switched, including keyboard selection.
Enter a new name and choose **Profil speichern** to save the current selection
separately. The independent **Startprofil (ohne -Profile)** selector saves which
profile to use on future runs without `-Profile`. Saving or loading another profile
does not change that start profile. An explicit `-Profile` takes precedence for
that run. Changing either selector counts as input and stops the ten-second autostart.

```powershell
./Publish.ps1 -Profile WPF            # Open GUI with the built-in WPF profile
./Publish.ps1 -NoGui -Profile Avalonia # Build the built-in Avalonia profile immediately
```

The four built-in selections are **Standard**, **WPF**, **Avalonia** and **Test Win**.
Create any additional profiles in the GUI.
An existing flat schema-1 configuration still loads as **Standard**. Saving a named
profile converts the file to the schema-2 profile container while preserving the
original selection and other profiles. Profile names support up to 60 letters,
numbers, spaces, dots, underscores and hyphens. `-GitHub` ignores local profiles.

## Predefined publish profiles

Four built-in profiles are available automatically, including in existing configuration
files. Existing custom profiles with the same names are preserved. **Standard** is
the default start profile for a new configuration.

| Profile | Applications / targets | .NET | Output | Firmware hashes | Test copies |
| --- | --- | --- | --- | --- | --- |
| Standard | All four apps; all seven targets | With and without | ZIPs in `Publish` | Yes | No |
| WPF | Both WPF apps; Windows x64/x86/ARM64 | With and without | ZIPs in `Publish/WPF` | No | No |
| Avalonia | Both Avalonia apps; all seven targets | With and without | ZIPs in `Publish/Avalonia` | No | No |
| Test Win | All four apps; Windows x64 | Without | Executables only in `Publish` | No | Windows x64 without .NET to `C:\tmp\NxFileViewerTest` |

```powershell
./Publish.ps1 -Profile WPF
./Publish.ps1 -Profile Avalonia
./Publish.ps1 -NoGui -Profile 'Test Win'
```

All application packages now use the internal `tools/Publish-Applications.ps1`
backend. GitHub/native release jobs use explicit arguments, without local profile
or test-copy preferences. The full tag-release builder remains separate.

## Executable-only output, test copies and size reports

Configure these profile fields through the GUI or JSON:

- `CreateZipArchives`: `false` writes only the executable in each variant folder.
- `CopyExecutables` / `CopyDirectory`: enable optional copies to the test folder.
- `CopyRuntimeIdentifier` / `CopyWithDotNet`: choose one built variant to copy.
- `Applications`, `RuntimeIdentifiers`, `WithDotNet` and `WithoutDotNet`: select builds.

Close running test applications before replacing their executables. Executable-only
mode omits firmware ZIPs and outputs program executables without installer or
package metadata. Unix builds use extensionless program files.

Every successful run writes `publish-sizes-WPF-v<Version>.txt` or
`publish-sizes-Avalonia-v<Version>.txt` to the output folder. The report lists each
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
