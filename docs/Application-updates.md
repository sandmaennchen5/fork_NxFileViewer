# Application updates and release packages

Open **Settings → Updates** to check for an application update or download and install an offered release. Start also links to this page. Automatic startup checks are optional and never install without confirmation. Network failures are logged and leave the viewer usable.

By default only stable releases are offered. Enable **Include pre-releases in application updates** and apply settings to include published previews. Drafts and older/equal semantic versions are ignored; changing the channel invalidates the current offer.

## Automatic releases from version tags

Push a tag such as `v4.2.0` (or `4.2.0`) to build and publish a stable GitHub Release. Tags such as `v4.2.0-beta`, `v4.2.0-beta1` and `v4.2.0-beta2` create pre-releases. Other version suffixes are not accepted by the release workflow.

`.github/workflows/release.yml` checks out the tagged commit and passes the tag version to `Publish.ps1 -Version`. The main application, LogViewer and legacy transition executables and their ZIP names therefore use the same version even if the project files still contain a different development version. Explorer MSIX packages retain their separately configured package version. Local publishing without `-Version` continues to use the main project version.

Before building, `.github/scripts/Prepare-Release.ps1` compares the application project version with the tag and corrects it in the workflow checkout if necessary. The tag is authoritative for the application, LogViewer, transition executables and package manifests. The correction is local to that workflow run: it does not commit, push or move the tag, and it does not change the independently configured Explorer MSIX version.

Release notes come from the matching version section in the tagged `CHANGELOG.md`, for example `## [4.2.0] - 2026-10-19` or `## [4.2.0-beta1]`. A `v` prefix inside the brackets is also accepted. Only that section body is used, preserving its Markdown subsections and lists; `Unreleased` and other versions are excluded. Add the version section before pushing the tag. A missing, duplicate or empty matching section stops the workflow before building or creating a release. There is no fallback to generated commit notes or to `Unreleased`.
All generated ZIPs, including the legacy transition packages, are uploaded to a draft release before it is published. A failed upload does not expose a partial release to the updater; inspect or remove any remaining draft before retrying a failed run. Existing published releases are not overwritten. The workflow needs Actions permission to write repository contents.

The package manifest is generated from the tagged source by `Publish.ps1` and is included in each application ZIP. Merely committing changes to `master` continues to build Actions artifacts; it does not publish a release or affect installed applications. Git tags must be pushed to trigger publication. The release descriptor and package migration instructions described below are generated from this same tagged source.

After building the release packages, the workflow appends a **Which ZIP file do I need?** guide to the changelog notes. It uses the generated release descriptor and actual ZIP filenames, including the independently versioned Explorer assets. Runtime requirements are read from the project target frameworks: self-contained ZIPs include .NET; standard ZIPs require the corresponding Desktop Runtime. Optional LogViewer, firmware hashes, transition updaters and update metadata are explained separately. Re-running the guide step replaces its own footer instead of duplicating it.

## Manifest-driven updates and repository moves

Edit `src/config/package-manifest.json` in the repository. `Publish.ps1` validates this template, fills in `Version` and `Executable`, and embeds it as `nxfileviewer-package.json` in every application ZIP. `-ExecutableName NewName.exe -PackageName NewName` changes the executable and ZIP prefix while keeping the internal `ApplicationId` stable.

Publishing also generates `nxfileviewer-update.json` next to the ZIPs. The release workflow uploads this small JSON asset alongside the packages; no additional ZIP or helper EXE is required. It maps each architecture/runtime distribution to its actual ZIP name. The updater verifies the descriptor against its GitHub asset SHA-256 digest, checks identity and version, and uses the chosen ZIP's own GitHub SHA-256 digest. Older releases without a descriptor continue to use the original naming convention. If the latest stable release lacks a matching distribution, the new updater searches older releases for a compatible version newer than the installed version. It never substitutes another architecture or runtime distribution.

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

`NxFileViewer_v<version>_<variant/architecture>.zip` contains only a transition `NxFileViewer.exe`, compatible with old single-file updaters. Before migration it reads IncludeViewerPrereleases from NxFileViewerFork.settings.json, or from NxFileViewer.settings.json when the fork file is absent. An explicit boolean true enables stable and pre-release candidates; otherwise it requests the latest stable release, independently of the transition EXE's build version. It selects the matching `NxFileViewerFork_v*` package, verifies its SHA-256 digest, architecture, package identity and actual target release version, then migrates data and installs the renamed application. Failed downloads stay visible with a Retry button. It selects the highest compatible semantic version. Drafts, old-name transition packages and another runtime variant are not substituted; previews require the explicit setting.

