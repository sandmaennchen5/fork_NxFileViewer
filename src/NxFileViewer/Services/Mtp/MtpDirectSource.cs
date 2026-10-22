using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Windows.Threading;
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

// COM stream creation, use and disposal stay on a single dedicated STA.
internal sealed class MtpDirectStream : Stream
{
    private readonly Dispatcher _dispatcher;
    private readonly Stream _source;
    private bool _disposed;
    private readonly object _gate = new();
    public MtpDirectStream(Func<Stream> open)
    {
        Dispatcher? dispatcher = null; Stream? source = null; Exception? failure = null;
        using var ready = new ManualResetEventSlim();
        var thread = new Thread(() => {
            dispatcher = Dispatcher.CurrentDispatcher;
            try { source = open(); } catch (Exception ex) { failure = ex; }
            ready.Set(); if (failure == null) Dispatcher.Run();
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); ready.Wait();
        if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        _dispatcher = dispatcher!; _source = source!;
    }
    private T Invoke<T>(Func<T> action) { lock (_gate) { ObjectDisposedException.ThrowIf(_disposed, this); return _dispatcher.Invoke(action); } }
    public override bool CanRead => !_disposed;
    public override bool CanSeek => !_disposed;
    public override bool CanWrite => false;
    public override long Length => Invoke(() => _source.Length);
    public override long Position { get => Invoke(() => _source.Position); set => Seek(value, SeekOrigin.Begin); }
    public override int Read(byte[] buffer, int offset, int count) => Invoke(() => _source.Read(buffer, offset, count));
    public override long Seek(long offset, SeekOrigin origin) => Invoke(() => _source.Seek(offset, origin));
    public override void Flush() { }
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    protected override void Dispose(bool disposing) {
        lock (_gate) if (disposing && !_disposed) {
            _disposed = true;
            try { _dispatcher.Invoke(() => _source.Dispose()); }
            finally { _dispatcher.BeginInvokeShutdown(DispatcherPriority.Send); }
        }
        base.Dispose(disposing);
    }
}
