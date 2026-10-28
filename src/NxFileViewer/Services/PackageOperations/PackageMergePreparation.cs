using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Emignatik.NxFileViewer.Services.BackgroundTask;
using Emignatik.NxFileViewer.Services.Integrity;

namespace Emignatik.NxFileViewer.Services.PackageOperations;

public interface IPackageMergePreparationBackend
{
    string Split(PackageEngineConfiguration configuration, string source, string destination, string keys,
        IProgressReporter progress, CancellationToken token);
    BatchIntegrityResult Read(string path, CancellationToken token);
}

/// <summary>Owns disposable split inputs; originals and final merge outputs stay outside this workspace.</summary>
public sealed class PackageMergePreparation : IDisposable
{
    private readonly IPackageMergePreparationBackend _backend;
    private readonly string _workspace;
    public List<string> Errors { get; } = new();
    private sealed class Backend(PackageOperationService service) : IPackageMergePreparationBackend
    {
        public string Split(PackageEngineConfiguration configuration, string source, string destination, string keys,
            IProgressReporter progress, CancellationToken token) =>
            service.Run(configuration, PackageOperation.Split, new[] { source }, destination, "nsp", keys, progress, token);
        public BatchIntegrityResult Read(string path, CancellationToken token) => service.ReadBatchSource(path, token);
    }
    public PackageMergePreparation(PackageOperationService service, string destination) : this(new Backend(service), destination) { }
    public PackageMergePreparation(IPackageMergePreparationBackend backend, string destination)
    {
        _backend = backend;
        _workspace = Path.Combine(Path.GetFullPath(destination), ".nxfv-merge-preparation-" + Guid.NewGuid().ToString("N"));
    }
    public PackageBatchPlan Prepare(PackageEngineConfiguration configuration, BatchIntegrityResult[] inputs,
        BatchIntegrityResult[] splitSources, PackageBatchFormat format, string keys,
        IProgressReporter progress, CancellationToken token)
    {
        var prepared = inputs.GroupBy(r => r.FilePath, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToList();
        foreach (var source in splitSources)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var output = _backend.Split(configuration, source.FilePath, _workspace, keys, progress, token);
                var parts = Directory.GetFiles(output, "*.nsp", SearchOption.AllDirectories)
                    .Select(path => _backend.Read(path, token)).ToArray();
                if (parts.Length < 2) throw new InvalidDataException("Split did not produce multiple packages.");
                prepared.RemoveAll(r => r.FilePath.Equals(source.FilePath, StringComparison.OrdinalIgnoreCase));
                prepared.AddRange(parts);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Errors.Add(Path.GetFileName(source.FilePath) + ": " + ex.Message);
            }
        }
        return PackageBatchPlanner.Merge(prepared, PackageMergeGrouping.ByGame, format);
    }
    public void Dispose()
    {
        try { if (Directory.Exists(_workspace)) Directory.Delete(_workspace, true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}