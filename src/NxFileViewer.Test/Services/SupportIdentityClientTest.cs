using Emignatik.NxFileViewer.Services;
using Xunit;
namespace Emignatik.NxFileViewer.Test.Services;
public class SupportIdentityClientTest
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("http://example.test/report")]
    [InlineData("ftp://example.test/report")]
    [InlineData("https://user:password@example.test/report")]
    [InlineData("https://example.test/report?participant=example")]
    [InlineData("https://example.test/report#fragment")]
    [InlineData("/relative/report")]
    public void RejectsNonCanonicalServiceEndpoints(string? value) => Assert.Null(SupportIdentityClient.Endpoint(value));
    [Fact]
    public void AcceptsHttpsServiceWithoutCredentialsQueryOrFragment() =>
        Assert.Equal("https://example.test/report.php", SupportIdentityClient.Endpoint("https://example.test/report.php")?.AbsoluteUri);
}
