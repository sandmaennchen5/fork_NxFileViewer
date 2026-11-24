using System.Collections.Generic;
namespace Emignatik.NxFileViewer.Services.Nand;
public static class NandPartitionCatalog
{
    public static IReadOnlyList<string> Names { get; } = new[]
    {
        "BOOT0", "BOOT1", "PRODINFO", "PRODINFOF", "SAFE", "SYSTEM", "USER",
        "BCPKG2-1-Normal-Main", "BCPKG2-2-Normal-Sub", "BCPKG2-3-SafeMode-Main",
        "BCPKG2-4-SafeMode-Sub", "BCPKG2-5-Repair-Main", "BCPKG2-6-Repair-Sub"
    };
}
