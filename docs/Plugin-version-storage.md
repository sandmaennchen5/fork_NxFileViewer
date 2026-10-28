# Plugin version storage

All managed plugins share the same pointer schema and installation metadata format.
The common implementation is `PluginVersionStorage`; plugin-specific code still performs
the required download, compatibility and extraction checks before activating a version.

| Plugin | Active pointer | Installation metadata |
| --- | --- | --- |
| nicoboss/nsz | `Plugins/NSZ/active.json` | `Plugins/NSZ/version-<id>/installation.json` |
| NxNandManager | `Plugins/NxNandManager/active.json` | `Plugins/NxNandManager/version-<id>/installation.json` |
| nscb_rust | `Plugins/PackageEngines/Rust/active.json` | `Plugins/PackageEngines/Rust-<id>/installation.json` |
| Kronos | `Plugins/PackageEngines/Kronos/active.json` | `Plugins/PackageEngines/Kronos-<id>/installation.json` |
| NSCB Desktop | `Plugins/PackageEngines/Desktop/active.json` | `Plugins/PackageEngines/Desktop-<id>/installation.json` |

An active pointer contains only its schema version and current/previous paths:

```json
{
  "SchemaVersion": 1,
  "Current": "../Rust-<id>/nscb_rust.exe",
  "Previous": "../Rust-<previous-id>/nscb_rust.exe"
}
```

Paths are relative to the directory containing `active.json`. NSZ/NAND point at an
executable; Kronos points at its installation directory. Resolved paths must remain
inside the corresponding managed plugin root. Existing version directories are not moved.

Each `installation.json` records its repository, release version or full source commit,
and applicable SHA-256, source-asset and runtime information. Unused fields are omitted.
Display labels use the release version or a 12-character source commit. Installation
metadata is retained when activating a different version or rolling back. Binary
rollback validates its stored executable digest; legacy previous binaries without a
retained digest receive a migration-time fingerprint, which cannot establish their
original download authenticity retroactively. Kronos retains its pinned source commit.

## Automatic migration

Old NSZ/NAND `active.json` files are first copied to `active.json.legacy`. The current
and previous version metadata is written before replacing the active pointer atomically.
The existing release tags and source/executable digests are preserved. Metadata can only
be reconstructed for installations recorded in the old state; older unrelated version
directories are not assigned invented release tags.

The old `Rust-active.json`, `Kronos-active.json` and `Desktop-active.json` files are read
when the new pointer does not yet exist. They remain as migration backups; subsequent
reads and changes use the new per-plugin `active.json` files. Their existing
`installation.json` metadata is reused.

Missing saved package-engine paths can be restored from a valid active pointer.
Explicit manual plugin paths retain precedence. Portable moves preserve the relative
pointers and metadata. Cleanup still protects active/manual installations, nonempty
key files, linked trees and files in use. Deleting a previous version clears its active
pointer reference; its metadata is deleted together with that version directory.