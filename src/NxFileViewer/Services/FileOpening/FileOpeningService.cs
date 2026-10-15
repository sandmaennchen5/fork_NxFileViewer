using System;
using System.Threading.Tasks;
using System.Windows;
using Emignatik.NxFileViewer.FileLoading;
using Emignatik.NxFileViewer.Localization;
using Emignatik.NxFileViewer.Models;
using Emignatik.NxFileViewer.Services.BackgroundTask;
using Emignatik.NxFileViewer.Services.BackgroundTask.RunnableImpl;
using Emignatik.NxFileViewer.Settings;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Emignatik.NxFileViewer.Services.FileOpening;

public class FileOpeningService : IFileOpeningService
{
    private readonly IAppSettings _appSettings;
    private readonly IFileLoader _fileLoader;
    private readonly IMainBackgroundTaskRunnerService _backgroundTaskRunnerService;
    private readonly ILogger _logger;
    private readonly IVerifyNcasIntegrityRunnable? _automaticVerifier;
    private readonly Dictionary<string, NxFile> _archiveFiles = new(StringComparer.Ordinal);
    private string? _archivePath;
    private int _openGeneration;

    public FileOpeningService(ILoggerFactory loggerFactory, IAppSettings appSettings, IFileLoader fileLoader, IMainBackgroundTaskRunnerService backgroundTaskRunnerService, IVerifyNcasIntegrityRunnable? automaticVerifier = null)
    {
        _automaticVerifier = automaticVerifier;
        _appSettings = appSettings ?? throw new ArgumentNullException(nameof(appSettings));
        _fileLoader = fileLoader ?? throw new ArgumentNullException(nameof(fileLoader));
        _backgroundTaskRunnerService = backgroundTaskRunnerService ?? throw new ArgumentNullException(nameof(backgroundTaskRunnerService));
        _logger = loggerFactory.CreateLogger(this.GetType());
    }

    public Task SafeOpenFile(string filePath) => SafeOpenFile(filePath, FileOpenOrigin.Program);

    public async Task SafeOpenFile(string filePath, FileOpenOrigin origin)
    {
        if (_backgroundTaskRunnerService.IsRunning) return;
        Application.Current?.Dispatcher.Invoke(() =>
            (Application.Current.MainWindow as Emignatik.NxFileViewer.Views.Windows.MainWindow)?.Navigate(Emignatik.NxFileViewer.Views.Windows.WorkspaceSection.File));
        var generation = ++_openGeneration;
        try
        {
            _appSettings.LastOpenedFile = PackageZip.ArchivePath(filePath);
            var sameArchive = PackageZip.IsMember(filePath) && _archivePath != null &&
                string.Equals(Path.GetFullPath(PackageZip.ArchivePath(filePath)), _archivePath, StringComparison.OrdinalIgnoreCase);
            if (sameArchive && _archiveFiles.TryGetValue(filePath.Split(PackageZip.Separator, 2)[1], out var cached))
            {
                _openedFile = cached;
                NotifyOpenedFileChanged(cached);
                await VerifyAutomatically(cached, origin, generation);
                return;
            }

            var runnableRelay = new RunnableRelay<NxFile>((reporter, token) =>
            {
                using var archiveProgress = PackageZip.ReportProgress(reporter);
                var loadingFilePleaseWait = LocalizationManager.Instance.Current.Keys.LoadingFile_PleaseWait;
                reporter.SetText(loadingFilePleaseWait);
                var file = _fileLoader.Load(filePath, token);
                try { token.ThrowIfCancellationRequested(); return file; }
                catch { file.Dispose(); throw; }
            })
            {
                SupportProgress = true,
                SupportsCancellation = true
            };

            var loaded = await _backgroundTaskRunnerService.RunAsync(runnableRelay);
            if (generation != _openGeneration) { loaded.Dispose(); return; }
            if (!sameArchive) DisposeOpenedFiles();
            _openedFile = loaded;
            if (loaded.ArchivePath != null && loaded.ArchiveEntry != null)
            {
                _archivePath = Path.GetFullPath(loaded.ArchivePath);
                _archiveFiles.Add(loaded.ArchiveEntry, loaded);
            }

            NotifyOpenedFileChanged(_openedFile);
            await VerifyAutomatically(_openedFile, origin, generation);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation(LocalizationManager.Instance.Current.Keys.Update_Cancelled);
        }
        catch (FileNotSupportedException ex)
        {
            _logger.LogError(ex, LocalizationManager.Instance.Current.Keys.FileNotSupported_Log.SafeFormat(filePath));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, LocalizationManager.Instance.Current.Keys.LoadingError_Failed.SafeFormat(filePath, ex.Message), ex);
            if (LoadingSpaceWarning.IsDiskFull(ex)) LoadingSpaceWarning.Show(filePath);
        }
    }

    private async Task VerifyAutomatically(NxFile file, FileOpenOrigin origin, int generation)
    {
        var enabled = origin == FileOpenOrigin.WindowsAssociation ? _appSettings.VerifyFilesOpenedFromWindows : _appSettings.VerifyFilesOpenedInProgram;
        if (!enabled || _automaticVerifier == null || generation != _openGeneration || !ReferenceEquals(_openedFile, file) ||
            _backgroundTaskRunnerService.IsRunning || file.NandResult != null ||
            file.RootItem is Models.TreeItems.Impl.FirmwareFileItem or Models.TreeItems.Impl.NroFileItem or Models.TreeItems.Impl.SaveBackupFileItem ||
            file.Overview.BaseFileType is not (Models.Overview.NxBaseFileType.NSP or Models.Overview.NxBaseFileType.XCI or Models.Overview.NxBaseFileType.NCA or Models.Overview.NxBaseFileType.NAX0) ||
            file.Overview.NcasIntegrity != Models.Overview.NcasIntegrity.Unchecked) return;
        _automaticVerifier.Setup(file.Overview, _appSettings.IgnoreMissingDeltaFragments);
        await _backgroundTaskRunnerService.RunAsync(_automaticVerifier);
    }

    public void SafeClose()
    {
        ++_openGeneration; // A pending load must not reopen the archive after Close.
        DisposeOpenedFiles();

        GC.Collect();
        GC.WaitForPendingFinalizers();

        NotifyOpenedFileChanged(null);
    }

    public event OpenedFileChangedHandler? OpenedFileChanged;

    private NxFile? _openedFile;

    private void DisposeOpenedFiles()
    {
        var files = _archiveFiles.Values.Concat(_openedFile == null ? Array.Empty<NxFile>() : new[] { _openedFile }).Distinct().ToArray();
        _archiveFiles.Clear();
        _archivePath = null;
        _openedFile = null;
        foreach (var file in files)
        {
            try { file.Dispose(); }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to close opened file resources."); }
        }
    }

    public NxFile? OpenedFile => _openedFile;

    private void NotifyOpenedFileChanged(NxFile? newFile)
    {
        var dispatcher = Application.Current?.Dispatcher;

        if (dispatcher != null && !dispatcher.CheckAccess())
        {
            dispatcher.InvokeAsync(() =>
            {
                NotifyOpenedFileChanged(newFile);
            });
        }
        else
        {
            OpenedFileChanged?.Invoke(this, new OpenedFileChangedHandlerArgs(newFile));
        }
    }
}
