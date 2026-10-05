namespace Emignatik.NxFileViewer.Services.Security;

public static class ProgramPermissionAnalyzer
{
    private const ulong PrivilegedFileSystemPermissionFlag = 0x8000000000000000;

    public static ProgramSecurityLevel Analyze(ulong permissionsBitmask)
    {
        if (permissionsBitmask == ulong.MaxValue)
            return ProgramSecurityLevel.Dangerous;

        return (permissionsBitmask & PrivilegedFileSystemPermissionFlag) != 0
            ? ProgramSecurityLevel.Unsafe
            : ProgramSecurityLevel.Safe;
    }
}

public enum ProgramSecurityLevel
{
    Safe,
    Unsafe,
    Dangerous,
}
