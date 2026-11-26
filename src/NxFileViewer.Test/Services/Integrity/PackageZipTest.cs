using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using Emignatik.NxFileViewer.FileLoading;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services.Integrity;

public sealed class PackageZipTest : IDisposable
{
    private readonly string _root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "NxZip-" + Guid.NewGuid())).FullName;
    private string CreateZip(params string[] names)
    {
        var path = Path.Combine(_root, Guid.NewGuid() + ".zip");
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var name in names)
        {
            using var output = zip.CreateEntry(name).Open();
            output.Write(new byte[] { 1, 2, 3, 4 });
        }
        return path;
    }
    private string WrapArchive(string inner)
    {
        var outer = Path.Combine(_root, Guid.NewGuid() + ".zip");
        using var zip = ZipFile.Open(outer, ZipArchiveMode.Create);
        using var output = zip.CreateEntry("nested/inner.zip").Open();
        using var input = File.OpenRead(inner);
        input.CopyTo(output);
        return outer;
    }
    [Fact] public void NestedZipRejectsUnsafeEntriesAndExcessiveDepth()
    {
        Assert.Throws<InvalidDataException>(() => PackageZip.GetEntries(WrapArchive(CreateZip("../bad.nsp")), token: TestContext.Current.CancellationToken));
        var archive = CreateZip("game.nsp");
        for (var i = 0; i < 8; i++) archive = WrapArchive(archive);
        Assert.Throws<InvalidDataException>(() => PackageZip.GetEntries(archive, token: TestContext.Current.CancellationToken));
    }
    [Fact] public void EnumeratesPackagesAndNcaWithSeparateBatchFilter()
    {
        var path = CreateZip("a.nsp", "sub/B.NSZ", "c.xci", "d.xcz", "control.nca", "readme.txt");
        Assert.Equal(5, PackageZip.GetEntries(path, token: TestContext.Current.CancellationToken).Count);
        Assert.Equal(4, PackageZip.GetEntries(path, false, TestContext.Current.CancellationToken).Count);
    }
    [Fact] public void ExtractsAllEntriesAndDeletesThemOnDispose()
    {
        var path = CreateZip("sub/game.nsp", "another.nsp");
        var original = File.ReadAllBytes(path);
        string extracted;
        using (var lease = PackageZip.Extract(path, "sub/game.nsp", TestContext.Current.CancellationToken, Path.Combine(_root, "temp")))
        {
            extracted = lease.FilePath;
            Assert.Equal("game.nsp", Path.GetFileName(extracted));
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, File.ReadAllBytes(extracted));
            Assert.Single(Directory.GetFiles(Path.GetDirectoryName(extracted)!));
            Assert.Equal(2, Directory.GetFiles(Path.GetDirectoryName(Path.GetDirectoryName(extracted)!)!, "*", SearchOption.AllDirectories).Length);
        }
        Assert.False(File.Exists(extracted));
        Assert.Equal(original, File.ReadAllBytes(path));
    }
    [Fact] public void ArchiveMembersReuseExtractionUntilLastLeaseIsClosed()
    {
        var path = CreateZip("a.nsp", "b.nsp", "readme.txt");
        var first = PackageZip.Extract(path, "a.nsp", TestContext.Current.CancellationToken, Path.Combine(_root, "temp"));
        var root = Path.GetDirectoryName(Path.GetDirectoryName(first.FilePath)!)!;
        Assert.Equal(3, Directory.GetFiles(root, "*", SearchOption.AllDirectories).Length);
        using var second = PackageZip.Extract(path, "b.nsp", TestContext.Current.CancellationToken, Path.Combine(_root, "temp"));
        Assert.Equal(root, Path.GetDirectoryName(Path.GetDirectoryName(second.FilePath)!));
        first.Dispose();
        first.Dispose();
        Assert.True(File.Exists(second.FilePath));
        second.Dispose();
        Assert.False(Directory.Exists(root));
    }
    [Fact] public void Solid7zExtractionKeepsAllMembersInOneSession()
    {
        var archive = Create7z();
        using var first = PackageZip.Extract(archive, "a.nsp", TestContext.Current.CancellationToken, Path.Combine(_root, "temp"));
        var root = Path.GetDirectoryName(Path.GetDirectoryName(first.FilePath)!)!;
        Assert.Equal(4, Directory.GetFiles(root, "*", SearchOption.AllDirectories).Length);
        using var second = PackageZip.Extract(archive, "d.xcz", TestContext.Current.CancellationToken, Path.Combine(_root, "temp"));
        Assert.Equal(root, Path.GetDirectoryName(Path.GetDirectoryName(second.FilePath)!));
    }
    [Theory] [InlineData("../outside.nsp")] [InlineData("/outside.nsp")] [InlineData("sub/../../outside.nca")]
    public void RejectsUnsafeNames(string entry)
    {
        var path = CreateZip(entry);
        Assert.Throws<InvalidDataException>(() => PackageZip.GetEntries(path, token: TestContext.Current.CancellationToken));
        Assert.Throws<InvalidDataException>(() => PackageZip.Extract(path, entry, TestContext.Current.CancellationToken, Path.Combine(_root, "temp")));
    }
    [Fact] public void RejectsDuplicateNamesAndCancelledExtraction()
    {
        var path = CreateZip("game.nsp", "game.nsp");
        Assert.Throws<InvalidDataException>(() => PackageZip.GetEntries(path, token: TestContext.Current.CancellationToken));
        Assert.Throws<InvalidDataException>(() => PackageZip.Extract(path, "game.nsp", TestContext.Current.CancellationToken, Path.Combine(_root, "temp")));
        path = CreateZip("game.nsp");
        Assert.ThrowsAny<OperationCanceledException>(() => PackageZip.Extract(path, "game.nsp", new CancellationToken(true), Path.Combine(_root, "temp")));
        Assert.False(Directory.Exists(Path.Combine(_root, "temp")));
    }
    private string Create7z(bool firmware = false)
    {
        const string packages = "N3q8ryccAAQfvjAjiAAAAAAAAAAhAAAAAAAAAJlBEg/gAD8ADV0AKBGGqjnzb9jkHZYAAAAAAIEzB64Pz0tvjAfIQ39Bsfr+GZCpcX2l2/2icWIofl78q/kOAvcIwEJlkCOpKyqQ1F6ABx9AEnWFEMORSSOLnvfON7LeV4wtHqhY5hkjoBT3K0avZcVhIDUZSXgNz5tVtLYVLCCgi/joYfH9ORxep2RsFwYVAQlzAAcLAQABIwMBAQVdABAAAAyArgoBVSIAWAAA";
        const string fw = "N3q8ryccAAQLlf5+ZwAAAAAAAAAgAAAAAAAAAIKDNhsBAAcBAgMEBQYHCAAAAIEzB64PzpwGxQkqrtOqhSYoExi7AyVnHGlhhhrxB+LwNl69MHfajTHqyt7dR+Y4ejG4oQGNO4Vy/d9XpxhNjMb26NtXHJH/K+RgBCu/FZaklpT8fWBGAAAAFwYMAQlbAAcLAQABIwMBAQVdABAAAAxuCgGuWoiMAAA=";
        var path = Path.Combine(_root, Guid.NewGuid() + ".7z");
        File.WriteAllBytes(path, Convert.FromBase64String(firmware ? fw : packages));
        return path;
    }
    [Theory]
    [InlineData("game.nsp", "NSP")]
    [InlineData("a.zip::sub/game.nsp", "NSP (ZIP)")]
    [InlineData("a.7z::sub/game.nsz", "NSZ (7z)")]
    [InlineData("a.zip::game.xci", "XCI (ZIP)")]
    [InlineData("a.7z::game.xcz", "XCZ (7z)")]
    public void BatchFileTypeIncludesArchiveOrigin(string path, string expected) => Assert.Equal(expected, PackageZip.DisplayFileType(path));
    public void Dispose() => Directory.Delete(_root, recursive: true);
}
