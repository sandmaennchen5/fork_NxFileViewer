using System;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;

namespace Emignatik.NxFileViewer.Services.Integrity;

/// <summary>One instance owns one temporary logo-only ZIP; it is never a saved scan.</summary>
public sealed class ActiveBatchLogoStore(string directory, ILogger? logger = null) : IDisposable
{
    public string ArchivePath { get; } = Path.Combine(Path.GetFullPath(directory), "active-logos-" + Guid.NewGuid().ToString("N") + ".zip");
    private readonly Dictionary<string, byte[]> _pending = new();
    private readonly HashSet<string> _known = new();
    private Guid? _scanId;
    private bool _disposed;

    public bool Contains(BatchIconReference reference) => !_disposed && _scanId == reference.EntryId && _known.Contains(reference.Key);

    public BatchIconReference? StageIcon(Guid scanId, byte[]? bytes)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (bytes == null || bytes.Length == 0) return null;
        if (_scanId != null && _scanId != scanId) throw new InvalidOperationException("An active logo archive belongs to one scan.");
        _scanId = scanId;
        var key = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var added = _known.Add(key);
        if (added) _pending[key] = bytes;
        logger?.LogTrace("Active logo staged: scan={ScanId}; key={Key}; bytes={Bytes}; deduplicated={Deduplicated}; unique={Unique}; pending={Pending}", scanId, key, bytes.Length, !added, _known.Count, _pending.Count);
        return new BatchIconReference(scanId, key);
    }

    public bool Flush()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_pending.Count == 0) return false;
        logger?.LogTrace("Flushing active logos: pending={Pending}; unique={Unique}; archive={ArchivePath}", _pending.Count, _known.Count, ArchivePath);
        Directory.CreateDirectory(Path.GetDirectoryName(ArchivePath)!);
        var temp = ArchivePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            if (File.Exists(ArchivePath)) File.Copy(ArchivePath, temp);
            // Update mode accepts an empty file as a new archive; account for lost old entries explicitly.
            var rebuilt = _known.Any(key => !_pending.ContainsKey(key)) &&
                (!File.Exists(temp) || new FileInfo(temp).Length == 0);
            ZipArchive archive;
            try { archive = ZipFile.Open(temp, File.Exists(temp) ? ZipArchiveMode.Update : ZipArchiveMode.Create); }
            catch (InvalidDataException)
            {
                // This is only our disposable logo cache, never a saved scan.
                // Preserve the original until the replacement is fully written.
                File.Delete(temp);
                archive = ZipFile.Open(temp, ZipArchiveMode.Create);
                rebuilt = true;
            }
            using (var zip = archive)
                foreach (var pair in _pending)
                {
                    using var output = zip.CreateEntry("icons/" + pair.Key, CompressionLevel.NoCompression).Open();
                    output.Write(pair.Value);
                }
            File.Move(temp, ArchivePath, true);
            if (rebuilt) _known.IntersectWith(_pending.Keys);
            logger?.LogTrace("Active logo flush completed: rebuilt={Rebuilt}; unique={Unique}", rebuilt, _known.Count);
            _pending.Clear();
            return rebuilt;
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    public byte[]? ReadIcon(BatchIconReference reference)
    {
        if (!Contains(reference)) return null;
        if (_pending.TryGetValue(reference.Key, out var bytes)) return bytes;
        try
        {
            using var zip = ZipFile.OpenRead(ArchivePath);
            var entry = zip.GetEntry("icons/" + reference.Key);
            if (entry == null || entry.Length > 16 * 1024 * 1024) return null;
            using var input = entry.Open();
            using var output = new MemoryStream();
            input.CopyTo(output);
            return output.ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { return null; }
    }

    public void Dispose()
    {
        _disposed = true;
        _pending.Clear();
        _known.Clear();
        if (File.Exists(ArchivePath)) File.Delete(ArchivePath);
    }
}