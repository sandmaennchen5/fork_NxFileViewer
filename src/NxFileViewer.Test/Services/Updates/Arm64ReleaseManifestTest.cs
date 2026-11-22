using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Emignatik.NxFileViewer.Services.Updates;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services.Updates;

public sealed class Arm64ReleaseManifestTest
{
    [Fact]
    public void Arm64ExtensionKeepsLegacyPackageListCompatibleAndSelectsSixDistributions()
    {
        var legacy = new[] { "x64", "x86" }.SelectMany(architecture => new[] { false, true }
            .Select(contained => new ViewerReleasePackage(architecture, contained,
                $"Viewer{(contained ? "_self-contained" : "")}_{architecture}.zip"))).ToArray();
        var arm = new[] { false, true }.Select(contained =>
            new ViewerReleasePackage("arm64", contained, $"Viewer{(contained ? "_self-contained" : "")}_arm64.zip")).ToArray();
        var manifest = new ViewerReleaseManifest(1, ViewerPackageLayout.ApplicationId, "4.1.1-beta2", legacy)
            { Arm64Packages = arm };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(manifest);
        using var json = JsonDocument.Parse(bytes);
        Assert.Equal(4, json.RootElement.GetProperty("Packages").GetArrayLength());
        var result = ViewerUpdateMetadata.ReadRelease(bytes, "v4.1.1-beta2");
        Assert.Equal(6, result.AllPackages.Count());
        Assert.Equal(2, result.AllPackages.Count(p => p.Architecture == "arm64"));
        Assert.Throws<InvalidDataException>(() => ViewerUpdateMetadata.ReadRelease(
            JsonSerializer.SerializeToUtf8Bytes(manifest with { Arm64Packages = new[] { arm[0], arm[0] } }), "4.1.1-beta2"));
        Assert.Throws<InvalidDataException>(() => ViewerUpdateMetadata.ReadRelease(
            JsonSerializer.SerializeToUtf8Bytes(manifest with { Arm64Packages = new[] { legacy[0] } }), "4.1.1-beta2"));
    }
}
