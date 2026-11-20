using System;
using System.Collections.Generic;
using System.Linq;
using Emignatik.NxFileViewer.Utils;
using Emignatik.NxFileViewer.Utils.LibHacExtensions;
using LibHac.Common.Keys;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Tools.Fs;
using LibHac.Tools.FsSystem;
using ContentType = LibHac.Ncm.ContentType;

namespace Emignatik.NxFileViewer.FileLoading.QuickFileInfoLoading;

internal static class PackageMetadataReader
{
    internal static List<Content> Read(IFileSystem partition, KeySet keySet, out bool containsNcz)
    {
        containsNcz = partition.EnumerateEntries()
            .Where(e => e.Type == DirectoryEntryType.File)
            .Any(entry => entry.Name.EndsWith(".ncz", StringComparison.OrdinalIgnoreCase));

        var contents = new List<Content>();

        foreach (var cnmt in partition.LoadCnmts(keySet))
        {
            var content = new Content(cnmt);
            contents.Add(content);

            var ncaControlEntry = cnmt.ContentEntries.FirstOrDefault(entry => entry.Type == ContentType.Control);

            if (ncaControlEntry != null)
            {
                var ncaId = ncaControlEntry.NcaId.ToStrId();

                var nacp = partition.LoadNacp(ncaId, keySet);

                if (nacp != null)
                {
                    content.NacpData = new NacpData(nacp.Value);
                }
            }
        }

        return contents;
    }
}

