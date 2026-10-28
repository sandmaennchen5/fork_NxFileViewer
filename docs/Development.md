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

Run Publish.ps1 from the **repository root**:

```powershell
.\Publish.ps1
```

It builds x64/x86 viewer, legacy transition and standalone LogViewer ZIPs, both
standard and self-contained, plus firmware hashes, the update descriptor and
optional Explorer packages. `-LogViewerOnly` selects the LogViewer packages;
`-SkipLogViewer` omits them. Explorer signing and packaging options are described
in [Explorer integration](Explorer-integration.md#publishing).

Application ZIPs contain NxFileViewerFork.exe, nxfileviewer-package.json and LICENSE.txt.

The Build workflow provides separate artifacts retained for seven days. Release
ZIPs belong in GitHub Releases for permanent downloads and application updates.
See [update packages and release automation](Application-updates.md).

## Individual executables

Select any of these switches to build only the requested EXEs:

| Program | x64 | x86 |
| --- | --- | --- |
| Viewer | `-ViewerX64` | `-ViewerX86` |
| LogViewer | `-LogViewerX64` | `-LogViewerX86` |
| Legacy transition updater | `-TransitionX64` | `-TransitionX86` |

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
