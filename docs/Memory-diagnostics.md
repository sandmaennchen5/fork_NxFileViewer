# Batch memory diagnostics

This is an opt-in experiment for issue #21. Normal application launches are unchanged.
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

Remove the environment variables (or start normally) to disable diagnostics.
The launcher sets them only for its child process.