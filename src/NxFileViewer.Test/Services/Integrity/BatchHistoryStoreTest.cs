using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Emignatik.NxFileViewer.Models.Overview;
using Emignatik.NxFileViewer.Services.Integrity;
using Xunit;
namespace Emignatik.NxFileViewer.Test.Services.Integrity;
public sealed class BatchHistoryStoreTest : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "BatchHistoryTest-" + Guid.NewGuid().ToString("N"));
    public BatchHistoryStoreTest() => Directory.CreateDirectory(root);
    private BatchHistoryEntry Entry(string file, NcasIntegrity integrity = NcasIntegrity.Original) => new(Guid.NewGuid(), DateTime.UtcNow,
        root, true, true, true, true, "Interrupted", new[] { new BatchIntegrityResult(file,"NSP","NSP","Cdn","None",integrity,null) },
        new Dictionary<string,string> { [file] = BatchHistoryStore.Fingerprint(file)! });
    [Fact]
    public void PreservesContainedPackagesWithoutSavingExpandedState()
    {
        var file = Path.Combine(root, "multi.nsp"); File.WriteAllText(file, "test");
        var result = Entry(file).Results[0] with
        {
            Packages = new[]
            {
                new BatchPackageSummary("Base title", "0100000000000000", "Application", "0"),
                new BatchPackageSummary("Update", "0100000000000800", "Patch", "65536"),
                new BatchPackageSummary("DLC", "0100000000001001", "AddOnContent", "0")
            },
            PackagesExpanded = true
        };
        var store = new BatchHistoryStore(Path.Combine(root, "history.json"));
        store.Save(Entry(file) with { Results = new[] { result } });
        var restored = Assert.Single(Assert.Single(store.Load()).Results);
        Assert.Equal(result.Packages, restored.Packages);
        Assert.True(restored.HasMultiplePackages);
        Assert.False(restored.PackagesExpanded);
    }

    [Fact]
    public void OlderResultsWithoutPackageListRemainCompatible()
    {
        var result = System.Text.Json.JsonSerializer.Deserialize<BatchIntegrityResult>(
            "{\"FilePath\":\"a.nsp\",\"FileType\":\"NSP\",\"PackageType\":\"NSP\",\"Structure\":\"Cdn\",\"Compression\":\"None\",\"Integrity\":0}");
        Assert.NotNull(result);
        Assert.Empty(result.Packages);
        Assert.False(result.HasMultiplePackages);
    }

    [Fact]
    public void KeepsFiveMostRecentRunsAndUpdatesSameRunWithoutDuplication()
    {
        var file = Path.Combine(root,"a.nsp"); File.WriteAllText(file,"test");
        var store = new BatchHistoryStore(Path.Combine(root,"history.json"));
        BatchHistoryEntry? last = null;
        for (var i=0;i<7;i++) { last = Entry(file) with { UpdatedUtc = DateTime.UtcNow.AddSeconds(i) }; store.Save(last); }
        var loaded = new BatchHistoryStore(Path.Combine(root,"history.json")).Load();
        Assert.Equal(5, loaded.Count); Assert.Equal(last!.Id,loaded[0].Id);
        store.Save(last with { State="Completed" });
        Assert.Equal(5,store.Load().Count); Assert.Equal("Completed",store.Load()[0].State);
        Assert.Empty(Directory.GetFiles(root,"*.tmp"));
    }
    [Fact]
    public void ChangedMissingUncheckedOrChangedPolicyMustBeRechecked()
    {
        var file = Path.Combine(root,"a.nsp"); File.WriteAllText(file,"test");
        var saved=Entry(file);
        Assert.True(saved.CanSkip(file,true));
        Assert.False(saved.CanSkip(file,false));
        Assert.False(Entry(file,NcasIntegrity.Unchecked).CanSkip(file,true));
        Assert.False(Entry(file,NcasIntegrity.Error).CanSkip(file,true));
        File.AppendAllText(file,"changed"); Assert.False(saved.CanSkip(file,true));
        File.Delete(file); Assert.False(saved.CanSkip(file,true));
    }
    [Fact]
    public void FirmwareFolderChangesInvalidateSavedResult()
    {
        var folder=Path.Combine(root,"fw"); Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder,"a.nca"),"test");
        var saved=Entry(folder); Assert.True(saved.CanSkip(folder,true));
        File.WriteAllText(Path.Combine(folder,"b.nca"),"test"); Assert.False(saved.CanSkip(folder,true));
    }
    public void Dispose() => Directory.Delete(root,true);
}
