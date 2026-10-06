using System;
using System.IO;
using System.Threading;
using Emignatik.NxFileViewer.Services.Nsz;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services.Nsz;

public sealed class NszProcessTest
{
    private static string PowerShell => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
        "WindowsPowerShell", "v1.0", "powershell.exe");

    [Fact]
    public void LargeStdoutAndStderrAreDrainedWithoutDeadlockAndRetentionIsBounded()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        var result = NszProcess.Run(PowerShell, new[] { "-NoProfile", "-NonInteractive", "-Command",
            "[Console]::Out.Write(('x' * 200000)); [Console]::Error.Write(('y' * 200000))" },
            Path.GetTempPath(), timeout.Token);
        Assert.Equal(65536, result.Length);
    }

    [Fact]
    public void NonzeroExitIsReportedWithoutPublishingConsoleContents()
    {
        var exception = Assert.Throws<IOException>(() => NszProcess.Run(PowerShell,
            new[] { "-NoProfile", "-NonInteractive", "-Command", "[Console]::Error.Write('private content'); exit 7" },
            Path.GetTempPath(), TestContext.Current.CancellationToken));
        Assert.Contains("7", exception.Message);
        Assert.DoesNotContain("private content", exception.Message);
    }

    [Fact]
    public void PythonBootloaderFailureIsExplainedWithoutPublishingKeysOrPaths()
    {
        var exception = Assert.Throws<NszRuntimeStartupException>(() => NszProcess.Run(PowerShell,
            new[] { "-NoProfile", "-NonInteractive", "-Command",
                "[Console]::Error.WriteLine('[PYI-123:ERROR] Failed to load Python DLL private-path/python311.dll'); [Console]::Error.WriteLine('master_key_00 = 0123456789abcdef0123456789abcdef'); exit -1" },
            Path.GetTempPath(), TestContext.Current.CancellationToken));
        Assert.Contains("-1", exception.Message);
        Assert.Contains(Emignatik.NxFileViewer.Localization.LocalizationManager.Instance.Current.Keys.Nsz_PythonRuntimeFailed, exception.Message);
        Assert.DoesNotContain("private-path", exception.Message);
        Assert.DoesNotContain("0123456789abcdef0123456789abcdef", exception.Message);
    }

    [Fact]
    public void ChildProcessTempIsInsideApplicationAndRemovedAfterExit()
    {
        var path = NszProcess.Run(PowerShell, new[] { "-NoProfile", "-NonInteractive", "-Command", "[Console]::Write($env:TEMP)" },
            Path.GetTempPath(), TestContext.Current.CancellationToken);
        Assert.StartsWith(Path.Combine(AppContext.BaseDirectory, "Temp", "NSZ") + Path.DirectorySeparatorChar, path);
        Assert.False(Directory.Exists(path));
    }

    [Fact]
    public void CancellationStopsRunningProcess()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(1));
        Assert.Throws<OperationCanceledException>(() => NszProcess.Run(PowerShell,
            new[] { "-NoProfile", "-NonInteractive", "-Command", "[System.Threading.Thread]::Sleep(60000)" },
            Path.GetTempPath(), timeout.Token));
    }
}
