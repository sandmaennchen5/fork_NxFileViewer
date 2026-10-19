# Batch memory diagnostics

## In-app Trace diagnostics (no PowerShell launcher required)

Set the general minimum log level to **Trace** in Settings and apply. The application starts passive process measurements immediately and repeats them every ten seconds, including while idle. Change the level back to stop them. A plugin-only Trace override does not activate the process-wide memory sampler.

The session log under `Logs` contains working-set/private/.NET managed bytes, heap and fragmentation at the last GC, total allocated bytes and generation 0/1/2 collection counters. File reading, integrity checks, detached metadata snapshots, logo deduplication, history writes/restores and cache release emit additional Trace events. No key material or icon contents are added to these events.

In the batch page, Trace automatically enables the existing memory diagnostic area, CSV measurements and **Measure memory + GC** button. No restart or environment variables are required. CSV files remain under `%LOCALAPPDATA%\NxFileViewer\Diagnostics`; their schema is compatible with earlier captures. Stage samples include opening/closing the batch page, starting/finishing scans, each 100 results, restoring History, changing the diagnostic level, and measurements before/after the manual GC. The periodic process samples are in the session log.

Automatic measurements never force GC. To compare retained memory after a scan, wait 30 seconds and use the manual button; it stays disabled during a running task. The last-GC heap counters are not live heap sizes, and total allocations are cumulative. Trace is more verbose and has diagnostic overhead. The on-screen log retains at most 2,000 entries, preserving the current file heading; older entries remain in the session log on disk.

For a regular memory investigation, enable Trace, scan the affected folder, wait, optionally measure with GC, then save the session log and batch CSV and return to Information. The launcher below remains available only for the older controlled `no-logos` / `one-preview` experiments.

## Optional comparison launcher

This is an opt-in experiment for issue #21. Normal launches with a level above Trace do not enable memory diagnostics.
Build first: dotnet build src/NxFileViewer/NxFileViewer.csproj

Close the application between runs. Start each run from the repository root:

1. powershell -NoProfile -File scripts/Start-MemoryDiagnostic.ps1 -Mode baseline
2. powershell -NoProfile -File scripts/Start-MemoryDiagnostic.ps1 -Mode no-logos
3. powershell -NoProfile -File scripts/Start-MemoryDiagnostic.ps1 -Mode one-preview

For a different build, add -ExePath 'C:\path\NxFileViewer.exe'.
Use the same executable, folder, scan options and History settings in all runs.
Use Scan, not Scan and Verify; do not select other rows during the scan.
After completion wait 30 seconds, then press "Measure memory + GC" once.
Record file count and save the CSV from each run. Then close the program.
For a fourth comparison, launch baseline fresh and load the same completed History
entry without scanning, wait 30 seconds, then press the measurement button.

The diagnostic area shows the mode, latest measurement and exact CSV path.
Logs are stored under %LOCALAPPDATA%\NxFileViewer\Diagnostics.
Rows are written at window creation, scan start/end, every 100 results,
History restore, and immediately before/after the manual full GC.
Values are bytes; divide by 1048576 for MiB. heapAtLastGcBytes and
fragmentedAtLastGcBytes describe the last GC, not a live heap measurement.
totalAllocatedBytes is cumulative allocation, not retained memory.
No forced GC happens during the scan. The manual GC button is disabled during work.

baseline preserves existing behavior.
no-logos skips BitmapImage decoding in TitleInfoViewModel, including all languages.
Encoded icon bytes and metadata remain loaded; the mode tests decoded images only.
one-preview keeps logos enabled but retains only the most recently cached overview.
Result summaries and History remain available; selecting an older result after the
scan can reopen its source to build a new preview.

Interpretation:
- A large reduction with no-logos implicates decoded bitmap retention.
- A large reduction with one-preview implicates retained overview graphs, including logos.
- A reduction only after GC points toward collectible scan allocations.
- Persistently high private memory with a small managed heap warrants a native/WPF profile.
These measurements narrow the cause; they do not prove a leak or uniquely identify
the allocation retaining all memory. Working set alone does not establish a leak.

Remove the environment variables and select a general log level above Trace to disable diagnostics.
The launcher sets them only for its child process.
After the History snapshot fix, baseline also releases completed live previews. The one-preview mode no longer restores the old unbounded cache; use previously captured baseline CSVs for before/after comparisons.
