using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Emignatik.NxFileViewer.Test.Views;
using NxFileViewer.Avalonia;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Models;

[Collection("Avalonia desktop")]
public sealed class OverviewTraversalTest
{
    [Fact]
    public Task ContentTreePreservesEntryOrderAndSizeAndDefersReadingNcaContents() =>
        AvaloniaWorkspace.RunAsync(() =>
        {
            var package = new PackageFile("archive.nsp", "NSP", 400, [
                new PackageEntry("program.nca", 300), new PackageEntry("ticket.tik", 100)
            ], [], false, null);
            var window = new MainWindow();
            try
            {
                window.ShowContentTree(package);
                var tree = window.FindControl<TreeView>("Entries")!;
                var root = Assert.IsType<MainWindow.ContentNode>(Assert.Single(tree.Items));
                Assert.Same(root, tree.SelectedItem);
                Assert.Equal(400, root.Size);
                Assert.Equal(new[] { "program.nca", "ticket.tik" }, root.Children.Select(node => node.Name));
                Assert.Equal(MainWindow.ContentKind.Placeholder, Assert.Single(root.Children[0].Children).Kind);
                Assert.Empty(root.Children[1].Children);
                Assert.Equal(300, root.Children[0].Size);
                Assert.False(root.Children[0].Loaded);
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken);

    [Fact]
    public Task WideTreeBuildsOneNodePerPackageEntry() =>
        AvaloniaWorkspace.RunAsync(() =>
        {
            var entries = Enumerable.Range(0, 2000).Select(index => new PackageEntry($"fixture-{index}.dat", index)).ToArray();
            var window = new MainWindow();
            try
            {
                window.ShowContentTree(new PackageFile("archive.nsp", "NSP", 5000, entries, [], false, null));
                var root = Assert.IsType<MainWindow.ContentNode>(Assert.Single(window.FindControl<TreeView>("Entries")!.Items));
                Assert.Equal(entries.Select(entry => entry.Name), root.Children.Select(node => node.Name));
                Assert.All(root.Children, node => Assert.Empty(node.Children));
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken);
}
