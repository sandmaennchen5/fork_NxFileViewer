using System;
using System.IO;
using System.Linq;
using Emignatik.NxFileViewer.Models.TreeItems;
using Emignatik.NxFileViewer.Models.TreeItems.Impl;
using System.Threading;
using Emignatik.NxFileViewer.FileLoading;
using Emignatik.NxFileViewer.Localization;
using Emignatik.NxFileViewer.Models.Overview;
using Emignatik.NxFileViewer.Services.BackgroundTask;
using Emignatik.NxFileViewer.Services.BackgroundTask.RunnableImpl;
using Emignatik.NxFileViewer.Services.Integrity;
using Emignatik.NxFileViewer.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace Emignatik.NxFileViewer.Services.Nsz;

public sealed class ConversionVerifier(IFileLoader loader, IServiceProvider services, IAppSettings settings) : IConversionVerifier
{
    public BatchIntegrityResult Verify(string path, IProgressReporter progress, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Container offsets and every NCA hash, including update/logo/normal, must agree.
        if (Path.GetExtension(path).Equals(".xci", StringComparison.OrdinalIgnoreCase))
        {
            progress.SetMode(true);
            XciPartitionIntegrity.Verify(path, cancellationToken);
            progress.SetMode(false);
        }
        using var file = loader.Load(path, cancellationToken);
        var runnable = services.GetRequiredService<IVerifyNcasIntegrityRunnable>();
        runnable.Setup(file.Overview, settings.IgnoreMissingDeltaFragments);
        runnable.Run(progress, cancellationToken);
        if (file.RootItem is XciItem xci && Path.GetExtension(path).Equals(".xcz", StringComparison.OrdinalIgnoreCase))
        {
            if (xci.DescendantErrors.Any(error => error.Category == Category.Loading))
                throw new InvalidDataException("XCZ partition contents could not be fully loaded.");
            var secure = xci.ChildItems.FirstOrDefault(partition => partition.XciPartitionType == LibHac.Tools.Fs.XciPartitionType.Secure);
            var gameItems = secure?.FindChildrenOfType<NcaItem>(includeItem: false).ToHashSet() ?? new();
            var integrity = services.GetRequiredService<INcaItemIntegrityService>();
            foreach (var nca in xci.FindChildrenOfType<NcaItem>(includeItem: false).Where(nca => !gameItems.Contains(nca)))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (integrity.SafeCheckAsync(nca, null, cancellationToken, progress, settings.ProgressBufferSize).GetAwaiter().GetResult() != NcaIntegrity.Original)
                    throw new InvalidDataException("XCZ non-game NCA verification failed: " + nca.FileName);
            }
        }
        var overview = file.Overview;
        return new(path, Path.GetExtension(path).TrimStart('.').ToUpperInvariant(), overview.FileType.ToString(),
            overview.PackageStructure.ToString(), overview.NcaCompressionType.ToString(), overview.NcasIntegrity,
            overview.NcasIntegrity == NcasIntegrity.Original ? null : LocalizationManager.Instance.Current.Keys.BatchIntegrity_IntegrityFailed);
    }
}
