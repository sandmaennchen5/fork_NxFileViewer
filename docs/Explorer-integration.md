# Explorer integration

Settings → Program → Windows file associations provides independent controls for the classic and modern Explorer menus. Registration does not change the default app, Windows UserChoice, or the existing Open with registration. Both menus use the same native `IExplorerCommand` extension.

The `NxFileViewer Fork` submenu offers these actions. Titles follow the app language (German, English, French or Spanish); before configuration, the Windows UI language is used with English as fallback. Changing the application language updates an installed menu for the next time it opens.

| Selection | Action | Result |
| --- | --- | --- |
| One supported file | Open in File | File page; no automatic hashing |
| One supported file | Verify in File | File page; automatic NCA hashing where supported |
| One or more supported files / folders | Open in Batch | Scan the selection into Batch without hashing |
| One or more supported files / folders | Verify in Batch | Scan and hash the selection in Batch |

File actions appear before Batch actions for a single file. Further Explorer Batch actions append to the current Explorer batch table until the application is closed or a scan is started using a program button. Only the new selection is processed, with its requested hash mode; existing rows stay in the table and repeated paths update their existing row. A manual scan or a dropped selection starts a fresh batch, and the next Explorer batch starts a new Explorer collection.

Explicit actions override automatic-verification preferences without changing those preferences. Files without NCA integrity data, such as NRO, have no NCA hashes to verify. Folders use the existing Batch recursion/archive settings. Unsupported and non-filesystem selections hide the menu; a mixed selection containing an unsupported file hides it too.

The first process starts the receiver before loading the main window. Subsequent processes pass the entire selection and action to that receiver over a current-user-only named pipe and exit. Requests wait while another operation is running. Separate portable installations have separate receivers. A native UTF-16 response file avoids the Windows command-line length limit; the viewer validates and consumes only its own response files under `%LOCALAPPDATA%\NxFileViewerFork\ExplorerRequests`. A cancelled/failed request is reported, rather than silently falling back to another action. Ordinary file associations retain their automatic-hashing setting; independent Explorer open invocations are not inferred to be one batch selection.

Compression actions are also available for matching local file selections: **Compress**
for NSP/XCI and **Decompress** for NSZ/XCZ, including multiple files. Mixed compressed
and uncompressed selections and folders hide these actions. The viewer prompts for
the destination and source-file handling, uses the configured compression engine,
and verifies results before publishing. Plugins must be enabled in the viewer.
After updating the viewer, add/update the classic menu or install the new modern
Explorer package (1.0.1.0) to receive the new commands.

## Optional feature visibility

Extension version **1.0.4.0** and later reads the saved feature switches whenever Explorer queries a command. Disabling **Compress / Decompress** hides both compression actions. Disabling experimental package operations hides Convert, Merge and Split. Package operations on NSZ/XCZ also require compression because those inputs need decompression first. Open and Verify remain available.

Update the classic registration or install the new modern MSIX once to enable this behavior. Subsequent feature changes require only saving the viewer settings, without rebuilding or reinstalling the MSIX. The native extension reads DWORD values `CompressionEnabled` and `PackageOperationsEnabled` under `HKCU\Software\NxFileViewerFork\ExplorerIntegration`; it never loads the managed viewer or parses its settings file. The viewer synchronizes these values on settings load/save and menu installation, only for the executable owning the registration. Older configurations without these values retain their previous menu visibility until synchronized. An already running viewer still applies its optional features after the restart indicated in Settings.

## Classic menu

Click **Add / update classic menu**. This extracts an immutable, architecture-matched native DLL to `%LOCALAPPDATA%\NxFileViewerFork\ExplorerIntegration` and registers only HKCU keys. No administrator rights or default-app change are needed. On Windows 11 this is the **Show more options** menu. **Remove classic menu** deletes the fork menu and COM registration. After menu installation, update and removal, and on subsequent application starts, unregistered DLL versions are cleaned up. Active registrations in either registry view, locked or loaded DLLs, links, changed payloads and folders containing unrelated files are preserved. Locked versions are retried on the next application start after Explorer releases them; Explorer is never forcibly restarted.

## Modern Windows 11 menu

