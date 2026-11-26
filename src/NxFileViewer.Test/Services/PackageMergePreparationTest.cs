using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NxFileViewer.Avalonia;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services;

public sealed class PackageMergePreparationTest
{
    [Fact]
    public void CollectsTitleVersionsAndAllowsSharedIdenticalContent()
    {
        var package = BatchPackageRowsTest.CreateContainer().Package!;
        var titleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var titles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var contents = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var title in package.Titles)
            MainWindow.AddPackageInputIdentity(package with { Titles = [title] },
                () => new Dictionary<string, string> { ["shared.nca"] = "same digest" }, titleIds, titles, contents);
        Assert.Equal(package.Titles.Select(title => title.TitleId + ":" + title.Version.Version).Order(), titles.Order());
        Assert.Single(contents);
    }

    [Fact]
    public void DuplicateTitleIsRejectedBeforeReadingItsContents()
    {
        var package = BatchPackageRowsTest.CreateContainer().Package!;
        var titleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var titles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var contents = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        MainWindow.AddPackageInputIdentity(package, () => new Dictionary<string, string>(), titleIds, titles, contents);
        var error = Assert.Throws<InvalidDataException>(() => MainWindow.AddPackageInputIdentity(package,
            () => throw new InvalidOperationException("Duplicate titles should be rejected first"), titleIds, titles, contents));
        Assert.Contains(package.Titles[0].TitleId, error.Message);
    }

    [Fact]
    public void ConflictingContentNamesCannotBeMerged()
    {
        var package = BatchPackageRowsTest.CreateContainer().Package!;
        var titleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var titles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var contents = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        MainWindow.AddPackageInputIdentity(package with { Titles = [package.Titles[0]] },
            () => new Dictionary<string, string> { ["shared.nca"] = "first" }, titleIds, titles, contents);
        var error = Assert.Throws<InvalidDataException>(() => MainWindow.AddPackageInputIdentity(
            package with { Titles = [package.Titles[1]] },
            () => new Dictionary<string, string> { ["SHARED.NCA"] = "different" }, titleIds, titles, contents));
        Assert.Contains("SHARED.NCA", error.Message);
    }
}
