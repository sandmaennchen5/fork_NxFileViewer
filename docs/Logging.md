# Logging

The general minimum severity is configured in Settings. Under **Plugins → Plugin log levels**,
you can override it for nicoboss/nsz, NxNandManager, MTP and NSC_BUILDER/nscb_rust.
The default inherits the general setting. **Off** suppresses messages from that plugin.
Filters apply to both the Log page and saved session files; the session header with
the application version and start time is retained.

For example, general **Warning** with NSZ **Debug** records detailed NSZ process
output while the rest of the application records only warnings and errors.
Overrides follow the plugin's logger category. Shared reader and integrity services
keep the general level even when a plugin starts the operation. Dialogs and progress
indicators remain visible.

| Level | Messages |
| --- | --- |
| Trace / Debug | Technical details; raw NSZ process output is logged at Debug |
| Information | Operation start, successful completion, successful renaming and deliberate cancellation |
| Warning | Missing or unreadable optional images, limitations and available fallbacks |
| Error | Failed requested actions, missing required contents or failed integrity checks |
| Critical | Severe failures; triggers the error indicator like Error. Ordinary file errors are not promoted to Critical |

A minimum severity includes all higher severities. **None** disables messages; it
is not an output severity.

## Trace and memory diagnostics

At the general **Trace** level, passive RAM, heap and GC counters are recorded
immediately and every ten seconds. Batch mode also enables memory diagnostics,
CSV measurements at scan/history events and the manual GC button. A plugin-only
Trace override does not enable global memory measurements. Automatic measurements
do not force garbage collection. See [memory diagnostics](Memory-diagnostics.md).

The live Log page retains at most 2,000 entries. Older entries remain in the session
file, and the heading of the active file group is preserved when the limit is applied.
