# Update source configuration

Edit `package-manifest.json`. Publishing fills in the version and executable name. Keep the internal application ID stable. The default still uses the existing GitHub repository, with no active migrations.

## Supported sources

`UpdateSources` is an ordered string array. Replace these example addresses before publishing:

```json
"UpdateSources": [
  "github:sandmaennchen5/fork_NxFileViewer",
  "gitlab:https://gitlab.example.org/team/subgroup/application",
  "forgejo:https://codeberg.org/team/application",
  "manifest:https://updates.example.org/application/index.json"
]
```

- Legacy `owner/repository` entries remain GitHub sources.
- GitLab supports nested groups and self-managed hosts. For a deployment prefix, use an explicit API endpoint: `gitlab:https://git.example.org/base/api/v4/projects/team%2Fapplication` (numeric project IDs also work).
- Forgejo accepts repository URLs and explicit API endpoints such as `forgejo:https://forge.example.org/base/api/v1/repos/team/application`. Deployment prefixes and HTTPS ports are preserved.
- `manifest:https://...` or a bare `https://...` reads independent JSON metadata from any HTTPS server or raw-file endpoint. Git/SSH clone URLs are not update sources. No Git executable or additional helper EXE is needed.
- Sources must be publicly accessible over HTTPS with valid certificates and without embedded credentials. Private-forge authentication is not implemented.

The newest eligible version across working sources wins. Unavailable or rate-limited sources do not block mirrors. Sources are saved transactionally only with successful installation. On first start without a state file, sources are also learned from the installed package manifest (supporting an older installer); this does not execute its migration rules. Publish old and new sources before a move; a later release can retire the old source. Clients that missed the preparatory update still need the old location or a manual update.

## Releases on a forge

Upload the application ZIPs and generated `nxfileviewer-update.json` as named assets/links on the same release. The descriptor maps architecture/runtime variants to ZIP names and contains each ZIP SHA-256. GitLab/Forgejo adapters enumerate releases, filter semantic versions/previews/drafts and can choose an older compatible release when a distribution is missing. Upcoming GitLab releases are ignored.

Native GitLab/Forgejo descriptor and ZIP addresses must use the configured HTTPS origin. GitLab prefers `direct_asset_url`. Normal HTTPS redirects to asset storage work through the HTTP client. To explicitly name another download host, use an independent manifest source.

GitHub keeps its existing API digest verification and compatibility with releases without descriptors. Other sources require ZIP checksums in the descriptor. HTTPS provides metadata authenticity; the checksums are integrity checks, not developer signatures. GitHub retains its persistent cache. Other adapters fetch fresh metadata and respect Retry-After cooldowns per origin during the running session.

## Independent HTTPS hosting

Serve the generated `nxfileviewer-update.json` unchanged with its ZIPs in the same directory. Publish ZIPs before replacing the live manifest. For stable/preview channels and intermediate versions together, serve a catalog:

```json
{
  "SchemaVersion": 1,
  "ApplicationId": "NxFileViewerFork",
  "Releases": [
    {
      "SchemaVersion": 1,
      "ApplicationId": "NxFileViewerFork",
      "Version": "4.2.0",
      "Draft": false,
      "Prerelease": false,
      "Packages": [
        {
          "Architecture": "x64",
          "SelfContained": false,
          "AssetName": "NewName_4.2.0_x64.zip",
          "Url": "https://downloads.example.org/releases/NewName_4.2.0_x64.zip",
          "Sha256": "REPLACE_WITH_THE_64_HEX_DIGIT_SHA256_OF_THE_ZIP"
        }
      ]
    }
  ]
}
```

`Url` is optional and can be absolute HTTPS or relative to the manifest. Without it, `AssetName` is resolved next to the manifest. Each release may list four architecture/runtime variants. Beta versions are previews; draft entries are never offered. Replacing a live independent catalog is its publication step: keep future entries marked as drafts until their files are ready. Committing the package template alone changes no installed programs.

