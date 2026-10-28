using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using Emignatik.NxFileViewer.Logging;
using Emignatik.NxFileViewer.Services.Integrity;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Logging;

public class MemoryTraceDiagnosticsTest
{
    [Fact]
    public void PassiveSnapshotContainsProcessAndGcCounters()
    {
        var sample = MemorySnapshot.Capture();
        Assert.True(sample.WorkingSetBytes > 0);
        Assert.True(sample.PrivateBytes > 0);
        Assert.True(sample.ManagedBytes > 0);
        Assert.True(sample.TotalAllocatedBytes >= sample.ManagedBytes);
        Assert.True(sample.Gen0 >= 0 && sample.Gen1 >= 0 && sample.Gen2 >= 0);
    }

    [Fact]
    public void SamplerFiltersLiveAndStopsOnDispose()
    {
        var logger = new LoggerSource { Level = LogLevel.Information };
        var messages = new ConcurrentQueue<string>();
        using var received = new ManualResetEventSlim();
        logger.Log += (level, message) => { Assert.Equal(LogLevel.Trace, level); messages.Enqueue(message); received.Set(); };
        using var sampler = new MemoryTraceDiagnostics(logger);
        sampler.CaptureNow();
        Assert.Empty(messages);
        logger.Level = LogLevel.Trace;
        sampler.Refresh();
        Assert.True(received.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        sampler.Dispose();
        var count = messages.Count;
        sampler.CaptureNow();
        Assert.Equal(count, messages.Count);
        Assert.Contains("workingSetBytes=", messages.ToArray()[0]);
        Assert.Contains("gen2=", messages.ToArray()[0]);
    }

    [Fact]
    public void TraceEnablesCompatibleBatchCsvWithoutLauncher()
    {
        var path = Path.Combine(Path.GetTempPath(), "NxMemoryTrace-" + Guid.NewGuid(), "memory.csv");
        try
        {
            var logger = new LoggerSource { Level = LogLevel.Information };
            var messages = new ConcurrentQueue<string>();
            logger.Log += (_, message) => messages.Enqueue(message);
            var diagnostics = new BatchMemoryDiagnostics(logger, path);
            Assert.False(diagnostics.IsEnabled);
            Assert.Equal("off", diagnostics.ActiveMode);
            Assert.Empty(diagnostics.Capture("disabled", 0, 0));
            Assert.False(File.Exists(path));
            logger.Level = LogLevel.Trace;
            Assert.True(diagnostics.IsEnabled);
            Assert.Equal("trace", diagnostics.ActiveMode);
            Assert.Contains("Results 123", diagnostics.Capture("scan-progress", 123, 1));
            var lines = File.ReadAllLines(path);
            Assert.Equal(2, lines.Length);
            Assert.Equal(14, lines[0].Split(',').Length);
            Assert.Equal(14, lines[1].Split(',').Length);
            Assert.Contains(",trace,scan-progress,123,1,", lines[1]);
            Assert.Contains("results=123; previews=1", Assert.Single(messages));
            diagnostics.Capture("scan-completed", 123, 0);
            Assert.Equal(3, File.ReadAllLines(path).Length);
            logger.Level = LogLevel.Debug;
            Assert.False(diagnostics.IsEnabled);
            Assert.Equal("off", diagnostics.ActiveMode);
            Assert.Empty(diagnostics.Capture("disabled-again", 123, 0));
            Assert.Equal(3, File.ReadAllLines(path).Length);
        }
        finally { if (Directory.Exists(Path.GetDirectoryName(path))) Directory.Delete(Path.GetDirectoryName(path)!, true); }
    }
}
