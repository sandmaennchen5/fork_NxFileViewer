using System;
using System.IO;
using Emignatik.NxFileViewer.Services.Integrity;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services.Integrity;

public sealed class BatchHistoryStoreTest : IDisposable
{
    private readonly string root = Directory.CreateTempSubdirectory("nx-history-").FullName;
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
    public void FirmwareFolderChangesInvalidateSavedResult()
    {
        File.WriteAllText(Path.Combine(root, "a.nca"), "first");
        var saved = BatchSourceFingerprint.Read(root);
        Assert.NotNull(saved);
        Assert.Equal(saved, BatchSourceFingerprint.Read(root));
        File.WriteAllText(Path.Combine(root, "b.nca"), "second");
        Assert.NotEqual(saved, BatchSourceFingerprint.Read(root));
    }

    public void Dispose() => Directory.Delete(root, true);
}
