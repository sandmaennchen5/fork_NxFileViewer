using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using Emignatik.NxFileViewer.Services.Integrity;
namespace NxFileViewer.Avalonia;
public partial class MainWindow
{
    private sealed record BatchSource(string Path, string DisplayPath);
    private bool IsBatchPackage(string path) => BatchArchiveSources.IsPackage(path) ||
        (_preferences.SharedSettings.EnableNup && Emignatik.NxFileViewer.FileLoading.NupReader.IsNup(path)) ||
        (_preferences.NandActive && File.Exists(path) && Emignatik.NxFileViewer.Services.Nand.NandDetection.IsCandidateName(path) &&
            !Emignatik.NxFileViewer.Services.Nand.NandDetection.IsContinuation(path));
    private IEnumerable<BatchSource> EnumerateBatchSources(string source, bool recursive, bool includeArchives, CancellationToken token)
    {
        var areas = Emignatik.NxFileViewer.FileLoading.SdBatchAreas.From(_preferences.SharedSettings);
        var files = areas.AppliesTo(source) ? areas.EnumerateFiles(source, recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly) : null;
        foreach (var entry in BatchArchiveSources.Enumerate(source, recursive, includeArchives, Path.Combine(AppContext.BaseDirectory, "Temp", "ZIP"), token, files,
            _preferences.FirmwareActive ? IsBatchFirmwareArchive : null, IsBatchPackage))
            yield return new(entry.Path, entry.DisplayPath);
    }
}
