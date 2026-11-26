using System.Linq;
using System.Threading.Tasks;
using Emignatik.NxFileViewer.Test.Views;
using NxFileViewer.Avalonia;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services.FileRenaming;

[Collection("Avalonia desktop")]
public sealed class SuperPackageRenamingTest
{
    [Theory]
    [InlineData("NSP")]
    [InlineData("NSZ")]
    [InlineData("XCI")]
    [InlineData("XCZ")]
    public Task MultipackageUsesCountsAndHighestNonDlcVersion(string type) =>
        AvaloniaWorkspace.RunAsync(() =>
        {
            var package = BatchPackageRowsTest.CreateContainer().Package! with { Type = type };
            var window = new MainWindow();
            try
            {
                var name = window.CreateRenameName("Multi." + type, null,
                    "{GameCount}G-{UpdateCount}U-{DlcCount}D-[v{PackageVerNum}].{Ext:L}", package);
                Assert.Equal("1G-1U-1D-[v131072]." + type.ToLowerInvariant(), name);
                var reversed = package with { Titles = package.Titles.Reverse().ToArray() };
                Assert.Equal(name, window.CreateRenameName("Multi." + type, null,
                    "{GameCount}G-{UpdateCount}U-{DlcCount}D-[v{PackageVerNum}].{Ext:L}", reversed));
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken);
}
