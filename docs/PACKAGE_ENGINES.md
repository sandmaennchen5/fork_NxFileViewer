# Package engines

Under **Plugins → Merge / Split contents / NSP ↔ XCI**, configure either backend:

- **cxfcxf / nscb_rust:** path to `nscb_rust.exe`.
- **Kronos / NSC_BUILDER:** portable directory containing `runtime/python.exe` and
  `ztools/squirrel.py`, such as a locally assembled Kronos test package.

Choose the engine and paths, then click **Apply**. Status and action buttons use
the saved settings.

## Installation

**Install / update plugin** beside each path downloads the engine and saves the new
path after validation. Other pending settings are not applied. Cancellation or
failure preserves the previous path. Each installation uses a new directory under
`Plugins/PackageEngines` without overwriting previous versions. Engine updates are
not automatic.

Both engines also appear under **Updates**, with their installed path and an
**Install / update plugin** button. **Check for updates** compares nscb_rust's
installed release version with the latest stable release and Kronos's installed
commit with the current master commit. Custom installations without matching
installation metadata cannot be checked automatically. Installing from Updates
also refreshes an open settings draft so applying it later retains the new path.

nscb_rust is downloaded as a Windows executable from the latest stable
[cxfcxf/nscb_rust release](https://github.com/cxfcxf/nscb_rust/releases), verified
against the release asset's SHA-256 digest.

Kronos has no ready-made release package. The installer records the current
`master` commit of [Kronos2308/NSC_BUILDER](https://github.com/Kronos2308/NSC_BUILDER)
before downloading it, then adds SHA-256-verified portable Python 3.12.10 (x64)
from python.org and Python dependencies from PyPI. This requires an internet
connection and can take several minutes. System Python is unchanged. The
installation is activated only after a successful CLI startup test. PyPI resolves
dependency versions, so the resulting installation is not fully reproducible.

The nscb_rust selection for compression/decompression is independent of the engine
selected for merge, split and NSP/XCI conversion.

## Actions

- **Merge to NSP / XCI:** combine multiple local NSP/XCI files.
- **Convert to NSP / XCI:** convert one file, or process selected files individually.
- **Split to NSP / XCI:** repack the contents of a multi-content file as individual
  packages. This is not FAT32 file splitting.

Only one version per title ID may be selected. Sources remain unchanged. Background
jobs support cancellation and retain the engine captured at their start, even if
settings change later.

## Verification and output handling

Engines write to an isolated temporary job directory under the selected destination.
The viewer checks NCA integrity and compares the complete set of title IDs and
versions before and after processing. Changed signatures with valid hashes are
allowed for repacking. Missing or corrupted contents prevent publication.

Only verified package files are published into a new `NXFV-<Operation>-<ID>`
directory. Existing results are never overwritten. Automatic firmware/key-generation
downgrading is not enabled.

Kronos receives job-local copies of its working files and the required key file.
Its installed configuration is unchanged. The job directory is cleaned after
completion or cancellation; allow space for both the working copy and output.

Verified files are moved individually; publication does not require renaming the
working directory. Temporary file locks are retried briefly. On a persistent
failure, verified outputs are retained and the error identifies the affected
directories. Partially published output contains `NXFV-INCOMPLETE.txt`. Private
engine working files are still cleaned up.

## Batch selection

Right-clicking a selected row preserves multiple selection. For local NSP/XCI
files, the menu offers Convert to NSP/XCI and, with multiple files, Merge to NSP/XCI.
Files with multiple detected CNMT contents also offer Split to NSP/XCI.

Convert and Split process each source separately. Merge passes all eligible
selected files to one engine invocation, sorted using the CNMT metadata reread
at job start: **Application (game) → Patch (update) → AddOnContent (DLC)**.
For a Super file, its earliest content type in that ranking determines its
position. Ties retain input order. Filenames and table sorting do not determine
content types.

The active engine from Settings is used. Matching actions are disabled when the
tool is unavailable. NSZ/XCZ inputs are automatically decompressed into a private temporary workspace before these package operations. The selected compression/decompression plugin must be enabled and configured. Both compressed inputs and decompressed outputs are verified; original files are retained. Temporary inputs are removed after completion, cancellation, or failure.
Results are saved under the selected destination, with the same verification and
no-overwrite rules.

## Validation

Automated checks cover settings/JSON, retention of independently saved plugin
settings, CLI arguments, cancellation before startup and rejection of invalid
sources without changing originals. The application build is checked. Actual
merge/split/convert outputs from both engines still require practical comparison
using representative NSP/XCI files and their matching keys.

When multipackages are shown as separate batch rows, Split still operates on the complete source file. Selecting several rows from the same source processes that file only once.


## Optional desktop GUI

Under **Plugins → NSCB Desktop · nscb_rust GUI**, install the optional
[cxfcxf/nscb-desktop](https://github.com/cxfcxf/nscb-desktop) portable Windows x64
application. The installer verifies the release asset's SHA-256 digest and
installs nscb_rust too if no configured backend exists. Existing backend paths and
the selected package engine are retained. The GUI has its own entry under
**Updates** and a direct launcher on **Start**.

Each launch receives a temporary copy of the configured backend and selected
keys, using a private `nscb-desktop-tools` folder via process-local TEMP/TMP.
The session folder is removed when the GUI exits; desktop-only settings stored
there are also temporary. Without configured keys, use the desktop setup wizard.
Windows WebView2 is required by the upstream desktop application.

The release repository can be changed in `src/config/external-sources.json`
using `RustDesktopRepository`. A moved program folder is detected for managed
desktop installations just as for nscb_rust.

## Kronos direct launch

The **Start** page and **Plugins** offer a direct launch of Kronos's browser GUI
using its configured portable Python installation and the selected prod.keys.
Each launch gets a private working copy under Plugins/KronosGuiSessions, bound
to 127.0.0.1 on an automatically selected port. Existing installation keys are
not replaced. When the GUI exits, its copied keys are removed; produced files
and the workspace are retained.

Under **Settings → Optional features → Direct launches of external interfaces**,
NSZ, NxNandManager, NSCB Desktop and Kronos can each be disabled independently.
These switches affect GUI launch buttons and commands immediately after applying
settings. Internal plugin operations and update/install commands stay available.
Their default values are configurable in src/config/default-settings.json.

Explorer package commands accept NSP, XCI, NSZ and XCZ, including mixed merge
selections. Before Split starts hashing or decompressing, the viewer opens the
source and checks whether it contains multiple CNMT contents. Single-content
files are skipped with an informational message. In a multi-file selection,
other eligible files continue. Compressed multipackages are then decompressed
and verified before splitting.

## Batch-wide split and merge

The Batch toolbar provides **Split all multipackages** and **Merge packages**,
operating on the loaded list with each physical file processed once.

- Split offers **Keep source format**, **NSP**, or **XCI**. Keeping source format
  means NSP for NSP/NSZ inputs and XCI for XCI/XCZ inputs; split outputs are
  uncompressed.
- Merge offers **By game ID** (base game, updates and DLC of the same base title)
  or **Multi-game package**, followed by **SuperNSP** or **SuperXCI**.
- The game-ID mode skips ambiguous metadata with a visible explanation. If a
  multi-game container has matching additional contents in the loaded list, the
  app asks whether to split it temporarily and merge its parts by game ID.
  Yes performs the preparation, No skips those containers, and Cancel stops the
  operation. Identical duplicate contents or unrelated games do not trigger it.
  Ordinary games with no merge partner are skipped; an existing same-game
  multipackage can be repacked to the chosen format.
- Mixed NSP/NSZ/XCI/XCZ inputs are supported. Compressed sources are temporarily
  decompressed and verified using the configured decompression plugin.
- Jobs run sequentially with shared progress. A failed batch job is reported and
  other jobs continue; cancellation stops the queue. Original files remain intact.
Automatic split preparation keeps originals unchanged, re-reads the produced metadata and uses the selected destination only once. Its private workspace is removed after the merge finishes, fails, or is cancelled; final published outputs remain outside that workspace.


## Managed versions and plugin settings

Plugin settings are divided into General (log levels), Compress / Decompress,
Merge / Split contents / Convert (experimental), and NxNandManager. Disabled
optional features hide their corresponding settings tabs. Path browse controls
stay available. Install plugin is shown only without an installation or manual
path; use Updates for subsequent installations, previous versions and cleanup.

nscb_rust, Kronos and NSCB Desktop track current and previous managed
installations under Plugins/PackageEngines. Rollback keeps the other version
available and checks an executable checksum where supplied. Cleanup asks before
deleting old versions and excludes active/manual paths, running files, linked
directories and versions containing key files. Custom installations are not
adopted or removed.
## Engine backup, versions and output locations

The package engine selector supports nscb_rust, Kronos, nscb_rust with Kronos backup, and Kronos with nscb_rust backup. Merge, Split and Convert also offer this choice in the output dialog. A failed engine process or invalid output triggers one attempt with the configured backup in a separate output workspace. Cancellation, input validation failures and publication errors do not trigger a backup. Every output must pass integrity and content-set checks before publication.

Plugin status uses the installed release tag for nscb_rust and NSCB Desktop. Kronos uses the first 12 characters of its pinned source commit. Full commits and SHA-256 digests remain in installation metadata for identity and verification; a short version label does not replace a digest.

The Start-page Kronos button launches the interactive NSCB.bat console (NSCB.exe when no batch starter exists), with a private copy of the selected keys. Its session configuration uses the managed private Python runtime. It does not automatically launch Interface.start or the web interface. Old Kronos versions can be removed when they contain the bundled empty title.keys placeholder; nonempty key files and active installations stay protected.

Result files are published directly into the selected output directory, without public NXFV-Merge-/NXFV-Split- GUID folders. Choose next to the source, an action subdirectory or a custom path. Merge uses the first original source as its source-relative destination; Split resolves its destination per source. Existing files are never overwritten by package publication. If publication fails, validated files and recovery information are retained. The success dialog offers **Open in Batch** for newly produced files.

Managed version pointers, per-version metadata and automatic migration use the [shared plugin version storage](Plugin-version-storage.md).

The original NSCB launchers and zconfig are copied into the private session; original plugin settings and keys remain intact. Only the session key copy is removed when the launcher exits. Results remain in the session directory.
