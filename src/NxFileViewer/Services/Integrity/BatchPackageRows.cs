using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace Emignatik.NxFileViewer.Services.Integrity;

public static class BatchPackageRows
{
    public static IReadOnlyList<BatchIntegrityResult> Create(BatchIntegrityResult result, bool separatePackages, bool preservePackageOrder = false) =>
        separatePackages && result.HasMultiplePackages && !result.IsFirmware && !result.IsNand
            ? result.Packages.Select((package, index) => result.ForPackage(package, index + 1)).ToArray()
            : new[] { BatchPackageSummaryOrder.Apply(result, preservePackageOrder) };
}
internal sealed class BatchRowCollection : ObservableCollection<BatchIntegrityResult>
{
    public void ReplaceAll(IEnumerable<BatchIntegrityResult> rows)
    {
        CheckReentrancy();
        Items.Clear();
        foreach (var row in rows) Items.Add(row);
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}