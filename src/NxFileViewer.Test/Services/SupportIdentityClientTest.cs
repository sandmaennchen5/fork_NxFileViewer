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

    [Fact]
    public void LinuxIdentityIsStableAndNormalizesMachineId()
    {
        var first = SupportIdentityClient.LinuxParticipant(_ => "0123456789ABCDEF0123456789ABCDEF\n", () => throw new System.Exception());
        var second = SupportIdentityClient.LinuxParticipant(_ => "0123456789abcdef0123456789abcdef", () => throw new System.Exception());
        Assert.Equal(first, second);
        Assert.Matches("^[a-f0-9]{64}$", first);
        Assert.DoesNotContain("0123456789abcdef", first);
    }
    [Fact]
    public void LinuxIdentityUsesDbusWhenPrimaryIsUnavailable()
    {
        var expected = SupportIdentityClient.LinuxParticipant(_ => "0123456789abcdef0123456789abcdef", () => throw new System.Exception());
        var actual = SupportIdentityClient.LinuxParticipant(path => path == "/etc/machine-id"
            ? throw new System.UnauthorizedAccessException() : "0123456789abcdef0123456789abcdef", () => throw new System.Exception());
        Assert.Equal(expected, actual);
    }
    [Theory]
    [InlineData("")]
    [InlineData("uninitialized")]
    [InlineData("00000000000000000000000000000000")]
    public void LinuxIdentityFallsBackForMissingOrInvalidMachineIds(string value)
    {
        var calls = 0;
        var first = SupportIdentityClient.LinuxParticipant(_ => value, () => { calls++; return "persistent-fixture"; });
        var second = SupportIdentityClient.LinuxParticipant(_ => throw new System.IO.FileNotFoundException(), () => "persistent-fixture");
        Assert.Equal(first, second);
        Assert.Equal(1, calls);
        Assert.Matches("^[a-f0-9]{64}$", first);
    }

    [Fact]
    public void MacIdentityIsStableAcrossRestartsAndUsesCreatedUserDirectory()
    {
        var profile = System.IO.Path.Combine(System.IO.Path.GetTempPath(),"mac-identity-test-"+System.Guid.NewGuid().ToString("N"));
        try
        {
            var directory = SupportIdentityClient.UserIdentityDirectory("",profile,true);
            Assert.Equal(System.IO.Path.Combine(profile,"Library","Application Support","NxFileViewerFork"),directory);
            var first = SupportIdentityClient.MacParticipant(()=>SupportIdentityClient.PersistentUserIdentity(directory));
            var second = SupportIdentityClient.MacParticipant(()=>SupportIdentityClient.PersistentUserIdentity(directory));
            Assert.Equal(first,second);
            Assert.Matches("^[a-f0-9]{64}$",first);
            Assert.Single(System.IO.Directory.GetFiles(directory));
        }
        finally { if(System.IO.Directory.Exists(profile))System.IO.Directory.Delete(profile,true); }
    }
    [Fact]
    public void ExistingIdentityDirectoryIsRetainedAndRelativeFallbackIsRejected()
    {
        var root=System.IO.Path.GetTempPath();
        Assert.Equal(System.IO.Path.Combine(root,"NxFileViewerFork"),SupportIdentityClient.UserIdentityDirectory(root,"unused",true));
        Assert.Throws<System.IO.IOException>(()=>SupportIdentityClient.UserIdentityDirectory("","relative",true));
    }
}