Click **Install / update modern menu**. The viewer checks the installed MSIX version separately from the application version, finds the highest compatible Explorer package in the official GitHub releases, downloads the x64 or ARM64 setup ZIP for the operating system and verifies its GitHub SHA-256 digest. The existing preview-update setting controls whether preview releases are eligible. Releases without a matching Explorer asset are skipped. If the installed MSIX is current or newer, nothing is downloaded or downgraded; only the current viewer executable path and menu language are refreshed.

Before certificate confirmation the viewer checks the archive contents, package identity, version, architecture, publisher and CMS signer against the public certificate. Only the MSIX and public CER are extracted. Scripts in the downloaded ZIP are never executed. The app uses its own embedded `Trust-ExplorerCertificate.ps1` with the verified public certificate. If the certificate is already in Local Machine > Trusted People, this step is skipped. Otherwise the user approves the administrator prompt and types `YES` in the visible helper window. Cancelling stops installation. After consent, the MSIX is installed as the original user, not as the administrator used for certificate trust. Windows validates the package integrity, signature, timestamp and trust during deployment. No Microsoft Store publication is required.

**Select local MSIX** retains the existing offline installation path. For a manually extracted setup ZIP, run its `Trust-ExplorerCertificate.cmd` first if the certificate is not trusted yet, then select the MSIX. The optional package contains only the native extension, an activation host and icons; the portable viewer stays outside the package.

Installing the modern package does not create an additional classic registry registration. Windows may also expose the packaged extension in the classic context menu. The separate classic registration is added only through its own button; both registrations can be removed independently. The modern package has a separate removal button. The classic and modern registrations can coexist. Re-add the desired integration after moving the viewer, because the extension stores the current executable path. If Explorer caches the old registration, restart Explorer or sign out and back in. Neither setup nor removal forcibly terminates Explorer.

## Removing normal file associations

The **Remove file associations** button unregisters only this fork's ProgID, Open with entries, launcher application registration and capabilities. Other applications and Windows UserChoice are left alone; choose another default app in Windows if the fork was previously the default. The classic and modern context menus have their own removal buttons and remain available independently.

## Publishing

Running `./Publish.ps1` creates signed Explorer packages alongside the viewer ZIPs in `Publish/`:

- `NxFileViewerFork.Explorer_1.0.1.0_x64.msix` and `_x64_setup.zip`
- `NxFileViewerFork.Explorer_1.0.1.0_arm64.msix` and `_arm64_setup.zip`

Upload both `_x64_setup.zip` and `_arm64_setup.zip` as assets of the official GitHub release. Keep their generated names so the in-app installer can find them. The downloader requires the GitHub SHA-256 asset digest (available on current uploads); re-upload an older asset if GitHub does not supply one. App ZIPs and Explorer setup ZIPs remain separate downloads.

Each setup ZIP contains its MSIX, the public `NxFileViewerFork.Explorer.cer`, `Trust-ExplorerCertificate.cmd`, the corresponding PowerShell helper and instructions. Extract the ZIP, run the CMD, approve the administrator prompt, check the publisher and thumbprint and type `YES`. This imports the public certificate into **Local Machine > Trusted People**, never Trusted Root Certification Authorities. Then start the viewer normally and add the modern menu with the included MSIX. The helper does not install the package as administrator. Cancelling leaves trust unchanged. Organization policies may restrict installation.

Publishing creates a five-year self-signed code-signing certificate in **Current User > Personal** on first use and reuses it on later runs. Publishing never adds it to a trust store. The private key stays on the publishing machine and is never included in release files. Keep this certificate and its private key; an encrypted PFX backup may be made separately using Windows certificate management. Do not commit or distribute private keys or PFX files. A new signing certificate requires a new trust confirmation. Certificate expiry alone does not stop an installed extension. Previously timestamped packages can still be accepted after expiry while trust remains in place; Windows validates the timestamp. A valid signing certificate is needed to sign new packages. With the same valid certificate, later releases require no further certificate import. This is individual user trust, not globally recognized publisher trust.

The numeric version is set by `ExplorerPackageVersion` in `NxFileViewer.csproj` (currently `1.0.1.0`). Increment this property when packaged Explorer contents change, independently of the viewer version. If absent, it is derived from the viewer version (`4.0.1-beta4` becomes `4.0.1.4`; the final release uses revision 65535). Use `-ExplorerPackageVersion` to override it. Increment it when updating an installed package with different contents. Signatures receive an RFC 3161 timestamp using DigiCert by default; use `-ExplorerTimestampServer` to override the endpoint.

