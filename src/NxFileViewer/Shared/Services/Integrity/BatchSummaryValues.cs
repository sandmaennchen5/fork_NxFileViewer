using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;

namespace Emignatik.NxFileViewer.Services.Integrity;
public static class BatchSummaryValues
{
    public static IEnumerable<string> Split(string value, string separator = " / ") =>
        value.Split(separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static IEnumerable<string> Distinct(IEnumerable<string> values) =>
        values.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal);

    public static string Text(IEnumerable<string> values, string separator = " / ") => string.Join(separator,
        Distinct(values).OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ThenBy(item => item, StringComparer.Ordinal));

    public static string Types(IEnumerable<string> values) => string.Join(" / ", Distinct(values)
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

    public static string Versions(IEnumerable<string> values) => string.Join(" / ", Distinct(values)
        .OrderByDescending(item => item, VersionComparer).ThenBy(item => item, StringComparer.Ordinal));

    private static BigInteger? MasterKeyNumber(string value) =>
        BigInteger.TryParse(Regex.Match(value, @"\d+").Value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : null;

    public static string MasterKeys(IEnumerable<string> values) => string.Join(" / ", Distinct(values)
        .OrderByDescending(MasterKeyNumber).ThenBy(item => item, StringComparer.OrdinalIgnoreCase).ThenBy(item => item, StringComparer.Ordinal));
}
