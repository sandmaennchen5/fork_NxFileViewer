using Emignatik.NxFileViewer.Services.Security;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services.Security;

public class ProgramPermissionAnalyzerTest
{
    [Theory]
    [InlineData(0x0000000000000000, ProgramSecurityLevel.Safe)]
    [InlineData(0x0000000000001234, ProgramSecurityLevel.Safe)]
    [InlineData(0x8000000000000000, ProgramSecurityLevel.Unsafe)]
    [InlineData(0x8000000000000080, ProgramSecurityLevel.Unsafe)]
    [InlineData(ulong.MaxValue, ProgramSecurityLevel.Dangerous)]
    public void Analyze_ClassifiesFileSystemPermissions(ulong permissions, ProgramSecurityLevel expected)
    {
        Assert.Equal(expected, ProgramPermissionAnalyzer.Analyze(permissions));
    }
}
