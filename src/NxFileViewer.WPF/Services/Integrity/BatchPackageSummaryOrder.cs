using System;
using static Emignatik.NxFileViewer.Services.Integrity.BatchSummaryValues;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;

namespace Emignatik.NxFileViewer.Services.Integrity;

public static class BatchPackageSummaryOrder
{
    public static BatchIntegrityResult Apply(BatchIntegrityResult result, bool preservePackageOrder = false)
    {
        if (preservePackageOrder || !result.HasMultiplePackages || result.IsFirmware || result.IsNand) return result;
        IEnumerable<string> PackageValues(Func<BatchPackageSummary, string> selector, string fallback)
        {
            // Work with complete package values: a title itself can contain " / ".
            var values = result.Packages.Select(selector).Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
            return values.Length > 0 ? values : Split(fallback);
        }
        var languages = result.Packages.SelectMany(package => Split(package.Languages, ", ")).ToArray();
        var summary = result with
        {
            ContentType = Types(PackageValues(package => package.ContentType, result.ContentType)),
            Version = Versions(PackageValues(package => package.Version, result.Version)),
            DisplayVersion = Versions(PackageValues(package => package.DisplayVersion, result.DisplayVersion)),
            SystemVersion = Versions(PackageValues(package => package.SystemVersion, result.SystemVersion)),
            SystemUpdateVersion = Versions(Split(result.SystemUpdateVersion)),
            MinimumApplicationVersion = Versions(PackageValues(package => package.MinimumApplicationVersion, result.MinimumApplicationVersion)),
            MasterKey = MasterKeys(PackageValues(package => package.MasterKey, result.MasterKey)),
            Title = Text(PackageValues(package => package.Title == "—" ? "" : package.Title, result.Title)),
            TitleId = Text(PackageValues(package => package.TitleId, result.TitleId)),
            Publisher = Text(PackageValues(package => package.Publisher, result.Publisher)),
            BaseTitleId = Text(PackageValues(package => package.BaseTitleId, result.BaseTitleId)),
            Distribution = result.FileType == "NAX0" ? result.Distribution : Text(PackageValues(package => package.Distribution, result.Distribution)),
            BuildId = Text(PackageValues(package => package.BuildId, result.BuildId)),
            FileSystemPermissions = Text(PackageValues(package => package.FileSystemPermissions, result.FileSystemPermissions)),
            AcidSignatureValidity = Text(PackageValues(package => package.AcidSignatureValidity, result.AcidSignatureValidity)),
            Languages = Text(languages.Length > 0 ? languages : Split(result.Languages, ", "), ", ")
        };
        return summary == result ? result : summary.DetachUiSubscriptions();
    }

}
