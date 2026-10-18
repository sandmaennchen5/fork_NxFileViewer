using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace Emignatik.NxFileViewer.Services.Integrity;

/// <summary>Opt-in measurements only; never forces GC during normal scans.</summary>
public sealed class BatchMemoryDiagnostics
{
    public static bool Enabled => Environment.GetEnvironmentVariable("NXFV_MEMORY_DIAGNOSTICS") == "1";
    public static bool DisableLogos => Enabled && Environment.GetEnvironmentVariable("NXFV_MEMORY_MODE") == "no-logos";
    public static bool LimitPreviews => Enabled && Environment.GetEnvironmentVariable("NXFV_MEMORY_MODE") == "one-preview";
    public static string Mode => !Enabled ? "off" : DisableLogos ? "no-logos" : LimitPreviews ? "one-preview" : "baseline";

    public string LogPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NxFileViewer", "Diagnostics",
        $"memory-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Environment.ProcessId}-{Guid.NewGuid():N}.csv");

    public string Capture(string stage, int results, int previews)
    {
        if (!Enabled) return "";
        try
        {
            using var process = Process.GetCurrentProcess();
            var managed = GC.GetTotalMemory(false);
            var info = GC.GetGCMemoryInfo();
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            if (!File.Exists(LogPath))
                File.AppendAllText(LogPath, "utc,mode,stage,results,previews,workingSetBytes,privateBytes,managedBytes,heapAtLastGcBytes,fragmentedAtLastGcBytes,totalAllocatedBytes,gen0,gen1,gen2\n");
            File.AppendAllText(LogPath, string.Join(",", DateTime.UtcNow.ToString("O"), Mode, stage, results, previews,
                process.WorkingSet64, process.PrivateMemorySize64, managed, info.HeapSizeBytes, info.FragmentedBytes,
                GC.GetTotalAllocatedBytes(false), GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2)) + "\n");
            return string.Format(CultureInfo.CurrentCulture,
                "{0}: RAM {1:N0} MiB | Private {2:N0} MiB | .NET {3:N0} MiB | Results {4} | Previews {5}",
                stage, process.WorkingSet64 / 1048576d, process.PrivateMemorySize64 / 1048576d, managed / 1048576d, results, previews);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return "Memory log unavailable: " + ex.Message;
        }
    }
}