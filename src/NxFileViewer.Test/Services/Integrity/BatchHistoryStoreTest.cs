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
                new BatchPackageSummary("Update", "0100000000000800", "Update", "65536")
                {
                    BaseTitleId = "0100000000000000", SystemVersion = "19.0.0", MasterKey = "master_key_12",
                    MinimumApplicationVersion = "0", BuildId = "build",
                    IsDemo = false, FileSystemPermissions = "0xffffffffffffffff",
                    AcidSignatureValidity = "Valid", Permission = Emignatik.NxFileViewer.Services.Security.ProgramSecurityLevel.Dangerous,
                    Distribution = "Download", Publisher = "Publisher", DisplayVersion = "1.2.0", Languages = "German",
                    Titles = new[] { new BatchTitleSummary("German", "Deutscher Titel", "Verlag"), new BatchTitleSummary("English", "English title", "Publisher") }
                },
                new BatchPackageSummary("DLC", "0100000000001001", "AddOnContent", "0")
            },
            PackagesExpanded = true
        };
        var store = new BatchHistoryStore(Path.Combine(root, "history.json"));
        var entry = Entry(file) with { Results = new[] { result }, SourcePaths = new[] { root, file } };
        store.Save(entry);
        var restoredEntry = Assert.Single(store.Load());
        Assert.Equal(entry.SourcePaths, restoredEntry.SourcePaths);
        var restored = Assert.Single(restoredEntry.Results);
        Assert.Equal(result.Packages.Length, restored.Packages.Length);
        for (var i = 0; i < result.Packages.Length; i++)
        {
            Assert.Equal(result.Packages[i].Titles, restored.Packages[i].Titles);
            Assert.Equal(result.Packages[i] with { Titles = System.Array.Empty<BatchTitleSummary>() },
                restored.Packages[i] with { Titles = System.Array.Empty<BatchTitleSummary>() });
        }
        Assert.True(File.Exists(store.EntryPath(entry.Id)));
        Assert.False(File.Exists(Path.Combine(root, "history.json")));
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
    [Theory]
    [InlineData(1)]
    [InlineData(12)]
    public void ConfigurableLimitControlsPersistedRetentionAndKeepsLatestRun(int limit)
    {
        var file = Path.Combine(root, "history-source.nsp"); File.WriteAllText(file, "test");
        var path = Path.Combine(root, "history.json");
        var store = new BatchHistoryStore(path);
        BatchHistoryEntry? latest = null;
        for (var i = 0; i < 15; i++)
        {
            latest = Entry(file) with { UpdatedUtc = DateTime.UtcNow.AddMinutes(i) };
            store.Save(latest, limit);
        }
        var restored = new BatchHistoryStore(path).Load(limit);
        Assert.Equal(limit, restored.Count);
        Assert.Equal(latest!.Id, restored[0].Id);
        Assert.Equal(limit, Directory.GetFiles(store.EntriesDirectory, "*.zip").Length);
        store.Save(latest with { State = "Completed" }, limit);
        Assert.Equal(limit, store.Load(limit).Count);
        Assert.Equal("Completed", store.Load(limit)[0].State);
    }

    [Fact]
    public void HistoryLimitSettingsPersistAndOlderSettingsKeepDefault()
    {
        Assert.Equal(5, System.Text.Json.JsonSerializer.Deserialize<Emignatik.NxFileViewer.Settings.AppSettings>("{}")!.BatchHistoryLimit);
        var settings = new Emignatik.NxFileViewer.Settings.AppSettings { BatchHistoryLimit = 12 };
        Assert.Equal(12, System.Text.Json.JsonSerializer.Deserialize<Emignatik.NxFileViewer.Settings.AppSettings>(
            System.Text.Json.JsonSerializer.Serialize(settings))!.BatchHistoryLimit);
        settings.BatchHistoryLimit = 0; Assert.Equal(1, settings.BatchHistoryLimit);
        settings.BatchHistoryLimit = 1000; Assert.Equal(100, settings.BatchHistoryLimit);
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
