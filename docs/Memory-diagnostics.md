# Batch memory diagnostics

## In-app Trace diagnostics

Set the general minimum log level to **Trace** in Settings and apply. The application starts passive process measurements immediately and repeats them every ten seconds, including while idle. Change the level back to stop them. A plugin-only Trace override does not activate the process-wide memory sampler.

The session log under `Logs` contains working-set/private/.NET managed bytes, heap and fragmentation at the last GC, total allocated bytes and generation 0/1/2 collection counters. File reading, integrity checks, detached metadata snapshots, logo deduplication, history writes/restores and cache release emit additional Trace events. No key material or icon contents are added to these events.

In the batch page, Trace automatically enables the existing memory diagnostic area, CSV measurements and **Measure memory + GC** button. No restart or environment variables are required. New CSV files are written under `Logs\Diagnostics` inside the application directory, alongside the session logs (existing captures under `%LOCALAPPDATA%\NxFileViewer` are left in place); their schema is compatible with earlier captures. Stage samples include opening/closing the batch page, starting/finishing scans, each 100 results, restoring History, changing the diagnostic level, and measurements before/after the manual GC. The periodic process samples are in the session log.

Automatic measurements never force GC. To compare retained memory after a scan, wait 30 seconds and use the manual button; it stays disabled during a running task. The last-GC heap counters are not live heap sizes, and total allocations are cumulative. Trace is more verbose and has diagnostic overhead. The on-screen log retains at most 2,000 entries, preserving the current file heading; older entries remain in the session log on disk.

For a regular memory investigation, enable Trace, scan the affected folder, wait, optionally measure with GC, then save the session log and batch CSV and return to Information.

## Comparing scan and History memory

Use the same executable, folder, scan options and History settings in both runs. First enable Trace, scan the folder and wait 30 seconds after completion before using the manual GC button. Then close and restart the application, load the same completed History entry without scanning, wait 30 seconds and measure again. Compare the result counts, private memory and managed heap in the session logs and CSVs.

A reduction after manual GC points toward collectible allocations. Persistently high private memory with a small managed heap warrants a native/WPF profile. Working set alone does not prove a leak. Divide byte values by 1048576 for MiB. The CSV mode is `trace`; diagnostics stop when the general level is raised above Trace.
