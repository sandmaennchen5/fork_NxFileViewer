using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
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
        { ".nsp", ".nsz", ".xci", ".xcz", ".nro" };

    private readonly IFileLoader _fileLoader;
    private readonly IServiceProvider _serviceProvider;
    private readonly IAppSettings _appSettings;
    private Action<NxFile>? _fileLoaded;
    private Action<BatchIntegrityResult>? _resultCompleted;
    private readonly ILogger<VerifyDirectoryIntegrityRunnable> _logger;
    private readonly string? _referenceDirectory;
    private readonly HttpClient? _firmwareClient;
    private string? _directory;
    private bool _includeSubdirectories;
    private bool _includeArchives, _verifyIntegrity;
    private IReadOnlyList<string>? _selectedFiles;
    private Func<string, bool>? _skipCompleted;

    public VerifyDirectoryIntegrityRunnable(IFileLoader fileLoader, IServiceProvider serviceProvider,
        IAppSettings appSettings, ILogger<VerifyDirectoryIntegrityRunnable> logger,
        string? referenceDirectory = null, HttpClient? firmwareClient = null)
    {
        _fileLoader = fileLoader;
        _serviceProvider = serviceProvider;
        _appSettings = appSettings;
        _logger = logger;
        _referenceDirectory = referenceDirectory;
        _firmwareClient = firmwareClient;
    }

    public bool SupportsCancellation => true;
    public bool SupportProgress => true;

    public IVerifyDirectoryIntegrityRunnable Setup(string directory, bool includeSubdirectories,
        Action<NxFile>? fileLoaded = null, Action<BatchIntegrityResult>? resultCompleted = null,
        bool includeArchives = true, bool verifyIntegrity = true, IReadOnlyList<string>? selectedFiles = null, Func<string, bool>? skipCompleted = null)
    {
        _directory = directory ?? throw new ArgumentNullException(nameof(directory));
        _includeSubdirectories = includeSubdirectories;
        _fileLoaded = fileLoaded;
        _resultCompleted = resultCompleted;
        _includeArchives = includeArchives; _verifyIntegrity = verifyIntegrity; _selectedFiles = selectedFiles; _skipCompleted = skipCompleted;
        return this;
    }

    public IReadOnlyList<BatchIntegrityResult> Run(IProgressReporter progressReporter, CancellationToken cancellationToken)
    {
        if (_directory == null)
            throw new InvalidOperationException($"{nameof(Setup)} should be called first.");

        var option = _includeSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        FirmwareIntegrityVerifier? firmware = null;
        var sdSources = SdCardSource.FindAllContents(_directory);
        var allFiles = _selectedFiles?.Select(PackageZip.ArchivePath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ??
            (Directory.Exists(_directory) ? Directory.GetFiles(_directory, "*", option) : new[] { _directory });
        var folderCandidates = allFiles.Where(p => p.EndsWith(".nca", StringComparison.OrdinalIgnoreCase) && SdCardSource.ContentsForFile(p) == null)
            .Select(Path.GetDirectoryName).Where(p => p != null).Select(p => p!)
            .Concat(_selectedFiles?.Where(p => Directory.Exists(p) && SdCardSource.FindContents(p) == null) ?? Array.Empty<string>()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var zipCandidates = allFiles.Where(p => (_selectedFiles != null || _includeArchives) && PackageZip.IsArchive(p))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var firmwareZips = zipCandidates.Where(p => ContainsNca(p) && ExpandPackages(p, cancellationToken).SequenceEqual(new[] { p }))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var expanded = allFiles.Where(path => SupportedExtensions.Contains(Path.GetExtension(path)) || zipCandidates.Contains(path) ||
                IsNandFile(path, cancellationToken))
            .SelectMany(path => ExpandPackages(path, cancellationToken)).ToArray();
        var archivedFirmware = (_selectedFiles ?? expanded).Where(path => PackageZip.IsMember(path) &&
            (path.EndsWith('/') || PackageZip.IsArchive(path))).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var hasFirmware = folderCandidates.Length > 0 || firmwareZips.Count > 0 || archivedFirmware.Count > 0;
        string? referenceNotice = null;
        if (hasFirmware)
        {
            progressReporter.SetText(LocalizationManager.Instance.Current.Keys.Firmware_LoadingOnline);
            using var ownedClient = _firmwareClient == null ? new HttpClient { Timeout = TimeSpan.FromSeconds(15) } : null;
            var client = _firmwareClient ?? ownedClient!;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(60));
            try
            {
                firmware = GitHubFirmwareReferences.LoadAsync(client, timeout.Token).GetAwaiter().GetResult();
                referenceNotice = LocalizationManager.Instance.Current.Keys.Firmware_OnlineSource;
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested &&
                ex is HttpRequestException or OperationCanceledException or System.Text.Json.JsonException or InvalidDataException or InvalidOperationException or KeyNotFoundException or ArgumentException)
            {
                _logger.LogWarning(ex, "GitHub firmware references unavailable; using bundled references.");
                referenceNotice = LocalizationManager.Instance.Current.Keys.Firmware_OfflineSource;
                try { firmware = new FirmwareIntegrityVerifier(_referenceDirectory); }
                catch (Exception referenceException) when (referenceException is IOException or UnauthorizedAccessException or
                    System.Text.Json.JsonException or InvalidOperationException or KeyNotFoundException or ArgumentException)
                {
                    _logger.LogWarning(referenceException, "Bundled firmware references unavailable.");
                    referenceNotice = LocalizationManager.Instance.Current.Keys.Firmware_NoReferences;
                }
            }
            cancellationToken.ThrowIfCancellationRequested();
        }
        // Loose NCA folders and NCA ZIPs are candidates even without bundled hash lists.
        var files = _selectedFiles?.ToArray() ?? expanded.Concat(folderCandidates)
            .Concat(sdSources.Select(source => source.ContentsPath)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
        files = files.Where(file => _skipCompleted?.Invoke(file) != true).ToArray();
        var results = new List<BatchIntegrityResult>(files.Length);
        var diskFull = false;

        for (var index = 0; index < files.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var file = files[index];
            var fingerprint = BatchHistoryStore.Fingerprint(file);
            progressReporter.SetText($"Checking {index + 1}/{files.Length}: {Path.GetFileName(file)}");

            try
            {
                if (!PackageZip.IsMember(file) && SaveBackupDetection.IsBackup(file, cancellationToken))
                {
                    results.Add(new BatchIntegrityResult(file, PackageZip.DisplayFileType(file),
                        SaveBackupDetection.IsLikelyLegacyBackup(file, cancellationToken)
                            ? LocalizationManager.Instance.Current.Keys.File_SaveBackupSuspected
                            : LocalizationManager.Instance.Current.Keys.File_SaveBackup, "SaveBackup", "None", NcasIntegrity.NoNca, null)
                        { FileSize = new FileInfo(file).Length });
                }
                else if ((Directory.Exists(file) && SdCardSource.FindContents(file) == null) || firmwareZips.Contains(file) || archivedFirmware.Contains(file))
                {
                    if (firmware == null) throw new InvalidDataException(referenceNotice);
                    using var extractedFirmware = archivedFirmware.Contains(file)
                        ? PackageZip.Extract(PackageZip.ArchivePath(file), file.Split(PackageZip.Separator, 2)[1], cancellationToken) : null;
                    var result = firmware.Verify(extractedFirmware?.FilePath ?? file, cancellationToken,
                        value => progressReporter.SetPercentage((index + value) / files.Length));
                    result = result with { FilePath = file, FileType = archivedFirmware.Contains(file)
                        ? file.EndsWith('/') ? "Folder (" + Path.GetExtension(PackageZip.ArchivePath(file)).TrimStart('.').ToUpperInvariant() + ")" : PackageZip.DisplayFileType(file) : result.FileType };
                    results.Add(result with { FirmwareDetails = referenceNotice + Environment.NewLine + result.FirmwareDetails });
                }
                else
                {
                    var nxFile = _fileLoader.Load(file, cancellationToken);
                    var retained = false;
                    try
                    {
                        try
                        {
                            _fileLoaded?.Invoke(nxFile);
                            retained = _fileLoaded != null;
                        }
                        catch (Exception previewException)
                        {
                            // A UI preview failure must not invalidate the package analysis.
                            _logger.LogWarning(previewException, "Failed to display preview for {FilePath}", file);
                        }
                        if (_verifyIntegrity && nxFile.NandResult == null && nxFile.RootItem is not Models.TreeItems.Impl.NroFileItem)
                        {
                            var verifier = _serviceProvider.GetRequiredService<IVerifyNcasIntegrityRunnable>();
                            verifier.Setup(nxFile.Overview, _appSettings.IgnoreMissingDeltaFragments);
                            verifier.Run(new ScaledProgressReporter(progressReporter, index, files.Length), cancellationToken);
                        }
                        var missingKeys = nxFile.Overview.MissingKeys.Count > 0;
                        var integrityError = missingKeys ? LocalizationManager.Instance.Current.Keys.File_MissingKeys + Environment.NewLine +
                            string.Join(", ", nxFile.Overview.MissingKeys.Select(k => k.KeyName).Distinct()) :
                            (_verifyIntegrity && nxFile.NandResult == null && nxFile.RootItem is not Models.TreeItems.Impl.NroFileItem ? BuildIntegrityError(nxFile.Overview) : null);
                        results.Add(new BatchIntegrityResult(
                            file,
                            nxFile.RootItem is Models.TreeItems.Impl.SdCardItem ? "NAX0" : nxFile.NandResult == null ? PackageZip.DisplayFileType(file) : "NAND" + (PackageZip.IsMember(file) ? " (" + Path.GetExtension(PackageZip.ArchivePath(file)).TrimStart('.').ToUpperInvariant() + ")" : ""),
                            nxFile.NandResult == null ? nxFile.Overview.FileType.ToString() : "NAND",
                            nxFile.NandResult?.Type ?? nxFile.Overview.PackageStructure.ToString(),
                            nxFile.Overview.NcaCompressionType.ToString(),
                            missingKeys ? NcasIntegrity.Error : nxFile.Overview.NcasIntegrity,
                            integrityError) { HasMissingKeys = missingKeys, IsNand = nxFile.NandResult != null,
                                NandDetails = nxFile.NandResult?.Information, FileSize = nxFile.NandResult?.Size });
                        if (nxFile.RootItem is Models.TreeItems.Impl.NroFileItem nro)
                            results[^1] = results[^1] with { Title = nro.Titles.FirstOrDefault()?.AppName ?? "", Publisher = nro.Titles.FirstOrDefault()?.Publisher ?? "",
                                DisplayVersion = nro.DisplayVersion, BuildId = nro.BuildId, Languages = string.Join(", ", nro.Titles.Select(t => t.Language)),
                                Distribution = "Homebrew", FileSize = nxFile.Overview.FileSize };
                    }
                    finally { if (!retained) nxFile.Dispose(); }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check integrity of {FilePath}", file);
                diskFull = Services.FileOpening.LoadingSpaceWarning.IsDiskFull(ex);
                if (diskFull) Services.FileOpening.LoadingSpaceWarning.Show(file);
                var isSdSource = SdCardSource.FindContents(file) != null;
                results.Add(new BatchIntegrityResult(
                    file,
                    isSdSource ? "NAX0" : PackageZip.DisplayFileType(file),
                    isSdSource ? "NAX0" : NxFileType.Unknown.ToString(),
                    isSdSource ? "Filesystem" : "Unknown",
                    "Unknown",
                    NcasIntegrity.Error,
                    ex.Message) { IsFirmware = (Directory.Exists(file) && SdCardSource.FindContents(file) == null) || firmwareZips.Contains(file) || archivedFirmware.Contains(file), FirmwareDetails = ex.Message });
            }

            results[^1] = results[^1] with { SourceFingerprint = fingerprint != null &&
                fingerprint == BatchHistoryStore.Fingerprint(file) ? fingerprint : null };
            _resultCompleted?.Invoke(results[^1]);
            if (diskFull) throw new IOException(LocalizationManager.Instance.Current.Keys.LoadingError_DiskFull, unchecked((int)0x80070070));

            progressReporter.SetPercentage(files.Length == 0 ? 1 : (double)(index + 1) / files.Length);
        }

        if (files.Length == 0)
            progressReporter.SetPercentage(1);
        return results;
    }

    private static bool ContainsNca(string path)
    {
        try
        {
            return PackageZip.GetFileEntries(path).Any(e => e.EndsWith(".nca", StringComparison.OrdinalIgnoreCase));
        }
        catch (InvalidDataException) { return Path.GetFileName(path).Contains("firmware", StringComparison.OrdinalIgnoreCase); }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    private static bool IsNandFile(string path, CancellationToken token)
    {
        if (!Services.Nand.NandDetection.IsCandidateName(path)) return false;
        try { return Services.Nand.NandDetection.Detect(path, token) != null; }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    private static IEnumerable<string> ExpandPackages(string path, CancellationToken token)
    {
        if (!PackageZip.IsArchive(path)) return new[] { path };
        try
        {
            if (SaveBackupDetection.IsBackup(path, token)) return new[] { path };
            var entries = PackageZip.GetEntries(path, includeNca: false, token: token, includeFirmware: true);
            if (entries.Count > 0) return entries.Select(entry => PackageZip.MemberPath(path, entry)).ToArray();
        }
        catch (IOException) { }
        catch (InvalidDataException) { }
        catch (UnauthorizedAccessException) { }
        return new[] { path };
    }

    private static string? BuildIntegrityError(FileOverview overview)
    {
        if (overview.NcasIntegrity is NcasIntegrity.Original or NcasIntegrity.NoNca)
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
        Action<NxFile>? fileLoaded = null, Action<BatchIntegrityResult>? resultCompleted = null,
        bool includeArchives = true, bool verifyIntegrity = true, IReadOnlyList<string>? selectedFiles = null, Func<string, bool>? skipCompleted = null);
}
