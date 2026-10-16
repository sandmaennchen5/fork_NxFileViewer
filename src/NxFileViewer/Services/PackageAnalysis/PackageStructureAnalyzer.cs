using System;
using System.Collections.Generic;
using System.Linq;
using Emignatik.NxFileViewer.Models.TreeItems;
using Emignatik.NxFileViewer.Models.TreeItems.Impl;
using LibHac.Tools.Fs;

namespace Emignatik.NxFileViewer.Services.PackageAnalysis;

public static class PackageStructureAnalyzer
{
    private static readonly string[] SceneXmlFiles =
        { "legalinfo.xml", "nacp.xml", "programinfo.xml", "cardspec.xml" };

    public static PackageStructure Analyze(IItem rootItem) => rootItem switch
    {
        NspItem nsp => AnalyzeNspNames(nsp.ChildItems.Select(item => item.Name)),
        XciItem xci => AnalyzeXciPartitions(xci.ChildItems.Select(item => item.XciPartitionType)),
        NroFileItem => PackageStructure.Homebrew,
        _ when string.Equals(rootItem.Format, "NAX0", StringComparison.OrdinalIgnoreCase) => PackageStructure.Filesystem,
        _ => PackageStructure.Unknown
    };

    public static string GetDisplayName(string value, Localization.Keys.ILocalizationKeys keys)
    {
        if (!Enum.TryParse<PackageStructure>(value, out var structure) || !Enum.IsDefined(structure)) return value;
        return structure switch
        {
            PackageStructure.Scene => keys.PackageStructure_Scene,
            PackageStructure.Cdn => keys.PackageStructure_Cdn,
            PackageStructure.Converted => keys.PackageStructure_Converted,
            PackageStructure.Homebrew => keys.PackageStructure_Homebrew,
            PackageStructure.Incomplete => keys.PackageStructure_Incomplete,
            PackageStructure.Filesystem => keys.PackageStructure_Filesystem,
            PackageStructure.NcaOnly => keys.PackageStructure_NcaOnly,
            _ => keys.PackageStructure_Unknown
        };
    }

    public static PackageStructure AnalyzeNspNames(IEnumerable<string> fileNames)
    {
        var names = fileNames.Select(name => name.ToLowerInvariant()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var hasNca = names.Any(name => name.EndsWith(".nca") || name.EndsWith(".ncz"));
        if (!hasNca)
            return PackageStructure.Incomplete;

        if (names.Contains("authoringtoolinfo.xml"))
            return PackageStructure.Homebrew;

        if (SceneXmlFiles.All(names.Contains))
            return PackageStructure.Scene;

        var hasTicket = names.Any(name => name.EndsWith(".tik"));
        var hasCertificate = names.Any(name => name.EndsWith(".cert"));
        if (hasTicket && hasCertificate)
            return PackageStructure.Cdn;

        var containsOnlyNcas = names.All(name => name.EndsWith(".nca") || name.EndsWith(".ncz"));
        if (containsOnlyNcas) return PackageStructure.NcaOnly;
        return hasTicket || hasCertificate ? PackageStructure.Incomplete : PackageStructure.Converted;
    }

    public static PackageStructure AnalyzeXciPartitions(IEnumerable<XciPartitionType> partitions)
    {
        var types = partitions.ToHashSet();
        if (types.Contains(XciPartitionType.Secure) &&
            types.Contains(XciPartitionType.Normal) &&
            types.Contains(XciPartitionType.Update))
            return PackageStructure.Scene;

        return types.SetEquals(new[] { XciPartitionType.Secure })
            ? PackageStructure.Converted
            : PackageStructure.Incomplete;
    }
}

public enum PackageStructure
{
    Unknown,
    Scene,
    Cdn,
    Converted,
    Homebrew,
    Incomplete,
    Filesystem,
    NcaOnly
}
