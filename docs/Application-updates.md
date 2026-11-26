# Application updates and release packages

Open **Settings → Updates** to check for an application update or download and install an offered release. Start also links to this page. Automatic startup checks are optional and never install without confirmation. Network failures are logged and leave the viewer usable.

By default only stable releases are offered. Enable **Include pre-releases in application updates** and apply settings to include published previews. Drafts and older/equal semantic versions are ignored; changing the channel invalidates the current offer.


## How the viewer finds updates

1. The Avalonia viewer requests up to 30 releases from its configured GitHub repository. The default channel includes stable releases. Enable **Include pre-releases in application updates** to also consider GitHub pre-releases. Drafts are excluded.
2. It sorts the candidates by the version in the Git tag, such as `4.1.2` or `4.1.2-beta3`. The display title can be edited without changing update detection.
3. It selects the newest candidate with a matching download. If that release lacks a package for the running platform, process architecture, .NET variant or installation format, it can fall back to an older compatible release that is still newer than the installed version.
4. Before installation, the updater verifies the SHA-256 checksum. It prefers GitHub's asset digest. The release manifest, package index and per-ZIP metadata provide checksum fallbacks where applicable.

| Running installation | Download it looks for |
| --- | --- |
| Portable viewer on Windows, Linux or macOS | A ZIP such as `NxFileViewerFork_v4.1.2_win-x64-with-net.zip`. The architecture and `with-net` / `without-net` variant must match. |
| Linux AppImage | A matching `.AppImage.zip` containing `NxFileViewerFork.AppImage`, or a direct `.AppImage`. For example, `NxFileViewerFork_v4.1.2_linux-x64-with-net.AppImage.zip` identifies the version and variant while the file inside keeps a fixed name. Updates replace the AppImage at its existing path. Older AppImage updaters require the direct asset. |
| Installed macOS app bundle | A matching `.dmg` if available, otherwise a matching `.app.zip` from that release. |

GitHub Actions artifacts and source archives are separate from the files attached to a release. For example, a beta with Linux and macOS packages only will not appear as a Windows application update.

## Which installed versions can update?

| Installed version | Update path |
| --- | --- |
| `3.0.4` and earlier | Install a newer version manually. These versions predate the built-in viewer updater. |
| Windows `4.0.0-beta.1` through the `4.0.x` line | The original updater expects a single `NxFileViewer.exe` in a legacy `NxFileViewer_v…` ZIP. The release workflow can supply a bridge for x86 and x64 on stable releases. The earliest 4.0 beta builds may require a manual upgrade if their version comparison rejects the update. |
| Windows WPF `4.1.x` | Early 4.1 previews look for the original `NxFileViewerFork_v…_<architecture>.zip` names. Manifest-aware releases such as `4.1.0` and `4.1.1` can find the matching Avalonia ZIP through `nxfileviewer-update.json`. |
| Avalonia `4.1.2-beta1` | Requires the old `NxFileViewerFork_Avalonia_v…` filename and compatible contents. A newer release must include the compatibility ZIP for the installed platform and .NET variant, or the user must upgrade manually. |
| Avalonia `4.1.2-beta2` and later | Recognizes the current platform-specific portable ZIP names. The published beta2 had Linux and macOS packages, so Windows users need a release with a Windows asset. |
| Avalonia `4.1.2-beta3` and later | Also supports updating a running Linux AppImage or installed macOS app bundle when the newer release has the corresponding native package. |

