using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Emignatik.NxFileViewer.FileLoading;
using Emignatik.NxFileViewer.Localization;
using Emignatik.NxFileViewer.Models.Overview;

namespace Emignatik.NxFileViewer.Services.Integrity;

public sealed class FirmwareIntegrityVerifier
{
    private readonly FirmwareComparison _comparison;

    public FirmwareIntegrityVerifier(string? referenceDirectory = null)
        : this(Directory.EnumerateFiles(referenceDirectory ?? ApplicationDataPaths.FirmwareHashes(AppContext.BaseDirectory), "*.json")
            .Select(File.ReadAllText))
    {
    }

    public FirmwareIntegrityVerifier(IEnumerable<string> manifests)
    {
        _comparison = new FirmwareComparison(manifests, PackageZip.GetFileEntries,
            (path, include, read, token) => PackageZip.ReadEntries(path, include, read, token));
    }

    public bool IsFirmwareFolder(string path) => _comparison.IsFirmwareFolder(path);
    public bool IsFirmwareZip(string path) => _comparison.IsFirmwareZip(path);

    public BatchIntegrityResult Verify(string path, CancellationToken cancellationToken = default, Action<double>? progress = null) =>
        ToBatchResult(path, _comparison.Verify(path, LocalizationManager.Instance.Current.Keys, cancellationToken, progress));

    public BatchIntegrityResult IdentifyNca(string path, CancellationToken token = default) =>
        ToBatchResult(path, _comparison.IdentifyNca(path, LocalizationManager.Instance.Current.Keys, token));

    private static BatchIntegrityResult ToBatchResult(string path, FirmwareVerification result)
    {
        var integrity = result.Match switch
        {
            FirmwareMatch.Exact => NcasIntegrity.Original,
            FirmwareMatch.Unchecked => NcasIntegrity.Unchecked,
            _ => NcasIntegrity.Error
        };
        var sourceType = Directory.Exists(path) ? "Folder" : Path.GetExtension(path).TrimStart('.').ToUpperInvariant();
        return new BatchIntegrityResult(path, sourceType, "Firmware", result.Version, "SHA-256", integrity, result.Error)
        {
            IsFirmware = result.IsFirmware,
            FirmwareDetails = result.Details
        };
    }
}
