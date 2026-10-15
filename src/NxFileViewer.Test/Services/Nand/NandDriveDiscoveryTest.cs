using System;
using Emignatik.NxFileViewer.Services.Nand;
using Xunit;
namespace Emignatik.NxFileViewer.Test.Services.Nand;
public sealed class NandDriveDiscoveryTest {
    [Fact]
    public void EnumeratesPhysicalDevicesWithoutLettersOrReadProbesAndWithoutIndexLimit() {
        var paths = NandDriveDiscovery.PhysicalPaths(new[] { "PhysicalDrive40", "C:", "PhysicalDrive1", "PhysicalDrive0", "PhysicalDrive1", "PhysicalDrive2suffix", "PhysicalDrive" });
        Assert.Equal(new[] { @"\\.\PhysicalDrive0", @"\\.\PhysicalDrive1", @"\\.\PhysicalDrive40" }, paths);
    }
    [Fact]
    public void FriendlyDisplayRetainsDistinctDevicePathAndCapacity() {
        var choice = new NandDriveChoice(@"\\.\PhysicalDrive1", "SD GPP hekate", 12UL * 1024 * 1024 * 1024);
        Assert.Contains("SD GPP hekate", choice.DisplayName); Assert.Contains(choice.Path, choice.DisplayName);
        Assert.Contains("GiB", choice.DisplayName);
        Assert.Equal(@"\\.\PhysicalDrive1", choice.Path);
    }
}
