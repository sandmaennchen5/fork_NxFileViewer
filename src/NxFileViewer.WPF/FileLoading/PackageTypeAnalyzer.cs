using System;
using System.IO;
using Emignatik.NxFileViewer.Localization;
using Microsoft.Extensions.Logging;

namespace Emignatik.NxFileViewer.FileLoading;

public class PackageTypeAnalyzer : IPackageTypeAnalyzer
{
    private readonly ILogger _logger;

    public PackageTypeAnalyzer(ILoggerFactory loggerFactory)
    {
        _logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger(this.GetType());
    }

    public PackageType GetType(string filePath)
    {
        using var fileStream = Services.Mtp.MtpDirectSource.OpenRead(filePath);
        var type = PackageSignature.Read(fileStream);
        if (type == PackageType.NSP)
            CheckExtensionConsistency(filePath, ".nsp", ".nsz");
        else if (type == PackageType.XCI)
            CheckExtensionConsistency(filePath, ".xci", ".xcz");
        return type;
    }

    private void CheckExtensionConsistency(string filePath, string expectedExt1, string expectedExt2)
    {
        var fileExtension = Path.GetExtension(filePath);
        if (!string.Equals(fileExtension, expectedExt1, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(fileExtension, expectedExt2, StringComparison.OrdinalIgnoreCase))
            _logger.LogWarning(LocalizationManager.Instance.Current.Keys.SuspiciousFileExtension.SafeFormat(fileExtension, expectedExt1, expectedExt2));
    }
}
