# Plugin nicoboss/nsz

NxFileViewer uses the official [nicoboss/nsz](https://github.com/nicoboss/nsz) Windows CLI as a separate process. The viewer's built-in NSZ reader remains responsible for browsing and integrity checks; conversion does not replace it.

## Opened file

Open **File check → Compress and verify…** for NSP/XCI, or **Decompress and verify…** for NSZ/XCZ, then select an output directory. Only the applicable action is enabled. Successfully converted output opens in the viewer with its verification result.

## Batch

Run a folder integrity check, then choose **Compress valid files…** or **Decompress valid files…**. These actions process all matching files reported as Original, including rows hidden by the error filter. Firmware is excluded. Relative subdirectories are preserved. Source files are rechecked immediately before conversion, so stale batch results cannot authorize an invalid source.

Completed outputs are added to the results. Source rows show conversion status and original/output byte sizes. Conversion failures also appear under the errors-only filter, without changing the original file's integrity result. CSV exports include conversion status, target path and sizes. Cancellation stops the process tree; completed outputs remain, while the current temporary output is discarded.

Right-click a batch row to open it in single-file mode, convert it, open its title website, verify its integrity or move it. Actions apply only to the clicked row. Individual conversions verify the source even when it has not yet been checked in the batch. Conversion and moving require a physical file, rather than an archive member.

## Installation and updates

- **Settings → Updates → Plugins → nicoboss/nsz → Install / update…** fetches the latest stable GitHub release, excluding drafts and prereleases. Initial conversion also installs the plugin when needed.
- Files are stored in `Plugins/NSZ` next to `NxFileViewer.exe`. The application directory must be writable for installation and updates. Managed paths in `active.json` are relative so the application can be moved with its plugin. AppData installations are not read, imported or used as a fallback. Install the plugin in the program directory. Custom executable paths remain unchanged.
- The updater selects the official Windows x64 or ARM64 CLI asset, validates the SHA-256 digest published by GitHub, and runs `--help` with a timeout to check the required options. Only then is the active-version pointer replaced. This is a CLI compatibility check, not a conversion test against every game.
- Automatic checks before conversion reuse the shared six-hour GitHub metadata cache; explicit update actions request fresh metadata when API limits permit. Cooldowns survive restarts. See [update checks](Application-updates.md#request-cache-and-github-limits).
- The previous installed version remains available through **Use previous version**. Rolling back turns off automatic update checks so the next conversion keeps that version.
- **Settings → Plugins → nicoboss/nsz** controls checks before conversion, compression level (1–22, default 18) and an optional custom executable. Leave the path empty for managed releases. A custom executable is checked for compatibility but is never automatically replaced.
- If an update check fails and a compatible version is already installed, the viewer logs a warning and continues with that version. A failed initial installation prevents conversion.
- Automatic installation requires 64-bit Windows and network access. New upstream releases with renamed assets or incompatible CLI options are rejected rather than activated.

## Verification and file handling

The source is kept under a read lock while its NCA hashes/signatures are verified, converted, and the result is checked. Only `Original` is accepted; the existing setting for missing delta fragments applies. This checks NCA integrity, not a whole-container byte-for-byte comparison.

The plugin receives the viewer's detected `prod.keys` path via `--keys`. Compression uses `-K -V` to preserve additional files/partitions and request NSZ's own verification. No deletion or overwrite flags are passed. See the upstream [CLI documentation](https://github.com/nicoboss/nsz/blob/master/docs/usage.md).

Each conversion writes to a unique temporary subdirectory on the destination volume. The expected non-empty output must pass NxFileViewer's integrity check before it is moved to its final name. Existing targets offer Replace, Save with numbering, or Cancel. Replacement occurs only after successful output verification; numbering skips occupied file and directory names, including conflicts arising during conversion. Before each operation, the viewer asks whether to delete sources after success (default: keep). Sources are deleted only after the verified output is published. Deletion failures retain the output and show a warning. A failed check, plugin failure or cancellation removes the temporary output. Temporary data may remain if Windows denies cleanup; it can be recognized by the `.nxfv-nsz-` folder prefix.

Allow disk space for both the original and the new file, plus temporary decoding data required by solid NSZ verification. Large conversions can take time; verification and conversion have separate progress phases, with CLI percentage, speed and ETA when available.

## Startup failures

`NSZ exit code: -1` at the `--help` compatibility check means the external executable failed to start or return successful help. On 2026-10-05 the official 5.0.0 Windows x64 CLI was downloaded, its published SHA-256 was verified, and the failure was reproduced on the local host: the PyInstaller bootloader could not load `python311.dll` (`LoadLibrary` reported an invalid memory access). The same failure occurred outside the sandbox and with a separate child-process TEMP/TMP directory. This does not establish the cause on every Windows computer.

The viewer now identifies this specific bootloader message and displays a localized explanation without logging raw CLI output, key values or DLL paths. Keys are not needed for the help check. A failed update is not activated and an existing installed version is retained. A working alternative standalone NSZ CLI can be selected in Settings → Plugins → nicoboss/nsz and must pass the same help check. Python scripts cannot be selected directly as the executable; a script-based installation requires a suitable launcher, which the current custom-executable setting does not provide automatically.

### Verified fallback for the Windows standalone CLI

Further diagnosis on the same Windows host (build 26300) found that the official 5.0.0 GUI archive, verified against its GitHub SHA-256, runs `--help` successfully with redirected input/output, whereas the standalone CLI fails to load Python. Thus this host can run the official GUI build in CLI mode; the exact DLL-level cause in the standalone package remains undetermined.

For the specifically identified PyInstaller Python-DLL startup failure on x64, managed installation now downloads the official `nsz-gui-windows-x64.zip` asset, verifies its published digest, extracts the executable and its bundled runtime files and checks `--help`. It passes CLI arguments when converting, so the GUI is not opened. No Windows protection settings or keys are changed. Other failures do not trigger this fallback. Source-asset and executable hashes are retained in installation state so subsequent update checks do not repeatedly download the GUI package. A fallback failure leaves the previous active version unchanged.

NSZ child processes receive TEMP/TMP/TMPDIR under `Temp/NSZ` next to the viewer executable. These per-process directories are removed after exit. Kivy/XDG cache locations are also set under the application Cache directory. No AppData storage fallback is used. Existing AppData files are left untouched.

The batch overview follows the selected result row. Completed overview metadata is retained in memory for immediate row selection without reopening or decompressing solid NSZ files. Files without retained metadata load asynchronously; stale loads are discarded and selecting firmware uses its firmware details.

## Compression mode and block size

Settings → Plugins → nicoboss/nsz provides Automatic, Solid / Blockless, and Block compression. Automatic preserves upstream defaults (solid for NSP→NSZ and blocks for XCI→XCZ); existing settings files keep this default. Solid explicitly passes `--solid`. Block explicitly passes `--block --bs <exponent>`; the size selector covers powers of two from 16 KiB to 4 GiB (exponents 14–32), default 1 MiB (20). The selector is enabled only for explicit Block mode. Block compression supports efficient random/backward reads; solid generally yields slightly better compression. These flags are omitted during decompression. Compatibility checks require the corresponding CLI options. See [official NSZ options](https://github.com/nicoboss/nsz/blob/master/docs/usage.md).

The file selected for `--keys` is `ActualProdKeysFilePath`, the same detected/selected `prod.keys` used by NxFileViewer and displayed in Settings. During conversion its temporary copy is passed using a relative ASCII path. No separate plugin keys setting is needed.

NSZ settings are in **Settings → Plugins → nicoboss/nsz**, alongside Program, Naming settings and Updates. Each settings page applies, cancels and resets its own options; saving a general draft does not overwrite separately saved plugin options.

## Operation progress

Conversion displays the file index and three phases: source verification, conversion, and output verification. Overall batch progress includes all three phases. When the NSZ CLI prints percentages, the viewer displays them with speed and estimated remaining time when available. Carriage-return progress output is processed immediately; updates are throttled to keep the interface responsive. Only recognized numeric progress fields are displayed, never raw CLI output or key values. Publishing the verified output has its own status message.
If the standalone CLI fails while loading its bundled Python DLL, the installer tries the official GUI package with CLI arguments. First startup can exceed 30 seconds; compatibility checks allow 120 seconds and log sanitized startup output. An unchanged executable successfully checked in the current application session is not checked again. Verified downloads survive a failed startup and can be reused.

### Errors and Unicode filenames

The child process enables Python UTF-8 mode and UTF-8 console output. On process failure, the last 30 sanitized diagnostic messages are also written at Error level. Key values are redacted. An exit code alone does not explain the cause; inspect the accompanying diagnostics.

Bundled executables may ignore Python UTF-8 settings. The viewer therefore passes input and keys through temporary ASCII aliases with relative paths. The original source remains unchanged; the expected output is renamed back to its original basename and verified. A hard link avoids copying a large input when supported; otherwise a cancellable temporary copy is created. Temporary input and keys are removed afterwards. Missing or empty output is an error even if the process exits with code 0.
## XCZ firmware partitions and NSZ 5.0.0

Some firmware NCZ sections begin at 0xC00, within the original 0x4000-byte prefix. NSZ 5.0.0 can overestimate their restored sizes by 0x3400 bytes in the HFS0 directory while writing the payload compactly. Later file offsets become incorrect even though the decompressor reports success. The Secure game partition can remain valid.

After XCZ decompression, NxFileViewer checks the staged directory against the source's NCZ section extents. It corrects only this recognized size/offset mismatch and only when all affected NCA content-ID hashes match at the corrected locations. Extra files must remain byte-identical; affected HFS0/root hashes are refreshed. Unknown corruption stops the operation before publication. Managed NSZ executables and source files are not patched.

Conversion verification now checks NCA hashes in every uncompressed XCI partition. Compressed XCZ source verification also checks non-game NCAs, including the embedded firmware. A successful game-only overview check is not a full-container conversion check. Older already-produced files are not automatically repaired; decompress the original XCZ again with the updated viewer. Failed conversion outputs stay unpublished and optional source deletion requires successful verification.

### Rebuilt Gamecard layout

After correcting recognized NCZ metadata, the viewer normalizes the private staged XCI before its final integrity check. Root partitions start on 512-byte boundaries and their extents are padded to whole sectors. Secure start (0x104), LimArea (0x18C), ValidDataEndAddress (the four bytes at 0x118; extended flags at 0x11C are preserved), the root header hash length and SHA-256 are derived from the actual rebuilt layout. Header validation checks bounds, alignment, overlaps, the Secure boundary, the data end and the root hash. Rebuilding uses a temporary file inside conversion staging and temporarily requires space for an additional uncompressed XCI; cancellation or failure cannot publish a partially rewritten XCI.

NSZ 5.0.0 copies only the first 0x200 bytes of the original XCI prefix. The viewer also preserves available bytes between 0x200 and the root directory during both compression and decompression, including certificate/extended header areas. Missing bytes already lost in an older XCZ cannot be recovered. Source and output root locations must agree; unknown layouts are rejected rather than overwriting possible partition data.

A repacked XCI is not a byte-identical archival dump. Changing signed Gamecard header fields cannot preserve or recreate Nintendo's RSA signature. NCA payloads/signatures remain unchanged, and normal conversion verification still checks their hashes. Layout consistency is not a promise of physical Gamecard authentication or hardware compatibility. Keep original dumps when archival fidelity is required.

## nscb_rust (4.1.1-beta1)

Under **Plugins → NSZ / nscb_rust**, select **cxfcxf / nscb_rust** as an alternative
compression engine. Set its executable path and compression level; the executable
path is shared with package operations. The Rust CLI does not expose Solid/Block
mode or block-size selection, so those options are disabled for Rust. The selected
engine handles both compression and decompression. Output is still verified
before publication.
