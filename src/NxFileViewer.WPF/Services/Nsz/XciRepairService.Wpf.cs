using System.IO;
using Emignatik.NxFileViewer.Localization;
using Emignatik.NxFileViewer.Models.Overview;

namespace Emignatik.NxFileViewer.Services.Nsz;

public sealed partial class XciRepairService
{
    public XciRepairService(IConversionVerifier verifier) : this((path, progress, token) =>
    {
        if (verifier.Verify(path, progress, token).Integrity != NcasIntegrity.Original)
            throw new InvalidDataException(LocalizationManager.Instance.Current.Keys.XciRepair_VerificationFailed);
    })
    {
    }
}
