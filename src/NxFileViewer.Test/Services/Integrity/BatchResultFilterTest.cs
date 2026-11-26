using System;
using System.Collections.Generic;
using Emignatik.NxFileViewer.Localization.Keys;
using NxFileViewer.Avalonia;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services.Integrity;

public sealed class BatchResultFilterTest
{
    private static readonly ILocalizationKeys Labels = new LocalizationKeys_EN();
    private static MainWindow.BatchRow Row(string type = "NSZ", string? archive = null) =>
        new("game.nsz", "")
        {
            DisplayPath = archive == null ? "game.nsz" : archive + " :: nested/game.nsz",
            Integrity = Labels.NcasIntegrity_Original,
            Naming = "Matches",
            StoredMetadata = new Dictionary<string, string>
            {
                ["RawFileType"] = type, ["Title"] = "Until Then", ["TitleId"] = "010019C023004000",
                ["Publisher"] = "Maximum Entertainment", ["MinimumSystemVersion"] = "20.0.1.0"
            }
        };

    [Theory]
    [InlineData("until maximum", "NSZ", false, true)]
    [InlineData("010019c023004000", null, false, true)]
    [InlineData("20.0.1.0", null, false, true)]
    [InlineData("until unknown", null, false, false)]
    [InlineData("", "NSP", false, false)]
    [InlineData("", null, true, false)]
    [InlineData("   ", null, false, true)]
    public void CombinesSearchTypeAndErrorFilters(string search, string? type, bool errorsOnly, bool expected) =>
        Assert.Equal(expected, MainWindow.MatchesBatchFilters(Row(), search.Split(' ', StringSplitOptions.RemoveEmptyEntries),
            type, Labels.NcasIntegrity_Original, "Matches", errorsOnly, Labels));

    [Theory]
    [InlineData("NSP", true)]
    [InlineData("NSZ", true)]
    [InlineData("XCI", true)]
    [InlineData("XCZ", true)]
    [InlineData("NRO", false)]
    [InlineData("NAND", false)]
    [InlineData("ZIP", false)]
    [InlineData("NAX0", false)]
    public void GamesFilterIncludesGameContainersInArchives(string type, bool expected) =>
        Assert.Equal(expected, MainWindow.MatchesBatchFilters(Row(type, "a.zip"), [],
            Labels.BatchTable_Games, null, null, false, Labels));

    [Theory]
    [InlineData("a.zip", "NSZ", true)]
    [InlineData("a.zip", "ZIP", true)]
    [InlineData("a.7z", "7Z", true)]
    [InlineData("a.7z", "NSP", false)]
    [InlineData("a.zip", "7Z", false)]
    public void ArchiveMembersCanBeFilteredByPackageTypeOrArchiveOrigin(string archive, string type, bool expected) =>
        Assert.Equal(expected, MainWindow.MatchesBatchFilters(Row(archive: archive), [], type, null, null, false, Labels));

    [Fact]
    public void IntegrityAndNamingCombineWithTheErrorFilter()
    {
        var row = Row() with { NamingError = "failure", Naming = "Error" };
        Assert.True(MainWindow.MatchesBatchFilters(row, [], null, Labels.NcasIntegrity_Original, "Error", true, Labels));
        Assert.False(MainWindow.MatchesBatchFilters(row, [], null, Labels.NcasIntegrity_Error, "Error", true, Labels));
        Assert.False(MainWindow.MatchesBatchFilters(row, [], null, Labels.NcasIntegrity_Original, "Matches", true, Labels));
    }
}
