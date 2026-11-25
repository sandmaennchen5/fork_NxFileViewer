namespace Emignatik.NxFileViewer.Settings;

public enum PackageEngine { Rust, Kronos, RustWithKronosBackup, KronosWithRustBackup }
public static class PackageEngineSelection
{
    public static PackageEngine Primary(this PackageEngine engine) => engine is PackageEngine.Kronos or PackageEngine.KronosWithRustBackup ? PackageEngine.Kronos : PackageEngine.Rust;
    public static bool HasBackup(this PackageEngine engine) => engine is PackageEngine.RustWithKronosBackup or PackageEngine.KronosWithRustBackup;
}
