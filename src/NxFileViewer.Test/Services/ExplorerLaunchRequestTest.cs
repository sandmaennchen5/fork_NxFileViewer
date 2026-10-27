using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Emignatik.NxFileViewer.Services.FileAssociations;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services;

public sealed class ExplorerLaunchRequestTest
{
    [Theory]
    [InlineData("--file-open", ExplorerLaunchMode.FileOpen)]
    [InlineData("--file-verify", ExplorerLaunchMode.FileVerify)]
    [InlineData("--batch-open", ExplorerLaunchMode.BatchOpen)]
    [InlineData("--batch-verify", ExplorerLaunchMode.BatchVerify)]
    [InlineData("--compress", ExplorerLaunchMode.Compress)]
    public void ExplicitActionsPreserveTheRequestedMode(string option, ExplorerLaunchMode mode)
    {
        var request = ExplorerLaunchRequest.Parse(new[] { option, "--", @"C:\Games with spaces\Übung.nsp" });
        Assert.Equal(mode, request.Mode);
        Assert.Equal(@"C:\Games with spaces\Übung.nsp", Assert.Single(request.Paths));
    }

    [Fact]
    public void FileActionsRejectMultipleFilesAndFolders()
    {
        Assert.Throws<ArgumentException>(() => ExplorerLaunchRequest.Parse(new[] { "--file-open", @"C:\a.nsp", @"C:\b.nsp" }));
        Assert.Throws<ArgumentException>(() => ExplorerLaunchRequest.Parse(new[] { "--file-verify", Path.GetTempPath() }));
        Assert.Throws<ArgumentException>(() => ExplorerLaunchRequest.Parse(new[] { "--batch-open" }));
        var batch = ExplorerLaunchRequest.Parse(new[] { "--batch-open", Path.GetTempPath(), @"C:\a.nsp", @"c:\A.nsp" });
        Assert.Equal(2, batch.Paths.Length);
    }

    [Theory]
    [InlineData("--compress", "a.NSP", "b.xci", ExplorerLaunchMode.Compress)]
    [InlineData("--decompress", "a.NSZ", "b.xcz", ExplorerLaunchMode.Decompress)]
    public void ConversionAcceptsMatchingSelectionsAndNativeRequests(string option, string first, string second, ExplorerLaunchMode mode)
    {
        var paths = new[] { Path.GetFullPath(first), Path.GetFullPath(second) };
        Assert.Equal(paths, ExplorerLaunchRequest.Parse(new[] { option, paths[0], paths[1] }).Paths);
        Directory.CreateDirectory(ExplorerLaunchRequest.RequestDirectory);
        var selection = Path.Combine(ExplorerLaunchRequest.RequestDirectory, Guid.NewGuid().ToString("N") + ".selection");
        File.WriteAllText(selection, "NXFV1\0" + option[2..] + "\0" + string.Join('\0', paths) + '\0', new UnicodeEncoding(false, false));
        var request = ExplorerLaunchRequest.Parse(new[] { "--explorer-request", selection });
        Assert.Equal(mode, request.Mode);
        Assert.Equal(paths, request.Paths);
        Assert.False(File.Exists(selection));
    }

    [Theory]
    [InlineData("--compress", "a.nsz")]
    [InlineData("--decompress", "a.nsp")]
    [InlineData("--compress", "a.zip")]
    [InlineData("--decompress", "a.nro")]
    public void ConversionRejectsUnsupportedOrMixedSelections(string option, string invalid)
    {
        Assert.Throws<ArgumentException>(() => ExplorerLaunchRequest.Parse(new[] { option, invalid }));
        Assert.Throws<ArgumentException>(() => ExplorerLaunchRequest.Parse(new[] { option, Path.GetTempPath() }));
        Assert.Throws<ArgumentException>(() => ExplorerLaunchRequest.Parse(new[] { option }));
        Assert.Throws<ArgumentException>(() => ExplorerLaunchRequest.Parse(new[] { option, "a.nsp", "b.nsz" }));
    }

