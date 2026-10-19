using System;
using Emignatik.NxFileViewer.Logging;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.IO;

namespace Emignatik.NxFileViewer.Services.Integrity;

/// <summary>Opt-in measurements only; never forces GC during normal scans.</summary>
public sealed class BatchMemoryDiagnostics(ILogger? logger = null, string? logPath = null)
{

    public bool IsEnabled => logger?.IsEnabled(LogLevel.Trace) == true;
    public string ActiveMode => IsEnabled ? "trace" : "off";

    public string LogPath { get; } = logPath ?? Path.Combine(
        AppContext.BaseDirectory, "Logs", "Diagnostics",
        $"memory-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Environment.ProcessId}-{Guid.NewGuid():N}.csv");

    public string Capture(string stage, int results, int previews)
    {
        if (!IsEnabled) return "";
        try
        {
            var sample = MemorySnapshot.Capture();
            if (logger?.IsEnabled(LogLevel.Trace) == true) sample.WriteTrace(logger, stage, results, previews);
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            if (!File.Exists(LogPath))
                File.AppendAllText(LogPath, "utc,mode,stage,results,previews,workingSetBytes,privateBytes,managedBytes,heapAtLastGcBytes,fragmentedAtLastGcBytes,totalAllocatedBytes,gen0,gen1,gen2\n");
            File.AppendAllText(LogPath, string.Join(",", DateTime.UtcNow.ToString("O"), ActiveMode, stage, results, previews,
                sample.WorkingSetBytes, sample.PrivateBytes, sample.ManagedBytes, sample.HeapAtLastGcBytes, sample.FragmentedAtLastGcBytes,
                sample.TotalAllocatedBytes, sample.Gen0, sample.Gen1, sample.Gen2) + "\n");
            return string.Format(CultureInfo.CurrentCulture,
                "{0}: RAM {1:N0} MiB | Private {2:N0} MiB | .NET {3:N0} MiB | Results {4} | Previews {5}",
                stage, sample.WorkingSetBytes / 1048576d, sample.PrivateBytes / 1048576d, sample.ManagedBytes / 1048576d, results, previews);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return "Memory log unavailable: " + ex.Message;
        }
    }
}