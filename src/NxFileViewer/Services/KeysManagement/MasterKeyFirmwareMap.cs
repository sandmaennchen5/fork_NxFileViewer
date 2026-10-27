namespace Emignatik.NxFileViewer.Services.KeysManagement;

public static class MasterKeyFirmwareMap
{
    public static string? GetSupportedFirmware(int masterKeyRevision) =>
        MasterKeyCatalog.Current.Firmware.TryGetValue(masterKeyRevision, out var firmware) ? firmware : null;
}