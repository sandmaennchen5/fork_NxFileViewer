# NxNandManager plugin

The **NAND** workspace includes a native, read-only FAT32 explorer for dump files, split dumps and Windows NAND drives. [THZoria/NxNandManager](https://github.com/THZoria/NxNandManager) remains available as a separate CLI process for detailed NAND information and raw partition export. Restore, resizing and AutoRCM changes are not exposed.

## Integrated explorer

Open a dump and choose **NAND Explorer**, or choose **Open driveâ€¦** and select or enter a Windows device path such as `\\.\PhysicalDrive2`. Raw physical drives may require starting NxFileViewer with administrator rights. The explorer itself does not elevate, install drivers or write to the source. It recognizes Switch GPT partitions, including common full-NAND and partition-based emuNAND offsets; generic PC partition tables are rejected.

Select USER or SYSTEM; changing the explorer partition automatically loads its root. Use the folder tree or double-click a folder in the file list. **Parent folder** navigates upwards. **Export fileâ€¦** copies the selected file to a new destination without replacing existing files. Split dump reads can cross file boundaries. Failed or cancelled file exports remove their partial output.

Encrypted FAT32 partitions use the configured BIS key file, or the active `prod.keys` when no override is configured. Both Lockpick `bis_key_00` entries and biskeydump crypt/tweak pairs are supported. Already decrypted FAT32 partitions do not need keys. Wrong or missing BIS keys produce an error before directory browsing.

The explorer browses the partition filesystem and exports its files. Save headers and readable NCA headers provide title IDs. Names are resolved from the locally cached TitleDB for the selected region; save headers also display user IDs. Missing metadata or catalog entries leave these columns blank. User profile names and NCA internal browsing/extraction are not yet implemented.

In `/save`, select a recognized save and choose **Open save** or double-click it. Browse its internal SaveFS directories using the same tree, file list and parent-folder button. **Export file…** exports the selected internal file. **Export entire save…** asks for a parent folder and creates a new folder named after the save with a timestamp; nested and empty directories are preserved. Existing destinations are not overwritten. Files are copied through a staging directory, which is removed on cancellation or failure. **Back to partition** returns to the partition’s `/save` folder; changing partitions also exits the save.

SaveFS reads directly from the read-only NAND file stream, without extracting the complete container to temporary storage. This also supports split dumps and the drive reader. SaveFS integrity checks, repair and committing changes are not exposed; exporting files does not certify the save’s integrity. The existing partition-export button still exports encrypted bytes through the plugin and is separate from explorer file export.

## Setup

Under **Settings â†’ Updates â†’ Plugins â†’ NxNandManager**, choose **Install / updateâ€¦**. The latest stable release is downloaded from THZoria/NxNandManager, checked against GitHub's SHA-256 digest, extracted with its bundled DLLs and checked for CLI compatibility before activation. Installations live in `Plugins/NxNandManager` beside NxFileViewer. The application directory must be writable. The previous version remains available through **Use previous version**; failed updates preserve the active version. Updates are requested manually; opening a dump also installs a missing managed plugin.

Leave the EXE path empty under **Settings â†’ Plugins** to use managed installations. A custom EXE path overrides them and disables managed update/rollback buttons; custom executables are never replaced. Clear the path and apply settings to return to managed installations.

The upstream EXE requests elevation and can fail with a host-dependent C++ locale error. File-only CLI processes receive `RunAsInvoker` and `LC_ALL/LANG=C` in their own environment, leaving Windows settings unchanged. The CLI usage probe accepts upstream's documented usage exit `-1001` and checks required options. Unsupported architectures or startup failures prevent activation. No Dokan driver is installed.

By default, the plugin uses NxFileViewer's active `prod.keys` file if it contains valid-format `bis_key_00`â€“`bis_key_03` entries. The viewer's normal key discovery and selected-file changes apply automatically. If no BIS entries are available, basic NAND information remains available without a keyset. BIS keys must belong to the source console; ordinary game keys alone cannot decrypt its NAND.

Optionally select a separate BIS key file to override the active `prod.keys`. NxNandManager accepts Lockpick-style `bis_key_00` entries or biskeydump format. Keys are passed by file path for information requests; values are not logged. BIS keys are required for extended information such as firmware and serial number, but not for copying encrypted partitions unchanged.

## Usage

The general **File check** and **Batch check** recognize NAND dumps alongside game packages. ZIP, solid 7z and nested archives can contain both. Header probes identify Switch GPT images by signature and Nintendo partition names, plus known BOOT/PRODINFO signatures. Generic PC GPT images are excluded. Exact conventional names such as `SYSTEM`, `USER`, `BOOT0.bin` or `PRODINFO.enc` with block-aligned data are shown as **candidates** when encryption hides signatures; use **NAND information** to confirm through NxNandManager. Basic recognition does not install the plugin or access firmware/title servers.

The individual file view has a NAND details page with **NAND information** and partition export. The archive entry selector switches between packages and NAND files. The dedicated NAND workspace's Open command also accepts ZIP and 7z and opens their entries in File check. Candidate files require a successful CLI information request before partition export is enabled.

For split dumps, select the first file (`rawnand.bin.00`, `full.00.bin`, `00`, etc.). Batch results list the first part once. Extraction preserves sibling files in the same temporary directory, including continuation parts, so NxNandManager receives the complete available sequence. Different archive folders remain separate. Changing a split companion invalidates saved batch fingerprints.

Batch NAND results show type, byte size and detection details, retained in history and CSV. They remain **Unchecked**: NAND detection is not NCA integrity verification. Package conversion, naming, moving and NCA verification actions exclude these rows. Open a row in File check for CLI information and export. Archive contents are extracted under the existing `Temp/ZIP` session and released on close. Allow disk space for the uncompressed archive, especially large NAND dumps. Listing probes bounded headers; selecting members reuses the existing extracted session.

Open a dump in **NAND**; for split dumps, select the first file. The page displays upstream CLI information. The partition selector recognizes storage types and partition rows in the documented English CLI output. Unknown output formats remain visible but may have no selectable partitions. Information requests time out after 60 seconds and can be cancelled.

Select a partition and export to a **new local file**. The plugin copies bytes as stored, including encryption. Existing files are never replaced, even if another process creates the destination during export. Export writes to a unique `.nxfv-nand-â€¦` directory beside the destination. Only a successful CLI exit and a non-empty file allow publication; NxNandManager's default MD5 verification remains enabled. NxFileViewer does not perform an independent NAND integrity check.

The main background task runner displays progress percentages when reported by the CLI. Cancel terminates the process tree. Failed or cancelled exports discard temporary files; temporary directories can remain if Windows denies cleanup. Sources are never deleted. Information text can contain console identifiers and stays on the NAND page rather than being copied to session logs.

Auf der Startseite Ã¶ffnet die zweite Button-Reihe installierte Plugin-GUIs in einem eigenen Fenster. NxNandManager wird mit --gui gestartet. NSZ ist nur mit einer installierten nsz-gui-Version verfÃ¼gbar; CLI-Versionen bleiben deaktiviert. Der GUI-Start lÃ¤dt keine Plugins herunter. Windows verwendet die vom Plugin angeforderten Berechtigungen.
