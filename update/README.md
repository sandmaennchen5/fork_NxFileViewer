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
