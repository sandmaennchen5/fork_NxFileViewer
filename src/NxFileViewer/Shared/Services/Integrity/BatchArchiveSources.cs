using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using SharpCompress.Archives.SevenZip;
namespace Emignatik.NxFileViewer.Services.Integrity;
public static class BatchArchiveSources
{
    public sealed record Source(string Path, string DisplayPath, string? Error = null);
    public sealed record CopyProgress(string DisplayPath, long Copied, long Total);
    public static bool IsPackage(string path) => new[] { ".nsp", ".nsz", ".xci", ".xcz", ".nca", ".ncz", ".nro" }.Contains(Path.GetExtension(path).ToLowerInvariant());
    public static IEnumerable<Source> Enumerate(string source, bool recursive, bool includeArchives, string cacheDirectory, CancellationToken token, IEnumerable<string>? inputPaths = null, Func<string, bool>? keepArchive = null, Func<string, bool>? isPackage = null, Action<CopyProgress>? copyProgress = null)
    {
        isPackage ??= IsPackage;
        var cacheRoot = Path.GetFullPath(cacheDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var paths = inputPaths ?? (Directory.Exists(source) ? Directory.EnumerateFiles(source, "*", new EnumerationOptions { RecurseSubdirectories = recursive, IgnoreInaccessible = true }) : new[] { source });
        foreach (var path in paths)
        {
            token.ThrowIfCancellationRequested();
            if (Path.GetFullPath(path).StartsWith(cacheRoot, StringComparison.OrdinalIgnoreCase)) continue;
            if (isPackage(path)) { yield return new(path, path); continue; }
            if (!includeArchives) continue;
            var extension = Path.GetExtension(path).ToLowerInvariant();
            if (extension is not (".zip" or ".7z")) continue;
            if (keepArchive?.Invoke(path) == true)
            {
                yield return new(path, path);
                continue;
            }
            var info = new FileInfo(path);
            var identity = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(info.FullName + info.Length + info.LastWriteTimeUtc.Ticks)));
            var folder = Path.Combine(cacheDirectory, identity); Directory.CreateDirectory(folder);
            if (extension == ".zip")
            {
                using var archive = ZipFile.OpenRead(path);
                foreach (var entry in archive.Entries.Where(entry => isPackage(entry.FullName)))
                { yield return Extract(path, folder, entry.FullName, entry.Length, entry.Open, token, copyProgress); }
            }
            else
            {
                using var archive = SevenZipArchive.OpenArchive(path);
                using var reader = archive.ExtractAllEntries();
                while (reader.MoveToNextEntry())
                {
                    token.ThrowIfCancellationRequested();
                    if (reader.Entry.IsDirectory || !isPackage(reader.Entry.Key ?? "")) continue;
                    yield return Extract(path, folder, reader.Entry.Key!, reader.Entry.Size, reader.OpenEntryStream, token, copyProgress);
                }
            }
        }
    }
    private static Source Extract(string archive, string folder, string name, long size, Func<Stream> open, CancellationToken token,
        Action<CopyProgress>? progress)
    {
        var display = archive + " :: " + name;
        try
        {
            using var input = open();
            return new Source(CacheBatchEntry(folder, name, size, input, token,
                (copied, total) => progress?.Invoke(new CopyProgress(display, copied, total))), display);
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or SharpCompress.Common.SharpCompressException)
        {
            return new Source(display, display, error.Message);
        }
    }

    private static string CacheBatchEntry(string folder, string name, long size, Stream input, CancellationToken token, Action<long, long> progress)
    {
        // The member name is never interpreted as a local path.
        var destination = Path.Combine(folder, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(name))) + Path.GetExtension(name).ToLowerInvariant());
        if (File.Exists(destination) && new FileInfo(destination).Length == size) return destination;
        var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var output = new FileStream(temporary, FileMode.CreateNew))
            {
                var buffer = new byte[1024 * 1024]; int read;
                var nextReport = 0L;
                progress(0, size);
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    token.ThrowIfCancellationRequested();
                    output.Write(buffer, 0, read);
                    if (output.Position < nextReport || output.Position == size) continue;
                    progress(output.Position, size);
                    nextReport = output.Position + 8L * 1024 * 1024;
                }
                if (output.Length != size) throw new InvalidDataException(name);
            }
            File.Move(temporary, destination, true);
            progress(size, size);
            return destination;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
