using System;
using System.IO;
using Emignatik.NxFileViewer.Services.Nand;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services.Nand;

public sealed class NandSaveExplorerTest
{
    [Theory]
    [InlineData("/../outside")]
    [InlineData("/folder/../../outside")]
    [InlineData("/folder\\outside")]
    [InlineData("/C:/outside")]
    [InlineData("/file:stream")]
    [InlineData("/trailing.")]
    [InlineData("/trailing ")]
    [InlineData("/NUL.bin")]
    [InlineData("/folder/CON")]
    [InlineData("/COM1.bin")]
    public void RejectsPathsThatCannotBeSafelyExported(string path)
    {
        Assert.Throws<InvalidDataException>(() => NandSaveExplorer.ExportPath(Path.GetTempPath(), path));
    }

    [Fact]
    public void KeepsNestedFilesInsideDestination()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Assert.Equal(Path.Combine(root, "slot", "data.bin"), NandSaveExplorer.ExportPath(root, "/slot/data.bin"));
    }
}
