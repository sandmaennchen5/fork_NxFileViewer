using System.Linq;
using System.Text.Json;
using Emignatik.NxFileViewer.Services.Integrity;
using Emignatik.NxFileViewer.Settings;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services;

public sealed class BatchPackageSummaryOrderTest
{
    [Fact]
    public void SummarySortsTypesAndVersionsWithoutReorderingParts()
    {
        var row = BatchPackageRowsTest.CreateContainer();
        Assert.Equal("Base / Update / DLC", row.ContentType);
        Assert.Equal("131072 / 65536 / 0", row.Version);
        Assert.Equal(new[] { 1, 2, 3 }, row.PackageDetails.Select(part => part.PartNumber));
        Assert.Equal(new[] { "65536", "0", "131072" }, row.PackageDetails.Select(part => part.Version));
        Assert.Equal("65536 / 0 / 131072", (row with { PreservePackageOrder = true }).Version);
    }

    [Fact]
    public void SummarySupportsPrereleasesStableTextTiesAndSlashesInTitles()
    {
        Assert.Equal("1.0.0 / 1.0.0-beta.12 / 1.0.0-beta.2 / —",
            BatchSummaryValues.Versions(["1.0.0-beta.2", "1.0.0", "—", "1.0.0-beta.12"]));
        Assert.Equal("Alpha / Omega / Beta", BatchSummaryValues.Text(["Beta", "Alpha / Omega"]));
        Assert.Equal("A / a", BatchSummaryValues.Text(["a", "A", "a"]));
        Assert.Equal("10 / 9 / 2 / Unknown", BatchSummaryValues.MasterKeys(["Unknown", "9", "10", "2"]));
    }

    [Fact]
    public void SummaryAndSeparateRowPreferencesRoundTripIndependently()
    {
        Assert.False(new AppSettings().BatchSummaryPreservePackageOrder);
        Assert.False(new AppSettings().BatchSeparatePackages);
        Assert.False(new AppSettings().BatchExportSeparatePackages);
        var json = JsonSerializer.Serialize(new AppSettings { BatchSummaryPreservePackageOrder = true, BatchExportSeparatePackages = true });
        var settings = JsonSerializer.Deserialize<AppSettings>(json)!;
        Assert.True(settings.BatchSummaryPreservePackageOrder);
        Assert.True(settings.BatchExportSeparatePackages);
        Assert.False(settings.BatchSeparatePackages);
    }
}
