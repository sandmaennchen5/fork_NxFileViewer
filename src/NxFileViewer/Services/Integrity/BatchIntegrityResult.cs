using System.IO;
using Emignatik.NxFileViewer.Models.Overview;

namespace Emignatik.NxFileViewer.Services.Integrity;

public sealed record BatchIntegrityResult(
    string FilePath,
    string FileType,
    string PackageType,
    string Structure,
    string Compression,
    NcasIntegrity Integrity,
    string? Error)
{
    public bool IsFirmware { get; init; }
    public string? FirmwareDetails { get; init; }
    public string FileName => Path.GetFileName(FilePath);
}