    [Theory]
    [InlineData("convert-nsp", ExplorerLaunchMode.ConvertNsp, "Convert", "nsp")]
    [InlineData("convert-xci", ExplorerLaunchMode.ConvertXci, "Convert", "xci")]
    [InlineData("merge-nsp", ExplorerLaunchMode.MergeNsp, "Merge", "nsp")]
    [InlineData("merge-xci", ExplorerLaunchMode.MergeXci, "Merge", "xci")]
    [InlineData("split-nsp", ExplorerLaunchMode.SplitNsp, "Split", "nsp")]
    [InlineData("split-xci", ExplorerLaunchMode.SplitXci, "Split", "xci")]
    public void PackageActionsSupportDirectAndNativeSelection(string action, ExplorerLaunchMode mode, string expectedOperation, string expectedType)
    {
        var paths = new[] { Path.GetFullPath("game.nsp"), Path.GetFullPath("patch.xci") };
        var request = ExplorerLaunchRequest.Parse(new[] { "--" + action, "--" }.Concat(paths).ToArray());
        Assert.Equal(mode, request.Mode);
        Assert.True(ExplorerLaunchRequest.TryGetPackageOperation(mode, out var operation, out var type));
        Assert.Equal(expectedOperation, operation.ToString()); Assert.Equal(expectedType, type);
        Assert.Equal(paths, request.Paths);
        Assert.Throws<ArgumentException>(() => ExplorerLaunchRequest.Create(mode, new[] { "compressed.nsz", "patch.xci" }));
        Assert.Throws<ArgumentException>(() => ExplorerLaunchRequest.Create(mode, new[] { Path.GetTempPath(), "patch.xci" }));
        Assert.Throws<ArgumentException>(() => ExplorerLaunchRequest.Create(mode, Array.Empty<string>()));
        if (expectedOperation == "Merge")
            Assert.Throws<ArgumentException>(() => ExplorerLaunchRequest.Create(mode, new[] { paths[0], paths[0] }));
        else Assert.Single(ExplorerLaunchRequest.Create(mode, new[] { paths[0] }).Paths);
        Directory.CreateDirectory(ExplorerLaunchRequest.RequestDirectory);
        var selection = Path.Combine(ExplorerLaunchRequest.RequestDirectory, Guid.NewGuid().ToString("N") + ".selection");
        File.WriteAllText(selection, "NXFV1\0" + action + "\0" + string.Join('\0', paths) + '\0', new UnicodeEncoding(false, false));
        var native = ExplorerLaunchRequest.Parse(new[] { "--explorer-request", selection });
        Assert.Equal(mode, native.Mode); Assert.Equal(paths, native.Paths);
        Assert.False(File.Exists(selection));
    }
    [Fact]
    public void DefaultArgumentsRetainAllFiles()
    {
        var request = ExplorerLaunchRequest.Parse(new[] { @"C:\a.nsp", @"C:\b.nsp" });
        Assert.Equal(ExplorerLaunchMode.Automatic, request.Mode);
        Assert.Equal(2, request.Paths.Length);
    }

    [Fact]
    public void NativeSelectionFormatSupportsLongListsAndDeletesOnlyItsOwnResponseFile()
    {
        Directory.CreateDirectory(ExplorerLaunchRequest.RequestDirectory);
        var selectionFile = Path.Combine(ExplorerLaunchRequest.RequestDirectory, Guid.NewGuid().ToString("N") + ".selection");
        var paths = Enumerable.Range(0, 2000).Select(i => @"C:\Games with spaces\Übung " + i + ".nsp").ToArray();
        File.WriteAllText(selectionFile, "NXFV1\0batch-verify\0" + string.Join('\0', paths) + '\0', new UnicodeEncoding(false, false));
        var request = ExplorerLaunchRequest.Parse(new[] { "--explorer-request", selectionFile });
        Assert.Equal(ExplorerLaunchMode.BatchVerify, request.Mode);
        Assert.Equal(paths, request.Paths);
        Assert.False(File.Exists(selectionFile));
        var unrelated = Path.GetTempFileName();
        try {
            Assert.Throws<ArgumentException>(() => ExplorerLaunchRequest.Parse(new[] { "--explorer-request", unrelated }));
            Assert.True(File.Exists(unrelated));
        } finally { File.Delete(unrelated); }
    }

