# Batch history and resume

The batch page lists recent saved scans (default 5, configurable from 1 to 100 under Settings > Program > History). Each saved scan has its own file:
History/YYYY-MM-DD_SSMM_<scan-id>.zip beside the executable.
SSMM denotes local hours and minutes (HHmm), fixed when the archive is first saved.
The archive contains scan.json as the scan index, one JSON file per ROM under roms/, and compressed original logos under icons/.
Each ROM record can be read independently; the current History list still loads all compact result records.
Identical logos within a scan share one image entry. Updating or resuming a scan updates that same archive; the result count is not the number of History files.

## Overview without reopening the source

Completed rows use detached metadata snapshots in the normal file/package overview.
Selecting a result, package or language reads only its stored metadata and needed logo.
It does not reopen the NSP/XCI/NSZ/XCZ or containing archive, and does not start verification.
The full scan object graph is released after each result. Only the current overview's
images are decoded; changing the selection releases the previous image cache.
Copy/save image actions use the saved logo. Homebrew titles and logos are included.

The option "Save logos in history" under Settings > Program > History is off by default, including for existing settings without this preference. Enable it before a new scan to include logos. When disabled, newly saved scans omit both logo references and image entries; saving an existing scan also removes its stored images. Other archived scans are unchanged until saved again.

Saved fields include multilingual title/publisher names, package versions and IDs,
security/signature information, missing-key diagnostics, firmware details, compression
information, and installed-title fallbacks. Decrypted game contents and key values are
not stored. Historical metadata describes the scan time; use an explicit new scan or
verification to refresh changed files.

Old History/batch-integrity.json arrays and batch-integrity-entries archives remain readable. On the next save they migrate
to individual archives in History, with the original array retained as a uniquely named .legacy backup.
Previous per-scan ZIPs are upgraded and moved from the subfolder to History on the next save, preserving their scan IDs and logos.
Old records cannot supply images or fields they never captured. They show the existing
history notice where no logo is available; a new scan captures the missing data.

## Checkpoints, retention and resume

History checkpoints are written at most once a second during result callbacks and once
more on completion/cancellation. Writes use a temporary archive and atomic replacement.
A failed save preserves the previous archive. Unexpected termination may lose the last
second of completed results; those files are checked again.

Selecting a saved scan displays its results immediately and offline; no Show button is needed. Resume rereads the sources and processes pending,
changed or failed entries; new files are included. Completed results are skipped only
when source size/timestamps and the missing-delta verification policy still match.
The same scan ID is retained on resume or Verify All. A new scan creates a new archive.
The configured retention limit removes entire older scan archives, including their logos.
The legacy backup is retained independently of the recent-scan limit.

Archive members use their parent ZIP/7z timestamp and size. Firmware folders use the
sorted file list, sizes and timestamps. Fingerprints are checked before and after reading.
This change check is metadata based, not an additional cryptographic verification.

Dropped selections may include multiple files, folders and archives. Their source list
is retained for resume; overlapping sources do not duplicate results.
Missing sources cannot be resumed, but stored overviews and logos remain available.
States are Running, Interrupted and Completed; completion does not imply every file passed.

The program directory must be writable to persist History. Errors are logged and do not
abort scanning. Disabling History hides its controls and keeps existing saved scans.
New scan snapshots then use a temporary cache under the system temp directory so that
overview selection still needs no ROM reread. This temporary cache is removed on normal
closure of the batch workspace; abnormal termination may leave temporary files.
The program starts with the empty History option selected and no restored batch results. Selecting a saved scan displays it automatically. Selecting the empty option again clears the displayed batch without deleting saved History. Checkpoint refreshes preserve that empty selection.

When logo persistence is off, the currently scanned batch still displays its logos. Compressed images are kept in a separate active-logos-<unique-id>.zip directly in History. This logo-only archive is excluded from the saved scan list and retention count. It is removed on selecting a History entry (including the empty option), starting a new scan, or normally closing the program. Appending files and verifying the current batch retain its active logos. Each running instance owns its own temporary archive; an abnormal termination may leave that instance's file behind.

If the temporary logo ZIP is corrupt or truncated, it is rebuilt from pending logos and a warning is logged; earlier temporary logos may then be unavailable. Logo-cache errors are isolated from metadata checkpoints, and malformed History archives are logged rather than aborting the batch scan. This recovery never changes the source ROMs.
