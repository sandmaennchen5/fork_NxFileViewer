using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using Emignatik.NxFileViewer.FileLoading.QuickFileInfoLoading;
using LibHac.Ncm;
using NxFileViewer.Avalonia;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services;

public sealed class BatchPackageRowsTest
{
    internal static MainWindow.BatchRow CreateContainer()
    {
        const string path = "multi.nsp";
        return new MainWindow.BatchRow(path, "")
        {
            Package = new PackageFile(path, "NSP", 123456, [],
                [
                    ReadContent(ContentMetaType.Patch, 0x0100000000000800, 65536),
                    ReadContent(ContentMetaType.AddOnContent, 0x0100000000001001, 0),
                    ReadContent(ContentMetaType.Application, 0x0100000000000000, 131072)
                ], false, null),
            Integrity = "Original"
        };
    }

    private static Content ReadContent(ContentMetaType type, ulong id, uint version)
    {
        var bytes = new byte[128];
        BinaryPrimitives.WriteUInt64LittleEndian(bytes, id);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), version);
        bytes[12] = (byte)type;
        bytes[14] = type == ContentMetaType.Patch ? (byte)24 : (byte)16;
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(0x20),
            type == ContentMetaType.Application ? id + 0x800 : 0x0100000000000000);
        using var input = new MemoryStream(bytes);
        return new Content(new LibHac.Tools.Ncm.Cnmt(input));
    }

    [Fact]
    public void SeparateRowsHaveStableNumbersAndKeepPhysicalPath()
    {
        var container = CreateContainer();
        var rows = MainWindow.ExpandedBatchRows([container], true).ToArray();
        Assert.Equal(new[] { "multi.nsp::1", "multi.nsp::2", "multi.nsp::3" }, rows.Select(row => row.FileName));
        Assert.Equal(new[] { "0100000000000800", "0100000000001001", "0100000000000000" }, rows.Select(row => row.TitleId));
        Assert.Equal(new[] { "65536", "0", "131072" }, rows.Select(row => row.Version));
        Assert.All(rows, row =>
        {
            Assert.Equal(container.Path, row.Path);
            Assert.Equal(container.Size, row.Size);
            Assert.Equal(container.Integrity, row.Integrity);
            Assert.False(row.ShowPackageDetails);
        });
        Assert.Same(container, Assert.Single(MainWindow.ExpandedBatchRows([container], false)));
    }

    [Fact]
    public void FilesWithoutMultiplePackagesRemainSingle()
    {
        var row = new MainWindow.BatchRow("firmware.zip", "") { FirmwareArchiveType = "ZIP" };
        Assert.Same(row, Assert.Single(MainWindow.ExpandedBatchRows([row], true)));
    }
}
