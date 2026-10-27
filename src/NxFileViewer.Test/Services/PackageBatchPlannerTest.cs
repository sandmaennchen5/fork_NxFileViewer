using System;
using System.Linq;
using Emignatik.NxFileViewer.Services.Integrity;
using Emignatik.NxFileViewer.Services.PackageOperations;
using Emignatik.NxFileViewer.Models.Overview;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services;

public sealed class PackageBatchPlannerTest
{
    private const string GameA = "0100000000000000";
    private const string GameB = "0100000000002000";
    private static BatchPackageSummary Base(string id) => new("Game", id, "Application", "0") { BaseTitleId = id };
    private static BatchPackageSummary Update(string id, string game) => new("Update", id, "Patch", "65536") { BaseTitleId = game };
    private static BatchIntegrityResult File(string path, params BatchPackageSummary[] packages) =>
        new(path, "NSP", "NSP", "", "", NcasIntegrity.Original, null) { Packages = packages };

    [Theory]
    [InlineData(PackageBatchFormat.KeepSource)]
    [InlineData(PackageBatchFormat.Nsp)]
    [InlineData(PackageBatchFormat.Xci)]
    public void SplitProcessesEachPhysicalMultipackageOnceAndChoosesDecompressedSourceFormat(PackageBatchFormat format)
    {
        var a = File("a.NSZ", Base(GameA), Update("0100000000000800", GameA));
        var b = File("b.xcz", Base(GameB), Update("0100000000002800", GameB));
        var plan = PackageBatchPlanner.Split(new[] { a, a with { PackageIndex = 1 }, b, File("single.nsp", Base(GameA)) }, format);
        Assert.Equal(2, plan.Jobs.Length);
        Assert.Equal(new[] { "a.NSZ", "b.xcz" }, plan.Jobs.Select(j => Assert.Single(j.Sources)));
        Assert.All(plan.Jobs, j => Assert.Equal(PackageOperation.Split, j.Operation));
        Assert.Equal(format == PackageBatchFormat.Xci ? "xci" : "nsp", plan.Jobs[0].Type);
        Assert.Equal(format == PackageBatchFormat.Nsp ? "nsp" : "xci", plan.Jobs[1].Type);
    }

    [Fact]
    public void MergeByGameKeepsBaseUpdatesAndDlcTogetherWithoutMixingGamesOrGuessingUnknownIds()
    {
        var game = File("a.nsz", Base(GameA) with { BaseTitleId = "0000000000000000" });
        var update = File("update.xci", Update("0100000000000800", GameA));
        var dlc = File("dlc.nsz", new BatchPackageSummary("DLC", "0100000000001001", "AddOnContent", "0") { BaseTitleId = GameA });
        var b = File("b.nsp", Base(GameB));
        var bUpdate = File("b-update.xcz", Update("0100000000002800", GameB));
        var unknown = File("unknown.nsz", Update("0100000000000800", ""));
        var mixed = File("mixed.nsz", Base(GameA), Base(GameB));
        var plan = PackageBatchPlanner.Merge(new[] { game, update, dlc, b, bUpdate, unknown, mixed }, PackageMergeGrouping.ByGame, PackageBatchFormat.Xci);
        Assert.Equal(2, plan.Jobs.Length);
        Assert.Equal(GameA, plan.Jobs[0].ExpectedGameId);
        Assert.Equal(GameB, plan.Jobs[1].ExpectedGameId);
        Assert.Equal(new[] { "a.nsz", "update.xci", "dlc.nsz" }, plan.Jobs[0].Sources);
        Assert.Equal(new[] { "b.nsp", "b-update.xcz" }, plan.Jobs[1].Sources);
        Assert.All(plan.Jobs, j => { Assert.Equal("xci", j.Type); Assert.Equal(PackageOperation.Merge, j.Operation); });
        Assert.Contains(plan.Skipped, s => s.Source == "unknown.nsz" && s.Reason == PackageBatchSkipReason.UnknownGame);
        Assert.Contains(plan.Skipped, s => s.Source == "mixed.nsz" && s.Reason == PackageBatchSkipReason.MultipleGames);
    }

    [Fact]
    public void AcrossGamesAcceptsMixedContainersAndDeduplicatesPhysicalFiles()
    {
        var a = File("a.nsp", Base(GameA));
        var b = File("b.xcz", Base(GameB));
        var patch = File("patch.nsz", Update("0100000000000800", GameA));
        var plan = PackageBatchPlanner.Merge(new[] { a, b, patch, a with { PackageIndex = 1 } }, PackageMergeGrouping.AcrossGames, PackageBatchFormat.Nsp);
        var job = Assert.Single(plan.Jobs);
        Assert.Equal("nsp", job.Type);
        Assert.Equal(new[] { "a.nsp", "b.xcz", "patch.nsz" }, job.Sources);
        Assert.Empty(plan.Skipped);
    }

