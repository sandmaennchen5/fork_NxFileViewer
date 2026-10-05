# Selectable title metadata sources

In Settings, under Miscellaneous, select **Title name source**:

- **Tinfoil** uses the existing configurable Title information API URL. Existing settings retain this source and URL.
- **TitleDB (GitHub)** downloads the selected regional JSON catalog, defaults to DE.de, and looks up entries by their embedded title ID rather than their NSU dictionary key. Missing titles are also looked up in US.en. Catalogs are cached in `%LOCALAPPDATA%/NxFileViewer/TitleDB`, refreshed after 24 hours, and retained when an update fails. Failed refreshes with existing cached data wait five minutes before retrying. Catalog files are replaced only after valid parsing; unwritable cache storage does not prevent using downloaded titles in memory.
- **NLib API** defaults to `https://api.nlib.cc/nx/{TitleId}?lang={Language}`. The URL is editable for other deployments. Language follows the app language or system language for automatic selection, with English as fallback for unsupported languages. The adapter maps NLib's `icon` field.

Applying settings changes the metadata source for subsequent requests. The per-title memory cache includes provider, source URL, region and app language, and expires after 24 hours. The separate title-web-page URL remains configurable independently.

If online information is unavailable, renaming uses the local NACP title when present. A DLC with neither online nor local title information still uses the existing NO_TITLE placeholder.

Sources reviewed:

- https://github.com/blawar/titledb
- https://github.com/ghost-land/Nlib-API

Verification on 2026-10-05: both live sources returned Until Then for title ID 010019C023004000. Solution build passed with zero warnings and errors; all 94 tests passed for version 3.0.4. Tests cover provider routing, NLib language/icon mapping, indexing by title ID, cache persistence across restarts, stale cache during outages, rejection of malformed updates, English fallback, settings persistence and source switching, plus the existing offline-renaming regression tests.

Version 3.0.4 includes these settings. Older Publish packages need rebuilding to include them.
