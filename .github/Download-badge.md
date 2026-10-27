# Download badge

The badge data is stored in [downloads.json](downloads.json).
[The workflow](workflows/update-download-badge.yml) refreshes it every six hours
or on manual dispatch. Its filter is defined in
[update_download_badge.py](scripts/update_download_badge.py).

## Counted downloads

All published releases, including betas, are included for x64/x86, with and without
the runtime:

- `NxFileViewerFork_v<Version>[_self-contained]_<x64|x86>.zip`
- Legacy `NxFileViewer_v<Version>...zip` files only for versions before 4.1.0.

Transition updaters from 4.1.0 onward, LogViewer, Explorer packages, firmware hashes,
manifests, source archives and Actions artifacts are excluded. Other or unversioned
filenames are not inferred.

GitHub and Forgejo expose `download_count`. GitLab release links do not expose an
equivalent counter; the previously used `direct_asset_download_count` field does
not exist there. GitLab is therefore explicitly excluded. See the
[GitLab release links API](https://docs.gitlab.com/api/releases/links/).

## Pagination and failures

Release lists and GitHub asset lists are read across all pages. Forgejo's attachment
endpoint returns its complete list without pagination. HTTP status, JSON shape and
counter values are validated. Requests have timeouts and transient failures receive
bounded retries. If any source fails, the existing badge is preserved and the
workflow fails visibly.

These are download counts, not unique users. Repeated downloads and automatic
application updates count too. Deleted assets can no longer be queried. The first
recalculation may reduce an old total that included auxiliary packages. Badge
caching can delay the displayed value.

## Local tests

From the repository root, without network access:

```powershell
python -B -m unittest discover -s .github/scripts -p test_update_download_badge.py
```
