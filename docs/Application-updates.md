# Application updates and release packages

Open **Settings → Updates** to check for an application update or download and install an offered release. Start also links to this page. Automatic startup checks are optional and never install without confirmation. Network failures are logged and leave the viewer usable.

By default only stable releases are offered. Enable **Include pre-releases in application updates** and apply settings to include published previews. Drafts and older/equal semantic versions are ignored; changing the channel invalidates the current offer.

## Request cache and GitHub limits

Viewer, managed-plugin, TitleDB and firmware-hash update checks share GitHub metadata requests. Automatic startup checks and NSZ checks before conversion reuse successful responses for six hours. Metadata is stored under `Plugins/CacheUpdates` beside the executable and survives restarts. Manual checks request fresh metadata; simultaneous requests for the same endpoint reuse a completed in-flight response.

GitHub API limits are detected from 403/429 responses and rate-limit headers. The viewer respects `Retry-After` and `X-RateLimit-Reset` and persists the cooldown across restarts. During that time it shows the next permitted check time. If a response was saved, its result remains visible with its original check date; otherwise the component is shown as unavailable for checking, not as up to date. An ordinary access-denied response is still treated as an error. No GitHub account or token is required.

## Download, verification and replacement

Installation requires an idle viewer and a writable program directory. The matching x64/x86 release ZIP is staged under `Updates/<unique-id>` beside the executable. GitHub's SHA-256 asset digest, the full EXE product version (including preview suffix) and PE architecture are checked before installation. Missing digests are rejected.

Release ZIPs contain their files directly at the archive root. Additional files and subdirectories are installed at the same relative paths beside the main executable. Existing files absent from the ZIP remain untouched. Unsafe paths, directory links, keys and settings files are rejected.

`nxfileviewer-package.json` identifies the package independently of the executable filename:

```json
{"SchemaVersion":1,"ApplicationId":"NxFileViewerFork","Executable":"NxFileViewerFork.exe"}
```

Keep the application ID stable. `./Publish.ps1 -ExecutableName NxFileViewerFork.exe` publishes a renamed executable and records its name automatically. File associations use a stable launcher with a sibling target reference. Settings use `NxFileViewerFork.settings.json`.

A hidden PowerShell helper waits for process exit, verifies all staged file hashes, backs up overwritten files and installs the complete package. On failure it restores the previous files and removes newly added files. A renamed previous main executable is removed only as part of this transaction. Backups and a journal remain under `Updates/<GUID>` for package rollback and diagnosis. No elevation is used.

Older single-executable archives remain readable by the new updater. Publishing includes legacy-compatible transition ZIPs by default, alongside the normal new-name application ZIPs:

```powershell
./Publish.ps1
# Optional: omit transition ZIPs when legacy upgrades are no longer needed.
./Publish.ps1 -IncludeLegacyTransition:$false
```

`NxFileViewer_v<version>_<variant/architecture>.zip` contains only a transition `NxFileViewer.exe`, compatible with old single-file updaters. Its download logic always requests GitHub's latest stable release, independently of the transition EXE's build version. It selects the matching `NxFileViewerFork_v*` package, verifies its SHA-256 digest, architecture, package identity and actual target release version, then migrates data and installs the renamed application. Failed downloads stay visible with a Retry button. Drafts, previews, old-name transition packages and another runtime variant are not substituted.

The transition stays running beyond older helpers' early-startup check. Its own embedded version must still match the release advertising it: old updaters check the downloaded EXE version before starting it. Therefore a transition ZIP cannot simply be renamed and reused unchanged in a later release. `Publish.ps1` rebuilds all four transition variants with the current application version automatically. Upload those generated old-name ZIPs with each new release, together with the new-name application packages. A user who skipped 4.1.0 can then update directly through a later release; manually starting an older transition EXE also downloads the current stable release.

Only the transition moves `Cache/TitleDB`, `Cache/Updates` and `fw/hashes` into `Plugins/TitleDB`, `Plugins/CacheUpdates` and `Plugins/fwhashes`, and renames `NxFileViewer.settings.json` to `NxFileViewerFork.settings.json`. Existing destination files take precedence; conflicting legacy files remain intact. The normal application does not inspect or migrate legacy locations. Manual upgrades from older versions must use the transition EXE or move these files once themselves.

