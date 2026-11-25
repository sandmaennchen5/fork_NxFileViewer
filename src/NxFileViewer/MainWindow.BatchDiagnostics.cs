using System;
using System.IO;
using Avalonia.Interactivity;
using Emignatik.NxFileViewer.Services.Integrity;

namespace NxFileViewer.Avalonia;

public partial class MainWindow
{
    private BatchMemoryDiagnostics? _batchMemoryDiagnostics;

    private void InitializeBatchDiagnostics()
    {
        var path = Path.Combine(ViewerDataDirectory.Root, "Logs", "Diagnostics",
            $"memory-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Environment.ProcessId}-{Guid.NewGuid():N}.csv");
        _batchMemoryDiagnostics = new BatchMemoryDiagnostics(new MemorySessionLogger(this), path);
        BatchMemoryPath.Text = path;
        RefreshBatchDiagnostics("window-opened");
    }

    private void RefreshBatchDiagnostics(string stage)
    {
        var enabled = _batchMemoryDiagnostics?.IsEnabled == true;
        BatchMemoryPanel.IsVisible = enabled;
        BatchMeasureMemory.IsEnabled = enabled && _workspaceJobs.Count == 0;
        if (!enabled) return;
        var german = _text.CultureName.StartsWith("de", StringComparison.OrdinalIgnoreCase);
        BatchMemoryMode.Text = german ? "Speicherdiagnose: TRACE" : "Memory diagnostic mode: TRACE";
        BatchMeasureMemory.Content = german ? "Speicher messen + GC" : "Measure memory + GC";
        RecordBatchMemory(stage);
    }

    private void RecordBatchMemory(string stage)
    {
        if (_batchMemoryDiagnostics?.IsEnabled != true) return;
        BatchMemoryStatus.Text = _batchMemoryDiagnostics.Capture(stage, _batchRows.Count, _batchIcon != null ? 1 : 0);
    }

    private void MeasureBatchMemory(object? sender, RoutedEventArgs e)
    {
        if (_workspaceJobs.Count != 0 || _batchMemoryDiagnostics?.IsEnabled != true) return;
        RecordBatchMemory("manual-before-gc");
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        RecordBatchMemory("manual-after-gc");
    }
}
