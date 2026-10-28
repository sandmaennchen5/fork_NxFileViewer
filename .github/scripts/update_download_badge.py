"""Count real viewer release ZIP downloads (GitHub + Forgejo)."""
import json
import os
import re
import sys
import time
from pathlib import Path
from urllib.error import HTTPError, URLError
from urllib.parse import urlencode, urlsplit
from urllib.request import Request, urlopen

VIEWER_ZIP = re.compile(
    r"^(NxFileViewerFork|NxFileViewer)_v(\d+)\.(\d+)\.(\d+)"
    r"(?:-[0-9a-z]+(?:[.-][0-9a-z]+)*)?(?:_self-contained)?_(?:x64|x86)\.zip$",
    re.IGNORECASE,
)
TIMEOUT = 20
MAX_PAGES = 10000


def is_viewer_zip(name):
    match = VIEWER_ZIP.fullmatch(name) if isinstance(name, str) else None
    if not match:
        return False
    # Since 4.1.0, the old product name denotes the transition updater.
    return match[1].lower() == "nxfileviewerfork" or tuple(map(int, match.group(2, 3, 4))) < (4, 1, 0)


def get_json(url, headers):
    for attempt in range(3):
        try:
            with urlopen(Request(url, headers=headers), timeout=TIMEOUT) as response:
                data = response.read(20 * 1024 * 1024 + 1)
            if len(data) > 20 * 1024 * 1024:
                raise ValueError("API response exceeds 20 MiB")
            return json.loads(data)
        except HTTPError as error:
            if error.code not in (429, 500, 502, 503, 504) or attempt == 2:
                raise RuntimeError(f"API returned HTTP {error.code}: {url}") from None
            retry_after = error.headers.get("Retry-After", "")
            time.sleep(min(int(retry_after), 30) if retry_after.isdigit() else 2 ** attempt)
        except (URLError, TimeoutError, OSError):
            if attempt == 2:
                raise RuntimeError(f"API connection failed: {url}") from None
            time.sleep(2 ** attempt)


def require_items(data, url):
    if not isinstance(data, list) or any(not isinstance(item, dict) for item in data):
        raise ValueError(f"Expected an API list: {url}")
    return data


def pages(url, headers, size_parameter="per_page"):
    seen = set()
    for page in range(1, MAX_PAGES + 1):
        # Continue until empty: servers may cap the requested page size.
        items = require_items(get_json(url + "?" + urlencode({size_parameter: 100, "page": page}), headers), url)
        if not items:
            return
        for item in items:
            item_id = item.get("id")
            if type(item_id) is not int or item_id in seen:
                raise ValueError(f"Missing/repeated item ID; pagination may be broken: {url}")
            seen.add(item_id)
            yield item
    raise ValueError(f"API pagination exceeded {MAX_PAGES} pages: {url}")


def asset_count(assets):
    total = 0
    for asset in assets:
        if is_viewer_zip(asset.get("name")):
            count = asset.get("download_count")
            if type(count) is not int or count < 0:
                raise ValueError("A viewer asset has no valid download_count")
            total += count
    return total


def count_source(base, provider, headers):
    total = 0
    for release in pages(base + "/releases", headers, "limit" if provider == "forgejo" else "per_page"):
        if release.get("draft"):
            continue
        assets_url = base + f"/releases/{release['id']}/assets"
        if provider == "github":
            assets = pages(assets_url, headers)
        else:
            # Forgejo's attachment endpoint returns the entire list, without page/limit.
            assets = require_items(get_json(assets_url, headers), assets_url)
        total += asset_count(assets)
    return total


def repo_name(value):
    if not re.fullmatch(r"[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+", value):
        raise ValueError("Expected owner/repository")
    return value


def generate(output):
    github_repo = repo_name(os.environ["GITHUB_REPO"])
    forgejo_repo = repo_name(os.environ["FORGEJO_OWNER"] + "/" + os.environ["FORGEJO_REPO"])
    forgejo_url = os.environ["FORGEJO_URL"].rstrip("/")
    parsed = urlsplit(forgejo_url)
    if parsed.scheme != "https" or not parsed.hostname or parsed.username or parsed.password or parsed.query or parsed.fragment:
        raise ValueError("Expected an HTTPS Forgejo base URL")
    headers = {"User-Agent": "NxFileViewer-download-badge", "Accept": "application/json"}
    github_headers = dict(headers)
    if os.environ.get("GITHUB_TOKEN"):
        github_headers["Authorization"] = "Bearer " + os.environ["GITHUB_TOKEN"]
    github = count_source("https://api.github.com/repos/" + github_repo, "github", github_headers)
    forgejo = count_source(forgejo_url + "/api/v1/repos/" + forgejo_repo, "forgejo", headers)
    badge = {"schemaVersion": 1, "label": "viewer downloads", "message": str(github + forgejo), "color": "blue"}
    # Do not change the old badge unless every source completed successfully.
    output = Path(output)
    output.parent.mkdir(parents=True, exist_ok=True)
    temporary = output.with_name(output.name + ".tmp")
    temporary.write_text(json.dumps(badge, ensure_ascii=False) + "\n", encoding="utf-8")
    temporary.replace(output)
    print(f"Viewer downloads: GitHub={github}, Forgejo={forgejo}, total={github + forgejo}.")
    print("GitLab release links expose no download counter and are not counted.")


if __name__ == "__main__":
    try:
        generate(sys.argv[1] if len(sys.argv) > 1 else ".github/downloads.json")
    except (RuntimeError, ValueError, KeyError, OSError) as error:
        print(f"Download badge unchanged: {error}", file=sys.stderr)
        sys.exit(1)