To use an existing signing identity instead:

```powershell
./Publish.ps1 -ExplorerPublisher 'CN=Your signing identity' -ExplorerCertificateThumbprint YOUR_THUMBPRINT
```

This uses the supplied certificate without generating a local certificate or trust helper. `-UnsignedExplorerPackages` explicitly creates unsigned `.unsigned.msix` files for later signing; they cannot be installed normally before signing and establishing trust. `-SkipExplorerPackages` publishes only viewer ZIPs. The script uses an installed Windows SDK or restores the pinned Microsoft Windows SDK BuildTools package into the NuGet cache. Use `-ExplorerSdkBin` to supply another SDK location.

## Building

Rebuild all native architectures with Zig 0.13.0 (the same toolchain used for the association launcher):

```powershell
./src/NxFileViewer.Explorer/build.ps1 -ZigExe C:/tools/zig/zig.exe
cd src
dotnet build ./NxFileViewer/NxFileViewer.csproj
```

The checked-in native DLLs are embedded in the viewer, so standard builds and single-file updates do not require a C++ toolchain. The x64/ARM64 package hosts are also checked in, so publishing MSIX does not need Zig either. Update these binaries together with the C++ source. Build a modern package separately using an existing code-signing certificate whose subject matches Publisher:

```powershell
./src/NxFileViewer.Explorer/Build-Package.ps1 -Publisher 'CN=Your signing identity' -CertificateThumbprint YOUR_THUMBPRINT -Architecture x64
```

Use `-Architecture arm64` for native ARM64 Explorer, even if the viewer runs under emulation. `-Unsigned` prepares a package for later signing; it does not bypass Windows certificate trust. The script does not create certificates or change trust stores. Supply `-SdkBin` if MakeAppx/SignTool are installed in a nonstandard location. Standalone package output is under `src/NxFileViewer.Explorer/bin`, or the supplied `-OutputDirectory`; the main publish script always uses its own output directory.

## Validation checklist for a release

Test one file, several files, folders, a mixed file/folder selection, spaces/Unicode, a selection longer than 32K command-line characters, viewer closed/running/busy, and both hash settings enabled/disabled. Inspect the classic menu and, with a trusted signed package installed, the modern Windows 11 menu. Verify removal of each integration independently. Automated tests cover action parsing, response-file bounds and consumption, instance forwarding, native menu visibility, registration structure and explicit hash policy.

Publish keeps Explorer packaging files in temporary staging and emits only the setup ZIPs into Publish. The MSIX, public certificate and trust helpers are included inside those ZIPs.

The independent Explorer package version starts at 1.0.1.0. A previously installed test package numbered 4.0.1.6 must be removed once before installing 1.0.1.0, because Windows treats it as a downgrade. The signing certificate and package identity remain unchanged.


## Package actions in the context menu

For NSP/XCI/NSZ/XCZ selections, the classic and modern Explorer menus offer Convert,
Merge and Split to NSP or XCI. Merge appears for at least two files, including
mixed compressed and uncompressed selections. Folders are excluded. Convert and
Split process multiple files individually. Engine selection (Rust or Kronos),
destination selection and verification match the Batch actions. Merge orders
input files by their actual contents: game, update, DLC.

Explorer does not read package metadata or keys to construct its menu. Split is
therefore offered for NSP/XCI/NSZ/XCZ regardless of whether the file contains
multiple contents. Before asking for a destination or decompressing, the
application opens each selected source and checks its CNMT contents. Single-content
files are skipped with an informational message; eligible files continue.
Compressed multipackages are temporarily decompressed and verified before
splitting or merging.

After updating, add the classic Explorer integration again in Settings or
install the modern Explorer package 1.0.3.0 so Windows uses the new native DLL.

The general update check includes the modern Explorer MSIX only when it is installed for the current user. It compares the installed package version with the newest compatible package from the configured Explorer sources, respecting the preview-release setting. New versions appear in the Start update summary; installation remains available through Install / update modern menu. An absent package causes no Explorer release request or update entry. The Windows package query has a 15-second limit and honors cancellation.
