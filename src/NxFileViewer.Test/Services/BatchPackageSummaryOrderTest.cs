using System;
using System.Linq;
using System.Text.Json;
using Emignatik.NxFileViewer.Models.Overview;
using Emignatik.NxFileViewer.Services.Integrity;
using Emignatik.NxFileViewer.Settings;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services;

public sealed class BatchPackageSummaryOrderTest
{
    private static BatchIntegrityResult Source => new(@"C:\games\multi.nsp", "NSP", "NSP", "", "", NcasIntegrity.Original, null)
    {
        ContentType = "Update / DLC / Base", Version = "65536 / 0 / 131072",
        DisplayVersion = "1.2.0 / 1.12.0 / 12.0.0", SystemVersion = "17.0.0 / 22.0.0 / 2.0.0",
        MinimumApplicationVersion = "2 / 12 / 0", SystemUpdateVersion = "2.0.0 / 12.0.0",
        MasterKey = "MasterKey 9 (master_key_09, Key Generation 10) / MasterKey 10 (master_key_0a, Key Generation 11) / MasterKey 2 (master_key_02, Key Generation 3)",
        Title = "Zulu / alpha / Beta", TitleId = "0100FFFF / 01000001 / 0100000A",
        Publisher = "Zebra / Apple", BaseTitleId = "000B / 000A", BuildId = "CCC / AAA / BBB",
        Languages = "Japanese, English, German", Distribution = "System / Download",
        FileSystemPermissions = "Write / Read", AcidSignatureValidity = "Valid / Invalid",
        Packages = new[] { new BatchPackageSummary("Zulu", "0100FFFF", "Update", "65536") { DisplayVersion = "1.2.0", SystemVersion = "17.0.0", MasterKey = "MasterKey 9 (master_key_09, Key Generation 10)", MinimumApplicationVersion = "2", Publisher = "Zebra", BaseTitleId = "000B", BuildId = "CCC", Languages = "Japanese", Distribution = "System", FileSystemPermissions = "Write", AcidSignatureValidity = "Valid" },
            new BatchPackageSummary("alpha", "01000001", "DLC", "0") { DisplayVersion = "1.12.0", SystemVersion = "22.0.0", MasterKey = "MasterKey 10 (master_key_0a, Key Generation 11)", MinimumApplicationVersion = "12", Publisher = "Apple", BaseTitleId = "000A", BuildId = "AAA", Languages = "English", Distribution = "Download", FileSystemPermissions = "Read", AcidSignatureValidity = "Invalid" },
            new BatchPackageSummary("Beta", "0100000A", "Base", "131072") { DisplayVersion = "12.0.0", SystemVersion = "2.0.0", MasterKey = "MasterKey 2 (master_key_02, Key Generation 3)", MinimumApplicationVersion = "0", BuildId = "BBB", Languages = "German" } }
    };

    [Fact]
    public void DefaultSummaryUsesTypePriorityNumericDescendingAndAlphabeticalText()
    {
        var source = Source;
        var sorted = BatchPackageSummaryOrder.Apply(source);
        Assert.Equal("Base / Update / DLC", sorted.ContentType);
        Assert.Equal("131072 / 65536 / 0", sorted.Version);
        Assert.Equal("12.0.0 / 1.12.0 / 1.2.0", sorted.DisplayVersion);
        Assert.Equal("22.0.0 / 17.0.0 / 2.0.0", sorted.SystemVersion);
        Assert.Equal("12 / 2 / 0", sorted.MinimumApplicationVersion);
        Assert.Equal("12.0.0 / 2.0.0", sorted.SystemUpdateVersion);
        Assert.StartsWith("MasterKey 10 (master_key_0a, Key Generation 11) / MasterKey 9", sorted.MasterKey);
        Assert.EndsWith("MasterKey 2 (master_key_02, Key Generation 3)", sorted.MasterKey);
        Assert.Equal("alpha / Beta / Zulu", sorted.Title);
        Assert.Equal("01000001 / 0100000A / 0100FFFF", sorted.TitleId);
        Assert.Equal("Apple / Zebra", sorted.Publisher);
        Assert.Equal("000A / 000B", sorted.BaseTitleId);
        Assert.Equal("AAA / BBB / CCC", sorted.BuildId);
        Assert.Equal("English, German, Japanese", sorted.Languages);
        Assert.Equal("Download / System", sorted.Distribution);
        Assert.Equal("Read / Write", sorted.FileSystemPermissions);
        Assert.Equal("Invalid / Valid", sorted.AcidSignatureValidity);
        Assert.Same(source.Packages, sorted.Packages);
        Assert.Equal("Zulu", sorted.Packages[0].Title);
        Assert.Equal("Update / DLC / Base", source.ContentType);
        Assert.Same(source, BatchPackageSummaryOrder.Apply(source, true));
    }

