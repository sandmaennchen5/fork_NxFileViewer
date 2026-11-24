using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using LibHac.Fs;
using LibHac.FsSystem;
using LibHac.Tools.Fs;
using LibHac.Tools.FsSystem;
using Path = System.IO.Path;
namespace Emignatik.NxFileViewer.Services.Mtp;

// Local zero-byte descriptors let existing batch discovery identify remote sources.
// All file contents come from MTP; descriptors are never used for conversion or renaming.
public static class MtpDirectSource
{
    private static readonly ConcurrentDictionary<string, Source> Sources = new(StringComparer.OrdinalIgnoreCase);
    public static bool IsDirect(string path) => Sources.ContainsKey(path);
    internal static void Register(string path, Func<Stream> open) => Sources[Path.GetFullPath(path)] = new Source(open);
    internal static void Remove(string path) => Sources.TryRemove(Path.GetFullPath(path), out _);
    public static Stream OpenRead(string path) => Sources.TryGetValue(path, out var source) ? source.Open() : File.OpenRead(path);
    public static IStorage OpenStorage(string path) => IsDirect(path) ? new StreamStorage(OpenRead(path), false) : new LocalStorage(path, FileAccess.Read);
    public static long Length(string path) => Sources.TryGetValue(path, out var source) ? source.Length : new FileInfo(path).Length;
    private sealed class Source(Func<Stream> open)
    {
        private long _length = -1;
        public Stream Open() { var stream = open(); try { Interlocked.Exchange(ref _length, stream.Length); return stream; } catch { stream.Dispose(); throw; } }
        public long Length { get { var length = Interlocked.Read(ref _length); if (length >= 0) return length; using var stream = Open(); return stream.Length; } }
    }
    public static bool Supports(string name) => Path.GetExtension(name).ToLowerInvariant() is ".nsp" or ".nsz" or ".xci" or ".xcz" or ".nca";
}

