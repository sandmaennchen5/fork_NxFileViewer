using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows.Controls;
using System.Xml.Linq;
using Emignatik.NxFileViewer.Models.Overview;
using Emignatik.NxFileViewer.Services.Integrity;
using Emignatik.NxFileViewer.Settings;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services;

public sealed class BatchResultExportTest
{
    private static BatchIntegrityResult Result => new(@"C:\games\Märchen.nsp", "NSP", "Application", "", "", NcasIntegrity.Unchecked, "line 1;\"quoted\"\nline 2")
    { TitleId = "0100000000000001", Title = "=1+1 & <test>" };

    [Fact]
    public void VisibleExportUsesTableOrderAndExcludesHiddenAndNonTableMetadata()
    {
        var layout = JsonSerializer.Serialize(new[]
        {
            new BatchColumnState("FileName", 2, true, 100, DataGridLengthUnitType.Pixel),
            new BatchColumnState("FilePath", 0, false, 100, DataGridLengthUnitType.Pixel),
            new BatchColumnState("TitleId", 1, true, 100, DataGridLengthUnitType.Pixel)
        });
        Assert.Equal(new[] { "TitleId", "FileName" }, BatchResultExport.SelectColumns(true, layout).Select(c => c.Id));
        Assert.Equal(BatchResultExport.AllColumns, BatchResultExport.SelectColumns(false, layout));
        Assert.Equal(BatchResultExport.AllColumns, BatchResultExport.SelectColumns(true, "broken"));
        Assert.All(BatchResultExport.AllColumns, column => Assert.NotNull(typeof(BatchIntegrityResult).GetProperty(column.Property)));
    }

    [Fact]
    public void CsvSeparatesNameAndPathAndEscapesDiagnosticsWithUtf8Bom()
    {
        using var stream = new MemoryStream();
        BatchResultExport.WriteCsv(stream, new[] { Result }, new[] { "FileName", "FilePath", "Error" }.Select(id => BatchResultExport.AllColumns.Single(column => column.Id == id)).ToArray());
        Assert.Equal(new byte[] { 0xef, 0xbb, 0xbf }, stream.ToArray().Take(3));
        var csv = Encoding.UTF8.GetString(stream.ToArray());
        Assert.Contains("\"File\";\"Path\";", csv);
        Assert.Contains("\"Märchen.nsp\";\"C:\\games\\Märchen.nsp\";", csv);
        Assert.Contains("\"line 1;\"\"quoted\"\"\nline 2\"", csv);
    }

    [Fact]
    public void ExcelHasReadableHeadersFiltersAndLiteralIdsAndFormulas()
    {
        using var stream = new MemoryStream();
        var columns = BatchResultExport.AllColumns.Where(c => c.Id is "FileName" or "FilePath" or "TitleId" or "Title").ToArray();
        BatchResultExport.WriteExcel(stream, new[] { Result }, columns);
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
