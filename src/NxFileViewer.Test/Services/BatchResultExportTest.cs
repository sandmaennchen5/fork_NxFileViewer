using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;
using Emignatik.NxFileViewer.Services.Integrity;
using Emignatik.NxFileViewer.Settings;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services;

public sealed class BatchResultExportTest
{
    [Fact]
    public void ExcelHasReadableHeadersFiltersAndLiteralIdsAndFormulas()
    {
        using var stream = new MemoryStream();
        BatchExportColumn[] columns =
        [
            new("FileName", "File", "FileName"),
            new("FilePath", "Path", "FilePath"),
            new("TitleId", "Title ID", "TitleId"),
            new("Title", "Title", "Title")
        ];
        BatchExcelWriter.Write(stream, columns,
            [new[] { "Märchen.nsp", @"C:\games\Märchen.nsp", "0100000000000001", "=1+1 & <test>" }]);
        stream.Position = 0;
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        foreach (var entry in zip.Entries)
        {
            using var part = entry.Open();
            Assert.NotNull(XDocument.Load(part).Root);
        }
        using var sheet = zip.GetEntry("xl/worksheets/sheet1.xml")!.Open();
        var xml = XDocument.Load(sheet);
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        Assert.Equal(2, xml.Descendants(ns + "row").Count());
        Assert.Empty(xml.Descendants(ns + "f"));
        Assert.Contains("0100000000000001", xml.Descendants(ns + "t").Select(e => e.Value));
        Assert.Contains("=1+1 & <test>", xml.Descendants(ns + "t").Select(e => e.Value));
        Assert.Equal("A1:D2", xml.Descendants(ns + "autoFilter").Single().Attribute("ref")!.Value);
        Assert.Equal("frozen", xml.Descendants(ns + "pane").Single().Attribute("state")!.Value);
    }

    [Fact]
    public void ExportPreferencesRoundTripAndKeepExistingDefaults()
    {
        Assert.False(new AppSettings().BatchExportVisibleColumnsOnly);
        Assert.False(new AppSettings().BatchExportExcel);
        var restored = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(new AppSettings
        { BatchExportVisibleColumnsOnly = true, BatchExportExcel = true }))!;
        Assert.True(restored.BatchExportVisibleColumnsOnly);
        Assert.True(restored.BatchExportExcel);
    }
}
