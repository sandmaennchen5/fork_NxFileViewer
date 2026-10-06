using System;
using System.Collections.Generic;
using Emignatik.NxFileViewer.Localization;
using Emignatik.NxFileViewer.Models;
using Emignatik.NxFileViewer.Models.Overview;
using Emignatik.NxFileViewer.Models.TreeItems;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.IO;
using Emignatik.NxFileViewer.Services.Integrity;
using Emignatik.NxFileViewer.Models.TreeItems.Impl;

namespace Emignatik.NxFileViewer.FileLoading;

internal class FileLoader : IFileLoader
{
    private readonly IPackageTypeAnalyzer _packageTypeAnalyzer;
    private readonly IFileItemLoader _fileItemLoader;
    private readonly IFileOverviewLoader _fileOverviewLoader;
    private readonly ILogger _logger;
    private readonly IFirmwareReferenceProvider _firmwareReferences;

    public FileLoader(ILoggerFactory loggerFactory, IPackageTypeAnalyzer packageTypeAnalyzer, IFileItemLoader fileItemLoader, IFileOverviewLoader fileOverviewLoader,
        IFirmwareReferenceProvider? firmwareReferences = null)
    {
        _packageTypeAnalyzer = packageTypeAnalyzer ?? throw new ArgumentNullException(nameof(packageTypeAnalyzer));
        _fileItemLoader = fileItemLoader ?? throw new ArgumentNullException(nameof(fileItemLoader));
        _fileOverviewLoader = fileOverviewLoader ?? throw new ArgumentNullException(nameof(fileOverviewLoader));
        _logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger(this.GetType());
        _firmwareReferences = firmwareReferences ?? new FirmwareReferenceProvider(_logger);
    }

    public NxFile Load(string filePath) => Load(filePath, default);

    public NxFile Load(string filePath, System.Threading.CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (PackageZip.IsMember(filePath) || PackageZip.IsArchive(filePath))
        {
            var archive = PackageZip.ArchivePath(filePath);
            var entries = PackageZip.GetEntries(archive);
            if (!PackageZip.IsMember(filePath) && entries.Count > 0 && PackageZip.GetEntries(archive, includeNca: false).Count == 0)
            {
                var (verifier, notice) = _firmwareReferences.Load(token);
                token.ThrowIfCancellationRequested();
                var result = verifier?.Verify(archive, token) ?? UnavailableFirmware(archive, notice);
                return FirmwareFile(archive, result with { FirmwareDetails = notice + Environment.NewLine + result.FirmwareDetails });
            }
            var entry = PackageZip.IsMember(filePath) ? filePath.Split(PackageZip.Separator, 2)[1] :
                entries.Count > 0 ? entries[0] : throw new FileNotSupportedException(filePath);
            var extracted = PackageZip.Extract(archive, entry, token);
            NxFile? loaded = null;
            try
            {
                loaded = Load(extracted.FilePath, token);
                return new NxFile(PackageZip.MemberPath(archive, entry), loaded.RootItem, loaded.Overview)
                { OwnedResource = extracted, ArchivePath = archive, ArchiveEntry = entry, ArchiveEntries = entries, FirmwareResult = loaded.FirmwareResult };
            }
            catch { loaded?.Dispose(); extracted.Dispose(); throw; }
        }
        _logger.LogInformation(LocalizationManager.Instance.Current.Keys.Log_OpeningFile.SafeFormat(filePath));

        BatchIntegrityResult? ncaFirmware = null;
        if (Path.GetExtension(filePath).Equals(".nca", StringComparison.OrdinalIgnoreCase))
        {
            var (verifier, notice) = _firmwareReferences.Load(token);
            token.ThrowIfCancellationRequested();
            ncaFirmware = verifier?.IdentifyNca(filePath, token) ?? UnavailableFirmware(filePath, notice) with { IsFirmware = false };
            ncaFirmware = ncaFirmware with { FirmwareDetails = notice + Environment.NewLine + ncaFirmware.FirmwareDetails };
        }


        HashSet<MissingKey> missingKeys = new();
        MissingKeyExceptionHandler missingKeyHandler = (_, args) =>
        {
            var ex = args.Exception;
            var missingKey = new MissingKey(ex.Name, ex.Type);

            missingKeys.Add(missingKey);
        };
        _fileItemLoader.MissingKey += missingKeyHandler;
        IItem? rootItem = null;
        try
        {

            FileOverview fileOverview;
            if (System.IO.Path.GetExtension(filePath).Equals(".nca", StringComparison.OrdinalIgnoreCase))
            {
                try { rootItem = _fileItemLoader.LoadNca(filePath); }
                catch (Exception ex) when (ncaFirmware?.IsFirmware == true && ex is not OperationCanceledException)
                {
                    // A hash-proven firmware NCA can still be identified when keys cannot decrypt its header.
                    _logger.LogDebug(ex, "Showing firmware hash identification without decrypted NCA contents.");
                    return FirmwareFile(filePath, ncaFirmware);
                }
                fileOverview = new FileOverview(rootItem);
            }
            else switch (_packageTypeAnalyzer.GetType(filePath))
            {
                case PackageType.UNKNOWN:
                    throw new FileNotSupportedException(filePath);

                case PackageType.XCI:
                    var xciItem = _fileItemLoader.LoadXci(filePath);
                    rootItem = xciItem;
                    fileOverview = _fileOverviewLoader.Load(xciItem);

                    break;
                case PackageType.NSP:
                    var nspItem = _fileItemLoader.LoadNsp(filePath);
                    rootItem = nspItem;
                    fileOverview = _fileOverviewLoader.Load(nspItem);
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }

            foreach (var missingKey in missingKeys)
                fileOverview.MissingKeys.Add(missingKey);

            fileOverview.FileSize = new System.IO.FileInfo(filePath).Length;

            var openedFile = new NxFile(filePath, rootItem, fileOverview) { FirmwareResult = ncaFirmware };
            return openedFile;
        }
        catch { rootItem?.Dispose(); throw; }
        finally { _fileItemLoader.MissingKey -= missingKeyHandler; }
    }


    private static BatchIntegrityResult UnavailableFirmware(string path, string notice) =>
        new(path, Path.GetExtension(path).TrimStart('.').ToUpperInvariant(), "Firmware", "?", "SHA-256", NcasIntegrity.Unchecked, notice)
        { IsFirmware = true, FirmwareDetails = notice };

    private static NxFile FirmwareFile(string path, BatchIntegrityResult result)
    {
        var root = new FirmwareFileItem(path);
        return new NxFile(path, root, new FileOverview(root) { FileSize = new FileInfo(path).Length, NcasIntegrity = result.Integrity })
        { FirmwareResult = result };
    }
}
