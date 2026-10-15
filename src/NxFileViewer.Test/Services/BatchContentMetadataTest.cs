using System;
using System.Text.Json;
using Emignatik.NxFileViewer.Models.Overview;
using Emignatik.NxFileViewer.Services.Integrity;
using Emignatik.NxFileViewer.Services.Security;
using LibHac.Ncm;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services;

public sealed class BatchContentMetadataTest
{
    [Fact]
    public void MultiPackageTypesIncludeEachTypeOnce() => Assert.Equal("Base / Update / DLC",
        BatchContentMetadata.Types(new[] { ContentMetaType.Application, ContentMetaType.Patch,
            ContentMetaType.AddOnContent, ContentMetaType.Patch }));

    [Fact]
    public void MissingOrInapplicableProgramMetadataIsNotSafe()
    {
        Assert.Null(BatchContentMetadata.Assess(Array.Empty<ProgramSecurityLevel?>()));
        Assert.Null(BatchContentMetadata.Assess(new ProgramSecurityLevel?[] { ProgramSecurityLevel.Safe, null }));
        Assert.Equal(ProgramSecurityLevel.Safe, BatchContentMetadata.Assess(new ProgramSecurityLevel?[] { ProgramSecurityLevel.Safe }));
    }

    [Theory]
    [InlineData(ProgramSecurityLevel.Unsafe)]
    [InlineData(ProgramSecurityLevel.Dangerous)]
    public void KnownRisksRemainVisibleDespiteMissingMetadata(ProgramSecurityLevel risk) =>
        Assert.Equal(risk, BatchContentMetadata.Assess(new ProgramSecurityLevel?[] { null, ProgramSecurityLevel.Safe, risk }));

    [Fact]
    public void HighestRiskWins() => Assert.Equal(ProgramSecurityLevel.Dangerous,
        BatchContentMetadata.Assess(new ProgramSecurityLevel?[] { ProgramSecurityLevel.Dangerous, ProgramSecurityLevel.Unsafe }));

    [Fact]
    public void SignatureRequiresAllValidAndInvalidWinsOverUnchecked()
    {
        Assert.Null(BatchContentMetadata.Signature(Array.Empty<LibHac.Common.Validity>()));
        Assert.Equal(LibHac.Common.Validity.Valid, BatchContentMetadata.Signature(new[] { LibHac.Common.Validity.Valid }));
        Assert.Equal(LibHac.Common.Validity.Invalid, BatchContentMetadata.Signature(new[] { LibHac.Common.Validity.Valid, LibHac.Common.Validity.Invalid }));
    }

    [Fact]
    public void HistoryRetainsMetadataAndOlderEntriesStayUnknown()
    {
        var original = new BatchIntegrityResult("game.nsp", "NSP", "NSP", "", "", default, null)
            { ContentType = "Base / Update", Permission = ProgramSecurityLevel.Unsafe };
        var restored = JsonSerializer.Deserialize<BatchIntegrityResult>(JsonSerializer.Serialize(original))!;
        Assert.Equal(original.ContentType, restored.ContentType);
        Assert.Equal(original.Permission, restored.Permission);
        var old = JsonSerializer.Deserialize<BatchIntegrityResult>("{\"FilePath\":\"old.nsp\"}")!;
        Assert.Null(old.Permission);
        Assert.Equal("", old.ContentType);
    }
}
