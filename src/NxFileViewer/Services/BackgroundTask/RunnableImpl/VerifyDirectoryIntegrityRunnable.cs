using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Emignatik.NxFileViewer.FileLoading;
using Emignatik.NxFileViewer.Models.Overview;
using Emignatik.NxFileViewer.Models;
using Emignatik.NxFileViewer.Models.TreeItems;
using Emignatik.NxFileViewer.Localization;
using Emignatik.NxFileViewer.Services.Integrity;
using Emignatik.NxFileViewer.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Emignatik.NxFileViewer.Services.BackgroundTask.RunnableImpl;

public sealed class VerifyDirectoryIntegrityRunnable : IVerifyDirectoryIntegrityRunnable
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".nsp", ".nsz", ".xci", ".xcz" };

    private readonly IFileLoader _fileLoader;
    private readonly IServiceProvider _serviceProvider;
    private readonly IAppSettings _appSettings;
    private Action<NxFile>? _fileLoaded;
    private Action<BatchIntegrityResult>? _resultCompleted;
    private readonly ILogger<VerifyDirectoryIntegrityRunnable> _logger;
    private string? _directory;
    private bool _includeSubdirectories;

    public VerifyDirectoryIntegrityRunnable(IFileLoader fileLoader, IServiceProvider serviceProvider,
        IAppSettings appSettings, ILogger<VerifyDirectoryIntegrityRunnable> logger)
    {
        _fileLoader = fileLoader;
        _serviceProvider = serviceProvider;
        _appSettings = appSettings;
        _logger = logger;
    }

    public bool SupportsCancellation => true;
    public bool SupportProgress => true;

    public IVerifyDirectoryIntegrityRunnable Setup(string directory, bool includeSubdirectories,
        Action<NxFile>? fileLoaded = null, Action<BatchIntegrityResult>? resultCompleted = null)
    {
        _directory = directory ?? throw new ArgumentNullException(nameof(directory));
        _includeSubdirectories = includeSubdirectories;
        _fileLoaded = fileLoaded;
        _resultCompleted = resultCompleted;
        return this;
    }

    public IReadOnlyList<BatchIntegrityResult> Run(IProgressReporter progressReporter, CancellationToken cancellationToken)
    {
        if (_directory == null)
            throw new InvalidOperationException($"{nameof(Setup)} should be called first.");

        var option = _includeSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var files = Directory.EnumerateFiles(_directory, "*", option)
            .Where(path => SupportedExtensions.Contains(Path.GetExtension(path)))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var results = new List<BatchIntegrityResult>(files.Length);

        for (var index = 0; index < files.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var file = files[index];
            progressReporter.SetText($"Checking {index + 1}/{files.Length}: {Path.GetFileName(file)}");

            try
            {
                var nxFile = _fileLoader.Load(file);
                try
                {
                    _fileLoaded?.Invoke(nxFile);
                }
                catch (Exception previewException)
                {
                    // A UI preview failure must not invalidate the package analysis.
                    _logger.LogWarning(previewException, "Failed to display preview for {FilePath}", file);
                }
                var verifier = _serviceProvider.GetRequiredService<IVerifyNcasIntegrityRunnable>();
                verifier.Setup(nxFile.Overview, _appSettings.IgnoreMissingDeltaFragments);
                verifier.Run(new ScaledProgressReporter(progressReporter, index, files.Length), cancellationToken);
                var integrityError = BuildIntegrityError(nxFile.Overview);
                results.Add(new BatchIntegrityResult(
                    file,
                    Path.GetExtension(file).TrimStart('.').ToUpperInvariant(),
                    nxFile.Overview.FileType.ToString(),
                    nxFile.Overview.PackageStructure.ToString(),
                    nxFile.Overview.NcaCompressionType.ToString(),
                    nxFile.Overview.NcasIntegrity,
                    integrityError));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check integrity of {FilePath}", file);
                results.Add(new BatchIntegrityResult(
                    file,
                    Path.GetExtension(file).TrimStart('.').ToUpperInvariant(),
                    NxFileType.Unknown.ToString(),
                    "Unknown",
                    "Unknown",
                    NcasIntegrity.Error,
                    ex.Message));
            }

            _resultCompleted?.Invoke(results[^1]);

            progressReporter.SetPercentage(files.Length == 0 ? 1 : (double)(index + 1) / files.Length);
        }

        if (files.Length == 0)
            progressReporter.SetPercentage(1);
        return results;
    }

    private static string? BuildIntegrityError(FileOverview overview)
    {
        if (overview.NcasIntegrity == NcasIntegrity.Original)
            return null;

        var messages = overview.RootItem.FindChildrenOfType<IItem>(includeItem: true)
            .SelectMany(item => item.Errors)
            .Where(error => error.Category == Category.IntegrityCheck)
            .Select(error => error.Message)
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (overview.NcaCompressionType != NcaCompressionType.None &&
            messages.Any(message => message.Contains("Data corruption detected", StringComparison.OrdinalIgnoreCase)))
            return LocalizationManager.Instance.Current.Keys.BatchIntegrity_NszDataCorrupted;

        return messages.Length > 0
            ? string.Join(" | ", messages)
            : LocalizationManager.Instance.Current.Keys.BatchIntegrity_IntegrityFailed;
    }

    private sealed class ScaledProgressReporter : IProgressReporter
    {
        private readonly IProgressReporter _inner;
        private readonly int _fileIndex;
        private readonly int _fileCount;

        public ScaledProgressReporter(IProgressReporter inner, int fileIndex, int fileCount)
        {
            _inner = inner;
            _fileIndex = fileIndex;
            _fileCount = fileCount;
        }

        public void SetMode(bool isIndeterminate) => _inner.SetMode(isIndeterminate);
        public void SetText(string text) { }
        public void SetPercentage(double value) =>
            _inner.SetPercentage(_fileCount == 0 ? 1 : (_fileIndex + value) / _fileCount);
    }
}

public interface IVerifyDirectoryIntegrityRunnable : IRunnable<IReadOnlyList<BatchIntegrityResult>>
{
    IVerifyDirectoryIntegrityRunnable Setup(string directory, bool includeSubdirectories,
        Action<NxFile>? fileLoaded = null, Action<BatchIntegrityResult>? resultCompleted = null);
}
