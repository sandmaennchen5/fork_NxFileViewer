using System;
using Emignatik.NxFileViewer.Settings;
using System.IO;
using System.Threading;
using Microsoft.Extensions.Logging;
using Emignatik.NxFileViewer.Localization;
using Emignatik.NxFileViewer.Models.Overview;
using Emignatik.NxFileViewer.Services.BackgroundTask;
using Emignatik.NxFileViewer.Services.Integrity;

namespace Emignatik.NxFileViewer.Services.Nsz;

public interface IConversionVerifier
{
    BatchIntegrityResult Verify(string path, IProgressReporter progress, CancellationToken cancellationToken);
}

public sealed record PackageConversionResult(string SourcePath, string OutputPath, long SourceSize,
    long OutputSize, BatchIntegrityResult Verification, bool SourceDeleted = false, string? SourceDeletionError = null);

public enum OutputConflictResolution { Cancel, Replace, Number }

public sealed class PackageConversionService(INszPlugin plugin, IConversionVerifier verifier, ILogger<PackageConversionService>? logger = null, IAppSettings? settings = null)
{
    public static string OutputExtension(string source, NszOperation operation) =>
        (Path.GetExtension(source).ToLowerInvariant(), operation) switch
        {
            (".nsp", NszOperation.Compress) => ".nsz",
            (".xci", NszOperation.Compress) => ".xcz",
            (".nsz", NszOperation.Decompress) => ".nsp",
            (".xcz", NszOperation.Decompress) => ".xci",
            _ => throw new ArgumentException("Unsupported conversion: " + source)
        };

    public static bool Supports(string source, NszOperation operation)
    {
        if (Mtp.MtpDirectSource.IsDirect(source) || Emignatik.NxFileViewer.FileLoading.PackageZip.IsMember(source)) return false;
        try { OutputExtension(source, operation); return true; }
        catch (ArgumentException) { return false; }
    }

    public PackageConversionResult Convert(string source, string destinationDirectory, NszOperation operation,
        IProgressReporter progress, CancellationToken cancellationToken,
        Func<string, OutputConflictResolution>? resolveConflict = null, bool deleteSource = false)
    {
        source = Path.GetFullPath(source);
        if (Mtp.MtpDirectSource.IsDirect(source)) throw new IOException("Direct MTP sources are read-only. Use local cache for conversion.");
        var output = Path.Combine(Path.GetFullPath(destinationDirectory),
            Path.GetFileNameWithoutExtension(source) + OutputExtension(source, operation));
        var originalOutput = output;
        var replace = false;
        void ResolveConflict()
        {
            if (resolveConflict == null)
                throw new IOException(LocalizationManager.Instance.Current.Keys.Nsz_OutputExists + " " + output);
            switch (resolveConflict(output))
            {
                case OutputConflictResolution.Replace:
                    if (Directory.Exists(output)) throw new IOException("Output is a directory: " + output);
                    replace = true;
                    break;
                case OutputConflictResolution.Number:
                    replace = false;
                    var number = 1;
                    do { output = Path.Combine(Path.GetDirectoryName(originalOutput)!,
                        Path.GetFileNameWithoutExtension(originalOutput) + $" ({number++})" + Path.GetExtension(originalOutput)); }
                    while (File.Exists(output) || Directory.Exists(output));
                    break;
                default: throw new OperationCanceledException(cancellationToken);
            }
        }
        if (File.Exists(output) || Directory.Exists(output)) ResolveConflict();
        cancellationToken.ThrowIfCancellationRequested();
        // Keep a read lock until publication so the source cannot be replaced or edited.
        using var sourceLock = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read);
        var sourceProgress = new NszProgressScope(progress, "1/3 " + LocalizationManager.Instance.Current.Keys.Nsz_PhaseSource + " " + Path.GetFileName(source), 0, 1.0 / 3);
        sourceProgress.SetMode(false);
        sourceProgress.SetPercentage(0);
        logger?.LogInformation("NSZ 1/3: verifying source {Source}; destination={Output}", source, output);
        var before = verifier.Verify(source, sourceProgress, cancellationToken);
        if (before.Integrity != NcasIntegrity.Original)
            throw new InvalidDataException(LocalizationManager.Instance.Current.Keys.Nsz_SourceInvalid + " " + before.Integrity);

