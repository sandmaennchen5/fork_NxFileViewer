using Emignatik.NxFileViewer.Logging;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Logging;

public sealed class LogAnonymizerTest
{
    [Theory]
    [InlineData(@"C:\Users\Alice\Private Folder\My Game.nsp", "Alice", "Private Folder", "My Game")]
    [InlineData(@"\\PersonalServer\Alice\My Game.nsp", "PersonalServer", "Alice", "My Game")]
    [InlineData(@"//PersonalServer/Alice/My Game.nsp", "PersonalServer", "Alice", "My Game")]
    [InlineData(@"/home/Alice/Private Folder/My Game.nsp", "Alice", "Private Folder", "My Game")]
    [InlineData(@"file:///C:/Users/Alice/Private%20Folder/My%20Game.nsp", "Alice", "Private", "My")]
    [InlineData(@"\\?\C:\Users\Alice\My Game.nsp", "Alice", "Users", "My Game")]
    public void PathsAreRemovedIncludingNamesAndNetworkLocations(string path, string first, string second, string third)
    {
        var anonymizer = new LogAnonymizer(System.Array.Empty<string>());
        var result = anonymizer.Anonymize("Cannot open \"" + path + "\": access denied");
        Assert.DoesNotContain(first, result);
        Assert.DoesNotContain(second, result);
        Assert.DoesNotContain(third, result);
        Assert.Contains("access denied", result);
        Assert.Contains("[PATH_", result);
    }

    [Fact]
    public void RepeatedPathsAreConsistentAndSanitizingAgainIsSafe()
    {
        var anonymizer = new LogAnonymizer(new[] { "Alice", "PersonalPC" });
        var result = anonymizer.Anonymize("Open \"C:\\Users\\Alice\\Game.nsp\"\nRetry \"C:\\Users\\Alice\\Game.nsp\"\nUser Alice on PersonalPC");
        Assert.Equal(2, result.Split("[PATH_1].nsp").Length - 1);
        Assert.DoesNotContain("Alice", result);
        Assert.DoesNotContain("PersonalPC", result);
        Assert.Equal(result, anonymizer.Anonymize(result));
    }

    [Theory]
    [InlineData("\"My Personal Game.nsp\"")]
    [InlineData("My Personal Game.nsp")]
    [InlineData("Alice_Save.json")]
    public void FilenamesAreRemovedWithoutAFullPath(string value)
    {
        var result = new LogAnonymizer(System.Array.Empty<string>()).Anonymize(value);
        Assert.Contains("[FILE_", result);
        Assert.DoesNotContain("Personal", result);
        Assert.DoesNotContain("Alice_Save", result);
    }

    [Fact]
    public void DiagnosticVersionAndExceptionsRemainAndKeysAreMasked()
    {
        var result = new LogAnonymizer(new[] { "Alice" }).Anonymize(
            "Version: 4.1.1-beta2; Assembly: 4.1.1.0\nSystem.IO.IOException: Access denied\nmaster_key_16 = 0123456789ABCDEF0123456789ABCDEF");
        Assert.Contains("4.1.1-beta2", result);
        Assert.Contains("System.IO.IOException: Access denied", result);
        Assert.Contains("[REDACTED]", result);
        Assert.DoesNotContain("0123456789ABCDEF0123456789ABCDEF", result);
    }
}