    [Fact]
    public void ExistingSameGameSuperPackageCanBeRepackedWhileLoneOrdinaryGamesAreSkipped()
    {
        var super = File("super.nsz", Base(GameA), Update("0100000000000800", GameA));
        var plan = PackageBatchPlanner.Merge(new[] { super, File("lone.xci", Base(GameB)) }, PackageMergeGrouping.ByGame, PackageBatchFormat.Xci);
        Assert.Equal(PackageOperation.Convert, Assert.Single(plan.Jobs).Operation);
        Assert.Equal("super.nsz", Assert.Single(plan.Jobs[0].Sources));
        Assert.Equal(PackageBatchSkipReason.SingleFile, Assert.Single(plan.Skipped).Reason);
    }

    [Fact]
    public void PresentationRowsCannotHideOtherGamesInsideTheSamePhysicalContainer()
    {
        var a = File("super.nsz", Base(GameA)) with { PackageIndex = 1, SourcePackageCount = 2 };
        var b = File("super.nsz", Base(GameB)) with { PackageIndex = 2, SourcePackageCount = 2 };
        var all = PackageBatchPlanner.Merge(new[] { a, b }, PackageMergeGrouping.ByGame, PackageBatchFormat.Nsp);
        Assert.Empty(all.Jobs);
        Assert.Equal(PackageBatchSkipReason.MultipleGames, Assert.Single(all.Skipped).Reason);
        var incomplete = PackageBatchPlanner.Merge(new[] { a }, PackageMergeGrouping.ByGame, PackageBatchFormat.Nsp);
        Assert.Empty(incomplete.Jobs);
        Assert.Equal(PackageBatchSkipReason.UnknownGame, Assert.Single(incomplete.Skipped).Reason);
    }

    [Fact]
    public void ActualMetadataMustStillMatchThePlannedGameBeforeAnEngineCanRun()
    {
        PackageOperationService.EnsureGameId(GameA, new[] {
            ("0000000000000000", GameA, LibHac.Ncm.ContentMetaType.Application),
            (GameA, "0100000000000800", LibHac.Ncm.ContentMetaType.Patch),
            (GameA, "0100000000001001", LibHac.Ncm.ContentMetaType.AddOnContent) });
        Assert.Throws<System.IO.InvalidDataException>(() => PackageOperationService.EnsureGameId(GameA,
            new[] { (GameB, GameB, LibHac.Ncm.ContentMetaType.Application) }));
        Assert.Throws<System.IO.InvalidDataException>(() => PackageOperationService.EnsureGameId(GameA,
            new[] { ("", "0100000000001001", LibHac.Ncm.ContentMetaType.AddOnContent) }));
    }
    [Fact]
    public void AutoSplitIsSuggestedOnlyForMatchingAdditionalContentsAndNotDuplicatesOrOtherGames()
    {
        var multi = File("multi.nsz", Base(GameA), Base(GameB));
        var duplicate = File("duplicate.nsp", Base(GameA));
        var unrelated = File("unrelated.xci", Base("0100000000004000"));
        Assert.Empty(PackageBatchPlanner.FindMultiGameMergeCandidates(new[] { multi, duplicate, unrelated }));
        var update = File("update.xcz", Update("0100000000000800", GameA));
        Assert.Equal("multi.nsz", Assert.Single(PackageBatchPlanner.FindMultiGameMergeCandidates(new[] { multi, update })).FilePath);
        var newerGame = File("newer.nsp", Base(GameA) with { Version = "65536" });
        Assert.Single(PackageBatchPlanner.FindMultiGameMergeCandidates(new[] { multi, newerGame }));
        var rows = new[] {
            multi with { Packages = new[] { Base(GameA) }, PackageIndex = 1, SourcePackageCount = 2 },
            multi with { Packages = new[] { Base(GameB) }, PackageIndex = 2, SourcePackageCount = 2 }, update };
        Assert.Single(PackageBatchPlanner.FindMultiGameMergeCandidates(rows));
    }
    [Fact]
    public void MergeRequiresExplicitTargetFormat() =>
        Assert.Throws<ArgumentException>(() => PackageBatchPlanner.Merge(Array.Empty<BatchIntegrityResult>(), PackageMergeGrouping.ByGame, PackageBatchFormat.KeepSource));
}