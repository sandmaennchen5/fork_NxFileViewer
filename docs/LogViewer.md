# NxFileViewer Fork LogViewer

A standalone Windows viewer for diagnostic logs. NxFileViewer, plugins and key
files are not required. Extract the ZIP and run `NxFileViewerFork.LogViewer.exe`.
Self-contained packages include .NET; standard packages require .NET 10 Desktop Runtime.

## Features

- Open a log with **Open / Ctrl+O**, drag and drop, or the first command-line argument.
- Information is blue, warnings yellow, and Error/Critical red. Entries are grouped
  by file, as on the main application's Log page.
- Search case-insensitively across messages, timestamps, severity and associated
  file paths; **Ctrl+F** focuses the search box.
- Combine search, minimum severity and the optional RAM/GC-only filter. File
  headings remain visible when their opening messages are filtered out.
- Multiline exceptions remain one entry. The lower detail pane shows the complete
  selectable text; **Ctrl+C** in the list copies the entry and its file context.
- **F5 / Reload** reads the current contents of an active session. Files are not
  watched automatically.
- The virtualized list has no 2,000-entry limit. Files load in the background, but
  their text stays in memory for searching, so large logs require more RAM.

The viewer reads `.log` and `.txt` files without modifying them. ZIPs are portable
application packages, not log input files. The interface uses German for a German
Windows display language and English otherwise.

## Build

Publish the LogViewer with `./Publish.ps1 -LogViewerOnly` from the repository root.
For a specific platform use `-AvaloniaLogViewer -AvaloniaRuntimeIdentifiers win-x64`.
Packages use the name `NxFileViewerFork.LogViewer_v<VERSION>_<RID>-with-net.zip`
or `NxFileViewerFork.LogViewer_v<VERSION>_<RID>-without-net.zip`.

The project is `src/NxFileViewer.LogViewer/NxFileViewer.LogViewer.csproj`.
See [development and publishing](Development.md) for more build targets.
