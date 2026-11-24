using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Emignatik.NxFileViewer.FileLoading;
using Emignatik.NxFileViewer.Models;
using Emignatik.NxFileViewer.Models.Overview;
using Emignatik.NxFileViewer.Services.Integrity;

namespace Emignatik.NxFileViewer.Services.BackgroundTask.RunnableImpl;

/// <summary>Scans dropped files and folders as one cancellable batch.</summary>
public sealed class VerifyBatchSourcesRunnable(
    IReadOnlyList<string> sources, Func<IVerifyDirectoryIntegrityRunnable> createRunner,
    bool includeSubdirectories, bool includeArchives, bool verifyIntegrity,
    Action<NxFile>? fileLoaded = null, Action<BatchIntegrityResult>? resultCompleted = null,
    Func<string, bool>? skipCompleted = null) : IRunnable<IReadOnlyList<BatchIntegrityResult>>
{
    public bool SupportsCancellation => true;
    public bool SupportProgress => true;
    public static string[] Normalize(IEnumerable<string> paths) => paths.Where(path => !string.IsNullOrWhiteSpace(path))
        .Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    public IReadOnlyList<BatchIntegrityResult> Run(IProgressReporter progressReporter, CancellationToken cancellationToken)
    {
        var roots = Normalize(sources);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var results = new Dictionary<string, BatchIntegrityResult>(StringComparer.OrdinalIgnoreCase);
        void Completed(BatchIntegrityResult result)
        {
            seen.Add(result.FilePath);
            results[result.FilePath] = result;
            resultCompleted?.Invoke(result);
        }
        for (var i = 0; i < roots.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var root = roots[i];
            try
            {
                var found = createRunner().Setup(root, includeSubdirectories, fileLoaded, Completed,
                    includeArchives, verifyIntegrity, skipCompleted: path => seen.Contains(path) || skipCompleted?.Invoke(path) == true)
                    .Run(new SourceProgress(progressReporter, i, roots.Length), cancellationToken);
                foreach (var result in found) { seen.Add(result.FilePath); results[result.FilePath] = result; }
            }
            catch (Exception ex) when ((ex is IOException or UnauthorizedAccessException) && !Services.FileOpening.LoadingSpaceWarning.IsDiskFull(ex))
            {
                Completed(new BatchIntegrityResult(root, Directory.Exists(root) ? "Folder" : PackageZip.DisplayFileType(root),
                    "Unknown", "Unknown", "Unknown", NcasIntegrity.Error, ex.Message));
            }
        }
        progressReporter.SetPercentage(1);
        return results.Values.ToArray();
    }
    private sealed class SourceProgress(IProgressReporter inner, int index, int count) : IProgressReporter
    {
        public void SetText(string text) => inner.SetText(text);
        public void SetMode(bool isIndeterminate) => inner.SetMode(isIndeterminate);
        public void SetPercentage(double value) => inner.SetPercentage((index + Math.Clamp(value, 0, 1)) / count);
    }
}
