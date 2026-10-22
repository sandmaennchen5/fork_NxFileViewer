using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Emignatik.NxFileViewer.Services.FileAssociations;

/// <summary>One receiver per user and installation, listening before the UI is loaded.</summary>
public sealed class ExplorerInstance : IDisposable
{
    private readonly Mutex _mutex;
    private readonly string _pipeName;
    private readonly CancellationTokenSource _stop = new();
    public bool IsPrimary { get; }

    public ExplorerInstance(string executable)
    {
        using var user = WindowsIdentity.GetCurrent();
        var identity = user.User?.Value + "|" + Path.GetFullPath(executable).ToUpperInvariant();
        _pipeName = "NxFileViewerFork-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))[..32];
        _mutex = new Mutex(false, @"Local\" + _pipeName);
        try { IsPrimary = _mutex.WaitOne(0); }
        catch (AbandonedMutexException) { IsPrimary = true; }
    }

    public void Listen(Action<ExplorerLaunchRequest> receive, Action<Exception> reportError)
    {
        if (!IsPrimary) throw new InvalidOperationException("Only the primary instance can listen.");
        _ = Task.Run(async () => {
            while (!_stop.IsCancellationRequested)
            {
                try
                {
                    using var pipe = new NamedPipeServerStream(_pipeName, PipeDirection.InOut, 1,
                        PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                    await pipe.WaitForConnectionAsync(_stop.Token).ConfigureAwait(false);
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
                    timeout.CancelAfter(TimeSpan.FromSeconds(10));
                    var header = new byte[4];
                    await pipe.ReadExactlyAsync(header, timeout.Token).ConfigureAwait(false);
                    var length = BinaryPrimitives.ReadInt32LittleEndian(header);
                    if (length <= 0 || length > ExplorerLaunchRequest.MaximumBytes) throw new InvalidDataException("Invalid Explorer request size.");
                    var bytes = new byte[length];
                    await pipe.ReadExactlyAsync(bytes, timeout.Token).ConfigureAwait(false);
                    var request = JsonSerializer.Deserialize<ExplorerLaunchRequest>(bytes) ?? throw new InvalidDataException("Empty Explorer request.");
                    receive(ExplorerLaunchRequest.Create(request.Mode, request.Paths));
                    await pipe.WriteAsync(new byte[] { 1 }, timeout.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (_stop.IsCancellationRequested) { break; }
                catch (Exception ex) { reportError(ex); }
            }
        });
    }

    public async Task Forward(ExplorerLaunchRequest request)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        using var pipe = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await pipe.ConnectAsync(timeout.Token).ConfigureAwait(false);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(request);
        if (bytes.Length > ExplorerLaunchRequest.MaximumBytes) throw new InvalidDataException("Explorer selection is too large.");
        var header = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header, bytes.Length);
        await pipe.WriteAsync(header, timeout.Token).ConfigureAwait(false);
        await pipe.WriteAsync(bytes, timeout.Token).ConfigureAwait(false);
        var ack = new byte[1];
        await pipe.ReadExactlyAsync(ack, timeout.Token).ConfigureAwait(false);
        if (ack[0] != 1) throw new IOException("Explorer request was not accepted.");
    }

    public void Dispose()
    {
        _stop.Cancel();
        if (IsPrimary) _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}
