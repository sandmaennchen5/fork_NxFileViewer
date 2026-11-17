using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Emignatik.NxFileViewer.Models.TreeItems.Impl;

namespace Emignatik.NxFileViewer.FileLoading;

internal sealed class NupContentTree(
    Lazy<byte[]> toc, int directories, int files, int children, int pool, int poolSize, uint directoryCount, uint fileCount)
{
    public static void Attach(NupFileItem root, ulong tocOffset, ulong tocSize, byte[] hash,
        ulong directories, ulong files, ulong pool, ulong poolSize, ulong directoryCount, ulong fileCount)
    {
        if (tocSize > (ulong)Array.MaxLength || directoryCount > uint.MaxValue || fileCount > uint.MaxValue)
            throw new InvalidDataException("NUP table of contents is too large to browse.");
        var entries = new Lazy<byte[]>(() =>
        {
            using var stream = File.OpenRead(root.FilePath);
            stream.Position = (long)tocOffset;
            var data = new byte[(int)tocSize];
            stream.ReadExactly(data);
            if (!SHA256.HashData(data).AsSpan().SequenceEqual(hash))
                throw new InvalidDataException("NUP table of contents changed since opening.");
            return data;
        });
        var tree = new NupContentTree(entries, (int)directories, (int)files, (int)(files + fileCount * 80),
            (int)pool, (int)poolSize, (uint)directoryCount, (uint)fileCount);
        var packages = root.ChildItems.Cast<NupPackageItem>().ToArray();
        foreach (var package in packages.Where(package => package.Kind == 0))
            tree.AttachDirectory(new NupContentItem(root.ContentRoot, "base", "Directory"), package.Directory);
        var updateRoot = packages.Any(package => package.Kind == 1)
            ? new NupContentItem(root.ContentRoot, "updates", "Directory") : null;
        var dlcRoot = packages.Any(package => package.Kind == 2)
            ? new NupContentItem(root.ContentRoot, "dlc", "Directory") : null;
        foreach (var package in packages.Where(package => package.Kind == 1).OrderBy(package => package.Version))
            tree.AttachDirectory(new NupContentItem(updateRoot, package.Version.ToString(), "Directory"), package.Directory);
        if (dlcRoot != null)
        {
            foreach (var group in packages.Where(package => package.Kind == 2).GroupBy(package => package.TitleId))
            {
                var title = new NupContentItem(dlcRoot, group.Key.ToLowerInvariant(), "Directory");
                var index = 0;
                foreach (var package in group)
                    tree.AttachDirectory(new NupContentItem(title, (index++).ToString(), "Directory"), package.Directory);
            }
        }
        foreach (var package in packages.Where(package => package.Kind > 2))
            tree.AttachDirectory(new NupContentItem(root.ContentRoot, package.DisplayName, "Directory"), package.Directory);
    }

    private void AttachDirectory(NupContentItem node, uint id)
    {
        node.DirectoryId = id;
        node.LoadChildren = () => ReadChildren(node, id);
    }

    private void ReadChildren(NupContentItem node, uint id)
    {
        var data = toc.Value.AsSpan();
        var record = data.Slice(directories + (int)id * 24, 24);
        var first = BinaryPrimitives.ReadUInt32LittleEndian(record[8..]);
        var count = BinaryPrimitives.ReadUInt32LittleEndian(record[12..]);
        if ((ulong)first + count > (ulong)(pool - children) / 4)
            throw new InvalidDataException("Invalid NUP directory children.");

        for (uint index = 0; index < count; index++)
        {
            var childId = BinaryPrimitives.ReadUInt32LittleEndian(data[(children + (int)(first + index) * 4)..]);
            if (childId < directoryCount)
            {
                var depth = 0;
                for (var ancestor = node; ancestor != null; ancestor = ancestor.ParentItem as NupContentItem)
                    if (ancestor.DirectoryId == childId || ++depth > 256)
                        throw new InvalidDataException("Invalid NUP directory hierarchy.");
                var child = data.Slice(directories + (int)childId * 24, 24);
                var folder = new NupContentItem(node, ReadName(data, child), "Directory");
                if (BinaryPrimitives.ReadUInt32LittleEndian(child[12..]) != 0)
                    AttachDirectory(folder, childId);
            }
            else
            {
                var fileId = childId - directoryCount;
                if (fileId >= fileCount) throw new InvalidDataException("Invalid NUP file index.");
                var child = data.Slice(files + (int)fileId * 80, 80);
                var size = BinaryPrimitives.ReadUInt64LittleEndian(child[8..]);
                if (size > long.MaxValue) throw new InvalidDataException("Invalid NUP file size.");
                _ = new NupContentItem(node, ReadName(data, child), "File", (long)size);
            }
        }
    }

    private string ReadName(ReadOnlySpan<byte> data, ReadOnlySpan<byte> record)
    {
        var start = BinaryPrimitives.ReadUInt32LittleEndian(record);
        var length = BinaryPrimitives.ReadUInt32LittleEndian(record[4..]);
        if ((ulong)start + length > (uint)poolSize)
            throw new InvalidDataException("Invalid NUP node name.");
        return Encoding.UTF8.GetString(data.Slice(pool + (int)start, (int)length));
    }
}
