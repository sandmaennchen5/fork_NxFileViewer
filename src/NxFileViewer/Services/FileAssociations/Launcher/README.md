# Fork file-association launcher

`NxFileViewerFork.Open.exe` is a native x86 Windows launcher (also usable on x64 Windows). It only starts the adjacent `NxFileViewer.exe`, forwards all arguments using Windows quoting, and exits immediately. It never downloads, updates, reads documents, or needs a runtime installation.

The checked-in binary is embedded into the main application so existing single-EXE release ZIPs and the current updater format remain compatible. Update `launcher.c` and rebuild this binary together. `ForkAssociationLauncher` compares SHA-256 bytes to refresh installed copies after application updates.

Rebuild using Zig 0.13.0 from https://ziglang.org/download/0.13.0/zig-windows-x86_64-0.13.0.zip (SHA-256 `d859994725ef9402381e557c60bb57497215682e355204d754ee3df75ee3c158`):

```powershell
.\build.ps1 -ZigExe C:\path\to\zig.exe
```

The launcher should always live beside the main executable. Registration never changes Windows UserChoice or original-viewer registrations.
