using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
namespace Emignatik.NxFileViewer.Services.Mtp;

// Native COM streams are created, read and disposed on one STA with a Windows message pump.
internal sealed class MtpDirectStream : Stream
{
    private readonly ConcurrentQueue<Action> _requests = new();
    private readonly AutoResetEvent _wake = new(false);
    private readonly Thread _worker;
    private Stream _source = null!;
    private volatile bool _stopping;
    private bool _disposed;
    private readonly object _gate = new();
    public MtpDirectStream(Func<Stream> open)
    {
        _worker = new Thread(Run) { IsBackground = true };
        if (OperatingSystem.IsWindows()) _worker.SetApartmentState(ApartmentState.STA);
        _worker.Start();
        try { Invoke(() => { _source = open(); return 0; }); }
        catch { Stop(); throw; }
    }
    private void Run()
    {
        var handles = new[] { _wake.SafeWaitHandle.DangerousGetHandle() };
        while (!_stopping)
        {
            while (_requests.TryDequeue(out var request)) request();
            if (_stopping) break;
            if (OperatingSystem.IsWindows())
            {
                MsgWaitForMultipleObjectsEx(1, handles, uint.MaxValue, 0x04ff, 0x0004);
                while (PeekMessage(out var message, IntPtr.Zero, 0, 0, 1)) { TranslateMessage(ref message); DispatchMessage(ref message); }
            }
            else _wake.WaitOne();
        }
    }
    private T Invoke<T>(Func<T> action)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            _requests.Enqueue(() => { try { completion.SetResult(action()); } catch (Exception ex) { completion.SetException(ex); } });
            _wake.Set();
            try { return completion.Task.GetAwaiter().GetResult(); }
            catch (COMException error) { throw new IOException(error.Message, error); }
        }
    }
    private void Stop() { _stopping = true; _wake.Set(); _worker.Join(); _wake.Dispose(); }
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
    protected override void Dispose(bool disposing)
    {
        lock (_gate) if (disposing && !_disposed)
        {
            try { Invoke(() => { _source.Dispose(); return 0; }); }
            finally { _disposed = true; Stop(); }
        }
        base.Dispose(disposing);
    }
    [StructLayout(LayoutKind.Sequential)] private struct NativeMessage { public IntPtr Window; public uint Message; public UIntPtr WParam; public IntPtr LParam; public uint Time; public int X; public int Y; public uint Private; }
    [DllImport("user32.dll", SetLastError = true)] private static extern uint MsgWaitForMultipleObjectsEx(uint count, IntPtr[] handles, uint timeout, uint wakeMask, uint flags);
    [DllImport("user32.dll", EntryPoint = "PeekMessageW")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool PeekMessage(out NativeMessage message, IntPtr window, uint min, uint max, uint remove);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool TranslateMessage(ref NativeMessage message);
    [DllImport("user32.dll", EntryPoint = "DispatchMessageW")] private static extern IntPtr DispatchMessage(ref NativeMessage message);
}
