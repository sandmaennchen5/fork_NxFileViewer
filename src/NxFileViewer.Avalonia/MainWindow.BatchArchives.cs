using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using Emignatik.NxFileViewer.Services.Integrity;
namespace NxFileViewer.Avalonia;
public partial class MainWindow
{
    private sealed record BatchSource(string Path, string DisplayPath);
    private static bool IsBatchPackage(string path) => BatchArchiveSources.IsPackage(path);
    private static IEnumerable<BatchSource> EnumerateBatchSources(string source, bool recursive, bool includeArchives, CancellationToken token)
    {
        foreach (var entry in BatchArchiveSources.Enumerate(source, recursive, includeArchives, Path.Combine(AppContext.BaseDirectory, "Cache", "Avalonia", "archives"), token))
            yield return new(entry.Path, entry.DisplayPath);
    }
}