All providers share package/EXE/version/architecture validation, transactional migrations, rollback and the embedded PowerShell installer. Migration rules and the supported move actions (including unsupported copy/delete actions) are documented in [Available manifest actions](../docs/Application-updates.md#available-manifest-actions). The publication workflow in this repository remains GitHub Actions; client support does not automatically deploy GitLab/Forgejo CI pipelines or mirrors.

API references: [GitLab releases](https://docs.gitlab.com/api/releases/), [Forgejo API and pagination](https://forgejo.org/docs/latest/user/api/usage/).

## Enable sources and configure timeouts

Keep source strings in `UpdateSources` and configure them by name in `UpdateSourceOptions`:

```json
"UpdateSourceOptions": {
  "github:sandmaennchen5/fork_NxFileViewer": {
    "Enabled": true,
    "TimeoutSeconds": 15
  },
  "gitlab:https://gitlab.com/sandmaennchen5/fork_NxFileViewer": {
    "Enabled": false,
    "TimeoutSeconds": 30
  }
}
```

Options must refer to a listed source. Missing options default to `Enabled: true` and `TimeoutSeconds: 30`. Timeouts are integer seconds from 1 to 300 and bound the entire metadata check for that source, including pagination and manifests; the existing HTTP request limits can end individual requests sooner (currently 30 seconds for metadata). ZIP installation/download behavior is unchanged. A timeout or source failure allows the next enabled source to be checked. Disabled sources make no requests. Disabling every listed source stops source checks without restoring the default GitHub source.

Edit the repository template for future packages. Successful installation saves these options together with the sources in `NxFileViewerFork.update-state.json`; failed installation rolls them back. When that state exists, edit it to change options locally; otherwise the installed `nxfileviewer-package.json` supplies them. A later package with a nonempty source list replaces sources and options. Old string-only manifests and state files remain valid and use the defaults.

## External component and online-service sources

Edit [external-sources.json](external-sources.json) before building or publishing. This separate configuration controls:

- `FirmwareHashes`: GitHub `Repository`, Git `Ref` (branch/tag/commit) and repository-relative `Path` for hash JSONs.
- `TitleDb`: the same fields for regional TitleDB JSONs; an empty `Path` means the repository root.
- `NszRepository`, `NandRepository`: GitHub `owner/repository` used for release discovery and allowed plugin download paths.
- `ExplorerRepository`: GitHub repository used to discover Explorer setup releases.
- `TitlePageUrl`, `TitleInfoApiUrl`, `NLibApiUrl`, `NxContentPageUrl`: HTTPS templates for title websites/API defaults and Settings presets. Keep `{TitleId}` and, where needed, `{Language}`.

For example, changing the firmware repository, branch and folder changes both the update check and the actual hash downloads together:

```json
"FirmwareHashes": {
  "Repository": "your-account/firmware-references",
  "Ref": "main",
  "Path": "hashes"
}
```

NSZ, NxNandManager and TitleDB retain their GitHub API/raw-file formats. Explorer and firmware mirror support is described below. Plugin assets must retain their supported package names/formats and supply GitHub SHA-256 digests. Download URLs remain restricted to the configured plugin repository. Generic GitHub API hostnames and format identifiers are protocol constants, not per-service destinations.

`Publish.ps1` validates this configuration before building. Standard IDE/CLI builds and publishing embed the same file into the main executable as `Viewer.ExternalSources.json`. Application ZIP contents remain unchanged; no additional companion JSON is needed. To change published defaults, edit this file, rebuild and publish a new package. There is no runtime fetch of the development configuration. The application updater and its mirrors/options remain separately configured in `package-manifest.json`.

Existing saved personal title website/API settings are retained; changed defaults apply to new settings and the corresponding Settings presets. The firmware ZIP assembled from local `fw/hashes` is independent of the runtime remote-source settings.

## Explorer and firmware mirrors on GitHub, GitLab and Forgejo

`ExplorerSources` lists typed repository sources for modern Explorer setup releases. `FirmwareHashes.Sources` lists typed repository mirrors for firmware reference files. Both are embedded from `external-sources.json` during the build. The shipped configuration includes GitHub, GitLab and `https://git.sandmaennchen5.de` for both. Older `ExplorerRepository` and firmware `Repository` values remain the fallback when their respective source array is empty; a nonempty array takes precedence.

```json
"ExplorerSources": [
  "github:sandmaennchen5/fork_NxFileViewer",
  "gitlab:https://gitlab.com/sandmaennchen5/fork_NxFileViewer",
  "forgejo:https://git.sandmaennchen5.de/sandmaennchen5/fork_NxFileViewer"
]
```

The firmware object accepts the same array as `Sources`, alongside its existing `Ref` and `Path`. The first complete, valid firmware source wins, in configured order. Each source is downloaded and verified on its own; a failed partial snapshot is discarded before moving to another source. Empty sources, HTTP errors, rate limits, timeouts, unsafe names, changed Git blob hashes and invalid reference contents trigger fallback. GitLab uses repository tree/file APIs; Forgejo uses contents/raw APIs. Native file URLs must stay on the configured HTTPS origin. GitHub retains its metadata cache and API cooldown handling; other forge requests honor Retry-After and have a 30-second request timeout. Firmware checks use the first valid directory listing; actual imports additionally validate every file and the complete reference set.

Explorer enumerates releases across enabled source entries, respects preview/draft/upcoming filtering and selects the highest matching x64/ARM64 MSIX package version. GitHub retains API SHA-256 asset digests. For GitLab/Forgejo, attach both the Explorer setup ZIPs and `nxfileviewer-update.json` from the same Publish output to the release. The descriptor now includes `ExplorerPackages` with architecture, ZIP name and SHA-256. `Publish.ps1` fills this after creating the Explorer packages; skipped Explorer builds emit an empty list. The existing release workflow already uploads the same descriptor and all ZIPs. Mirror those files together: syncing Git commits alone does not publish the Explorer assets. Native release links must use the configured HTTPS origin. ZIP, architecture, MSIX identity and certificate/signature validation remain in place, with the existing certificate consent flow.

These mirror additions do not change NSZ, NxNandManager or TitleDB into multi-forge adapters; those retain their documented settings above. No production release or mirror upload is performed by editing this configuration alone.

API references: [GitLab repository tree](https://docs.gitlab.com/api/repositories/#list-repository-tree), [GitLab raw files](https://docs.gitlab.com/api/repository_files/#get-raw-file-from-repository), [Forgejo API](https://forgejo.org/docs/latest/user/api/usage/).
