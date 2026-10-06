using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using SharpCompress.Archives.SevenZip;

namespace Emignatik.NxFileViewer.FileLoading;

public static class PackageZip
{
    public const string Separator = "::";
    public static bool IsMember(string path) => path.Contains(Separator, StringComparison.Ordinal);
    public static string ArchivePath(string path) => path.Split(Separator, 2)[0];
    public static string MemberPath(string archive, string entry) => archive + Separator + entry;
    public static string DisplayFileType(string path)
    {
        var type = Path.GetExtension(path).TrimStart('.').ToUpperInvariant();
        if (!IsMember(path)) return type;
        var archiveType = Path.GetExtension(ArchivePath(path)).TrimStart('.').ToUpperInvariant();
        return type + " (" + (archiveType == "7Z" ? "7z" : archiveType) + ")";
    }
    public static bool IsArchive(string path) => Path.GetExtension(path).ToLowerInvariant() is ".zip" or ".7z";
    public static IReadOnlyList<string> GetFileEntries(string path)
    {
        if (Path.GetExtension(path).Equals(".7z", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                using var archive = SevenZipArchive.OpenArchive(path);
                return archive.Entries.Where(e => !e.IsDirectory).Select(e => e.Key!).ToArray();
            }
            catch (SharpCompress.Common.SharpCompressException ex) { throw new InvalidDataException("Invalid or unsupported 7z archive.", ex); }
        }
        using var zip = ZipFile.OpenRead(path);
        return zip.Entries.Where(e => !e.FullName.EndsWith('/')).Select(e => e.FullName).ToArray();
    }
    // Read solid 7z entries sequentially, reusing the decoder across the whole archive.
    public static void ReadEntries(string path, Func<string, bool> include, Action<string, Stream, long> read,
        CancellationToken token = default, bool stopAfterMatch = false)
    {
        if (Path.GetExtension(path).Equals(".7z", StringComparison.OrdinalIgnoreCase))
        {
            using var archive = SevenZipArchive.OpenArchive(path);
            using var reader = archive.ExtractAllEntries();
            while (reader.MoveToNextEntry())
            {
                token.ThrowIfCancellationRequested();
                if (reader.Entry.IsDirectory) continue;
                using var input = reader.OpenEntryStream();
                if (include(reader.Entry.Key!))
                {
                    read(reader.Entry.Key!, input, reader.Entry.Size);
                    if (stopAfterMatch) return;
                }
                else
                {
                    var buffer = new byte[128 * 1024];
                    while (input.Read(buffer, 0, buffer.Length) > 0) token.ThrowIfCancellationRequested();
                }
            }
            return;
        }
        using var zip = ZipFile.OpenRead(path);
        foreach (var entry in zip.Entries.Where(e => !e.FullName.EndsWith('/') && include(e.FullName)))
        {
            token.ThrowIfCancellationRequested();
            using var input = entry.Open();
            read(entry.FullName, input, entry.Length);
        }
    }
    public static IReadOnlyList<string> GetEntries(string path, bool includeNca = true)
    {
        var entries = GetFileEntries(path).Where(e => IsSupported(e, includeNca)).ToArray();
        foreach (var entry in entries) ValidateName(entry);
        if (entries.Distinct(StringComparer.Ordinal).Count() != entries.Length)
            throw new InvalidDataException("Duplicate package names in archive.");
        return entries;
    }
    private static bool IsSupported(string name, bool includeNca) => Path.GetExtension(name).ToLowerInvariant() is
        ".nsp" or ".nsz" or ".xci" or ".xcz" || (includeNca && name.EndsWith(".nca", StringComparison.OrdinalIgnoreCase));
    private static void ValidateName(string name)
    {
        var normalized = name.Replace('\\', '/');
        if (normalized.StartsWith('/') || normalized.Split('/').Any(p => p is ".." or "." || p.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
            throw new InvalidDataException("Invalid package name in ZIP.");
    }
    public static ExtractedPackage Extract(string archive, string entry, CancellationToken token = default, string? tempRoot = null)
    {
        ValidateName(entry);
        var matches = GetFileEntries(archive).Where(e => e == entry).ToArray();
        if (matches.Length != 1 || !IsSupported(entry, true)) throw new InvalidDataException("Package entry not found or ambiguous.");
        token.ThrowIfCancellationRequested();
        var root = Path.GetFullPath(tempRoot ?? Path.Combine(AppContext.BaseDirectory, "Temp", "ZIP"));
        Directory.CreateDirectory(root);
        if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0) throw new IOException("ZIP temporary directory is a link.");
        var directory = Path.Combine(root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var result = new ExtractedPackage(directory, Path.Combine(directory, entry.Replace('\\', '/').Split('/')[^1]));
        try
        {
            ReadEntries(archive, name => name == entry, (_, input, size) =>
            {
                using var output = new FileStream(result.FilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                var buffer = new byte[1024 * 1024];
                long total = 0;
                int count;
                while ((count = input.Read(buffer)) != 0)
                {
                    token.ThrowIfCancellationRequested();
                    total += count;
                    if (total > size) throw new InvalidDataException("Invalid archive entry size.");
                    output.Write(buffer, 0, count);
                }
                if (total != size) throw new InvalidDataException("Truncated archive entry.");
            }, token, stopAfterMatch: true);
            return result;
        }
        catch { result.Dispose(); throw; }
    }
}

public sealed class ExtractedPackage : IDisposable
{
    private readonly string _directory;
    internal ExtractedPackage(string directory, string filePath) { _directory = directory; FilePath = filePath; }
    public string FilePath { get; }
    public void Dispose()
    {
        // Only the unique, directly created directory is removed; archive paths are never extracted.
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}
