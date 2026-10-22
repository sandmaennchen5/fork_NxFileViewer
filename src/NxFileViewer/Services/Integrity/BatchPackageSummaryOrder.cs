using System;
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

    private static IEnumerable<string> Split(string value, string separator = " / ") =>
        value.Split(separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static IEnumerable<string> Distinct(IEnumerable<string> values) =>
        values.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal);

    private static string Text(IEnumerable<string> values, string separator = " / ") => string.Join(separator,
        Distinct(values).OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ThenBy(item => item, StringComparer.Ordinal));

    private static string Types(IEnumerable<string> values) => string.Join(" / ", Distinct(values)
        .OrderBy(item => item switch { "Base" or "Application" => 0, "Update" or "Patch" => 1, "DLC" or "AddOnContent" => 2, _ => 3 })
        .ThenBy(item => item, StringComparer.OrdinalIgnoreCase).ThenBy(item => item, StringComparer.Ordinal));

    private static readonly IComparer<string> VersionComparer = Comparer<string>.Create((left, right) =>
    {
        var leftNumeric = BigInteger.TryParse(left, NumberStyles.None, CultureInfo.InvariantCulture, out var leftNumber);
        var rightNumeric = BigInteger.TryParse(right, NumberStyles.None, CultureInfo.InvariantCulture, out var rightNumber);
        if (leftNumeric && rightNumeric) return leftNumber.CompareTo(rightNumber);
        if (leftNumeric != rightNumeric) return leftNumeric ? 1 : -1;
        return new BatchVersionSortKey(left).CompareTo(new BatchVersionSortKey(right));
    });

    private static string Versions(IEnumerable<string> values) => string.Join(" / ", Distinct(values)
        .OrderByDescending(item => item, VersionComparer).ThenBy(item => item, StringComparer.Ordinal));

    private static BigInteger? MasterKeyNumber(string value) =>
        BigInteger.TryParse(Regex.Match(value, @"\d+").Value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : null;

    private static string MasterKeys(IEnumerable<string> values) => string.Join(" / ", Distinct(values)
        .OrderByDescending(MasterKeyNumber).ThenBy(item => item, StringComparer.OrdinalIgnoreCase).ThenBy(item => item, StringComparer.Ordinal));
}