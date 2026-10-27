namespace Emignatik.NxFileViewer.Settings;

public enum NszCompressionEngine { Nsz, Rust, RustWithNszBackup, NszWithRustBackup }
public static class NszEnginePolicy
{
    public static bool PrimaryRust(this NszCompressionEngine engine) => engine is NszCompressionEngine.Rust or NszCompressionEngine.RustWithNszBackup;
    public static bool HasBackup(this NszCompressionEngine engine) => engine is NszCompressionEngine.RustWithNszBackup or NszCompressionEngine.NszWithRustBackup;
}