The transition stays running beyond older helpers' early-startup check. Its own embedded version must still match the release advertising it: old updaters check the downloaded EXE version before starting it. Therefore a transition ZIP cannot simply be renamed and reused unchanged in a later release. `Publish.ps1` rebuilds all four transition variants with the current application version automatically. Upload those generated old-name ZIPs with each new release, together with the new-name application packages. A user who skipped 4.1.0-beta1 can then update directly through a later release; manually starting a transition EXE also follows its supported release-selection behavior (older transition builds may still be stable-only).

Only the transition moves `Cache/TitleDB`, `Cache/Updates` and `fw/hashes` into `Plugins/TitleDB`, `Plugins/CacheUpdates` and `Plugins/fwhashes`, and renames `NxFileViewer.settings.json` to `NxFileViewerFork.settings.json`. Existing destination files take precedence; conflicting legacy files remain intact. The normal application does not inspect or migrate legacy locations. Manual upgrades from older versions must use the transition EXE or move these files once themselves.

## Publishing 4.1.0-beta1

The project uses package version `4.1.0-beta1`. Run `Publish.ps1` and create a GitHub Release with tag `v4.1.0-beta1` with the GitHub pre-release flag enabled. Attach:

- `NxFileViewerFork_v4.1.0-beta1_x64.zip`
- `NxFileViewerFork_v4.1.0-beta1_x86.zip`
- `NxFileViewerFork_v4.1.0-beta1_self-contained_x64.zip` (compressed, includes .NET)
- `NxFileViewerFork_v4.1.0-beta1_self-contained_x86.zip` (compressed, includes .NET)
- `NxFileViewerFork_v4.1.0-beta1_firmware-hashes.zip` (optional reference add-on)

Also attach the four NxFileViewer_v4.1.0-beta1_<variant/architecture>.zip transition packages for older installations. The transition installs this beta when IncludeViewerPrereleases is true in the existing configuration; false or an absent setting keeps stable-only selection. If both old and fork settings files exist, the fork file takes precedence. Malformed or unreadable configuration stops the transition with its retry/error view; files are not migrated before a package is downloaded and verified.

Application ZIPs include the portable executable with embedded debug symbols; firmware references stay in their separate ZIP. GitHub Actions artifacts alone are not releases available to the updater. Architecture selection follows the running viewer process.

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

Each x64/x86 release offers a standard ZIP requiring .NET 10 Desktop Runtime and a compressed self-contained ZIP with .NET included. Standard names remain NxFileViewerFork_v<VERSION>_<ARCH>.zip; bundled-runtime names are NxFileViewerFork_v<VERSION>_self-contained_<ARCH>.zip. Both contain exactly NxFileViewerFork.exe, nxfileviewer-package.json and LICENSE.txt. Credits and third-party notices are embedded and available offline on the Info page.

The executable records its runtime distribution at build time. Updates preserve this distribution and architecture, including preview releases. A missing compatible package is reported or skipped while listing previews; another runtime distribution is never substituted. Previously installed standard builds continue using the original ZIP names.

The Info tab displays the application version, process architecture and whether .NET is bundled. Standard builds explicitly show the .NET 10 Desktop Runtime requirement.

## Removing old versions

Use **Remove old versions** in the Update Center for the application, NSZ or NxNandManager. Plugin Settings also offers the respective plugin cleanup buttons. The confirmation lists the exact managed paths and warns that the corresponding rollback versions will be lost.

Plugin cleanup removes only inactive `version-<GUID>` directories. It preserves the registered active installation, configured custom executables, folders containing `.keys` files, locked files and directory links. It clears rollback references before removing a previous installation. Missing or invalid active-state metadata prevents cleanup.

Application cleanup removes complete successful automatic-update directories under `Updates/<GUID>`, including `previous.exe`, `install.ps1`, `plan.json`, `ready` and `completed`. Completed leftover folders whose backup EXE was already deleted are also offered. It waits at least one minute after completion and excludes pending, failed, unrecognized, linked or locked updates. Only folders containing known updater files and validated package payload/backup subdirectories are eligible; folders with keys or unrelated user files remain untouched. The current EXE, settings, manually extracted old releases and download caches are not removed.
### Restore the previous program version

The update center offers **Use previous version** while a completed update still contains its validated package backups. It selects the most recently completed eligible update, asks for confirmation, and restarts through the same atomic replacement and startup recovery helper used for updates. The complete previous package is restored, including overwritten companions and removal of files added by that update. The current package becomes a new recovery backup. Cleaning old program versions removes the corresponding rollback backups; the button is disabled when none remain.