    [Fact]
    public void MalformedSelectionIsRejectedAndConsumed()
    {
        Directory.CreateDirectory(ExplorerLaunchRequest.RequestDirectory);
        var selectionFile = Path.Combine(ExplorerLaunchRequest.RequestDirectory, Guid.NewGuid().ToString("N") + ".selection");
        File.WriteAllText(selectionFile, "NXFV1\0unknown\0C:\\a.nsp\0", new UnicodeEncoding(false, false));
        Assert.Throws<InvalidDataException>(() => ExplorerLaunchRequest.Parse(new[] { "--explorer-request", selectionFile }));
        Assert.False(File.Exists(selectionFile));
    }

    [Fact]
    public async Task SecondaryInstanceForwardsTheEntireSelectionBeforeUiStartup()
    {
        var executable = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "NxFileViewer.exe");
        var received = new TaskCompletionSource<ExplorerLaunchRequest>(TaskCreationOptions.RunContinuationsAsynchronously);
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var stop = new ManualResetEventSlim();
        // A mutex must be released by its owning thread; keep the primary on a dedicated thread.
        var thread = new Thread(() => {
            try {
                using var primary = new ExplorerInstance(executable);
                Assert.True(primary.IsPrimary);
                primary.Listen(request => received.TrySetResult(request), error => received.TrySetException(error));
                ready.TrySetResult();
                stop.Wait();
            } catch (Exception ex) { ready.TrySetException(ex); received.TrySetException(ex); }
            finally { stopped.TrySetResult(); }
        }) { IsBackground = true };
        thread.Start();
        try {
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            using var secondary = new ExplorerInstance(executable);
            Assert.False(secondary.IsPrimary);
            var original = ExplorerLaunchRequest.Parse(new[] { "--batch-verify", @"C:\a.nsp", @"C:\b.nsp" });
            await secondary.Forward(original);
            var actual = await received.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.Equal(original.Mode, actual.Mode);
            Assert.Equal(original.Paths, actual.Paths);
        }
        finally { stop.Set(); await stopped.Task.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None); }
    }

    [Fact]
    public void ClassicRegistrationDoesNotChangeFileAssociationsOrUserChoice()
    {
        var entries = ExplorerIntegration.ClassicEntries(@"C:\Apps with spaces\NxFileViewer.exe", @"C:\User\extension.dll");
        Assert.All(entries, e => Assert.StartsWith(@"Software\", e.Key));
        Assert.Equal(2, entries.Count(e => e.Name == "ExplorerCommandHandler" && e.Value == ExplorerIntegration.ClassId));
        Assert.Equal(2, entries.Count(e => e.Name == "MultiSelectModel" && e.Value == "Player"));
        Assert.Equal(2, entries.Count(e => e.Name == "MUIVerb" && e.Value == "NxFileViewer Fork"));
        var german = ExplorerIntegration.ClassicEntries(@"C:\NxFileViewer.exe", @"C:\extension.dll", "de-DE");
        Assert.Contains(german, e => e.Key == ExplorerIntegration.ConfigurationKey && e.Name == "Language" && e.Value == "de-DE");
        Assert.DoesNotContain(entries, e => e.Key.Contains("UserChoice") || e.Key.Contains("OpenWith") || e.Key.Contains("\\.nsp"));
        Assert.Contains(entries, e => e.Key.EndsWith("\\InprocServer32") && e.Name == "" && e.Value == @"C:\User\extension.dll");
    }
}