    [Fact]
    public void SummaryDoesNotDependOnPackageOrAggregateOrder()
    {
        var expected = BatchPackageSummaryOrder.Apply(Source);
        var reversed = Source with
        {
            Packages = Source.Packages.Reverse().ToArray(),
            ContentType = "Base / DLC / Update", DisplayVersion = "12.0.0 / 1.12.0 / 1.2.0",
            Version = "131072 / 0 / 65536", SystemVersion = "2.0.0 / 22.0.0 / 17.0.0",
            MasterKey = string.Join(" / ", Source.MasterKey.Split(" / ").Reverse()),
            Title = "Beta / Zulu / alpha", TitleId = "0100000A / 01000001 / 0100FFFF"
        };
        var actual = BatchPackageSummaryOrder.Apply(reversed);
        Assert.Equal(expected.ContentType, actual.ContentType);
        Assert.Equal(expected.DisplayVersion, actual.DisplayVersion);
        Assert.Equal(expected.Version, actual.Version);
        Assert.Equal(expected.SystemVersion, actual.SystemVersion);
        Assert.Equal(expected.MasterKey, actual.MasterKey);
        Assert.Equal(expected.Title, actual.Title);
        Assert.Equal(expected.TitleId, actual.TitleId);
        Assert.Same(reversed, BatchPackageSummaryOrder.Apply(reversed, true));
    }

    [Fact]
    public void SummarySupportsPrereleasesUnknownValuesAndStableTextTies()
    {
        var sorted = BatchPackageSummaryOrder.Apply(Source with
        {
            DisplayVersion = "1.0.0-beta.2 / 1.0.0 / 1.0.0-beta.12 / —",
            MasterKey = "Unknown / 9 / 10 / 2", Title = "a / A / a",
            Packages = new[] { new BatchPackageSummary("", "", "", ""), new BatchPackageSummary("", "", "", "") },
            ContentType = "Unknown / DLC / Update / Base / Base"
        });
        Assert.Equal("1.0.0 / 1.0.0-beta.12 / 1.0.0-beta.2 / —", sorted.DisplayVersion);
        Assert.Equal("10 / 9 / 2 / Unknown", sorted.MasterKey);
        Assert.Equal("A / a", sorted.Title);
        Assert.Equal("Base / Update / DLC / Unknown", sorted.ContentType);
    }

    [Fact]
    public void ProjectionAndHistoryCanSwitchBackWithoutChangingPackageNumbering()
    {
        var source = JsonSerializer.Deserialize<BatchIntegrityResult>(JsonSerializer.Serialize(Source))!;
        var sorted = Assert.Single(BatchPackageRows.Create(source, false));
        Assert.Equal("Base / Update / DLC", sorted.ContentType);
        Assert.Same(source, Assert.Single(BatchPackageRows.Create(source, false, true)));
        var separate = BatchPackageRows.Create(source, true);
        Assert.Equal(new[] { "Zulu", "alpha", "Beta" }, separate.Select(row => row.Title));
        Assert.Equal(new[] { 1, 2, 3 }, separate.Select(row => row.PackageIndex));
        Assert.False(new AppSettings().BatchSummaryPreservePackageOrder);
        var restored = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(new AppSettings { BatchSummaryPreservePackageOrder = true }))!;
        Assert.True(restored.BatchSummaryPreservePackageOrder);
    }

    [Fact]
    public void TextValuesWithSlashesAreKeptWhole()
    {
        var source = Source with { Packages = new[]
        {
            new BatchPackageSummary("Alpha / Omega", "01", "Base", "0") { Publisher = "A / Z" },
            new BatchPackageSummary("Beta", "02", "Update", "1") { Publisher = "B" }
        } };
        var summary = BatchPackageSummaryOrder.Apply(source);
        Assert.Equal("Alpha / Omega / Beta", summary.Title);
        Assert.Equal("A / Z / B", summary.Publisher);
    }
    [Fact]
    public void SinglePackagesAndFileLevelResultsAreUnchanged()
    {
        foreach (var result in new[] { Source with { Packages = Array.Empty<BatchPackageSummary>() },
            Source with { Packages = new[] { Source.Packages[0] } }, Source with { IsFirmware = true }, Source with { IsNand = true } })
            Assert.Same(result, BatchPackageSummaryOrder.Apply(result));
        var calls = 0;
        var source = Source;
        source.PropertyChanged += (_, _) => calls++;
        BatchPackageSummaryOrder.Apply(source).PackagesExpanded = true;
        Assert.Equal(0, calls);
    }
}