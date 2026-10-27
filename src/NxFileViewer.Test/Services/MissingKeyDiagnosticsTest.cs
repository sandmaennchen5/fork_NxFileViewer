using System;
using System.Linq;
using System.Text.Json;
using Emignatik.NxFileViewer.Localization.Keys;
using Emignatik.NxFileViewer.Models.Overview;
using Emignatik.NxFileViewer.Services.Integrity;
using Emignatik.NxFileViewer.Services.KeysManagement;
using LibHac.Common.Keys;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services;

public sealed class MissingKeyDiagnosticsTest
{
    private static readonly ILocalizationKeys[] Languages = { new LocalizationKeys_EN(), new LocalizationKeys_DE(), new LocalizationKeys_FR(), new LocalizationKeys_ES() };
    [Fact]
    public void TitleKeysHaveSpecificHeadingRightsIdAndTicketGuidance()
    {
        var title = new MissingKey(new string('a', 32), KeyType.Title);
        foreach (var keys in Languages)
        {
            Assert.Equal(keys.File_MissingTitleKeysHeading, MissingKeyDiagnostics.Heading(new[] { title }, keys));
            Assert.Equal(keys.File_MissingTitleKeysHelp, MissingKeyDiagnostics.Explain(new[] { title }, keys));
            Assert.Contains(title.KeyName, MissingKeyDiagnostics.Describe(new[] { title }, keys));
            Assert.Contains("Rights", MissingKeyDiagnostics.Describe(new[] { title }, keys));
            Assert.Contains("title.keys", keys.File_MissingTitleKeysHelp);
        }
    }
    [Fact]
    public void CommonAndMixedKeysKeepBothKindsDistinctAndLegacyHistoryStaysGeneric()
    {
        var common = new MissingKey("key_area_key_application_0a", KeyType.Common);
        var title = new MissingKey(new string('b', 32), KeyType.Title);
        foreach (var keys in Languages)
        {
            Assert.Equal(keys.File_MissingKeysHeader, MissingKeyDiagnostics.Heading(new[] { common }, keys));
            Assert.Equal(keys.File_MissingSystemKeysHelp, MissingKeyDiagnostics.Explain(new[] { common }, keys));
            Assert.Equal(keys.File_MissingKeysHeader, MissingKeyDiagnostics.Heading(new[] { title, common }, keys));
            Assert.Contains(keys.File_MissingTitleKeysHelp, MissingKeyDiagnostics.Explain(new[] { title, common }, keys));
            Assert.Contains(keys.File_MissingSystemKeysHelp, MissingKeyDiagnostics.Explain(new[] { title, common }, keys));
            Assert.Equal(keys.File_MissingKeysHeader, MissingKeyDiagnostics.Heading(Array.Empty<MissingKey>(), keys));
        }
    }
    [Fact]
    public void HistoryRoundtripPreservesDiagnosticTypesAndLegacyRecordsRemainReadable()
    {
        var result = new BatchIntegrityResult("game.nsz", "NSZ", "NSZ", "NcaOnly", "Solid", NcasIntegrity.Error, null)
        {
            MissingKeyDetails = new[] { new MissingKey(new string('a',32), KeyType.Title), new MissingKey("master_key_0a", KeyType.Common) }
        };
        var json = JsonSerializer.Serialize(result);
        var restored = JsonSerializer.Deserialize<BatchIntegrityResult>(json)!;
        Assert.Equal(KeyType.Title, restored.MissingKeyDetails[0].KeyType);
        Assert.Equal(KeyType.Common, restored.MissingKeyDetails[1].KeyType);
        Assert.Equal(result.MissingKeyDetails.Select(k => k.KeyName), restored.MissingKeyDetails.Select(k => k.KeyName));
        Assert.Empty(JsonSerializer.Deserialize<BatchIntegrityResult>(JsonSerializer.Serialize(result with { MissingKeyDetails = Array.Empty<MissingKey>() }))!.MissingKeyDetails);
    }
}
