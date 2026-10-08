using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using LibHac;
using LibHac.Common;
using LibHac.Common.Keys;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Tools.Fs;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.Save;
using Path = System.IO.Path;

namespace Emignatik.NxFileViewer.Services.Nand;

// Operates on a read-only FAT stream. No save commits or repair operations are exposed.
public sealed class NandSaveExplorer : IDisposable
{
    private readonly StreamStorage _storage;
    private readonly SaveDataFileSystem _fileSystem;
    private readonly CancellationToken _token;
    public NandSaveExplorer(Stream source, KeySet keys, CancellationToken token = default)
    {
        _token = token;
        _storage = new StreamStorage(source, true);
        try { _fileSystem = new SaveDataFileSystem(keys, _storage, IntegrityCheckLevel.None, true); }
        catch { _storage.Dispose(); throw; }
    }
    public IReadOnlyList<NandExplorerEntry> List(string path)
    {
        _token.ThrowIfCancellationRequested();
        return _fileSystem.EnumerateEntries(path, "*", SearchOptions.Default).Select(entry =>
        {
            _token.ThrowIfCancellationRequested();
            return new NandExplorerEntry(entry.Name, entry.FullPath, entry.Type == DirectoryEntryType.Directory, 0, entry.Size);
        }).OrderByDescending(e => e.IsDirectory).ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }
    public void Export(string path, Stream destination)
    {
        using var file = new UniqueRef<IFile>();
        _fileSystem.OpenFile(ref file.Ref, path.ToU8Span(), OpenMode.Read).ThrowIfFailure();
        file.Get.GetSize(out var length).ThrowIfFailure();
        var buffer = new byte[1024 * 1024];
        long position = 0;
        while (position < length)
        {
            _token.ThrowIfCancellationRequested();
            file.Get.Read(out var read, position, buffer.AsSpan(0, (int)Math.Min(buffer.Length, length - position)), ReadOption.None).ThrowIfFailure();
            if (read <= 0) throw new InvalidDataException("Truncated save file.");
            destination.Write(buffer, 0, checked((int)read)); position += read;
        }
    }
    public static string ExportPath(string root, string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments.Any(s => s is "." or ".." || s.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || s.EndsWith('.') || s.EndsWith(' ') || IsDeviceName(s)))
            throw new InvalidDataException("Invalid save export path.");
        var prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var destination = Path.GetFullPath(Path.Combine(prefix, Path.Combine(segments)));
        if (!destination.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Save export exceeds destination.");
        return destination;
    }
    private static bool IsDeviceName(string name)
    {
        var stem = name.Split('.')[0].ToUpperInvariant();
        return stem is "CON" or "PRN" or "AUX" or "NUL" or "CONIN$" or "CONOUT$" ||
            stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal)) && stem[3] is >= '1' and <= '9';
    }
    public void ExportAll(string destination)
    {
        if (Directory.Exists(destination) || File.Exists(destination)) throw new IOException("Select a new destination.");
        var parent = Path.GetDirectoryName(Path.GetFullPath(destination)) ?? throw new ArgumentException(nameof(destination));
        var staging = Path.Combine(parent, ".nxfv-save-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try
        {
            var pending = new Stack<string>(); pending.Push("/");
            var visited = new HashSet<string>(StringComparer.Ordinal);
            while (pending.TryPop(out var path))
            {
                _token.ThrowIfCancellationRequested();
                if (!visited.Add(path) || visited.Count > 100000) throw new InvalidDataException("Invalid save directory tree.");
                foreach (var entry in List(path))
                {
                    var target = ExportPath(staging, entry.Path);
                    if (entry.IsDirectory) { Directory.CreateDirectory(target); pending.Push(entry.Path); }
                    else
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                        using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                        Export(entry.Path, output);
                    }
                }
            }
            _token.ThrowIfCancellationRequested();
            Directory.Move(staging, destination);
        }
        finally { if (Directory.Exists(staging)) Directory.Delete(staging, true); }
    }
    public void Dispose() { _fileSystem.Dispose(); _storage.Dispose(); }
}
