using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using NxFileViewer.LogViewer;
using Xunit;
namespace Emignatik.NxFileViewer.Test.Logging;

public class LogDocumentTest
{
    public const string Sample = "[2026-10-19 10:51:55.761 +02:00] [Information] NxFileViewer Fork session started. Version: 4.1.0\n"
        + "[2026-10-19 10:51:56.001 +02:00] [Information] Loader: =====> G:\\Games\\GameOne.nsz <=====\n"
        + "[2026-10-19 10:51:57.001 +02:00] [Information] Metadata ready\n"
        + "[2026-10-19 10:51:58.001 +02:00] [Warning] Optional language logo missing\n"
        + "[2026-10-19 10:51:59.001 +02:00] [Error] Cannot read content\nSystem.IO.IOException: Broken data\n   at Loader.Read()\n"
        + "[2026-10-19 10:52:00.001 +02:00] [Trace] Memory scan-progress: workingSetBytes=1024; managedBytes=512; gen2=1\n"
        + "[2026-10-19 10:52:01.001 +02:00] [Information] =====> G:\\Games\\GameTwo.nsp <=====\n"
        + "[2026-10-19 10:52:02.001 +02:00] [Critical] Device unavailable\n";

    [Fact]
    public void PreservesTimestampsMultilineExceptionsAndFileContext()
    {
        var entries = LogDocument.Read(new StringReader(Sample), token: TestContext.Current.CancellationToken);
        Assert.Equal(8, entries.Count);
        Assert.Null(entries[0].FilePath);
        Assert.Equal("2026-10-19 10:51:55.761 +02:00", entries[0].Timestamp);
        Assert.Equal(EntryLevel.Error, entries[4].Level);
        Assert.Contains("at Loader.Read()", entries[4].Message);
        Assert.Contains("[Error] Cannot read content", entries[4].RawText);
        Assert.Equal(@"G:\Games\GameOne.nsz", entries[4].FilePath);
        Assert.Equal(@"G:\Games\GameTwo.nsp", entries[7].FilePath);
        Assert.True(entries[1].IsFileMarker);
        Assert.False(entries[4].IsFileMarker);
    }

    [Fact]
    public void CombinedFiltersKeepHeadingsEvenWhenOpeningMarkersAreExcluded()
    {
        var entries = LogDocument.Read(new StringReader(Sample), token: TestContext.Current.CancellationToken);
        var rows = LogDocument.Filter(entries, "gameone.NSZ", EntryLevel.Warning, token: TestContext.Current.CancellationToken);
        Assert.Equal(2, rows.Count);
        Assert.True(rows[0].ShowHeader);
        Assert.False(rows[1].ShowHeader);
        Assert.Equal(@"G:\Games\GameOne.nsz", rows[0].Header);
        Assert.Equal(EntryLevel.Warning, rows[0].Entry.Level);
        Assert.Equal(EntryLevel.Error, rows[1].Entry.Level);
        Assert.Single(LogDocument.Filter(entries, "BROKEN DATA", EntryLevel.Error, token: TestContext.Current.CancellationToken));
        Assert.Single(LogDocument.Filter(entries, "+02:00", EntryLevel.Critical, token: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void MemoryFilterFindsOnlyMeasurementsAndCombinesWithSeverity()
    {
        var entries = LogDocument.Read(new StringReader(Sample), token: TestContext.Current.CancellationToken);
        var row = Assert.Single(LogDocument.Filter(entries, "", memoryOnly: true, token: TestContext.Current.CancellationToken));
        Assert.True(row.ShowHeader);
        Assert.Equal(EntryLevel.Trace, row.Entry.Level);
        Assert.Contains("workingSetBytes", row.Entry.Message);
        Assert.Empty(LogDocument.Filter(entries, "", EntryLevel.Warning, memoryOnly: true, token: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void RepeatedScansOfSameFileAndNewSessionsRemainSeparate()
    {
        var entries = LogDocument.Read(new StringReader(Sample + Sample), token: TestContext.Current.CancellationToken);
        var warnings = LogDocument.Filter(entries, "", EntryLevel.Warning, token: TestContext.Current.CancellationToken);
        Assert.Equal(6, warnings.Count);
        Assert.True(warnings[0].ShowHeader);
        Assert.True(warnings[2].ShowHeader);
        Assert.True(warnings[3].ShowHeader);
        Assert.Null(entries[8].FilePath);
    }

    [Theory]
    [InlineData("INFO", EntryLevel.Information)]
    [InlineData("WARN", EntryLevel.Warning)]
    [InlineData("FATAL", EntryLevel.Critical)]
    [InlineData("Debug", EntryLevel.Debug)]
    [InlineData("CUSTOM", EntryLevel.Unknown)]
    public void SupportsAliasesAndUnknownLevels(string label, EntryLevel expected)
    {
        var entry = Assert.Single(LogDocument.Read(new StringReader($"[2026-10-19 10:51:55.761 +02:00] [{label}] Test"), token: TestContext.Current.CancellationToken));
        Assert.Equal(expected, entry.Level);
    }

    [Fact]
    public void PlainTextIsRetainedAndCancellationIsObserved()
    {
        var rows = LogDocument.Read(new StringReader("Unstructured log\nsecond line"), token: TestContext.Current.CancellationToken);
        Assert.Equal(EntryLevel.Unknown, Assert.Single(rows).Level);
        Assert.Contains("second line", rows[0].Message);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => LogDocument.Read(new StringReader(Sample), cancellation.Token));
        Assert.Throws<OperationCanceledException>(() => LogDocument.Filter(rows, "", token: cancellation.Token));
        Assert.Empty(LogDocument.Read(new StringReader(""), token: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ReadsBomFileWhileWriterIsOpenWithoutChangingIt()
    {
        var path = Path.Combine(Path.GetTempPath(), "NxLogViewer-" + Guid.NewGuid() + ".log");
        try
        {
            File.WriteAllText(path, Sample, new UTF8Encoding(true));
            var before = File.ReadAllBytes(path);
            using (var writer = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
                Assert.Equal(8, LogDocument.ReadFile(path, token: TestContext.Current.CancellationToken).Count);
            Assert.Equal(before, File.ReadAllBytes(path));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void RetainsAllLargeLogEntriesForSearchBeyondMainAppLimit()
    {
        var text = string.Concat(Enumerable.Range(0, 10000).Select(index =>
            $"[2026-10-19 10:51:55.761 +02:00] [Trace] entry-{index}\n"));
        var entries = LogDocument.Read(new StringReader(text), token: TestContext.Current.CancellationToken);
        Assert.Equal(10000, entries.Count);
        Assert.Equal("entry-9999", Assert.Single(LogDocument.Filter(entries, "entry-9999", token: TestContext.Current.CancellationToken)).Entry.Message);
        Assert.Equal("entry-0", Assert.Single(LogDocument.Filter(entries, "entry-0", token: TestContext.Current.CancellationToken)).Entry.Message);
    }
}
