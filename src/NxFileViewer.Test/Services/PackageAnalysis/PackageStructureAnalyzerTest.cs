using Emignatik.NxFileViewer.Services.PackageAnalysis;
using LibHac.Tools.Fs;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services.PackageAnalysis;

public class PackageStructureAnalyzerTest
{
    [Theory]
    [InlineData("title.tik")]
    [InlineData("title.cert")]
    public void TicketOrCertificateAloneIsIncomplete(string name) =>
        Assert.Equal(PackageStructure.Incomplete, PackageStructureAnalyzer.AnalyzeNspNames(new[] { "program.nca", name }));

    [Theory]
    [InlineData("program.NCA")]
    [InlineData("program.NCZ")]
    public void NcaOnlyClassificationIsCaseInsensitive(string name) =>
        Assert.Equal(PackageStructure.NcaOnly, PackageStructureAnalyzer.AnalyzeNspNames(new[] { name }));

    [Fact]
    public void LabelsAndHelpAgreeInAllLanguagesAndLegacyHistoryIsNotReclassified()
    {
        Emignatik.NxFileViewer.Localization.Keys.ILocalizationKeys[] languages =
        {
            new Emignatik.NxFileViewer.Localization.Keys.LocalizationKeys_EN(),
            new Emignatik.NxFileViewer.Localization.Keys.LocalizationKeys_DE(),
            new Emignatik.NxFileViewer.Localization.Keys.LocalizationKeys_FR(),
            new Emignatik.NxFileViewer.Localization.Keys.LocalizationKeys_ES()
        };
        foreach (var keys in languages)
        {
            Assert.Equal(keys.PackageStructure_NcaOnly, PackageStructureAnalyzer.GetDisplayName("NcaOnly", keys));
            Assert.Contains(keys.PackageStructure_NcaOnly, keys.ToolTip_PackageStructure);
            Assert.Contains(keys.PackageStructure_Incomplete, keys.ToolTip_PackageStructure);
            Assert.Equal(keys.PackageStructure_Incomplete, PackageStructureAnalyzer.GetDisplayName("Incomplete", keys));
            Assert.Equal("RAWNAND", PackageStructureAnalyzer.GetDisplayName("RAWNAND", keys));
        }
    }

    [Fact]
    public void EmptyOrNonNcaPackageIsStillStructurallyIncomplete()
    {
        Assert.Equal(PackageStructure.Incomplete, PackageStructureAnalyzer.AnalyzeNspNames(System.Array.Empty<string>()));
        Assert.Equal(PackageStructure.Incomplete, PackageStructureAnalyzer.AnalyzeNspNames(new[] { "readme.txt" }));
    }

    [Fact]
    public void AnalyzeNspNames_ClassifiesSceneRelease()
    {
        var names = new[] { "program.nca", "legalinfo.xml", "nacp.xml", "programinfo.xml", "cardspec.xml" };
        Assert.Equal(PackageStructure.Scene, PackageStructureAnalyzer.AnalyzeNspNames(names));
    }

    [Fact]
    public void AnalyzeNspNames_ClassifiesHomebrew()
    {
        var names = new[] { "program.nca", "authoringtoolinfo.xml" };
        Assert.Equal(PackageStructure.Homebrew, PackageStructureAnalyzer.AnalyzeNspNames(names));
    }

    [Fact]
    public void AnalyzeNspNames_ClassifiesCdnRip()
    {
        var names = new[] { "program.ncz", "title.tik", "title.cert" };
        Assert.Equal(PackageStructure.Cdn, PackageStructureAnalyzer.AnalyzeNspNames(names));
    }

    [Fact]
    public void AnalyzeNspNames_ClassifiesConvertedPackage()
    {
        var names = new[] { "program.nca", "control.xml" };
        Assert.Equal(PackageStructure.Converted, PackageStructureAnalyzer.AnalyzeNspNames(names));
    }

    [Fact]
    public void AnalyzeNspNames_ClassifiesNcaOnlyPackageWithoutClaimingMissingContent()
    {
        var names = new[] { "meta.nca", "program.ncz" };
        Assert.Equal(PackageStructure.NcaOnly, PackageStructureAnalyzer.AnalyzeNspNames(names));
    }

    [Fact]
    public void AnalyzeXciPartitions_ClassifiesSceneRelease()
    {
        var partitions = new[] { XciPartitionType.Update, XciPartitionType.Normal, XciPartitionType.Secure };
        Assert.Equal(PackageStructure.Scene, PackageStructureAnalyzer.AnalyzeXciPartitions(partitions));
    }

    [Fact]
    public void AnalyzeXciPartitions_ClassifiesSecureOnlyAsConverted()
    {
        Assert.Equal(PackageStructure.Converted,
            PackageStructureAnalyzer.AnalyzeXciPartitions(new[] { XciPartitionType.Secure }));
    }

    [Fact]
    public void AnalyzeXciPartitions_ClassifiesMissingPartitionsAsIncomplete()
    {
        Assert.Equal(PackageStructure.Incomplete,
            PackageStructureAnalyzer.AnalyzeXciPartitions(new[] { XciPartitionType.Normal, XciPartitionType.Secure }));
    }
}
