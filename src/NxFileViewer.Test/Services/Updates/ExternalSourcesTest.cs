using System;
using System.IO;
using System.Text.Json;
using Emignatik.NxFileViewer.Services;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services.Updates;

public sealed class ExternalSourcesTest
{
    private static ExternalSourceConfiguration Read(ExternalSourceConfiguration config) => ExternalSources.Read(JsonSerializer.SerializeToUtf8Bytes(config));

    [Fact]
    public void EmbeddedConfigurationKeepsExistingDefaults()
    {
        var config = ExternalSources.Current;
        Assert.Equal("nicoboss/nsz", config.NszRepository);
        Assert.Equal("THZoria/NxNandManager", config.NandRepository);
        Assert.Equal("https://api.github.com/repos/sandmaennchen5/fork_NxFileViewer/contents/fw/hashes?ref=master", config.FirmwareHashes.ApiUrl());
        Assert.Equal("https://raw.githubusercontent.com/blawar/titledb/master/US.en.json", config.TitleDb.RawUrl("US.en.json"));
        Assert.Equal("https://api.github.com/repos/blawar/titledb/contents/US.en.json?ref=master", config.TitleDb.ApiUrl("US.en.json"));
    }

    [Fact]
    public void ChangedRepositoryRefAndPathAffectBothChecksAndDownloads()
    {
        var config = Read(ExternalSources.Current with {
            FirmwareHashes = new("other/firmware", "release/test", "data/hashes"),
            TitleDb = new("other/titles", "beta", "regions"),
            NszRepository = "other/nsz", NandRepository = "other/nand", ExplorerRepository = "other/explorer"
        });
        Assert.Equal("https://api.github.com/repos/other/firmware/contents/data/hashes?ref=release%2Ftest", config.FirmwareHashes.ApiUrl());
        Assert.Equal("https://raw.githubusercontent.com/other/firmware/release%2Ftest/data/hashes/fw%20test.json", config.FirmwareHashes.RawUrl("fw test.json"));
        Assert.Equal("https://raw.githubusercontent.com/other/titles/beta/regions/DE.de.json", config.TitleDb.RawUrl("DE.de.json"));
        Assert.Equal("https://api.github.com/repos/other/nsz/releases/latest", ExternalSources.ReleaseApi(config.NszRepository));
        Assert.Equal("/other/nsz/releases/download/", ExternalSources.ReleaseDownloadPrefix(config.NszRepository));
        Assert.Equal("/other/nand/releases/download/", ExternalSources.ReleaseDownloadPrefix(config.NandRepository));
    }

    [Theory]
    [InlineData("http://example.org/{TitleId}")]
    [InlineData("https://user:password@example.org/{TitleId}")]
    [InlineData("https://example.org/{TitleId}#fragment")]
    [InlineData("https://example.org/no-placeholder")]
    [InlineData("https://example.org/{TitleId}/{Unknown}")]
    public void RejectsUnsafeOrMalformedTemplates(string template) => Assert.Throws<InvalidDataException>(() =>
        Read(ExternalSources.Current with { TitlePageUrl = template }));

    [Theory]
    [InlineData("../hashes")]
    [InlineData("/hashes")]
    [InlineData("hashes/")]
    [InlineData("fw\\hashes")]
    [InlineData("fw/hashes?ref=evil")]
    public void RejectsInvalidContentPaths(string path) => Assert.Throws<InvalidDataException>(() =>
        Read(ExternalSources.Current with { FirmwareHashes = ExternalSources.Current.FirmwareHashes with { Path = path } }));

    [Fact]
    public void RejectsMissingLocationsWrongSchemaAndInvalidRepositories()
    {
        Assert.Throws<InvalidDataException>(() => Read(ExternalSources.Current with { SchemaVersion = 2 }));
        Assert.Throws<InvalidDataException>(() => Read(ExternalSources.Current with { FirmwareHashes = null! }));
        Assert.Throws<InvalidDataException>(() => Read(ExternalSources.Current with { NszRepository = "https://github.com/other/nsz" }));
        Assert.Throws<InvalidDataException>(() => Read(ExternalSources.Current with { TitleDb = ExternalSources.Current.TitleDb with { Ref = "" } }));
    }

    [Fact]
    public void ChangedOnlineDefaultsAreReadWithoutChangingPersonalSettings()
    {
        var custom = Read(ExternalSources.Current with { TitlePageUrl = "https://titles.example/{TitleId}" });
        Assert.Equal("https://titles.example/{TitleId}", custom.TitlePageUrl);
        var settings = new Emignatik.NxFileViewer.Settings.AppSettings();
        Assert.Equal(ExternalSources.Current.TitlePageUrl, settings.TitlePageUrl);
        settings.TitlePageUrl = "https://personal.example/{TitleId}";
        Assert.Equal("https://personal.example/{TitleId}", settings.TitlePageUrl);
    }
}
