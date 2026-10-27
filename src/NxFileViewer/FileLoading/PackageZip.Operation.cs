using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Emignatik.NxFileViewer.FileLoading;

public static partial class PackageZip
{
    private static readonly AsyncLocal<Operation?> ActiveOperation = new();
    public static IDisposable BeginOperation() => new OperationScope();
    private sealed class OperationScope : IDisposable
    {
        private readonly Operation? _owned;
        public OperationScope()
        {
            if (ActiveOperation.Value == null) ActiveOperation.Value = _owned = new Operation();
        }
        public void Dispose()
        {
            if (_owned == null) return;
            ActiveOperation.Value = null;
            _owned.Dispose();
        }
    }
    private sealed class Operation : IDisposable
    {
        public Dictionary<string, IReadOnlyList<string>> Names { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<(string, bool, bool, bool, int), IReadOnlyList<string>> Listings { get; } = new();
        private readonly HashSet<ArchiveSession> _retained = new();
        private readonly List<(string Key, ArchiveSession Session)> _sessions = new();
        public void Retain(string key, ArchiveSession session)
        {
            if (!_retained.Add(session)) return;
            session.References++;
            _sessions.Add((key, session));
        }
        public void Dispose()
        {
            // Inner sessions can depend on files retained by outer sessions.
            try
            {
                for (var i = _sessions.Count - 1; i >= 0; i--) ReleaseSession(_sessions[i].Key, _sessions[i].Session);
            }
            finally { _sessions.Clear(); _retained.Clear(); Names.Clear(); Listings.Clear(); }
        }
    }
    private static void ReleaseSession(string key, ArchiveSession session)
    {
        lock (SessionLock)
        {
            if (--session.References != 0) return;
            Sessions.Remove(key);
            Directory.Delete(session.Directory, recursive: true);
        }
    }
    public sealed class BatchOperations : IDisposable
    {
        private IDisposable? _operation;
        private string? _archive;
        public void Select(string path)
        {
            var archive = IsMember(path) || IsArchive(path) ? Path.GetFullPath(ArchivePath(path)) : null;
            if (string.Equals(archive, _archive, StringComparison.OrdinalIgnoreCase)) return;
            Dispose();
            _archive = archive;
            if (archive != null) _operation = BeginOperation();
        }
        public void Dispose() { _operation?.Dispose(); _operation = null; _archive = null; }
    }
}
