using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Emignatik.NxFileViewer.Models.Overview;
using Emignatik.NxFileViewer.Services.Integrity;
using Emignatik.NxFileViewer.Settings;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services;

public sealed class BatchPackageRowsTest
{
    private static BatchIntegrityResult Container => new(@"C:\games\Pokemon Violet [xxx].nsp", "NSP", "Application", "", "", NcasIntegrity.Original, null)
    {
        FileSize = 123456, Title = "Combined", TitleId = "joined", DisplayVersion = "1.0.0 / 2.0.0",
        Packages = new[]
        {
            new BatchPackageSummary("Base", "0100000000000000", "Application", "0") { DisplayVersion = "1.0.0", SystemVersion = "12.0.0", Publisher = "Publisher A" },
            new BatchPackageSummary("Update", "0100000000000800", "Patch", "65536") { DisplayVersion = "2.0.0", SystemVersion = "23.0.0", Publisher = "Publisher B" },
            new BatchPackageSummary("DLC", "0100000000001001", "AddOnContent", "0")
        }
    };

    [Fact]
    public void SeparateRowsHaveStableNamesAndIndividualMetadataButRealFilePaths()
    {
        var source = Container;
        var rows = BatchPackageRows.Create(source, true);
        Assert.Equal(new[] { "Pokemon Violet [xxx].nsp::1", "Pokemon Violet [xxx].nsp::2", "Pokemon Violet [xxx].nsp::3" }, rows.Select(row => row.FileName));
        Assert.Equal(new[] { "Base", "Update", "DLC" }, rows.Select(row => row.Title));
        Assert.Equal(source.Packages.Select(package => package.TitleId), rows.Select(row => row.TitleId));
        Assert.Equal(source.Packages.Select(package => package.Version), rows.Select(row => row.Version));
        Assert.Equal(source.Packages.Select(package => package.DisplayVersion), rows.Select(row => row.DisplayVersion));
        Assert.Equal(source.Packages.Select(package => package.SystemVersion), rows.Select(row => row.SystemVersion));
        Assert.All(rows, row => { Assert.Equal(source.FilePath, row.FilePath); Assert.Equal(source.FileSize, row.FileSize); Assert.Equal(source.Integrity, row.Integrity); Assert.False(row.HasMultiplePackages); });
        Assert.All(rows, row => { Assert.True(row.HasMultiplePackagesInSource); Assert.Equal(3, row.SourcePackageCount); });
        Assert.False((Container with { Packages = new[] { Container.Packages[0] } }).HasMultiplePackagesInSource);
        Assert.Equal("Pokemon Violet [xxx].nsp", source.FileName);
        Assert.Equal(3, source.Packages.Length);
        Assert.Same(source, Assert.Single(BatchPackageRows.Create(source, false, true)));
        Assert.Same(rows[0], Assert.Single(BatchPackageRows.Create(rows[0], true)));
        Assert.True(BatchResultFilter.Matches(rows[1], "::2 Update", "", "", false));
        Assert.False(BatchResultFilter.Matches(rows[0], "::2 Update", "", "", false));
    }

    [Fact]
    public void PresentationCopiesDoNotNotifyOriginalSubscribers()
    {
        var source = Container;
        var calls = 0;
        source.PropertyChanged += (_, _) => calls++;
        var row = BatchPackageRows.Create(source, true)[0];
        row.PackagesExpanded = true;
        Assert.Equal(0, calls);
        Assert.False(source.PackagesExpanded);
    }

    [Fact]
    public void SinglePackagesAndSpecialSourcesStaySingle()
    {
        foreach (var result in new[] { Container with { Packages = Array.Empty<BatchPackageSummary>() },
            Container with { Packages = new[] { Container.Packages[0] } }, Container with { IsFirmware = true }, Container with { IsNand = true } })
            Assert.Same(result, Assert.Single(BatchPackageRows.Create(result, true)));
        var archiveMember = Container with { FilePath = @"C:\games\bundle.zip::nested/game.nsp" };
        Assert.Equal("game.nsp::1", BatchPackageRows.Create(archiveMember, true)[0].FileName);
        Assert.Equal(archiveMember.FilePath, BatchPackageRows.Create(archiveMember, true)[0].FilePath);
    }

    [Fact]
    public void CsvAndExcelWriteOneRowPerPackage()
    {
        var rows = BatchPackageRows.Create(Container, true);
        var columns = new[] { "FileName", "TitleId", "DisplayVersion", "FilePath" }
            .Select(id => BatchResultExport.AllColumns.Single(column => column.Id == id)).ToArray();
        using var csv = new MemoryStream();
        BatchResultExport.WriteCsv(csv, rows, columns);
        var text = Encoding.UTF8.GetString(csv.ToArray());
        Assert.Equal(4, text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length);
        Assert.Contains("Pokemon Violet [xxx].nsp::3", text);
        using var xlsx = new MemoryStream();
        BatchResultExport.WriteExcel(xlsx, rows, columns);
        xlsx.Position = 0;
        using var zip = new ZipArchive(xlsx, ZipArchiveMode.Read);
        using var sheet = zip.GetEntry("xl/worksheets/sheet1.xml")!.Open();
        var xml = XDocument.Load(sheet);
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        Assert.Equal(4, xml.Descendants(ns + "row").Count());
        Assert.Contains("Pokemon Violet [xxx].nsp::2", xml.Descendants(ns + "t").Select(node => node.Value));
    }

    [Fact]
    public void SettingsPersistIndependentlyAndDefaultToCombinedRows()
    {
        Assert.False(new AppSettings().BatchSeparatePackages);
        Assert.False(new AppSettings().BatchExportSeparatePackages);
        foreach (var display in new[] { false, true })
            foreach (var export in new[] { false, true })
            {
                var settings = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(new AppSettings
                { BatchSeparatePackages = display, BatchExportSeparatePackages = export }))!;
                Assert.Equal(display, settings.BatchSeparatePackages);
                Assert.Equal(export, settings.BatchExportSeparatePackages);
            }
        var history = JsonSerializer.Deserialize<BatchIntegrityResult>(JsonSerializer.Serialize(Container))!;
        Assert.Equal(3, BatchPackageRows.Create(history, true).Count);
        Assert.Equal(0, history.PackageIndex);
    }
}