        Directory.CreateDirectory(destinationDirectory);
        var staging = Path.Combine(Path.GetFullPath(destinationDirectory), ".nxfv-nsz-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try
        {
            logger?.LogInformation("NSZ 2/3: {Operation}; source={Source}; staging={Staging}", operation, source, staging);
            var mode = settings?.NszCompressionEngine ?? NszCompressionEngine.Nsz;
            var retry = plugin is INszEnginePlugin && mode.HasBackup();
            string stagedOutput = "";
            BatchIntegrityResult? after = null;
            Exception? primaryFailure = null;
            for (var attempt = 0; attempt < (retry ? 2 : 1); attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var attemptDirectory = retry ? Path.Combine(staging, "engine-" + attempt) : staging;
                Directory.CreateDirectory(attemptDirectory);
                try
                {
                    if (plugin is INszEnginePlugin enginePlugin && settings != null)
                        enginePlugin.ConvertUsingEngine(attempt == 0 ? mode.PrimaryRust() : !mode.PrimaryRust(),
                            source, attemptDirectory, operation, new NszProgressScope(progress, "2/3", 1.0 / 3, 1.0 / 3), cancellationToken);
                    else plugin.Convert(source, attemptDirectory, operation, new NszProgressScope(progress, "2/3", 1.0 / 3, 1.0 / 3), cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    stagedOutput = Path.Combine(attemptDirectory, Path.GetFileName(originalOutput));
                    if (!File.Exists(stagedOutput) || new FileInfo(stagedOutput).Length == 0)
                        throw new InvalidDataException(LocalizationManager.Instance.Current.Keys.Nsz_OutputMissing + " Expected: " + stagedOutput);
                    var outputProgress = new NszProgressScope(progress, "3/3 " + LocalizationManager.Instance.Current.Keys.Nsz_PhaseOutput, 2.0 / 3, 1.0 / 3);
                    outputProgress.SetMode(false);
                    outputProgress.SetPercentage(0);
                    after = verifier.Verify(stagedOutput, outputProgress, cancellationToken);
                    if (after.Integrity != NcasIntegrity.Original)
                        throw new InvalidDataException(LocalizationManager.Instance.Current.Keys.Nsz_OutputInvalid + " " + after.Integrity);
                    break;
                }
                catch (Exception ex) when (retry && !cancellationToken.IsCancellationRequested &&
                    ex is IOException or InvalidDataException or System.ComponentModel.Win32Exception)
                {
                    if (attempt == 1) throw new AggregateException("Both compression/decompression engines failed.", primaryFailure!, ex);
                    primaryFailure = ex;
                    try { Directory.Delete(attemptDirectory, true); }
                    catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException) { logger?.LogWarning(cleanup, "Failed engine workspace could not be removed before backup."); }
                    logger?.LogWarning(ex, "Primary compression engine failed; retrying with the selected backup engine.");
                }
            }
            cancellationToken.ThrowIfCancellationRequested();
            var outputSize = new FileInfo(stagedOutput).Length;
            progress.SetText(LocalizationManager.Instance.Current.Keys.Nsz_PhasePublish);
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try { File.Move(stagedOutput, output, replace); break; }
                catch (IOException) when (!replace && (File.Exists(output) || Directory.Exists(output)))
                { ResolveConflict(); }
            }
            var sourceSize = sourceLock.Length;
            var deleted = false;
            string? deletionError = null;
            if (deleteSource)
            {
                // Publication and both integrity checks completed before releasing the source.
                sourceLock.Dispose();
                try { File.Delete(source); deleted = true; }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                { deletionError = ex.Message; }
            }
            progress.SetPercentage(1);
            return new(source, output, sourceSize, outputSize, after! with { FilePath = output }, deleted, deletionError);
        }
        finally
        {
            // Cleanup only the temporary directory owned by this conversion.
            try { Directory.Delete(staging, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
