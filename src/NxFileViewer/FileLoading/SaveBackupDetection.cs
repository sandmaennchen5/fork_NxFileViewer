using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Text.RegularExpressions;

namespace Emignatik.NxFileViewer.FileLoading;

public static class SaveBackupDetection
{
    public static bool IsBackup(string path, CancellationToken token = default) =>
        IsContentBackup(path, token) || IsLikelyLegacyBackup(path, token);

    public static bool IsLikelyLegacyBackup(string path, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if (!PackageZip.IsArchive(path)) return false;
        var inBackupFolder = Path.GetFullPath(path).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .SkipLast(1).Any(part => part.Equals("JKSV", StringComparison.OrdinalIgnoreCase) ||
                part.Equals("oldJKSV", StringComparison.OrdinalIgnoreCase));
        if (!inBackupFolder || !Regex.IsMatch(Path.GetFileNameWithoutExtension(path),
                @"^.+ - (?:\d{4}\.\d{2}\.\d{2} @ \d{2}\.\d{2}\.\d{2}|\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2})$")) return false;
        var names = PackageZip.GetFileEntries(path);
        if (names.Count == 0 || names.Any(name =>
                Path.GetFileName(name.Replace('\\', '/')).Equals(".nx_save_meta.bin", StringComparison.OrdinalIgnoreCase) ||
                Path.GetExtension(name).ToLowerInvariant() is ".nsp" or ".nsz" or ".xci" or ".xcz" or ".nca" or ".nro" or ".exe" or ".dll" or ".zip" or ".7z")) return false;
        return !IsContentBackup(path, token);
    }

    private static bool IsContentBackup(string path, CancellationToken token)
    {
        if (!PackageZip.IsArchive(path)) return false;
        var names = PackageZip.GetFileEntries(path);
        var metadata = names.Where(name => name.Replace('\\', '/').Split('/').Last() == ".nx_save_meta.bin").ToArray();
        if (metadata.Length > 0)
        {
            var valid = false;
            PackageZip.ReadEntries(path, name => metadata.Contains(name), (_, stream, length) =>
            {
                if (length != 86) return;
                var header = new byte[5];
                stream.ReadExactly(header);
                valid |= header.AsSpan().SequenceEqual(new byte[] { 0x4a, 0x4b, 0x53, 0x56, 1 });
            }, token);
            return valid;
        }
        // Older exports lack generic metadata. Require a complete known save
        // layout and matching binary headers, not merely .sav/.dat extensions.
        var normalized = names.Select(name => name.Replace('\\', '/')).ToHashSet(StringComparer.Ordinal);
        var progress = names.Where(name => name.Replace('\\', '/').EndsWith("/progress.sav", StringComparison.Ordinal))
            .Where(name =>
            {
                var prefix = name.Replace('\\', '/')[..^"progress.sav".Length];
                var slot = prefix.TrimEnd('/').Split('/').Last();
                return slot.Length == 7 && slot.StartsWith("slot_", StringComparison.Ordinal) &&
                    char.IsAsciiDigit(slot[5]) && char.IsAsciiDigit(slot[6]) &&
                    new[] { "caption.sav", "footprint.sav", "direct_file_save_related.sav" }.All(file => normalized.Contains(prefix + file));
            }).ToArray();
        if (progress.Length == 0) return false;
        var matching = false;
        PackageZip.ReadEntries(path, name => progress.Contains(name), (_, stream, length) =>
        {
            if (length < 48) return;
            var header = new byte[4];
            stream.ReadExactly(header);
            matching |= header.AsSpan().SequenceEqual(new byte[] { 4, 3, 2, 1 });
        }, token);
        return matching;
    }
}
