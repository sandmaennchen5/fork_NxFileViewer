using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Emignatik.NxFileViewer.FileLoading;
using LibHac;
using LibHac.Common;
using LibHac.Common.Keys;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.Tools.Fs;
using LibHac.Tools.FsSystem;
using Path = System.IO.Path;

namespace NxFileViewer.Avalonia;

internal static class Nax0Contents
{
    internal static string LogicalPath(string path)
    {
        var parent = Path.GetDirectoryName(path);
        return Path.GetFileName(path) == "00" &&
            string.Equals(Path.GetExtension(parent), ".nca", StringComparison.OrdinalIgnoreCase)
            ? parent! : path;
    }

    internal static bool IsCandidate(string path) =>
        Path.GetExtension(path).Equals(".nax0", StringComparison.OrdinalIgnoreCase) ||
        LogicalPath(path) != path;

    internal static bool IsNax0(string path) =>
        (IsCandidate(path) || Path.GetExtension(path).Equals(".nca", StringComparison.OrdinalIgnoreCase)) &&
        SdCardSource.IsNax0(path);

    internal static T Read<T>(string path, KeySet keys, Func<Stream, T> read)
    {
        var source = LogicalPath(Path.GetFullPath(path));
        var contents = SdCardSource.ContentsForFile(source)
            ?? throw new InvalidDataException("NAX0 requires its original path under Nintendo/Contents.");
        var chunkSize = File.Exists(Path.Combine(source, "01"))
            ? new FileInfo(Path.Combine(source, "00")).Length : 0xffff0000L;
        using var encrypted = OpenFileSystem(contents, keys, chunkSize);
        var relative = "/" + Path.GetRelativePath(contents, source).Replace('\\', '/');
        using var file = new UniqueRef<IFile>();
        encrypted.OpenFile(ref file.Ref, relative.ToU8Span(), OpenMode.Read).ThrowIfFailure();
        using var stream = file.Get.AsStream();
        return read(stream);
    }

    internal static IEnumerable<DirectoryEntryEx> Enumerate(string contents, CancellationToken token)
    {
        var root = Path.Combine(contents, "registered");
        var paths = Directory.EnumerateFiles(root, "*.nca", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateDirectories(root, "*.nca", SearchOption.AllDirectories)
                .Where(path => File.Exists(Path.Combine(path, "00"))));
        foreach (var path in paths)
        {
            token.ThrowIfCancellationRequested();
            var size = Directory.Exists(path)
                ? Directory.EnumerateFiles(path).Where(chunk => Path.GetFileName(chunk) is { Length: 2 } name && name.All(char.IsAsciiDigit))
                    .Sum(chunk => new FileInfo(chunk).Length)
                : new FileInfo(path).Length;
            yield return new DirectoryEntryEx(Path.GetFileName(path), "/" + Path.GetRelativePath(contents, path).Replace('\\', '/'),
                DirectoryEntryType.File, size);
        }
    }

    internal static IFileSystem OpenFileSystem(string contents, KeySet keys, long chunkSize, bool plain = false)
    {
        if (!plain)
        {
            if (keys.SdCardEncryptionSeed.IsZeros())
                throw new InvalidDataException("Missing sd_seed in prod.keys for this SD card.");
            keys.DeriveSdCardKeys();
            if (keys.SdCardEncryptionKeys[1].IsZeros())
                throw new InvalidDataException("Missing SD content encryption keys in prod.keys.");
        }

        var local = new UniqueRef<IAttributeFileSystem>(new SdLocalFileSystem(contents));
        try
        {
            var concatenation = new ConcatenationFileSystem(ref local, chunkSize);
            try { return plain ? concatenation : new AesXtsFileSystem(concatenation, keys.SdCardEncryptionKeys[1].Data.ToArray(), 0x4000); }
            catch { concatenation.Dispose(); throw; }
        }
        finally { local.Destroy(); }
    }

    private sealed class SdLocalFileSystem : LocalFileSystem
    {
        private readonly string _root;

        public SdLocalFileSystem(string root) : base(root) => _root = root;

        protected override Result DoGetFileAttributes(out NxFileAttributes attributes, in LibHac.Fs.Path path)
        {
            var result = base.DoGetFileAttributes(out attributes, in path);
            var relative = path.ToString().TrimStart('/');
            if (result.IsSuccess() && relative.EndsWith(".nca", StringComparison.OrdinalIgnoreCase) &&
                File.Exists(Path.Combine(_root, relative, "00")))
                attributes |= NxFileAttributes.Archive;
            return result;
        }

        protected override Result DoOpenFile(ref UniqueRef<IFile> file, in LibHac.Fs.Path path, OpenMode mode) =>
            mode == OpenMode.Read ? base.DoOpenFile(ref file, in path, mode) : ResultFs.UnsupportedOperation.Value;
    }
}
