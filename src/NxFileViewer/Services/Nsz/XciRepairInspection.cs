using System;
using System.IO;
using System.Linq;
using System.Threading;

namespace Emignatik.NxFileViewer.Services.Nsz;

public static partial class XciPartitionIntegrity
{
    private static void ValidateRepairableRoot(Stream input)
    {
        var root = Root(input);
        if (root.Offset < 0x200 || root.Offset % 512 != 0 || root.Entries.Count(e => e.Name == "secure") != 1)
            throw new InvalidDataException("Unsupported XCI root layout.");
        var end = checked(root.Offset + root.HeaderSize);
        foreach (var entry in root.Entries.OrderBy(e => e.Offset))
        {
            if (entry.Offset < end) throw new InvalidDataException("Overlapping XCI partitions.");
            end = checked(entry.Offset + entry.Size);
        }
        // Do not offer rebuilding when it would silently drop unknown trailing data.
        if (end != input.Length) throw new InvalidDataException("Unexpected data after the XCI partitions.");
    }

    public static bool NeedsGamecardHeaderRepair(string path, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        try { VerifyGamecardHeader(path, token); return false; }
        catch (InvalidDataException) { }
        using (var input = File.OpenRead(path)) ValidateRepairableRoot(input);
        Verify(path, token);
        return true;
    }

    public static int InspectLayoutCorrection(string sourceXcz, string xci, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        using var source = File.OpenRead(sourceXcz);
        using var output = File.OpenRead(xci);
        var plan = BuildLayoutPlan(source, output, token);
        var count = plan.Changes.Count(c => c.Offset != c.Entry.Offset || c.Size != c.Entry.Size);
        if (count > 0)
        {
            ValidateRepairableRoot(output);
            _ = BuildLayoutPlan(source, output, token, verifyAllNcas: true);
        }
        return count;
    }
}
