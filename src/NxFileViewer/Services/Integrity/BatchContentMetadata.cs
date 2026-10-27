using System.Collections.Generic;
using System.Linq;
using Emignatik.NxFileViewer.Services.Security;
using LibHac.Ncm;

namespace Emignatik.NxFileViewer.Services.Integrity;

public static class BatchContentMetadata
{
    public static string PermissionDisplay(ProgramSecurityLevel? permission) => permission switch
    {
        ProgramSecurityLevel.Safe => Localization.LocalizationManager.Instance.Current.Keys.Security_Safe,
        ProgramSecurityLevel.Unsafe => Localization.LocalizationManager.Instance.Current.Keys.Security_Unsafe,
        ProgramSecurityLevel.Dangerous => Localization.LocalizationManager.Instance.Current.Keys.Security_Dangerous,
        _ => Localization.LocalizationManager.Instance.Current.Keys.Security_Unavailable
    };
    public static LibHac.Common.Validity? Signature(IEnumerable<LibHac.Common.Validity> validities)
    {
        var items = validities.ToArray();
        if (items.Contains(LibHac.Common.Validity.Invalid)) return LibHac.Common.Validity.Invalid;
        return items.Length > 0 && items.All(value => value == LibHac.Common.Validity.Valid)
            ? LibHac.Common.Validity.Valid : null;
    }
    public static string Types(IEnumerable<ContentMetaType> types) => string.Join(" / ", types.Distinct().Select(type => type switch
    {
        ContentMetaType.Application => "Base",
        ContentMetaType.Patch => "Update",
        ContentMetaType.AddOnContent => "DLC",
        _ => type.ToString()
    }));

    // Fehlende Programmdaten dürfen nicht als sicher gelten; bekannte Risiken bleiben sichtbar.
    public static ProgramSecurityLevel? Assess(IEnumerable<ProgramSecurityLevel?> levels)
    {
        var items = levels.ToArray();
        if (items.Contains(ProgramSecurityLevel.Dangerous)) return ProgramSecurityLevel.Dangerous;
        if (items.Contains(ProgramSecurityLevel.Unsafe)) return ProgramSecurityLevel.Unsafe;
        return items.Length > 0 && items.All(level => level == ProgramSecurityLevel.Safe)
            ? ProgramSecurityLevel.Safe : null;
    }
}
