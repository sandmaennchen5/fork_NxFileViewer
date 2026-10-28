using System;
using System.IO;
using System.Threading;
using Emignatik.NxFileViewer.FileLoading;
using Emignatik.NxFileViewer.Localization;
using Emignatik.NxFileViewer.Models.Overview;
using Emignatik.NxFileViewer.Services.BackgroundTask;

namespace Emignatik.NxFileViewer.Services.Nsz;

public sealed record XciRepairCandidate(string Path, string? SourceXcz, int LayoutChanges,
    bool HeaderRepair, long Length, long ModifiedTicks, long SourceLength = 0, long SourceModifiedTicks = 0);

public interface IXciRepairService
{
    XciRepairCandidate? Inspect(string path, IProgressReporter progress, CancellationToken token);
    string RepairCopy(XciRepairCandidate candidate, string destination, IProgressReporter progress, CancellationToken token);
}

public sealed class XciRepairService(IConversionVerifier verifier) : IXciRepairService
{
    public static bool Supports(string path) => !PackageZip.IsMember(path) &&
        !Mtp.MtpDirectSource.IsDirect(path) && Path.GetExtension(path).Equals(".xci", StringComparison.OrdinalIgnoreCase);

    public XciRepairCandidate? Inspect(string path, IProgressReporter progress, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!Supports(path) || !File.Exists(path)) return null;
        progress.SetMode(true);
        progress.SetText(LocalizationManager.Instance.Current.Keys.XciRepair_Inspect);
        try
        {
            path = System.IO.Path.GetFullPath(path);
            using var original = File.OpenRead(path);
            var headerRepair = false;
            try { XciPartitionIntegrity.VerifyGamecardHeader(path, token); }
            catch (InvalidDataException) { headerRepair = true; }
            var source = System.IO.Path.ChangeExtension(path, ".xcz");
            var corrections = 0;
            if (File.Exists(source))
            {
                try { corrections = XciPartitionIntegrity.InspectLayoutCorrection(source, path, token); }
                catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or OverflowException or ArgumentException)
                { /* An unrelated or damaged companion must never qualify a file for repair. */ }
            }
            if (corrections == 0 && (!headerRepair || !XciPartitionIntegrity.NeedsGamecardHeaderRepair(path, token))) return null;
            var info = new FileInfo(path);
            var sourceInfo = corrections > 0 ? new FileInfo(source) : null;
            return new(path, sourceInfo?.FullName, corrections, headerRepair, original.Length, info.LastWriteTimeUtc.Ticks,
                sourceInfo?.Length ?? 0, sourceInfo?.LastWriteTimeUtc.Ticks ?? 0);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or OverflowException or ArgumentException)
        { return null; }
    }

    public string RepairCopy(XciRepairCandidate candidate, string destination, IProgressReporter progress, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        destination = System.IO.Path.GetFullPath(destination);
        var keys = LocalizationManager.Instance.Current.Keys;
        if (!Supports(candidate.Path) || !System.IO.Path.GetExtension(destination).Equals(".xci", StringComparison.OrdinalIgnoreCase) ||
            destination.Equals(System.IO.Path.GetFullPath(candidate.Path), StringComparison.OrdinalIgnoreCase) ||
            File.Exists(destination) || Directory.Exists(destination))
            throw new IOException(keys.XciRepair_NewFileRequired);
        // Keep both verified inputs immutable until the repaired copy has been published.
        using var original = new FileStream(candidate.Path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var source = candidate.SourceXcz == null ? null : File.OpenRead(candidate.SourceXcz);
        if (Inspect(candidate.Path, progress, token) != candidate)
            throw new InvalidOperationException(keys.XciRepair_SourceChanged);
        var directory = System.IO.Path.GetDirectoryName(destination)!;
        Directory.CreateDirectory(directory);
        var staging = System.IO.Path.Combine(directory, ".nxfv-repair-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        var staged = System.IO.Path.Combine(staging, "repaired.xci");
        try
        {
            progress.SetMode(false);
            progress.SetText(keys.XciRepair_Copying);
            using (var output = new FileStream(staged, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var buffer = new byte[1024 * 1024];
                int count;
                while ((count = original.Read(buffer, 0, buffer.Length)) > 0)
                {
                    token.ThrowIfCancellationRequested();
                    output.Write(buffer, 0, count);
                    progress.SetPercentage(original.Length == 0 ? 0 : 0.3 * original.Position / original.Length);
                }
                output.Flush(true);
            }
            progress.SetMode(true);
            progress.SetText(keys.Nsz_PhaseLayout);
            if (candidate.SourceXcz != null)
                XciPartitionIntegrity.CorrectStagedLayout(candidate.SourceXcz, staged, token);
            XciPartitionIntegrity.NormalizeStagedGamecardHeader(staged, token);
            XciPartitionIntegrity.VerifyGamecardHeader(staged, token);
            var result = verifier.Verify(staged, new NszProgressScope(progress, keys.XciRepair_Verifying, 0.3, 0.7), token);
            if (result.Integrity != NcasIntegrity.Original) throw new InvalidDataException(keys.XciRepair_VerificationFailed);
            token.ThrowIfCancellationRequested();
            File.Move(staged, destination, overwrite: false);
            progress.SetPercentage(1);
            return destination;
        }
        finally
        {
            try { Directory.Delete(staging, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
