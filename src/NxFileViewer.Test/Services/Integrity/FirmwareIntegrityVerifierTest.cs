using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using Emignatik.NxFileViewer.FileLoading;
using Emignatik.NxFileViewer.Localization.Keys;
using Emignatik.NxFileViewer.Services.Integrity;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services.Integrity;

public sealed class FirmwareIntegrityVerifierTest : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "NxFirmwareTest-" + Guid.NewGuid());
    private static readonly ILocalizationKeys Keys = new LocalizationKeys_EN();
    private readonly FirmwareComparison _verifier;
    private readonly string _folder;
    public FirmwareIntegrityVerifierTest()
    {
        _folder = Directory.CreateDirectory(Path.Combine(_root, "firmware")).FullName;
        var a = new byte[] { 1, 2, 3 }; var b = new byte[] { 4, 5 };
        File.WriteAllBytes(Path.Combine(_folder, "a.nca"), a);
        File.WriteAllBytes(Path.Combine(_folder, "b.cnmt.nca"), b);
        _verifier = Create(JsonSerializer.Serialize(new {
            name = "test", file_count = 2, files = new System.Collections.Generic.Dictionary<string, object> {
                ["a.nca"] = new { size = a.Length, sha256 = Convert.ToHexString(SHA256.HashData(a)) },
                ["b.cnmt.nca"] = new { size = b.Length, sha256 = Convert.ToHexString(SHA256.HashData(b)) }
            }
        }));

    }
    [Fact] public void CompleteFolderMatchesAndIsDetected()
    {
        Assert.True(_verifier.IsFirmwareFolder(_folder));
        var result = _verifier.Verify(_folder, Keys, TestContext.Current.CancellationToken);
        Assert.Equal(FirmwareMatch.Exact, result.Match);
        Assert.True(result.IsFirmware);
        Assert.Equal("test", result.Version);
    }
    [Fact] public void RepackedZipAndNestedEntriesMatchWithoutExtraction()
    {
        var zip = Path.Combine(_root, "repacked.zip");
        using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
            foreach (var file in Directory.GetFiles(_folder)) archive.CreateEntryFromFile(file, "nested/" + Path.GetFileName(file));
        Assert.True(_verifier.IsFirmwareZip(zip));
        Assert.Equal(FirmwareMatch.Exact, _verifier.Verify(zip, Keys, TestContext.Current.CancellationToken).Match);
        Assert.False(Directory.Exists(Path.Combine(_root, "nested")));
    }
    [Theory] [InlineData("missing")] [InlineData("changed")] [InlineData("extra")]
    public void IncompleteOrChangedFirmwareFails(string modification)
    {
        if (modification == "missing") File.Delete(Path.Combine(_folder, "b.cnmt.nca"));
        if (modification == "changed") File.WriteAllBytes(Path.Combine(_folder, "a.nca"), new byte[] { 3, 2, 1 });
        if (modification == "extra") File.WriteAllBytes(Path.Combine(_folder, "extra.nca"), new byte[] { 0 });
        Assert.Equal(FirmwareMatch.Error, _verifier.Verify(_folder, Keys, TestContext.Current.CancellationToken).Match);
    }
    [Fact] public void DuplicateZipBasenamesFail()
    {
        var zip = Path.Combine(_root, "duplicate.zip");
        using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
        {
            foreach (var file in Directory.GetFiles(_folder)) archive.CreateEntryFromFile(file, Path.GetFileName(file));
            archive.CreateEntryFromFile(Path.Combine(_folder, "a.nca"), "other/a.nca");
        }
        Assert.Equal(FirmwareMatch.Error, _verifier.Verify(zip, Keys, TestContext.Current.CancellationToken).Match);
    }
    [Fact] public void UnknownFilesAreNotDetectedAsFirmware()
    {
        foreach (var file in Directory.GetFiles(_folder)) File.Delete(file);
        File.WriteAllBytes(Path.Combine(_folder, "game.nca"), new byte[] { 1 });
        Assert.False(_verifier.IsFirmwareFolder(_folder));
        Assert.Equal(FirmwareMatch.Error, _verifier.Verify(_folder, Keys, TestContext.Current.CancellationToken).Match);
    }
    [Fact] public void CancellationPropagates() => Assert.Throws<OperationCanceledException>(() => _verifier.Verify(_folder, Keys, new CancellationToken(true)));
    [Theory] [InlineData(false)] [InlineData(true)]
    public void RenamedContentIsReportedOnceWithExpectedFilename(bool zip)
    {
        File.Move(Path.Combine(_folder, "a.nca"), Path.Combine(_folder, "typo.nca"));
        var path = _folder;
        if (zip)
        {
            path = Path.Combine(_root, "renamed.zip");
            using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
            foreach (var file in Directory.GetFiles(_folder)) archive.CreateEntryFromFile(file, "nested/" + Path.GetFileName(file));
        }
        var result = _verifier.Verify(path, Keys, TestContext.Current.CancellationToken);
        Assert.Equal("test", result.Version);
        Assert.Equal(FirmwareMatch.Error, result.Match); // Correct contents still need the expected names.
        Assert.Contains(Keys.Firmware_Renamed + ": typo.nca → a.nca", result.Details);
        Assert.DoesNotContain(Keys.Firmware_Missing + ": a.nca", result.Details);
        Assert.DoesNotContain(Keys.Firmware_Extra + ": typo.nca", result.Details);
        Assert.Contains(string.Format(Keys.Firmware_RenamedSummary, 1), result.Details);
        Assert.True(File.Exists(Path.Combine(_folder, "typo.nca"))); // Verification does not rename files.
    }
    [Fact] public void EntirelyRenamedSetStillIdentifiesVersion()
    {
        File.Move(Path.Combine(_folder, "a.nca"), Path.Combine(_folder, "first.nca"));
        File.Move(Path.Combine(_folder, "b.cnmt.nca"), Path.Combine(_folder, "second.nca"));
        var result = _verifier.Verify(_folder, Keys, TestContext.Current.CancellationToken);
        Assert.Equal("test", result.Version);
        Assert.Contains("first.nca → a.nca", result.Details);
        Assert.Contains("second.nca → b.cnmt.nca", result.Details);
        Assert.Contains(string.Format(Keys.Firmware_RenamedSummary, 2), result.Details);
        Assert.Equal(FirmwareMatch.Error, result.Match);
    }
    [Fact] public void DifferentHashCannotBeClassifiedAsRenamed()
    {
        File.Delete(Path.Combine(_folder, "a.nca"));
        File.WriteAllBytes(Path.Combine(_folder, "typo.nca"), new byte[] { 3, 2, 1 });
        var result = _verifier.Verify(_folder, Keys, TestContext.Current.CancellationToken);
        Assert.Contains(Keys.Firmware_Missing + ": a.nca", result.Details);
        Assert.Contains(Keys.Firmware_Extra + ": typo.nca", result.Details);
        Assert.DoesNotContain("typo.nca → a.nca", result.Details);
    }
    [Fact] public void OneRenamedFileCannotSatisfyTwoExpectedFilesWithSameContent()
    {
        var bytes = new byte[] { 1, 2, 3 };
        File.Delete(Path.Combine(_folder, "b.cnmt.nca"));
        File.Move(Path.Combine(_folder, "a.nca"), Path.Combine(_folder, "typo.nca"));
        var fingerprint = new { size = bytes.Length, sha256 = Convert.ToHexString(SHA256.HashData(bytes)) };
        var verifier = Create(JsonSerializer.Serialize(new {
            name = "shared-content", file_count = 2, files = new System.Collections.Generic.Dictionary<string, object> {
                ["a.nca"] = fingerprint, ["b.nca"] = fingerprint
            }
        }));
        var result = verifier.Verify(_folder, Keys, TestContext.Current.CancellationToken);
        Assert.Contains("typo.nca → a.nca", result.Details);
        Assert.Contains(Keys.Firmware_Missing + ": b.nca", result.Details);
        Assert.Contains(string.Format(Keys.Firmware_RenamedSummary, 1), result.Details);
        Assert.Equal(FirmwareMatch.Error, result.Match);
    }
    [Fact] public void DuplicateContentWithNoMissingFilenameRemainsExtra()
    {
        File.Copy(Path.Combine(_folder, "a.nca"), Path.Combine(_folder, "copy.nca"));
        var result = _verifier.Verify(_folder, Keys, TestContext.Current.CancellationToken);
        Assert.Contains(Keys.Firmware_Extra + ": copy.nca", result.Details);
        Assert.Contains(string.Format(Keys.Firmware_RenamedSummary, 0), result.Details);
    }
    private static FirmwareComparison Create(string manifest) =>
        new([manifest], PackageZip.GetFileEntries,
            (path, include, read, token) => PackageZip.ReadEntries(path, include, read, token));
    public void Dispose() => Directory.Delete(_root, true);
}
