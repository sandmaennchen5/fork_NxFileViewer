using System.IO;
using Emignatik.NxFileViewer.Services.KeysManagement;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services.KeysManagement;

public sealed class MasterKeyCatalogTest
{
    [Fact]
    public void EmbeddedCatalogPreservesExistingChecksumsAndFirmware()
    {
        var catalog = MasterKeyCatalog.Current;
        Assert.True(catalog.Checksums.Count >= 23);
        Assert.Equal(catalog.Checksums.Count, catalog.Firmware.Count);
        Assert.Equal(3540309694u, catalog.Checksums["master_key_00"]);
        Assert.Equal(4082108335u, catalog.Checksums["master_key_0f"]);
        Assert.Equal(418653682u, catalog.Checksums["master_key_16"]);
        Assert.Equal("1.0.0–2.3.0", MasterKeyFirmwareMap.GetSupportedFirmware(0));
        Assert.Equal("16.0.0", MasterKeyFirmwareMap.GetSupportedFirmware(15));
        Assert.Null(MasterKeyFirmwareMap.GetSupportedFirmware(-1));
        Assert.Null(MasterKeyFirmwareMap.GetSupportedFirmware(256));
        foreach (var entry in catalog.Firmware)
            Assert.Equal(entry.Value, MasterKeyFirmwareMap.GetSupportedFirmware(entry.Key));
    }
    [Fact]
    public void RevisionDefinesBothChecksumNameAndFirmwareWithoutParallelLists()
    {
        var catalog = MasterKeyCatalog.Parse("""{"SchemaVersion":1,"MasterKeys":{"17":{"Firmware":"example","Crc32":4294967295}}}""");
        Assert.Equal(uint.MaxValue, catalog.Checksums["master_key_17"]);
        Assert.Equal("example", catalog.Firmware[23]);
    }
    [Theory]
    [InlineData("""{"SchemaVersion":2,"MasterKeys":{}}""")]
    [InlineData("""{"SchemaVersion":1,"MasterKeys":{}}""")]
    [InlineData("""{"SchemaVersion":1,"MasterKeys":{"0G":{"Firmware":"example","Crc32":0}}}""")]
    [InlineData("""{"SchemaVersion":1,"MasterKeys":{"00":{"Firmware":"","Crc32":0}}}""")]
    [InlineData("""{"SchemaVersion":1,"MasterKeys":{"00":{"Firmware":"example","Crc32":-1}}}""")]
    [InlineData("""{"SchemaVersion":1,"MasterKeys":{"00":{"Firmware":"example","Crc32":"bad"}}}""")]
    [InlineData("""{"SchemaVersion":1,"MasterKeys":{"00":{"Firmware":"example","Crc32":0},"00":{"Firmware":"other","Crc32":1}}}""")]
    public void InvalidCatalogIsRejected(string json) => Assert.Throws<InvalidDataException>(() => MasterKeyCatalog.Parse(json));
}