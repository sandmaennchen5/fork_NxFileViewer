using System;
using System.Collections.Generic;
using System.Linq;
using Emignatik.NxFileViewer.Models.TreeItems;
using Emignatik.NxFileViewer.Models.TreeItems.Impl;
using LibHac.Tools.Fs;

namespace Emignatik.NxFileViewer.Services.PackageAnalysis;

public static partial class PackageStructureAnalyzer
{
    public static PackageStructure Analyze(IItem rootItem) => rootItem switch
    {
        NspItem nsp => AnalyzeNspNames(nsp.ChildItems.Select(item => item.Name)),
        XciItem xci => AnalyzeXciPartitions(xci.ChildItems.Select(item => item.XciPartitionType)),
        NroFileItem => PackageStructure.Homebrew,
        _ when string.Equals(rootItem.Format, "NAX0", StringComparison.OrdinalIgnoreCase) => PackageStructure.Filesystem,
        _ => PackageStructure.Unknown
    };

}