## Publishing 4.1.0

The project uses package version `4.1.0`. Run `Publish.ps1`, create a GitHub Release with tag `v4.1.0`, as a stable release. Attach:

- `NxFileViewerFork_v4.1.0_x64.zip`
- `NxFileViewerFork_v4.1.0_x86.zip`
- `NxFileViewerFork_v4.1.0_self-contained_x64.zip` (compressed, includes .NET)
- `NxFileViewerFork_v4.1.0_self-contained_x86.zip` (compressed, includes .NET)
- `NxFileViewerFork_v4.1.0_firmware-hashes.zip` (optional reference add-on)

Application ZIPs include the portable executable with embedded debug symbols; firmware references stay in their separate ZIP. GitHub Actions artifacts alone are not releases available to the updater. Architecture selection follows the running viewer process.

## Preview version ordering

The updater uses the installed assembly's informational version, including its beta/RC suffix, and compares it with the release tag using semantic precedence. Examples:

- 4.0.0-beta.2 is newer than 4.0.0-beta.1.
- 4.0.0-beta.10 is newer than 4.0.0-beta.2.
- 4.0.0-rc.1 follows 4.0.0-beta.10; stable 4.0.0 follows all 4.0.0 previews.
- Build metadata after + does not affect ordering.
- Equal or older versions are never offered.

Preview updates require Include pre-releases to be enabled. Stable-only mode can still offer the final release at the same numeric version to an installed beta. The downloaded EXE must match the full offered version, not just its numeric part.

The older 4.0.0 Beta 1 builds contained numeric-only comparison and needed a manual upgrade once. The 4.1.0 transition ZIPs retain the names expected by compatible older updaters.

## Validation

Tests use mocked releases/downloads, corrupt and unsafe archives, and isolated dummy EXEs for replacement and backup checks. They never replace an installed user application. A complete live GitHub upgrade/restart has not been validated during development.

## Runtime variants

Each x64/x86 release offers a standard ZIP requiring .NET 8 Desktop Runtime and a compressed self-contained ZIP with .NET included. Standard names remain NxFileViewerFork_v<VERSION>_<ARCH>.zip; bundled-runtime names are NxFileViewerFork_v<VERSION>_self-contained_<ARCH>.zip. Both contain the main executable and the package manifest, plus any additional published files.

The executable records its runtime distribution at build time. Updates preserve this distribution and architecture, including preview releases. A missing compatible package is reported or skipped while listing previews; another runtime distribution is never substituted. Previously installed standard builds continue using the original ZIP names.

The Info tab displays the application version, process architecture and whether .NET is bundled. Standard builds explicitly show the .NET 8 Desktop Runtime requirement.

## Removing old versions

Use **Remove old versions** in the Update Center for the application, NSZ or NxNandManager. Plugin Settings also offers the respective plugin cleanup buttons. The confirmation lists the exact managed paths and warns that the corresponding rollback versions will be lost.

Plugin cleanup removes only inactive `version-<GUID>` directories. It preserves the registered active installation, configured custom executables, folders containing `.keys` files, locked files and directory links. It clears rollback references before removing a previous installation. Missing or invalid active-state metadata prevents cleanup.

Application cleanup removes complete successful automatic-update directories under `Updates/<GUID>`, including `previous.exe`, `install.ps1`, `plan.json`, `ready` and `completed`. Completed leftover folders whose backup EXE was already deleted are also offered. It waits at least one minute after completion and excludes pending, failed, unrecognized, linked or locked updates. Only folders containing known updater files and validated package payload/backup subdirectories are eligible; folders with keys or unrelated user files remain untouched. The current EXE, settings, manually extracted old releases and download caches are not removed.
### Restore the previous program version

The update center offers **Use previous version** while a completed update still contains its validated package backups. It selects the most recently completed eligible update, asks for confirmation, and restarts through the same atomic replacement and startup recovery helper used for updates. The complete previous package is restored, including overwritten companions and removal of files added by that update. The current package becomes a new recovery backup. Cleaning old program versions removes the corresponding rollback backups; the button is disabled when none remain.
