using System;
using Emignatik.NxFileViewer.Services.BackgroundTask;
using Emignatik.NxFileViewer.Services.Updates;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services.Updates;

public sealed class OldVersionCleanupCommandTest
{
    [Fact]
    public void CommandQueriesNeverScanPluginDirectories()
    {
        var enabled = true;
        var scans = 0;
        var command = OldVersionCleanupCommand.Create("test", new MainBackgroundTaskRunnerService(),
            () => enabled, () => { scans++; throw new InvalidOperationException("Must not scan during navigation."); },
            _ => throw new InvalidOperationException(), () => { });
        for (var i = 0; i < 100; i++) Assert.True(command.CanExecute(null));
        enabled = false;
        Assert.False(command.CanExecute(null));
        Assert.Equal(0, scans);
    }
}