# Changelog

All notable changes to NxFileViewer will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Added validation results for `prod.keys` and `title.keys` to the settings window.
- Added detection and reporting of missing `master_key_XX` revisions in outdated `prod.keys` files.
- Added CRC32-based validation of known master-key revisions through `master_key_15`.
- Added malformed-line detection for key files.
- Added structural validation of Rights ID and title-key pairs in `title.keys`.
- Added an estimate of the newest supported firmware based on the highest valid master-key revision.
- Added a warning when a key file contains a newer master-key revision that this application version cannot validate or map to a firmware.
- Added localized validation messages for English, German, French, and Spanish.
- Added automated tests for key-file validation, firmware mapping, and unknown master-key detection.
- Added anonymous FTP support to the existing HTTP/HTTPS key downloader.
- Added editable default Sphaira FTP locations for `prod.keys` and `title.keys`; customized addresses continue to be stored in the application settings.
- Downloads now use a temporary file and only replace the destination after a successful transfer.

### Notes

- The firmware shown for `prod.keys` is the newest content firmware supported by its keys. The exact firmware on which the file was dumped cannot be determined from the key file.
- `title.keys` can be checked for valid structure, but title-key values cannot be compared against a universal list of expected values.

## [3.0.3]

- Existing release preceding this changelog.