The last stable WPF release was [4.1.1](https://github.com/sandmaennchen5/fork_NxFileViewer/releases/tag/4.1.1). [4.1.2-beta1](https://github.com/sandmaennchen5/fork_NxFileViewer/releases/tag/4.1.2-beta1) was the last preview with both WPF and Avalonia packages. See [beta2 files](https://github.com/sandmaennchen5/fork_NxFileViewer/releases/expanded_assets/4.1.2-beta2) and the [beta3 release notes](https://github.com/sandmaennchen5/fork_NxFileViewer/releases/tag/4.1.2-beta3) for the package changes.

## When x86 releases are discontinued

An x86 application additionally checks for newer x64 releases in the same runtime
distribution, using the configured sources and preview setting. This check applies
both at startup and in Settings → Updates.

- On x64 Windows, a newer release available only for x64 appears as an update with
  **x64 only** and **Switch to 64-bit**. The normal Install button also routes through
  the explicit 64-bit switch confirmation; it never silently changes architecture.
- On x86 Windows, the newer release is listed as requiring **64-bit Windows**.
  It is not installable. If an older compatible x86 update is still available, that
  update remains installable and both versions are listed.
- If the same new version is available for x86 and x64, the normal update stays on
  x86. An older x64 release never replaces a newer compatible x86 update.
- The standard / self-contained distribution is retained. A standard x64 package
  requires the x64 .NET Desktop Runtime, as stated in the switch confirmation.
- Native x64/ARM64 checks and x86 emulation on ARM64 retain their existing behavior.

Publish a final x86 release containing this updater **before** removing x86 assets
from subsequent releases. Previously installed versions do not acquire this discovery
behavior retroactively. No test or startup check downloads or installs a release ZIP
without the normal user-initiated installation and confirmation.


## Automatic releases from version tags

Push a tag such as `4.1.2` or `v4.1.2` for a stable release. A tag such as `4.1.2-beta3` selects the pre-release channel. The tagged commit supplies both the workflow and its package selections.

Edit `RELEASE_TARGETS` and `PRERELEASE_TARGETS` in `.github/workflows/release.yml`, or open **Publish.ps1 → GitHub-Workflows…**. The lists control the platform, ZIP or native format, and .NET variant independently. The tag sets the viewer version. Explorer keeps its separately configured version. The notes come from the matching section of the tagged `CHANGELOG.md`.

Stable releases wait for the selected platform builds. A pre-release can publish successful packages first. Re-running an unsuccessful job can add its packages to the same tag and refresh the manifests. GitHub re-runs use the original workflow from the tagged commit.

For local compatibility packages, select **Kompatibilitaets-Updates + Manifest** in Publish or run the following after building the portable ZIPs:

```powershell
./Publish.ps1 -Version 4.1.2 -OutputDirectory Publish -Extras UpdateCompatibility
```

The compatibility step creates old-name Avalonia ZIPs with the manifest format beta1 expects, plus the Windows compatibility names and manifest entries for older WPF updaters. A stable version with matching Windows x86 or x64 ZIPs also builds the self-contained bridge EXE. Its version must match the release because the old updater checks the downloaded EXE before launching it.

## Windows package manifests and legacy update sources

Windows Avalonia ZIPs include `nxfileviewer-package.json` for older manifest-aware Windows installers and `nxfileviewer-viewer.json` for the current Avalonia updater. The release manifest `nxfileviewer-update.json` lists portable Avalonia packages and, when compatibility packages are enabled, maps Windows architectures and .NET variants to the appropriate ZIP for older updaters. Keep the application ID `NxFileViewerFork` stable across releases.

The following repository-move and migration settings describe the manifest-aware Windows updater and compatibility installer.

`UpdateSources` accepts legacy GitHub names and typed GitHub, GitLab, Forgejo or independent HTTPS-manifest sources. See [update source configuration](../src/config/Update-sources.md) for formats and portable hosting. To prepare a move, publish an update containing both the new and old repositories before moving. After successful installation they are remembered in `NxFileViewerFork.update-state.json`; failed installations roll this state back. Checks try the remembered sources and choose the highest eligible version found. GitHub downloads must belong to the supplying repository. Native GitLab/Forgejo addresses use the configured origin; independent HTTPS manifests can explicitly name download hosts/CDNs. A nonempty UpdateSources list replaces the remembered list after installation. Include both repositories during the transition; a later release can explicitly remove the old source. Without local update state, the original repository is the initial source. Installations that missed the preparatory update still need a working old location or a manual update; keep the old source available during the transition. This uses the existing GitHub/HTTPS trust model and asset digests, not a separate developer-signature system.

A migration example (add to the template's `Migrations` array):

```json
{
  "Id": "titles-from-before-4.0.1",
  "WhenInstalledVersionBelow": "4.0.1",
  "ApplyFromTargetVersion": "4.2.0",
  "Actions": [
    {
      "Type": "MoveDirectory",
      "From": "Cache/TitleDB",
      "To": "Plugins/TitleDB",
      "IfTargetExists": "KeepExisting"
    }
  ]
}
```

This applies only when upgrading from below 4.0.1 to 4.2.0 or later. Committing the template, checking for updates or downloading metadata moves no files. Rules come from the verified ZIP being installed, not from the current development branch. A rule committed ahead of its target release remains inactive in earlier packages. Applicable migrations execute in manifest order; completed IDs are remembered only with the successful installation. IDs must remain stable and unique.

### Available manifest actions

| Action | Supported | Behavior |
| --- | --- | --- |
| `MoveFile` | Yes | Moves one file; changing its name also renames it. |
| `MoveDirectory` | Yes | Moves a directory recursively, including subdirectories. |
| `CopyFile` / `CopyDirectory` | No | Copying while retaining all source files is not implemented. |
| `DeleteFile` / `DeleteDirectory` | No | Standalone deletion is not implemented. |
| Run a command or script | No | Migration entries cannot execute arbitrary commands. |

For both supported actions, `From` and `To` are paths relative to the application directory. `IfTargetExists` supports only `KeepExisting`: an existing destination file and its conflicting source file are both retained. Successfully moved source files are removed; source directories are removed only when empty. This cleanup is part of moving, not a general delete action.

For example, add this entry to `Migrations` in `src/config/package-manifest.json` to move and rename a file:

```json
{
  "Id": "rename-title-list-for-4.2.0",
  "WhenInstalledVersionBelow": "4.2.0",
  "ApplyFromTargetVersion": "4.2.0",
  "Actions": [
    {
      "Type": "MoveFile",
      "From": "Data/old-titles.json",
      "To": "Data/titles.json",
      "IfTargetExists": "KeepExisting"
    }
  ]
}
```

This runs only during installation of a newer release, when the original installed version is below 4.2.0 and the target version is 4.2.0 or newer. Keep required rules in later releases so users can skip versions. Merely committing the template or checking for updates does not run actions. Each successfully installed migration ID is remembered, so do not reuse an ID for a different operation. Applicable entries run in manifest order.

Paths outside the application directory, directory links, keys, settings files and updater metadata are excluded. Files are staged and installed transactionally with backup and rollback; user changes detected between staging and installation cancel installation. Unsupported action names are rejected during publication and package validation.

Supported actions are `MoveDirectory` and `MoveFile`, relative to the application directory. Existing target files and their conflicting source files are both retained. Unsupported commands, overlapping paths, links, paths escaping the application directory, keys, settings files and updater metadata are rejected. Changed source files or newly appearing target files cancel installation instead of overwriting user changes. Files are staged first and committed through the existing embedded PowerShell installer, with the same recovery journal and manual rollback as program files. Newly empty source directories are removed; rollback can recreate them. The shipped template has no active migrations.

These features require this new updater. Already distributed older versions do not gain manifest or migration support merely from a repository commit. Continue publishing legacy transition ZIPs while supporting old-name installations. Pre-manifest 4.1 updaters also still need the original NxFileViewerFork_v... ZIP names; keep those names as compatibility aliases when renaming packages, or require users to install the preparatory updater first. Keep `SchemaVersion: 1` and `ApplicationId: NxFileViewerFork`; the visible product, executable and ZIP names may change independently.

## Request cache and GitHub limits

Viewer, managed-plugin, TitleDB and firmware-hash update checks share GitHub metadata requests. Automatic startup checks reuse successful responses for six hours. NSZ no longer checks installed plugin versions before conversion; plugin updates are requested manually under Updates. Missing plugins can still be installed on demand. Metadata is stored under `Plugins/CacheUpdates` beside the executable and survives restarts. Manual checks request fresh metadata; simultaneous requests for the same endpoint reuse a completed in-flight response.

GitHub API limits are detected from 403/429 responses and rate-limit headers. The viewer respects `Retry-After` and `X-RateLimit-Reset` and persists the cooldown across restarts. During that time it shows the next permitted check time. If a response was saved, its result remains visible. Automatic startup checks omit the saved-result date suffix; manual checks retain it when showing cached data. Otherwise the component is shown as unavailable for checking, not as up to date. An ordinary access-denied response is still treated as an error. No GitHub account or token is required.

## Download, verification and replacement

The following describes the manifest-aware Windows ZIP installer. Installation requires an idle viewer and a writable program directory. The matching release ZIP is staged under `Updates/<unique-id>` beside the executable. GitHub's SHA-256 asset digest, the full EXE product version (including preview suffix) and PE architecture are checked before installation. Missing digests are rejected.

Release ZIPs contain their files directly at the archive root. Additional files and subdirectories are installed at the same relative paths beside the main executable. Existing files absent from the ZIP remain untouched. Unsafe paths, directory links, keys and settings files are rejected.

`nxfileviewer-package.json` identifies the package independently of the executable filename:

```json
{"SchemaVersion":1,"ApplicationId":"NxFileViewerFork","Executable":"NxFileViewerFork.exe"}
```

Keep the application ID stable. `./Publish.ps1 -ExecutableName NxFileViewerFork.exe` publishes a renamed executable and records its name automatically. File associations use a stable launcher with a sibling target reference. Settings use `NxFileViewerFork.settings.json`.

A hidden PowerShell helper waits for process exit, verifies all staged file hashes, backs up overwritten files and installs the complete package. On failure it restores the previous files and removes newly added files. A renamed previous main executable is removed only as part of this transaction. Backups and a journal remain under `Updates/<GUID>` for package rollback and diagnosis. No elevation is used.

## Preview version ordering

The updater uses the installed assembly's informational version, including its beta/RC suffix, and compares it with the release tag using semantic precedence. Examples:

- 4.0.0-beta.2 is newer than 4.0.0-beta.1.
- 4.0.0-beta.10 is newer than 4.0.0-beta.2.
- 4.0.0-rc.1 follows 4.0.0-beta.10; stable 4.0.0 follows all 4.0.0 previews.
- Build metadata after + does not affect ordering.
- Equal or older versions are never offered.

Preview updates require Include pre-releases to be enabled. Stable-only mode can still offer the final release at the same numeric version to an installed beta. The downloaded EXE must match the full offered version, not just its numeric part.

The older 4.0.0 Beta 1 builds contained numeric-only comparison and needed a manual upgrade once. Legacy transition ZIPs retain the names expected by compatible older updaters and follow the configured stable/pre-release preference.

## Validation

Tests use mocked releases/downloads, corrupt and unsafe archives, and isolated dummy EXEs for replacement and backup checks. They never replace an installed user application. A complete live GitHub upgrade/restart has not been validated during development.

## Runtime variants

Windows packages are published for x64, x86 and ARM64 with or without .NET.
The ZIP name includes the runtime identifier and ends in `-with-net.zip` or
`-without-net.zip`. See [Avalonia releases](Avalonia.md) for the package matrix.

Normal updates preserve the installed architecture and runtime distribution.
On x64 Windows, the x86 viewer can offer **Switch to 64-bit** in the Update Center.
This action asks for confirmation and uses the verified installer.

## Removing old versions

Use **Remove old versions** in the Update Center for the application, NSZ or NxNandManager. Plugin Settings also offers the respective plugin cleanup buttons. The confirmation lists the exact managed paths and warns that the corresponding rollback versions will be lost.

Plugin cleanup removes only inactive `version-<GUID>` directories. It preserves the registered active installation, configured custom executables, folders containing `.keys` files, locked files and directory links. It clears rollback references before removing a previous installation. Missing or invalid active-state metadata prevents cleanup.

Application cleanup removes complete successful automatic-update directories under `Updates/<GUID>`, including `previous.exe`, `install.ps1`, `plan.json`, `ready` and `completed`. Completed leftover folders whose backup EXE was already deleted are also offered. It waits at least one minute after completion and excludes pending, failed, unrecognized, linked or locked updates. Only folders containing known updater files and validated package payload/backup subdirectories are eligible; folders with keys or unrelated user files remain untouched. The current EXE, settings, manually extracted old releases and download caches are not removed.
### Restore the previous program version

The update center offers **Use previous version** while a completed update still contains its validated package backups. It selects the most recently completed eligible update, asks for confirmation, and restarts through the same atomic replacement and startup recovery helper used for updates. The complete previous package is restored, including overwritten companions and removal of files added by that update. The current package becomes a new recovery backup. Cleaning old program versions removes the corresponding rollback backups; the button is disabled when none remain.

### Download caches and Kronos GUI sessions

The Updates page provides separate NSZ **Clear download cache** and Kronos
**Remove old GUI sessions** actions. Both show the exact candidates and require
confirmation. They do not remove installed plugin versions.

The download action only deletes recognized SHA-256-named .download files from
the managed NSZ Downloads folder. Locked files and unrelated files are retained.
A later installation may download the archives again.

The session action permanently removes the selected workspaces, including their
generated output and settings; its confirmation explicitly explains this.
Only GUID-named sessions older than 24 hours are offered. Keys, links and locked
files are protected. New Kronos launches hold an exclusive session lock.
For older sessions without tracking, cleanup is conservative: it does not offer
sessions while any cmd, Python, py or NSCB process is running. Close those
consoles first. Eligibility is checked again immediately before deletion.

## Windows ARM64

The Viewer and standalone LogViewer have native Windows ARM64 packages. Updates
preserve architecture and runtime distribution. See [Avalonia releases](Avalonia.md)
for package names and publishing options.

NSZ already selects nsz-cli-windows-arm64.exe on Windows ARM64. The other external
plugins do not currently provide equivalent native Windows ARM64 packages;
x64 emulation and native-device behavior still require separate testing.
Cross-publishing verifies packaging and architecture, not execution on ARM hardware.
