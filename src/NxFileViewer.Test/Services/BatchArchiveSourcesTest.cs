using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using Emignatik.NxFileViewer.Services.Integrity;
using Xunit;
namespace Emignatik.NxFileViewer.Test.Services;
public sealed class BatchArchiveSourcesTest : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "NxFileViewer-archives-" + Guid.NewGuid().ToString("N"));
    public BatchArchiveSourcesTest() => Directory.CreateDirectory(_root);
    private string Archive(params (string Name, string Text)[] entries)
    {
        var path = Path.Combine(_root, Guid.NewGuid().ToString("N") + ".zip");
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var entry in entries) { using var writer = new StreamWriter(zip.CreateEntry(entry.Name).Open()); writer.Write(entry.Text); }
        return path;
    }
    [Fact] public void MembersCannotEscapeCacheAndSameBasenamesStayDistinct()
    {
        var zip = Archive(("../../escape.nsp", "one"), ("folder/escape.nsp", "two"), ("skip.txt", "ignored"));
        var cache = Path.Combine(_root, "cache");
        var files = BatchArchiveSources.Enumerate(zip, false, true, cache, CancellationToken.None).ToArray();
        Assert.Equal(2, files.Length); Assert.NotEqual(files[0].Path, files[1].Path);
        Assert.All(files, file => { Assert.StartsWith(cache + Path.DirectorySeparatorChar, file.Path); Assert.Contains(" :: ", file.DisplayPath); });
        Assert.Equal(new[] { "one", "two" }, files.Select(file => File.ReadAllText(file.Path)));
        Assert.False(File.Exists(Path.Combine(_root, "escape.nsp")));
    }
    [Fact] public void ArchivesAreOptInAndCacheDoesNotBecomeAnotherSource()
    {
        var zip = Archive(("game.nsp", "source"));
        var cache = Path.Combine(_root, "cache");
        Assert.Empty(BatchArchiveSources.Enumerate(zip, false, false, cache, CancellationToken.None));
        var first = BatchArchiveSources.Enumerate(zip, false, true, cache, CancellationToken.None).Single();
        var second = BatchArchiveSources.Enumerate(zip, false, true, cache, CancellationToken.None).Single();
        Assert.Equal(first, second); Assert.Equal("source", File.ReadAllText(second.Path));
    }
    [Fact] public void CancellationDoesNotPublishPartialMember()
    {
        var zip = Archive(("game.nsp", "source")); using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var cache = Path.Combine(_root, "cache");
        Assert.Throws<OperationCanceledException>(() => BatchArchiveSources.Enumerate(zip, false, true, cache, cancellation.Token).ToArray());
        Assert.False(Directory.Exists(cache));
    }
    [Fact] public void RecursiveFolderChoiceHonorsSupportedTypes()
    {
        var folder = Path.Combine(_root, "nested"); Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "game.NSZ"), "content"); File.WriteAllText(Path.Combine(folder, "notes.txt"), "ignored");
        var cache = Path.Combine(_root, "cache");
        Assert.Empty(BatchArchiveSources.Enumerate(_root, false, false, cache, CancellationToken.None));
        Assert.Single(BatchArchiveSources.Enumerate(_root, true, false, cache, CancellationToken.None));
    }
    [Fact] public void RecursiveScanDoesNotReingestItsOwnCache()
    {
        Archive(("game.nsp", "source")); var cache = Path.Combine(_root, "cache");
        BatchArchiveSources.Enumerate(_root, true, true, cache, CancellationToken.None).ToArray();
        var files = BatchArchiveSources.Enumerate(_root, true, true, cache, CancellationToken.None).ToArray();
        Assert.Single(files); Assert.Contains(" :: ", files[0].DisplayPath);
    }
    public void Dispose() => Directory.Delete(_root, true);
}
