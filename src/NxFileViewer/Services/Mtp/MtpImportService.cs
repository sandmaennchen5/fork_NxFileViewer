using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Diagnostics;
using Emignatik.NxFileViewer.Localization;
using Emignatik.NxFileViewer.Services.BackgroundTask;
using Emignatik.NxFileViewer.Services.GlobalEvents;

namespace Emignatik.NxFileViewer.Services.Mtp;

public sealed class MtpImportService
{
    private readonly IMtpBackend _backend;
    private readonly string _cache;
    private readonly List<string> _sessions = new();
    public MtpImportService(IAppEvents events, Services.FileOpening.IFileOpeningService opening) : this(new ShellMtpBackend(), Path.Combine(AppContext.BaseDirectory, "Temp", "MTP"))
    { events.AppShuttingDown += () => { opening.SafeClose(); Cleanup(); }; }
    public MtpImportService(IMtpBackend backend, string cache) { _backend = backend; _cache = Path.GetFullPath(cache); }
    public TaskWrapper Import(MtpEntry entry, IProgressReporter reporter, CancellationToken token, string? cacheRoot = null, FileLoading.SdBatchAreas? areas = null) => new(this, entry, reporter, token, cacheRoot, areas);
    // The entire transfer uses one STA, so native COM objects never cross apartments.
    public sealed class TaskWrapper(MtpImportService owner, MtpEntry entry, IProgressReporter reporter, CancellationToken token, string? cacheRoot, FileLoading.SdBatchAreas? areas)
    {
        public string Run() => MtpSta.Run(() => owner.ImportCore(entry, reporter, token, cacheRoot, areas)).GetAwaiter().GetResult();
    }
    public static bool Supports(string name)
    {
        var extension = Path.GetExtension(name).ToLowerInvariant();
        return extension is ".nsp" or ".nsz" or ".xci" or ".xcz" or ".nca" or ".nro" or ".zip" or ".7z" or ".bin" or ".img" or ".enc"
            || (name.Length == 2 && name.All(char.IsAsciiDigit))
            || (extension.Length == 3 && extension[0] == '.' && extension[1..].All(char.IsAsciiDigit))
            || Services.Nand.NandDetection.IsCandidateName(name);
    }
    private string RelativeSource(MtpEntry entry)
    {
        var parts = new List<string>();
        MtpEntry? current = entry;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < 128 && current != null && seen.Add(current.Path); i++)
        {
            if (current.Name.StartsWith("Contents", StringComparison.OrdinalIgnoreCase))
                return parts.Count == 0 ? Path.Combine("Nintendo", "Contents") : Path.Combine("Nintendo", "Contents", Path.Combine(parts.AsEnumerable().Reverse().ToArray()));
            if (current.Name.Equals("Nintendo", StringComparison.OrdinalIgnoreCase))
                return parts.Count == 0 ? "Nintendo" : Path.Combine("Nintendo", Path.Combine(parts.AsEnumerable().Reverse().ToArray()));
            parts.Add(current.Name);
            current = _backend.Parent(current.Path);
        }
        return entry.IsDirectory ? "source" : entry.Name;
    }
    private string ImportCore(MtpEntry selected, IProgressReporter reporter, CancellationToken token, string? cacheRoot, FileLoading.SdBatchAreas? areas)
    {
        if (selected.IsDevice) throw new InvalidOperationException(LocalizationManager.Instance.Current.Keys.Mtp_SelectContent);
        token.ThrowIfCancellationRequested();
        // A FAT32 split NCA must retain all parts even when a single numeric part was selected.
        if (!selected.IsDirectory && selected.Name.Length == 2 && selected.Name.All(char.IsAsciiDigit))
        {
            var parent = _backend.Parent(selected.Path);
            if (parent?.Name.EndsWith(".nca", StringComparison.OrdinalIgnoreCase) == true) selected = parent;
        }
        var sourceRelative = RelativeSource(selected).Replace('\\', '/').TrimEnd('/');
        var plan = new List<(MtpEntry Entry, string Relative)>();
        var pending = new Stack<(MtpEntry Entry, string Relative)>(); pending.Push((selected, sourceRelative));
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var filterAreas = false;
        reporter.SetMode(true); reporter.SetText(LocalizationManager.Instance.Current.Keys.Mtp_Listing);
        while (pending.TryPop(out var item))
        {
            token.ThrowIfCancellationRequested();
            if (!visited.Add(item.Entry.Path) || visited.Count > 100000) throw new InvalidDataException("Invalid MTP folder tree.");
            if (item.Entry.IsDirectory)
            {
                var children = _backend.List(item.Entry.Path);
                if (item.Entry.Path == selected.Path)
                    filterAreas = areas?.AppliesToNames(children.Select(child => child.Name)) == true;
                if (filterAreas) children = children.Where(child => areas!.AllowsTraversal(
                    (item.Relative + "/" + child.Name)[(sourceRelative.Length + 1)..], child.IsDirectory)).ToArray();
                foreach (var child in children) pending.Push((child, item.Relative + "/" + child.Name));
            }
            else if (Supports(item.Entry.Name)) plan.Add(item);
        }
        if (plan.Count == 0) throw new InvalidDataException(LocalizationManager.Instance.Current.Keys.Mtp_NoFiles);
        var cache = Path.GetFullPath(cacheRoot ?? _cache);
        Directory.CreateDirectory(cache);
        if ((File.GetAttributes(cache) & FileAttributes.ReparsePoint) != 0) throw new IOException("MTP cache is a link.");
        var session = Path.Combine(cache, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(session);
        try
        {
            var buffer = new byte[1024 * 1024]; var clock = Stopwatch.StartNew();
            for (var index = 0; index < plan.Count; index++)
            {
                token.ThrowIfCancellationRequested();
                var file = plan[index];
                var destination = CachePath(session, file.Relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                using var input = _backend.OpenRead(file.Entry.Path);
                long length = -1;
                try { length = input.Length; } catch (Exception ex) when (ex is NotSupportedException or System.Runtime.InteropServices.COMException) { }
                if (length >= 0 && !session.StartsWith(@"\\", StringComparison.Ordinal) && new DriveInfo(Path.GetPathRoot(session)!).AvailableFreeSpace < length)
                    throw new IOException(LocalizationManager.Instance.Current.Keys.LoadingError_DiskFull, unchecked((int)0x80070070));
                using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                long copied = 0;
                void Report()
                {
                    reporter.SetMode(length < 0);
                    reporter.SetText(string.Format(LocalizationManager.Instance.Current.Keys.Mtp_Copying, file.Relative, index + 1, plan.Count, copied, Math.Max(0, length)));
                    if (length >= 0) reporter.SetPercentage(length == 0 ? 1 : Math.Clamp((double)copied / length, 0, 1));
                }
                Report(); int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    token.ThrowIfCancellationRequested(); output.Write(buffer, 0, read); copied += read;
                    if (length >= 0 && copied > length) throw new InvalidDataException("Invalid MTP file size.");
                    if (clock.ElapsedMilliseconds >= 200) { Report(); clock.Restart(); }
                }
                if (length >= 0 && copied != length) throw new InvalidDataException("Incomplete MTP transfer.");
                Report();
            }
            token.ThrowIfCancellationRequested();
            var path = CachePath(session, sourceRelative);
            var contents = FileLoading.SdCardSource.FindContents(path) ?? FileLoading.SdCardSource.ContentsForFile(path);
            if (contents != null) File.WriteAllText(Path.Combine(contents, ".nxfileviewer-mtp"), "Sphaira (MTP)");
            lock (_sessions) _sessions.Add(session);
            return path;
        }
        catch { Directory.Delete(session, true); throw; }
    }
    public string PrepareDirect(MtpEntry selected, IProgressReporter reporter, CancellationToken token, FileLoading.SdBatchAreas? areas = null)
        => MtpSta.Run(() => PrepareDirectCore(selected, reporter, token, areas)).GetAwaiter().GetResult();
    private string PrepareDirectCore(MtpEntry selected, IProgressReporter reporter, CancellationToken token, FileLoading.SdBatchAreas? areas)
    {
        var keys = LocalizationManager.Instance.Current.Keys;
        if (selected.IsDevice) throw new InvalidOperationException(keys.Mtp_SelectContent);
        if (selected.IsDirectory && RelativeSource(selected).Replace('\\', '/').StartsWith("Nintendo", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException(keys.Mtp_DirectUnsupported);
        var session = Path.Combine(_cache, "Direct", Guid.NewGuid().ToString("N"));
        var descriptors = new List<string>();
        try {
            var pending = new Stack<(MtpEntry Entry, string Relative)>();
            pending.Push((selected, selected.IsDirectory ? "source" : selected.Name));
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var filterAreas = false;
            var rootRelative = selected.IsDirectory ? "source" : selected.Name;
            reporter.SetMode(true); reporter.SetText(keys.Mtp_Listing);
            while (pending.TryPop(out var item)) {
                token.ThrowIfCancellationRequested();
                if (!visited.Add(item.Entry.Path) || visited.Count > 100000) throw new InvalidDataException("Invalid MTP folder tree.");
                if (item.Entry.IsDirectory) {
                    if (item.Entry.Name.EndsWith(".nca", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException(keys.Mtp_DirectUnsupported);
                    var children = _backend.List(item.Entry.Path);
                    if (item.Entry.Path == selected.Path)
                        filterAreas = areas?.AppliesToNames(children.Select(child => child.Name)) == true;
                    if (filterAreas) children = children.Where(child => areas!.AllowsTraversal(
                        (item.Relative + "/" + child.Name)[(rootRelative.Length + 1)..], child.IsDirectory)).ToArray();
                    foreach (var child in children) pending.Push((child, item.Relative + "/" + child.Name));
                } else if (MtpDirectSource.Supports(item.Entry.Name)) {
                    var path = CachePath(session, item.Relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllBytes(path, Array.Empty<byte>());
                    var remote = item.Entry.Path;
                    MtpDirectSource.Register(path, () => new MtpDirectStream(() => _backend.OpenRead(remote)));
                    descriptors.Add(path);
                } else if (Supports(item.Entry.Name)) throw new InvalidDataException(keys.Mtp_DirectUnsupported);
            }
            if (descriptors.Count == 0) throw new InvalidDataException(keys.Mtp_NoFiles);
            lock (_sessions) _sessions.Add(session);
            return CachePath(session, selected.IsDirectory ? "source" : selected.Name);
        } catch {
            foreach (var path in descriptors) MtpDirectSource.Remove(path);
            if (Directory.Exists(session)) Directory.Delete(session, true);
            throw;
        }
    }
    public static string CachePath(string root, string relative) => Services.Nand.NandSaveExplorer.ExportPath(root, relative.Replace('\\', '/'));
    public void Cleanup()
    {
        lock (_sessions)
        {
            foreach (var session in _sessions)
                try { if (Directory.Exists(session)) { foreach (var path in Directory.EnumerateFiles(session, "*", SearchOption.AllDirectories)) MtpDirectSource.Remove(path); Directory.Delete(session, true); } }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            _sessions.Clear();
        }
    }
}
