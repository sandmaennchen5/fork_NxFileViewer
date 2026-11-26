using System;
using Emignatik.NxFileViewer.Localization.Keys;
using NxFileViewer.Avalonia;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services;

public sealed class MissingKeyDiagnosticsTest
{
    private static readonly ILocalizationKeys[] Languages =
        [new LocalizationKeys_EN(), new LocalizationKeys_DE(), new LocalizationKeys_FR(), new LocalizationKeys_ES()];

    [Fact]
    public void TitleKeysHaveSpecificHeadingRightsIdAndTicketGuidance()
    {
        var title = new string('a', 32);
        foreach (var keys in Languages)
        {
            var warning = MainWindow.CreateMissingKeysWarning([title], keys)!;
            Assert.Equal(keys.File_MissingTitleKeysHeading, warning.Heading);
            Assert.Equal(keys.File_MissingTitleKeysHelp, warning.Help);
            Assert.Contains(string.Format(keys.File_MissingTitleKeyEntry, title.ToUpperInvariant()), warning.Entries);
            Assert.Contains("title.keys", warning.Help);
            Assert.Equal(title, warning.KeyNames);
        }
    }

    [Fact]
    public void SystemAndTitleKeysKeepSeparateGuidanceInMixedWarnings()
    {
        const string common = "key_area_key_application_0a";
        var title = new string('b', 32);
        foreach (var keys in Languages)
        {
            var system = MainWindow.CreateMissingKeysWarning([common], keys)!;
            Assert.Equal(keys.File_MissingKeysHeader, system.Heading);
            Assert.Equal(keys.File_MissingSystemKeysHelp, system.Help);
            var mixed = MainWindow.CreateMissingKeysWarning([title, common], keys)!;
            Assert.Equal(keys.File_MissingKeysHeader, mixed.Heading);
            Assert.Contains(keys.File_MissingTitleKeysHelp, mixed.Help);
            Assert.Contains(keys.File_MissingSystemKeysHelp, mixed.Help);
            Assert.Contains(common, mixed.Entries);
            Assert.Equal(title + Environment.NewLine + common, mixed.KeyNames);
            Assert.Null(MainWindow.CreateMissingKeysWarning([], keys));
        }
    }
}
