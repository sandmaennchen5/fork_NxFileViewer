# NxFileViewer application updates

The viewer checks the latest regular GitHub Release in `sandmaennchen5/fork_NxFileViewer` at startup. Disable **Settings → Updates → Check for application updates on startup** to opt out. Checks are read-only and do not install automatically. The start page and Application updates menu show the result; manual checks are available there and in Settings. Network failures leave the application usable and are logged. Drafts and older/equal numeric versions are ignored. By default only stable releases are checked. Enable **Settings → Updates → Include pre-releases in application updates** and apply settings to include published pre-releases. The option is saved in the configuration and applies to manual and automatic checks; the offered version is labeled as a pre-release. Changing the option discards an existing offer and requires a new check.

**Download and install** is available for a newer release when no integrity/conversion task is running. Confirm the download/restart. The archive is staged under `Updates/<unique-id>` next to the executable. SHA-256 is verified against GitHub's asset digest before extraction. Missing digests are rejected; re-upload the ZIP asset to GitHub if necessary. The extracted EXE's version and PE architecture must match the release. ZIPs must contain only `NxFileViewer.exe`, either at their root or within their versioned application folder. Unexpected entries and paths are rejected.

The viewer starts a hidden Windows PowerShell helper and waits for its initialization before closing normally, which saves settings. The helper waits for the viewer process to exit, checks the staged EXE's hash, atomically replaces only `NxFileViewer.exe`, and restarts the viewer. Its previous EXE is retained as `Updates/<unique-id>/previous.exe`. Keys, title.keys, settings, Plugins, Cache, Temp and optional firmware references are not copied, replaced or removed. There is no AppData fallback or privilege elevation; the application directory must be writable.

Replacement errors keep the existing EXE. If launching the updated EXE fails or it exits within two seconds, the helper attempts to restore and restart the previous EXE. This is an early-start check, not a guarantee against later application failures. Installer failures are shown and recorded in `Updates/<unique-id>/error.txt`. Staged files and the previous EXE remain available for diagnosis/recovery.

## Publishing compatible releases

Create a GitHub Release with a numeric tag such as `3.0.5` or `v3.0.5`, matching the project version. Upload the app ZIPs produced by `Publish.ps1`:

- `NxFileViewer_v3.0.5_x64.zip`
- `NxFileViewer_v3.0.5_x86.zip`

The firmware-hashes ZIP remains a separate optional download. GitHub Actions artifacts alone are not application updates; attach the ZIPs to the Release. The updater chooses the architecture of the running process. Native ARM64 releases and multi-file application packages are not currently supported. Installation requires the portable executable named `NxFileViewer.exe`.

Tests use mocked release/download responses, malicious/corrupt archives, and isolated dummy EXE files for the actual PowerShell replacement/backup operation. They never replace the user's installed viewer. A complete upgrade and restart from a live newer GitHub Release has not been performed during development.

## Pre-release packages

With pre-releases enabled, the updater reads the paginated GitHub release list, filters drafts and incompatible packages, and selects the highest newer numeric version. A stable release takes precedence over a pre-release with the same numeric version; otherwise the API listing order resolves ties. Tags such as `v3.0.5-beta.1` or `v3.0.5-rc.1` are supported. Packages can use the usual `NxFileViewer_v3.0.5_x64.zip` name or the tagged `NxFileViewer_v3.0.5-beta.1_x64.zip` name, with the matching folder name inside the ZIP (or the EXE directly at the ZIP root).

The EXE version must match the numeric part of the release tag. Each update, including successive preview builds, must increase the numeric application version: suffix-only changes at the installed numeric version are not offered. Existing SHA-256, architecture, archive-content and installer checks also apply to pre-releases.