using System;
using System.IO;
using System.Threading;
using Emignatik.NxFileViewer.Settings;

namespace Emignatik.NxFileViewer.Services.PackageOperations;

public static class PackageEngineRetry
{
    public static T Run<T>(PackageEngine selection, Func<PackageEngine, int, T> run, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        try { return run(selection.Primary(), 0); }
        catch (Exception first) when (selection.HasBackup() && !token.IsCancellationRequested &&
            first is IOException or InvalidDataException or System.ComponentModel.Win32Exception)
        {
            token.ThrowIfCancellationRequested();
            try { return run(selection.Primary() == PackageEngine.Rust ? PackageEngine.Kronos : PackageEngine.Rust, 1); }
            catch (Exception second) when (!token.IsCancellationRequested && second is not OperationCanceledException)
            { throw new AggregateException("Both package engines failed.", first, second); }
        }
    }
}