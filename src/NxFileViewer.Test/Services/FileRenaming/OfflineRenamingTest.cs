using System.Threading.Tasks;
using Emignatik.NxFileViewer.Test.Views;
using LibHac.Ncm;
using NxFileViewer.Avalonia;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services.FileRenaming;

[Collection("Avalonia desktop")]
public sealed class OfflineRenamingTest
{
    [Theory]
    [InlineData("{WTitle}.{Ext:L}")]
    [InlineData("{WAppTitle}.{Ext:L}")]
    public Task MissingOnlineTitleFallsBackToLocalName(string pattern) =>
        AvaloniaWorkspace.RunAsync(() =>
        {
            var package = BatchPackageRowsTest.CreateContainer().Package!;
            var application = System.Linq.Enumerable.Single(package.Titles, title => title.Type == ContentMetaType.Application);
            var window = new MainWindow();
            try
            {
                var lookups = 0;
                string? Lookup(string id)
                {
                    Assert.Equal(application.TitleId, id);
                    lookups++;
                    return null;
                }

                Assert.Equal("Local game.nsp", window.CreateRenameName("Local game [0100].nsp", null, pattern,
                    package with { Titles = [application] }, Lookup));
                Assert.Equal(1, lookups);
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken);
}
