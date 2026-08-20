using System.IO;
using Emignatik.NxFileViewer.Models.Overview;

namespace Emignatik.NxFileViewer.Services.Integrity;

public sealed record BatchIntegrityResult(
    string FilePath,
    string FileType,
    string PackageType,
    string Compression,
    NcasIntegrity Integrity,
    string? Error)
{
    public string FileName => Path.GetFileName(FilePath);
}
