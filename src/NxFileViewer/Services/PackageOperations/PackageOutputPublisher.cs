using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;

namespace Emignatik.NxFileViewer.Services.PackageOperations;

/// <summary>Publish files without renaming a directory, which sync clients may keep open.</summary>
public static class PackageOutputPublisher
{
    public static void Publish(string ready, string destination, CancellationToken token,
        Action<string, string>? moveFile = null, IReadOnlyList<string>? files = null)
    {
        token.ThrowIfCancellationRequested();
        if (Directory.Exists(destination) || File.Exists(destination))
            throw new IOException("Output already exists: " + destination);
        Directory.CreateDirectory(destination);
        var marker = Path.Combine(destination, "NXFV-INCOMPLETE.txt");
        File.WriteAllText(marker, "Publication is incomplete. Validated files may be in this directory and in:\n" + ready);
        foreach (var source in files ?? Directory.GetFiles(ready, "*", SearchOption.AllDirectories))
        {
            token.ThrowIfCancellationRequested();
            var target = Path.Combine(destination, Path.GetRelativePath(ready, source));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            Retry(() => { if (moveFile != null) moveFile(source, target); else File.Move(source, target); }, token);
        }
        Retry(() => File.Delete(marker), token);
    }

    private static void Retry(Action action, CancellationToken token)
    {
        for (var attempt = 0; ; attempt++)
        {
            token.ThrowIfCancellationRequested();
            try { action(); return; }
            catch (Exception ex) when (attempt < 4 && ex is IOException or UnauthorizedAccessException)
            {
                if (token.WaitHandle.WaitOne(100 << attempt)) token.ThrowIfCancellationRequested();
            }
        }
    }
}
