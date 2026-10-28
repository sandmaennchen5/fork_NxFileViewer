using System.Collections.Generic;
using System.Linq;
using Emignatik.NxFileViewer.Localization.Keys;

namespace Emignatik.NxFileViewer.Services.Integrity;

/// <summary>Displays persisted metadata without reopening a source file or archive.</summary>
public static class BatchStoredOverview
{
    public static IReadOnlyList<KeyValuePair<string, string>> BuildFile(BatchIntegrityResult result, ILocalizationKeys keys) =>
        new KeyValuePair<string, string>[]
        {
            new(keys.BatchIntegrity_FileType, result.FileType),
            new(keys.BatchTable_Type, result.TypeDisplay),
            new(keys.PackageStructure_Title, PackageAnalysis.PackageStructureAnalyzer.GetDisplayName(result.Structure, keys)),
            new(keys.Signature_Title, result.NcaSignature),
            new(keys.Title_FileInfo_Integrity, result.Integrity.ToString()),
            new(keys.FileInfo_FileSize, result.FileSizeDisplay),
            new(keys.Title_FileInfo_Compression, result.Compression),
            new(keys.FileInfo_CompressionRatio, result.CompressionRatio?.ToString("P1") ?? "—"),
            new(keys.FileInfo_SystemUpdate, result.SystemUpdateVersion),
            new(keys.File_MissingKeysHeader, result.MissingKeys),
            new(keys.BatchIntegrity_Error, result.Diagnostic ?? "")
        };

    public static IReadOnlyList<KeyValuePair<string, string>> BuildPackage(BatchPackageSummary package, ILocalizationKeys keys) =>
        new KeyValuePair<string, string>[]
        {
            new(keys.BatchIntegrity_PackageType, package.ContentType),
            new(keys.CnmtOverview_TitleId, package.TitleId),
            new(keys.CnmtOverview_TitleVersion, package.Version),
            new(keys.CnmtOverview_MinimumSystemVersion, package.SystemVersion),
            new(keys.CnmtOverview_BaseTitleId, package.BaseTitleId),
            new(keys.CnmtOverview_MasterKey, package.MasterKey),
            new(keys.CnmtOverview_MinimumApplicationVersion, package.MinimumApplicationVersion),
            new(keys.CnmtOverview_Distribution, package.Distribution),
            new(keys.CnmtOverview_BuildID, package.BuildId),
            new(keys.CnmtOverview_IsDemo, package.IsDemo?.ToString() ?? "—"),
            new(keys.Security_Title, package.PermissionDisplay),
            new(keys.Security_FileSystemPermissions, package.FileSystemPermissions),
            new(keys.Security_AcidSignature, package.AcidSignatureValidity),
            new(keys.AppTitle, package.Title),
            new(keys.Publisher, package.Publisher),
            new(keys.DisplayVersion, package.DisplayVersion),
            new(keys.AvailableLanguages, package.Languages)
        }.Concat(package.Titles.SelectMany(title => new[]
        {
            new KeyValuePair<string, string>($"{keys.AppTitle} ({title.Language})", title.Title),
            new KeyValuePair<string, string>($"{keys.Publisher} ({title.Language})", title.Publisher)
        })).ToArray();

    public static IReadOnlyList<KeyValuePair<string, string>> Build(BatchIntegrityResult result, ILocalizationKeys keys) =>
        new KeyValuePair<string, string>[]
        {
            new(keys.Title_FileInfo_FileType, result.FileType),
            new(keys.BatchTable_Type, result.TypeDisplay),
            new(keys.BatchIntegrity_PackageType, result.ContentType),
            new(keys.Signature_Title, result.NcaSignature),
            new(keys.Security_Level, result.PermissionDisplay),
            new(keys.PackageStructure_Title, PackageAnalysis.PackageStructureAnalyzer.GetDisplayName(result.Structure, keys)),
            new(keys.Title_FileInfo_Compression, result.Compression),
            new(keys.Title_FileInfo_Integrity, result.Integrity.ToString()),
            new(keys.FileInfo_FileSize, result.FileSize.HasValue ? result.FileSize.Value.ToString("N0") + " B" : "—"),
            new(keys.FileInfo_CompressionRatio, result.CompressionRatio?.ToString("P1") ?? "—"),
            new(keys.AppTitle, result.Title),
            new(keys.CnmtOverview_TitleId, result.TitleId),
            new(keys.Publisher, result.Publisher),
            new(keys.CnmtOverview_TitleVersion, result.Version),
            new(keys.DisplayVersion, result.DisplayVersion),
            new(keys.CnmtOverview_MinimumSystemVersion, result.SystemVersion),
            new(keys.CnmtOverview_MasterKey, result.MasterKey),
            new(keys.CnmtOverview_BuildID, result.BuildId),
            new(keys.CnmtOverview_Distribution, result.Distribution),
            new(keys.AvailableLanguages, result.Languages),
            new(keys.CnmtOverview_BaseTitleId, result.BaseTitleId),
            new(keys.CnmtOverview_MinimumApplicationVersion, result.MinimumApplicationVersion),
            new(keys.FileInfo_SystemUpdate, result.SystemUpdateVersion),
            new(keys.CnmtOverview_IsDemo, result.IsDemo?.ToString() ?? "—"),
            new(keys.Security_FileSystemPermissions, result.FileSystemPermissions),
            new(keys.Security_AcidSignature, result.AcidSignatureValidity),
            new(keys.File_MissingKeysHeader, result.MissingKeys),
            new(keys.BatchIntegrity_Error, result.Diagnostic ?? "")
        }.Concat(result.Titles.SelectMany(title => new[]
        {
            new KeyValuePair<string, string>($"{keys.AppTitle} ({title.Language})", title.Title),
            new KeyValuePair<string, string>($"{keys.Publisher} ({title.Language})", title.Publisher)
        })).ToArray();
}
