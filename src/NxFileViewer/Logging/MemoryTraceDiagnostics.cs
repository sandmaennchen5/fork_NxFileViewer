using System;
using System.Diagnostics;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Emignatik.NxFileViewer.Logging;

/// <summary>Passive process samples. Reading counters never requests a collection.</summary>
public sealed record MemorySnapshot(long WorkingSetBytes, long PrivateBytes, long ManagedBytes,
    long HeapAtLastGcBytes, long FragmentedAtLastGcBytes, long TotalAllocatedBytes,
    int Gen0, int Gen1, int Gen2)
{
    public static MemorySnapshot Capture()
    {
        using var process = Process.GetCurrentProcess();
        var info = GC.GetGCMemoryInfo();
        return new(process.WorkingSet64, process.PrivateMemorySize64, GC.GetTotalMemory(false),
            info.HeapSizeBytes, info.FragmentedBytes, GC.GetTotalAllocatedBytes(false),
            GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2));
    }

    public void WriteTrace(ILogger logger, string stage, int results = -1, int previews = -1) =>
        logger.LogTrace("Memory {Stage}: workingSetBytes={WorkingSetBytes}; privateBytes={PrivateBytes}; managedBytes={ManagedBytes}; heapAtLastGcBytes={HeapAtLastGcBytes}; fragmentedAtLastGcBytes={FragmentedAtLastGcBytes}; totalAllocatedBytes={TotalAllocatedBytes}; gen0={Gen0}; gen1={Gen1}; gen2={Gen2}; results={Results}; previews={Previews}",
            stage, WorkingSetBytes, PrivateBytes, ManagedBytes, HeapAtLastGcBytes, FragmentedAtLastGcBytes,
            TotalAllocatedBytes, Gen0, Gen1, Gen2, results, previews);
}

/// <summary>One sampler per logging provider; starts and stops with the live Trace setting.</summary>
public sealed class MemoryTraceDiagnostics : IDisposable
{
    private readonly ILogger _logger;
    private readonly Timer _timer;
    private readonly object _gate = new();
    private bool _active;
    private bool _disposed;

    public MemoryTraceDiagnostics(ILogger logger)
    {
        _logger = logger;
        _timer = new Timer(_ => CaptureNow(), null, Timeout.Infinite, Timeout.Infinite);
        Refresh();
    }

    public void Refresh()
    {
        lock (_gate)
        {
            if (_disposed) return;
            var active = _logger.IsEnabled(LogLevel.Trace);
            if (_active == active) return;
            _active = active;
            _timer.Change(active ? 0 : Timeout.Infinite, active ? 10000 : Timeout.Infinite);
        }
    }

    public void CaptureNow()
    {
        lock (_gate)
        {
            if (_disposed || !_logger.IsEnabled(LogLevel.Trace)) return;
            try { MemorySnapshot.Capture().WriteTrace(_logger, "process-sample"); }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException
                or System.IO.IOException or UnauthorizedAccessException)
            { /* An unavailable diagnostic counter must not terminate a timer callback. */ }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _timer.Dispose();
        }
    }
}
