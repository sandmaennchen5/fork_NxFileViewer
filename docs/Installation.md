# Install NxFileViewer Fork

[Documentation index](README.md) · [GitHub releases](https://github.com/sandmaennchen5/fork_NxFileViewer/releases)

Open **Assets** on the release page and choose the Viewer package for your system. Packages named `NxFileViewerFork.LogViewer` contain the separate log viewer. If the stable release has no package for your platform, check the prereleases.

| System | Architecture | Package |
| --- | --- | --- |
| Windows | `win-x64` for 64-bit Intel or AMD, `win-arm64` for ARM, `win-x86` for 32-bit Windows | ZIP |
| Linux | `linux-x64` for Intel or AMD, `linux-arm64` for ARM | `.AppImage` or ZIP with the Linux executable |
| macOS | `osx-arm64` for Apple Silicon, `osx-x64` for Intel | Direct `.dmg` or `.app.zip` |

Choose **`with-net`** to use the included .NET runtime. With **`without-net`**, install the matching .NET 10 runtime for your architecture. On Windows, use the .NET 10 Desktop Runtime. On Linux and macOS, use the .NET 10 Runtime for your platform. [Microsoft .NET downloads](https://dotnet.microsoft.com/download/dotnet/10.0)

## Windows

1. Extract the ZIP to a folder you can write to, such as `%USERPROFILE%\Apps\NxFileViewerFork`.
2. Start `NxFileViewerFork.exe` from the extracted folder.
3. Configure your key files under **Settings → Keys**, if needed. [Keys and title sources](User-guide.md#keys-and-title-sources)

Windows file associations and Explorer integration are optional. [Explorer integration](Explorer-integration.md)

## Linux

### AppImage

Save the `.AppImage` to a writable folder such as `~/Applications`. In that folder, mark the file as executable and start it. Substitute your actual download filename in both commands.

```sh
chmod +x './NxFileViewerFork.AppImage'
./NxFileViewerFork.AppImage
```

If the AppImage reports a FUSE error on Debian or LMDE, follow the AppImage FUSE guide. You can also try `APPIMAGE_EXTRACT_AND_RUN=1 ./NxFileViewerFork.AppImage` to start it without FUSE. [AppImage FUSE troubleshooting](https://docs.appimage.org/user-guide/troubleshooting/fuse.html)

### ZIP with the Linux executable

Extract the ZIP into a writable folder and open that folder in a terminal. Start the executable directly.

```sh
chmod +x ./NxFileViewerFork
./NxFileViewerFork
```

For either Linux package, **Settings → Program → Shortcuts** can create entries in the applications menu, on the desktop, or both. Recreate them if you move the program.

## macOS

- **DMG** — Open the `.dmg`, drag the app into **Applications** or a writable folder such as `~/Applications`, eject the disk image, then open the copied app.
- **App ZIP** — Extract the `.app.zip`, move the resulting `.app` to the same kind of destination, then open it in Finder.

The installed app needs write access to its data directory inside the bundle. The updater also needs write access to the folder containing the bundle. Use a location you can write to if the system-wide Applications folder is restricted.

## Where the Viewer stores data

| Package | Default data location |
| --- | --- |
| Windows ZIP | `NxFileViewerFork-Daten\` next to `NxFileViewerFork.exe` |
| Linux AppImage | `NxFileViewerFork-Daten/` next to the original `.AppImage` |
| Linux ZIP | `NxFileViewerFork-Daten/` next to the `NxFileViewerFork` executable |
| macOS DMG or App ZIP | `Contents/MacOS/` **inside the installed `.app`** |

On macOS, right-click the installed app in Finder, choose **Show Package Contents**, and open **Contents → MacOS**. For example, a bundle installed in Applications uses `/Applications/NxFileViewerFork Avalonia.app/Contents/MacOS/`. Back up this data before manually replacing the bundle in Finder.

The data location holds `NxFileViewerFork.settings.json`, plus folders such as `Logs`, `History` and `Plugins` as they are used. Key files can also live there. A custom path configured under **Settings → Keys** points to the location you selected.

### Move existing program data

On Windows and Linux, an installation with recognized data directly beside the program continues to use that directory until `NxFileViewerFork-Daten` exists. **Settings → Updates → Migrate application data** appears when the Viewer finds a source it can offer to migrate. Both Linux package types support this and also check previous folders under the user's local data directory. The migration copies missing files and keeps existing destination files and the source. Restart the Viewer afterward.

## Update the installed application

Use **Settings → Updates**. The Viewer selects an update matching the installed platform, architecture and .NET variant. On macOS it prefers a direct DMG and can use the App ZIP when a matching DMG is unavailable. The installed app stays at the same path. AppImage updates likewise replace the file at its current path. If your installed version predates support for updating its package type, replace it manually once. [Application updates](Application-updates.md)
