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
tool is unavailable. NSZ/XCZ must be decompressed before these package operations.
Results are saved under the selected destination, with the same verification and
no-overwrite rules.

## Validation

Automated checks cover settings/JSON, retention of independently saved plugin
settings, CLI arguments, cancellation before startup and rejection of invalid
sources without changing originals. The application build is checked. Actual
merge/split/convert outputs from both engines still require practical comparison
using representative NSP/XCI files and their matching keys.
