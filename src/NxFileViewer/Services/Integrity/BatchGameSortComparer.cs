using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Emignatik.NxFileViewer.Services.PackageOperations;

namespace Emignatik.NxFileViewer.Services.Integrity;

public sealed class BatchGameSortComparer : IComparer
{
    private readonly Dictionary<string, string> _titles = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _order = new(StringComparer.OrdinalIgnoreCase);
    private readonly bool _alphabetical;
    public BatchGameSortComparer(IEnumerable<BatchIntegrityResult> sources, bool alphabetical)
    {
        _alphabetical = alphabetical;
        foreach (var source in sources)
        {
            foreach (var row in BatchPackageRows.Create(source, true))
            {
                var id = Id(row);
                if (!_order.ContainsKey(id)) _order[id] = _order.Count;
                if (!_titles.ContainsKey(id) || Rank(row) == 0) _titles[id] = row.Title;
            }
        }
    }
    private static string Id(BatchIntegrityResult row) =>
        PackageBatchPlanner.SourceGameId(row.BaseTitleId, row.TitleId, row.ContentType) ?? row.FilePath;
    private static int Rank(BatchIntegrityResult row) => row.ContentType switch
    { "Base" or "Application" => 0, "Update" or "Patch" => 1, "DLC" or "AddOnContent" => 2, _ => 3 };
    public int Compare(object? x, object? y)
    {
        if (x is not BatchIntegrityResult a || y is not BatchIntegrityResult b) return 0;
        var ai = Id(a); var bi = Id(b);
        int result;
        if (_alphabetical)
        {
            result = StringComparer.OrdinalIgnoreCase.Compare(_titles.GetValueOrDefault(ai, a.Title), _titles.GetValueOrDefault(bi, b.Title));
            if (result != 0) return result;
            result = StringComparer.OrdinalIgnoreCase.Compare(ai, bi);
        }
        else result = _order.GetValueOrDefault(ai, int.MaxValue).CompareTo(_order.GetValueOrDefault(bi, int.MaxValue));
        if (result != 0) return result;
        result = Rank(a).CompareTo(Rank(b)); if (result != 0) return result;
        result = StringComparer.OrdinalIgnoreCase.Compare(a.TitleId, b.TitleId); if (result != 0) return result;
        result = Nullable.Compare(b.VersionNumber, a.VersionNumber); if (result != 0) return result;
        result = StringComparer.OrdinalIgnoreCase.Compare(a.FilePath, b.FilePath);
        return result != 0 ? result : a.PackageIndex.CompareTo(b.PackageIndex);
    }
}