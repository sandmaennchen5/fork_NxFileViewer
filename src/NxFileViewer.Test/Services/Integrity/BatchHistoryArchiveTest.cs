using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Emignatik.NxFileViewer.Models.Overview;
using Emignatik.NxFileViewer.Services.Integrity;
using Emignatik.NxFileViewer.Views.UserControls;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services.Integrity;

public sealed class BatchHistoryArchiveTest : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "HistoryArchive-" + Guid.NewGuid().ToString("N"));
    public BatchHistoryArchiveTest() => Directory.CreateDirectory(_root);
    private string LegacyPath => Path.Combine(_root, "history.json");
    private BatchHistoryEntry Entry() => new(Guid.NewGuid(), DateTime.UtcNow, _root, true, true, false, false,
        "Completed", new[] { new BatchIntegrityResult(Path.Combine(_root, "unavailable.nsp"), "NSP", "NSP", "Cdn", "None", NcasIntegrity.Unchecked, null) },
        new Dictionary<string, string>());

    [Fact]
    public void EachScanIsOneArchiveAndUpdatingItPreservesDeduplicatedOfflineLogos()
    {
        var store = new BatchHistoryStore(LegacyPath);
        var entry = Entry();
        var bytes = new byte[] { 1, 2, 3, 4 };
        var icon = store.StageIcon(entry.Id, bytes);
        Assert.Equal(icon, store.StageIcon(entry.Id, bytes));
        var result = entry.Results[0] with
        {
            Packages = new[]
            {
                new BatchPackageSummary("Base", "0100000000000000", "Base", "0")
                {
                    PatchLevel = "Patch 0", SecurityDetails = "stored security",
                    Titles = new[] { new BatchTitleSummary("German", "Titel", "Verlag") { Icon = icon },
                        new BatchTitleSummary("AmericanEnglish", "Title", "Publisher") { Icon = icon } }
                }
            }
        };
        entry = entry with { Results = new[] { result } };
        store.Save(entry);
        // A completely new store must work while the source does not exist.
        store = new BatchHistoryStore(LegacyPath);
        var restored = Assert.Single(store.Load());
        var package = Assert.Single(Assert.Single(restored.Results).Packages);
        Assert.Equal(2, package.Titles.Length);
        Assert.Equal(bytes, store.ReadIcon(package.Titles[1].Icon!));
        Assert.Equal("stored security", package.SecurityDetails);
        Assert.Equal("Patch 0", package.PatchLevel);
        store.Save(restored with { State = "Interrupted" });
        using (var zip = ZipFile.OpenRead(store.EntryPath(entry.Id)))
        {
            Assert.Single(zip.Entries, e => e.FullName.StartsWith("icons/"));
            Assert.NotNull(zip.GetEntry("scan.json"));
            Assert.Null(zip.GetEntry("overview.json"));
            Assert.Single(zip.Entries, e => e.FullName.StartsWith("roms/"));
        }
        Assert.Single(Directory.GetFiles(store.EntriesDirectory, "*.zip"));
        Assert.Empty(Directory.GetFiles(store.EntriesDirectory, "*.tmp"));
        Assert.Equal(bytes, new BatchHistoryStore(LegacyPath).ReadIcon(icon!));
    }

    [Fact]
    public void LegacyArrayMigratesWithoutLosingEntriesAndIsBackedUp()
    {
        var first = Entry();
        var second = Entry() with { UpdatedUtc = DateTime.UtcNow.AddSeconds(1) };
        File.WriteAllText(LegacyPath, JsonSerializer.Serialize(new[] { first, second }));
        var store = new BatchHistoryStore(LegacyPath);
        Assert.Equal(2, store.Load().Count);
        store.Save(second with { State = "Interrupted" });
        Assert.False(File.Exists(LegacyPath));
        Assert.Single(Directory.GetFiles(_root, "*.legacy"));
        Assert.Equal(2, Directory.GetFiles(store.EntriesDirectory, "*.zip").Length);
        var restored = new BatchHistoryStore(LegacyPath).Load();
        Assert.Equal(2, restored.Count);
        Assert.Equal("Interrupted", restored.First(e => e.Id == second.Id).State);
        Assert.Contains(restored, e => e.Id == first.Id);
    }

    [Fact]
    public void CorruptArchiveDoesNotHideOtherRunsAndMissingLogoIsOptional()
    {
        var store = new BatchHistoryStore(LegacyPath);
        var entry = Entry();
        store.Save(entry);
        File.WriteAllText(store.EntryPath(Guid.NewGuid()), "broken archive");
        Assert.Equal(entry.Id, Assert.Single(new BatchHistoryStore(LegacyPath).Load()).Id);
        Assert.Null(store.ReadIcon(new BatchIconReference(entry.Id, new string('a', 64))));
        Assert.Null(store.ReadIcon(new BatchIconReference(entry.Id, "../overview.json")));
        Assert.Null(store.ReadIcon(new BatchIconReference(Guid.NewGuid(), new string('a', 64))));
    }

    [Fact]
    public void RetentionRemovesTheWholeOldScanIncludingItsLogos()
    {
        var store = new BatchHistoryStore(LegacyPath);
        var old = Entry();
        var icon = store.StageIcon(old.Id, new byte[] { 1 });
        store.Save(old, 1);
        var latest = Entry() with { UpdatedUtc = DateTime.UtcNow.AddMinutes(1) };
        store.Save(latest, 1);
        Assert.False(File.Exists(store.EntryPath(old.Id)));
        Assert.Null(store.ReadIcon(icon!));
        Assert.Equal(latest.Id, Assert.Single(store.Load()).Id);
        Assert.Single(Directory.GetFiles(store.EntriesDirectory, "*.zip"));
    }

    [Fact]
    public void StoredTitleDecodesOnlyOnDemandAndWorksFromArchiveWithoutSource()
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                var store = new BatchHistoryStore(LegacyPath);
                var entry = Entry();
                var bitmap = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { 0, 0, 255, 255 }, 4);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var output = new MemoryStream();
                encoder.Save(output);
                var reference = store.StageIcon(entry.Id, output.ToArray());
                store.Save(entry);
                var reopened = new BatchHistoryStore(LegacyPath);
                var reads = 0;
                var title = new StoredTitleInfoViewModel(new BatchTitleSummary("German", "Titel", "Verlag") { Icon = reference },
                    icon => { reads++; return reopened.ReadIcon(icon); });
                Assert.Equal(0, reads);
                Assert.True(title.HasIcon);
                Assert.Equal(0, reads);
                Assert.Equal(1, title.Icon!.PixelWidth);
                Assert.True(title.Icon.IsFrozen);
                Assert.Equal(1, reads);
                Assert.Equal("Titel", title.LoadTitle().AppName);
                title.ReleaseImage();
                Assert.NotNull(title.Icon);
                Assert.Equal(3, reads); // decoding, explicit export title, decoding after release
            }
            catch (Exception ex) { error = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
    }

    [Fact]
    public void FailedCheckpointPreservesPreviousArchiveAndCanBeRetried()
    {
        var store = new BatchHistoryStore(LegacyPath);
        var entry = Entry();
        store.Save(entry);
        var icon = store.StageIcon(entry.Id, new byte[] { 7, 8, 9 });
        using (var locked = new FileStream(store.EntryPath(entry.Id), FileMode.Open, FileAccess.Read, FileShare.None))
            Assert.ThrowsAny<IOException>(() => store.Save(entry with { State = "Interrupted" }));
        Assert.Equal("Completed", Assert.Single(new BatchHistoryStore(LegacyPath).Load()).State);
        Assert.Empty(Directory.GetFiles(store.EntriesDirectory, "*.tmp"));
        store.Save(entry with { State = "Interrupted" });
        Assert.Equal("Interrupted", Assert.Single(store.Load()).State);
        Assert.Equal(new byte[] { 7, 8, 9 }, new BatchHistoryStore(LegacyPath).ReadIcon(icon!));
    }
    [Fact]
    public void RomMetadataIsSeparateAndLogosAreSharedAcrossRoms()
    {
        var store = new BatchHistoryStore(LegacyPath);
        var entry = Entry() with { UpdatedUtc = new DateTime(2026, 10, 19, 12, 34, 0, DateTimeKind.Utc) };
        var icon = store.StageIcon(entry.Id, new byte[] { 1, 2 });
        var first = entry.Results[0] with { Title = "First", Titles = new[] { new BatchTitleSummary("German", "Erster", "Verlag") { Icon = icon } } };
        var second = first with { FilePath = Path.Combine(_root, "other.nsz"), Title = "Second" };
        entry = entry with { Results = new[] { first, second } };
        store.Save(entry);
        var savedPath = store.EntryPath(entry.Id);
        Assert.Equal(_root, Path.GetDirectoryName(savedPath));
        Assert.Equal(entry.UpdatedUtc.ToLocalTime().ToString("yyyy-MM-dd_HHmm") + "_" + entry.Id.ToString("N") + ".zip", Path.GetFileName(savedPath));
        using (var zip = ZipFile.OpenRead(savedPath))
        {
            Assert.Equal(2, zip.Entries.Count(e => e.FullName.StartsWith("roms/")));
            Assert.Single(zip.Entries, e => e.FullName.StartsWith("icons/"));
            using var input = zip.GetEntry("scan.json")!.Open();
            using var json = JsonDocument.Parse(input);
            Assert.Equal(0, json.RootElement.GetProperty("Scan").GetProperty("Results").GetArrayLength());
        }
        Assert.Equal("Second", new BatchHistoryStore(LegacyPath).ReadResult(entry.Id, second.FilePath)!.Title);
        store.Save(entry with { UpdatedUtc = entry.UpdatedUtc.AddDays(1), Results = new[] { second } });
        Assert.Equal(savedPath, store.EntryPath(entry.Id));
        Assert.Single(Directory.GetFiles(_root, "*.zip"));
        Assert.Null(store.ReadResult(entry.Id, first.FilePath));
        Assert.Equal("Second", Assert.Single(Assert.Single(store.Load()).Results).Title);
    }

    [Fact]
    public void OldPerScanArchiveMovesToHistoryRootWithLogosPreserved()
    {
        var entry = Entry();
        var store = new BatchHistoryStore(LegacyPath);
        var oldDirectory = Path.Combine(_root, "history-entries");
        Directory.CreateDirectory(oldDirectory);
        var oldPath = Path.Combine(oldDirectory, entry.Id.ToString("N") + ".zip");
        var bytes = new byte[] { 4, 5, 6 };
        var key = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();
        var reference = new BatchIconReference(entry.Id, key);
        entry = entry with { Results = new[] { entry.Results[0] with { Titles = new[] { new BatchTitleSummary("German", "Alt", "Verlag") { Icon = reference } } } } };
        using (var zip = ZipFile.Open(oldPath, ZipArchiveMode.Create))
        {
            using (var output = zip.CreateEntry("overview.json").Open()) JsonSerializer.Serialize(output, entry);
            using (var output = zip.CreateEntry("icons/" + key).Open()) output.Write(bytes);
        }
        Assert.Equal(entry.Id, Assert.Single(store.Load()).Id);
        Assert.Equal(bytes, store.ReadIcon(reference));
        var next = Entry() with { UpdatedUtc = entry.UpdatedUtc.AddSeconds(1) };
        store.Save(next);
        Assert.False(File.Exists(oldPath));
        Assert.Equal(2, Directory.GetFiles(_root, "*.zip").Length);
        Assert.Equal(_root, Path.GetDirectoryName(store.EntryPath(entry.Id)));
        Assert.Equal(bytes, new BatchHistoryStore(LegacyPath).ReadIcon(reference));
        Assert.Equal("Alt", store.ReadResult(entry.Id, entry.Results[0].FilePath)!.Titles[0].Title);
    }

    [Fact]
    public void ReadingOneRomDoesNotDeserializeOtherRomMetadata()
    {
        var store = new BatchHistoryStore(LegacyPath);
        var entry = Entry();
        var first = entry.Results[0] with { Title = "Available" };
        var second = first with { FilePath = Path.Combine(_root, "other.nsp"), Title = "Corrupt me" };
        store.Save(entry with { Results = new[] { first, second } });
        using (var zip = ZipFile.Open(store.EntryPath(entry.Id), ZipArchiveMode.Update))
        {
            var metadata = zip.Entries.Last(e => e.FullName.StartsWith("roms/"));
            var name = metadata.FullName;
            metadata.Delete();
            using var text = new StreamWriter(zip.CreateEntry(name).Open());
            text.Write("invalid JSON");
        }
        Assert.Equal("Available", store.ReadResult(entry.Id, first.FilePath)!.Title);
    }
    [Fact]
    public void ExcludingLogosRemovesImagesAndReferencesFromSavedScanOnly()
    {
        var store = new BatchHistoryStore(LegacyPath);
        var entry = Entry();
        var icon = store.StageIcon(entry.Id, new byte[] { 1, 2, 3 });
        var title = new BatchTitleSummary("German", "Titel", "Verlag") { Icon = icon };
        entry = entry with { Results = new[] { entry.Results[0] with
        {
            Titles = new[] { title },
            Packages = new[] { new BatchPackageSummary("Titel", "123", "Base", "0") { Titles = new[] { title } } }
        } } };
        store.Save(entry);
        Assert.NotNull(store.ReadIcon(icon!));
        store.Save(entry, includeLogos: false);
        var restored = Assert.Single(store.Load());
        Assert.Null(restored.Results[0].Titles[0].Icon);
        Assert.Null(restored.Results[0].Packages[0].Titles[0].Icon);
        Assert.Equal("Titel", restored.Results[0].Titles[0].Title);
        Assert.NotNull(entry.Results[0].Titles[0].Icon); // Saving does not mutate the caller.
        Assert.Null(store.ReadIcon(icon!));
        using var zip = ZipFile.OpenRead(store.EntryPath(entry.Id));
        Assert.DoesNotContain(zip.Entries, e => e.FullName.StartsWith("icons/"));
    }

    [Fact]
    public void LogoPreferenceDefaultsOffAndPersistsWhenEnabled()
    {
        var settings = JsonSerializer.Deserialize<Emignatik.NxFileViewer.Settings.AppSettings>("{}")!;
        Assert.False(settings.SaveBatchHistoryLogos);
        settings.SaveBatchHistoryLogos = true;
        Assert.True(JsonSerializer.Deserialize<Emignatik.NxFileViewer.Settings.AppSettings>(JsonSerializer.Serialize(settings))!.SaveBatchHistoryLogos);
    }
    public void Dispose() => Directory.Delete(_root, true);
}