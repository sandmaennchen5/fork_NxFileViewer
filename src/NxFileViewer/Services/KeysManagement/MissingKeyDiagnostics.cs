using System;
using System.Collections.Generic;
using System.Linq;
using Emignatik.NxFileViewer.Localization.Keys;
using Emignatik.NxFileViewer.Models.Overview;
using LibHac.Common.Keys;

namespace Emignatik.NxFileViewer.Services.KeysManagement;

public static class MissingKeyDiagnostics
{
    public static string Heading(IEnumerable<MissingKey> missing, ILocalizationKeys keys)
    {
        var entries = missing.ToArray();
        return entries.Length > 0 && entries.All(key => key.KeyType == KeyType.Title)
            ? keys.File_MissingTitleKeysHeading : keys.File_MissingKeysHeader;
    }
    public static string Explain(IEnumerable<MissingKey> missing, ILocalizationKeys keys)
    {
        var entries = missing.ToArray();
        var parts = new List<string>();
        if (entries.Length == 0 || entries.Any(key => key.KeyType == KeyType.Title)) parts.Add(keys.File_MissingTitleKeysHelp);
        if (entries.Length == 0 || entries.Any(key => key.KeyType != KeyType.Title)) parts.Add(keys.File_MissingSystemKeysHelp);
        return string.Join(Environment.NewLine + Environment.NewLine, parts);
    }
    public static string Describe(IEnumerable<MissingKey> missing, ILocalizationKeys keys) => string.Join(Environment.NewLine,
        missing.Distinct().Select(key => "• " + (key.KeyType == KeyType.Title
            ? string.Format(keys.File_MissingTitleKeyEntry, key.KeyName)
            : string.Format(keys.ToolTip_KeyMissing, key.KeyName, key.KeyType))));
}
