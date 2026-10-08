using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using LibHac;
using LibHac.Common;
using LibHac.Common.Keys;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.Tools.FsSystem;
using LibHac.Tools.Fs;
using FsPath = LibHac.Fs.Path;
using Path = System.IO.Path;

namespace Emignatik.NxFileViewer.Models.TreeItems.Impl;

public sealed class SdCardItem : PartitionFileSystemItemBase
{
    private readonly string _contents;
    private SdCardItem(IFileSystem fs, string source, KeySet keys) : base(fs, null) { _contents = source; Name = Path.GetFileName(source.TrimEnd(Path.DirectorySeparatorChar)); KeySet = keys; }
    public IEnumerable<DirectoryEntryEx> EnumerateContent(CancellationToken token = default)
    {
        var root = Path.Combine(_contents, "registered");
        var paths = Directory.EnumerateFiles(root, "*.nca", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateDirectories(root, "*.nca", SearchOption.AllDirectories));
        foreach (var physical in paths)
        {
            token.ThrowIfCancellationRequested();
            var relative = "/" + Path.GetRelativePath(_contents, physical).Replace('\\', '/');
            using var file = new UniqueRef<IFile>();
            PartitionFileSystem.OpenFile(ref file.Ref, relative.ToU8Span(), OpenMode.Read).ThrowIfFailure();
            file.Get.GetSize(out var size).ThrowIfFailure();
            yield return new DirectoryEntryEx(Path.GetFileName(physical), relative, DirectoryEntryType.File, size);
        }
    }
    public static SdCardItem Open(string contents, KeySet keys)
    {
        if (keys.SdCardEncryptionSeed.IsZeros()) throw new InvalidDataException("Missing sd_seed in prod.keys for this SD card.");
        keys.DeriveSdCardKeys();
        if (keys.SdCardEncryptionKeys[1].IsZeros()) throw new InvalidDataException("Missing SD content encryption keys in prod.keys.");
        var local = new UniqueRef<IAttributeFileSystem>(new SdLocalFileSystem(contents));
        ConcatenationFileSystem concatenation;
        try
        {
            var chunkSizes = Directory.EnumerateDirectories(Path.Combine(contents, "registered"), "*.nca", SearchOption.AllDirectories)
                .Where(directory => File.Exists(Path.Combine(directory, "01")))
                .Select(directory => new FileInfo(Path.Combine(directory, "00")).Length).Distinct().ToArray();
            if (chunkSizes.Length > 1 || chunkSizes.Any(size => size <= 0)) throw new InvalidDataException("Inconsistent SD split-file chunk sizes.");
            concatenation = new ConcatenationFileSystem(ref local, chunkSizes.FirstOrDefault(0xffff0000L));
        }
        finally { local.Destroy(); }
        try { return new SdCardItem(new AesXtsFileSystem(concatenation, keys.SdCardEncryptionKeys[1].Data.ToArray(), 0x4000), contents, keys); }
        catch { concatenation.Dispose(); throw; }
    }
    public override string Name { get; }
    public override string DisplayName => Name;
    public override string Format => "NAX0";
    public override KeySet KeySet { get; }
    public override void Dispose()
    {
        foreach (var nca in NcaChildItems)
        {
            foreach (var section in nca.ChildItems) section.Dispose();
            nca.Nca.BaseStorage.Dispose();
        }
        PartitionFileSystem.Dispose();
    }

    // Copied FAT32 split directories often lose their archive attribute. Recognize
    // the standard numbered chunks without changing attributes on the user's SD.
    private sealed class SdLocalFileSystem : LocalFileSystem
    {
        private readonly string _root;
        public SdLocalFileSystem(string root) : base(root) { _root = root; }
        protected override Result DoGetFileAttributes(out NxFileAttributes attributes, in FsPath path)
        {
            var result = base.DoGetFileAttributes(out attributes, in path);
            if (result.IsSuccess() && path.ToString().EndsWith(".nca", StringComparison.OrdinalIgnoreCase) &&
                File.Exists(Path.Combine(_root, path.ToString().TrimStart('/').Replace('/', Path.DirectorySeparatorChar), "00")))
                attributes |= NxFileAttributes.Archive;
            return result;
        }
        protected override Result DoOpenFile(ref UniqueRef<IFile> file, in FsPath path, OpenMode mode) =>
            mode == OpenMode.Read ? base.DoOpenFile(ref file, in path, mode) : ResultFs.UnsupportedOperation.Value;
    }
}
