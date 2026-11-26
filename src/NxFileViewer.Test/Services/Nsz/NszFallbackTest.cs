using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using NxFileViewer.Avalonia;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services.Nsz;

public sealed class NszFallbackTest : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("nx-nsz-fallback-");

    [Theory]
    [InlineData(2, false, "process")]
    [InlineData(3, true, "missing")]
    [InlineData(2, false, "invalid")]
    public void BackupUsesCleanStagingAndOnlyMovesValidatedOutput(int mode, bool primaryNsz, string failure)
    {
        var output = _root.CreateSubdirectory("output").FullName;
        var source = Path.Combine(_root.FullName, "game.nsp");
        File.WriteAllText(source, "source");
        var calls = new List<bool>();
        var messages = new List<string>();
        MainWindow.RunCompressionAttempts(mode, output, (useNsz, stage) =>
        {
            Assert.Empty(Directory.GetFiles(stage));
            calls.Add(useNsz);
            if (calls.Count == 1)
            {
                if (failure == "process") { File.WriteAllText(Path.Combine(stage, "partial"), "incomplete"); throw new IOException("engine failed"); }
                if (failure == "missing") return;
            }
            File.WriteAllText(Path.Combine(stage, "game.nsz"), calls.Count == 1 ? "bad" : "validated");
        }, stage =>
        {
            if (File.ReadAllText(Path.Combine(stage, "game.nsz")) != "validated")
                throw new InvalidDataException("Content differs");
        }, _ => { }, (_, report) => messages.Add(report), TestContext.Current.CancellationToken);

        Assert.Equal(new[] { primaryNsz, !primaryNsz }, calls);
        Assert.Single(messages);
        Assert.Equal("validated", File.ReadAllText(Path.Combine(output, "game.nsz")));
        Assert.Equal("source", File.ReadAllText(source));
        Assert.Single(_root.GetDirectories());
    }

    [Fact]
    public void CancellationNeverStartsBackupOrMovesAnOutput()
    {
        var output = _root.CreateSubdirectory("output").FullName;
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var calls = 0;
        Assert.ThrowsAny<OperationCanceledException>(() =>
            MainWindow.RunCompressionAttempts(2, output, (_, stage) =>
            {
                calls++;
                File.WriteAllText(Path.Combine(stage, "game.nsz"), "partial");
                cancel.Cancel();
            }, null, _ => { }, (_, _) => Assert.Fail("Cancellation started a backup."), cancel.Token));
        Assert.Equal(1, calls);
        Assert.Empty(Directory.GetFiles(output));
        Assert.Single(_root.GetDirectories());
    }

    public void Dispose() => _root.Delete(recursive: true);
}
