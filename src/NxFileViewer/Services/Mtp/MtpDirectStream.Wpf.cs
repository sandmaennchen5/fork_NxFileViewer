using System;
using System.IO;
using System.Threading;
using System.Windows.Threading;
namespace Emignatik.NxFileViewer.Services.Mtp;

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
