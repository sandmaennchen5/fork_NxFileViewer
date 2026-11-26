using System.IO;
using NxFileViewer.Avalonia;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services.Integrity;

public sealed class BatchSourceGroupingTest
{
    [Theory]
    [InlineData(@"C:\batch\games\one.nsp", @"C:\batch\games")]
    [InlineData(@"C:\batch\bundle.zip :: one.nsp", @"C:\batch\bundle.zip")]
    [InlineData(@"C:\batch\bundle.7z :: nested/two.nsp", @"C:\batch\bundle.7z")]
    [InlineData(@"C:\batch\saves.zip", @"C:\batch\saves.zip")]
    public void GroupsByPhysicalFolderOrOuterArchive(string path, string expected) =>
        Assert.Equal(expected, new MainWindow.BatchRow(Path.GetTempPath(), "") { DisplayPath = path }.SourceGroupKey);
}
