using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;
using Emignatik.NxFileViewer.Services.PackageOperations;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services;

public sealed class PackageOutputPublisherTest : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "PackagePublish-" + Guid.NewGuid().ToString("N"));
    private string Ready => Path.Combine(_root, "ready");
    private string Output => Path.Combine(_root, "published");
    public PackageOutputPublisherTest()
    {
        Directory.CreateDirectory(Ready);
        File.WriteAllText(Path.Combine(Ready, "base.nsp"), "validated base");
        Directory.CreateDirectory(Path.Combine(Ready, "dlc"));
        File.WriteAllText(Path.Combine(Ready, "dlc", "addon.nsp"), "validated dlc");
    }

    [Fact]
    public void PublishesFilesWithoutRenamingOpenSourceDirectory()
    {
        // Keep the source directory open as a sync client/indexer would.
        using var handle = CreateFile(Ready, 0x80, 3, IntPtr.Zero, 3, 0x02000000, IntPtr.Zero);
        Assert.False(handle.IsInvalid);
        PackageOutputPublisher.Publish(Ready, Output, TestContext.Current.CancellationToken);
        Assert.Equal("validated base", File.ReadAllText(Path.Combine(Output, "base.nsp")));
        Assert.Equal("validated dlc", File.ReadAllText(Path.Combine(Output, "dlc", "addon.nsp")));
        Assert.False(File.Exists(Path.Combine(Output, "NXFV-INCOMPLETE.txt")));
        Assert.True(Directory.Exists(Ready)); // the directory itself was never renamed
        Assert.Empty(Directory.GetFiles(Ready, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public void TransientAccessDenialIsRetried()
    {
        var denials = 0;
        PackageOutputPublisher.Publish(Ready, Output, TestContext.Current.CancellationToken, (source, target) =>
        {
            if (denials++ < 2) throw new UnauthorizedAccessException("Temporary sharing conflict");
            File.Move(source, target);
        });
        Assert.Equal(4, denials); // two transient failures, two successful file moves
        Assert.False(File.Exists(Path.Combine(Output, "NXFV-INCOMPLETE.txt")));
    }

    [Fact]
    public void PermanentFailureRetainsAllValidatedFilesAndMarksPartialOutput()
    {
        var moved = 0;
        Assert.Throws<UnauthorizedAccessException>(() => PackageOutputPublisher.Publish(Ready, Output,
            TestContext.Current.CancellationToken, (source, target) =>
            {
                if (moved != 0) throw new UnauthorizedAccessException("Persistent lock");
                File.Move(source, target); moved++;
            }));
        Assert.True(File.Exists(Path.Combine(Output, "NXFV-INCOMPLETE.txt")));
        Assert.Single(Directory.GetFiles(Ready, "*.nsp", SearchOption.AllDirectories));
        Assert.Single(Directory.GetFiles(Output, "*.nsp", SearchOption.AllDirectories));
        Assert.Contains(Ready, File.ReadAllText(Path.Combine(Output, "NXFV-INCOMPLETE.txt")));
    }

    [Fact]
    public void CancellationDuringPublicationKeepsValidatedFiles()
    {
        using var cancellation = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => PackageOutputPublisher.Publish(Ready, Output,
            cancellation.Token, (source, target) => { File.Move(source, target); cancellation.Cancel(); }));
        Assert.True(File.Exists(Path.Combine(Output, "NXFV-INCOMPLETE.txt")));
        Assert.Single(Directory.GetFiles(Ready, "*.nsp", SearchOption.AllDirectories));
        Assert.Single(Directory.GetFiles(Output, "*.nsp", SearchOption.AllDirectories));
    }

    [Fact]
    public void OnlySelectedValidatedPackagesArePublished()
    {
        File.WriteAllText(Path.Combine(Ready, "temporary.nca"), "scratch data");
        var selected = Directory.GetFiles(Ready, "*.nsp", SearchOption.AllDirectories);
        PackageOutputPublisher.Publish(Ready, Output, TestContext.Current.CancellationToken, files: selected);
        Assert.True(File.Exists(Path.Combine(Ready, "temporary.nca")));
        Assert.False(File.Exists(Path.Combine(Output, "temporary.nca")));
        Assert.Equal(2, Directory.GetFiles(Output, "*.nsp", SearchOption.AllDirectories).Length);
    }

    [Fact]
    public void ExistingDestinationIsNeverOverwritten()
    {
        Directory.CreateDirectory(Output);
        File.WriteAllText(Path.Combine(Output, "base.nsp"), "existing");
        Assert.Throws<IOException>(() => PackageOutputPublisher.Publish(Ready, Output, TestContext.Current.CancellationToken));
        Assert.Equal("existing", File.ReadAllText(Path.Combine(Output, "base.nsp")));
        Assert.Equal(2, Directory.GetFiles(Ready, "*.nsp", SearchOption.AllDirectories).Length);
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr security,
        uint disposition, uint flags, IntPtr template);

    public void Dispose() => Directory.Delete(_root, true);
}
