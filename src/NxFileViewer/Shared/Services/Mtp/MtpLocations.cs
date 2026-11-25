using System;
using System.Collections.Generic;
using System.Linq;

namespace Emignatik.NxFileViewer.Services.Mtp;

public static class MtpLocations
{
    public static bool IsContents(MtpEntry entry) => entry.IsDirectory &&
        (Matches(entry, "Contents") || entry.Name.StartsWith("Contents (", StringComparison.OrdinalIgnoreCase));

    public static IReadOnlyList<(MtpEntry Entry, string Label)> Discover(IMtpBackend backend, IReadOnlyList<MtpEntry> roots)
    {
        var result = new List<(MtpEntry, string)>();
        foreach (var root in roots.Where(root => IsContents(root) || Matches(root, "Games")))
            result.Add((root, root.Name));

        foreach (var sd in roots.Where(root => Matches(root, "microSD card")))
        {
            var folders = backend.List(sd.Path);
            AddContents(folders, sd.Name);
            foreach (var emu in folders.Where(folder => Matches(folder, "emuMMC")))
            foreach (var slot in backend.List(emu.Path).Where(slot => slot.IsDirectory &&
                (slot.Name.StartsWith("RAW", StringComparison.OrdinalIgnoreCase) ||
                 slot.Name.StartsWith("SD", StringComparison.OrdinalIgnoreCase))))
                AddContents(backend.List(slot.Path), $"{sd.Name}/{emu.Name}/{slot.Name}");
        }
        return result;

        void AddContents(IReadOnlyList<MtpEntry> folders, string path)
        {
            foreach (var nintendo in folders.Where(folder => Matches(folder, "Nintendo")))
            foreach (var contents in backend.List(nintendo.Path).Where(IsContents))
                result.Add((contents, $"{path}/{nintendo.Name}/{contents.Name}"));
        }
    }

    private static bool Matches(MtpEntry entry, string name) =>
        entry.IsDirectory && entry.Name.Equals(name, StringComparison.OrdinalIgnoreCase);
}
