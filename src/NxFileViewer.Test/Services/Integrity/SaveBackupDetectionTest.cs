using System;
using System.IO;
using System.IO.Compression;
using Emignatik.NxFileViewer.FileLoading;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services.Integrity;

public sealed class SaveBackupDetectionTest
{
    [Theory]
    [InlineData("oldJKSV", "User - 2025.05.15 @ 15.40.24.zip", "file0", true)]
    [InlineData("JKSV", "User - 2025-01-01_02-15-47.zip", "save", true)]
    [InlineData("Downloads", "User - 2025.05.15 @ 15.40.24.zip", "file0", false)]
    [InlineData("oldJKSV", "documents.zip", "file0", false)]
    [InlineData("oldJKSV", "User - 2025.05.15 @ 15.40.24.zip", "game.nsp", false)]
    [InlineData("oldJKSV", "User - 2025.05.15 @ 15.40.24.zip", ".nx_save_meta.bin", false)]
    public void LegacyContextRequiresBackupFolderAndNameAndRejectsPackages(string folder, string name, string entry, bool expected)
    {
        var root = Path.Combine(Path.GetTempPath(), "NxSave-" + Guid.NewGuid());
        var directory = Directory.CreateDirectory(Path.Combine(root, folder, "Title")).FullName;
        var path = Path.Combine(directory, name);
        try
        {
            using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
            using (var output = zip.CreateEntry(entry).Open()) output.Write(new byte[] { 1, 2, 3, 4 });
            Assert.Equal(expected, SaveBackupDetection.IsBackup(path, TestContext.Current.CancellationToken));
            Assert.Equal(expected, SaveBackupDetection.IsLikelyLegacyBackup(path, TestContext.Current.CancellationToken));
        }
        finally { File.Delete(path); Directory.Delete(directory); Directory.Delete(Path.GetDirectoryName(directory)!); Directory.Delete(root); }
    }

    [Theory]
    [InlineData(true, 86, true)]
    [InlineData(false, 86, false)]
    [InlineData(true, 5, false)]
    public void RequiresValidMetadata(bool magic, int size, bool expected)
    {
        WithZip(zip =>
        {
            var data = new byte[size];
            if (magic) new byte[] { 0x4a, 0x4b, 0x53, 0x56, 1 }.CopyTo(data, 0);
            using var stream = zip.CreateEntry(".nx_save_meta.bin").Open();
            stream.Write(data);
        }, path => Assert.Equal(expected, SaveBackupDetection.IsBackup(path)));
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    public void LegacyLayoutRequiresCompanionFilesAndBinaryHeader(bool companions, bool magic, bool expected)
    {
        WithZip(zip =>
        {
            using (var stream = zip.CreateEntry("slot_00/progress.sav").Open())
            {
                var data = new byte[48];
                if (magic) new byte[] { 4, 3, 2, 1 }.CopyTo(data, 0);
                stream.Write(data);
            }
            if (companions)
                foreach (var name in new[] { "caption.sav", "footprint.sav", "direct_file_save_related.sav" })
                    zip.CreateEntry("slot_00/" + name);
        }, path => Assert.Equal(expected, SaveBackupDetection.IsBackup(path)));
    }

    private static void WithZip(Action<ZipArchive> create, Action<string> check)
    {
        var path = Path.Combine(Path.GetTempPath(), "NxSave-" + Guid.NewGuid() + ".zip");
        try
        {
            using (var zip = ZipFile.Open(path, ZipArchiveMode.Create)) create(zip);
            check(path);
        }
        finally { File.Delete(path); }
    }
}
