using Emignatik.NxFileViewer.Services.Integrity;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services;

public sealed class BatchVersionSortTest
{
    [Theory]
    [InlineData("2.0.0", "12.0.0")]
    [InlineData("2.0.0 / 12.0.0", "2.0.0 / 23.0.0")]
    [InlineData("2.0.0 / 12.0.0", "12.0.0")]
    [InlineData("12.0.0", "23.0.0")]
    [InlineData("1.2.0", "1.12.0")]
    [InlineData("1.0.2", "1.0.12")]
    [InlineData("1.0.0-beta.2", "1.0.0-beta.12")]
    [InlineData("1.0.0-rc.1", "1.0.0")]
    [InlineData("1.0.0.2", "1.0.0.12")]
    [InlineData("", "1.0.0")]
    [InlineData("unknown", "1.0.0")]
    public void ComparesVersionComponentsAndPrereleases(string lower, string higher)
    {
        Assert.True(new BatchVersionSortKey(lower).CompareTo(new BatchVersionSortKey(higher)) < 0);
        Assert.True(new BatchVersionSortKey(higher).CompareTo(new BatchVersionSortKey(lower)) > 0);
    }

    [Theory]
    [InlineData("v1.0.0", "1.0.0")]
    [InlineData("1.0.0+build1", "1.0.0+build2")]
    [InlineData("1.0", "1.0.0")]
    public void NormalizesNumericVersionsAndIgnoresBuildMetadata(string left, string right) =>
        Assert.Equal(0, new BatchVersionSortKey(left).CompareTo(new BatchVersionSortKey(right)));

}