using System;
using System.IO;
using System.Linq;
using System.Threading;
using Emignatik.NxFileViewer.Models.Overview;
using Emignatik.NxFileViewer.Services.Integrity;
using Emignatik.NxFileViewer.Services.PackageOperations;
using Emignatik.NxFileViewer.Services.BackgroundTask;
using Emignatik.NxFileViewer.Settings;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services;

public sealed class PackageMergePreparationTest
{
    private const string GameA = "0100000000000000";
    private const string GameB = "0100000000002000";
    private static BatchIntegrityResult Source(string path, string game, string title = "") =>
        new(path, "NSP", "", "", "", NcasIntegrity.Original, null)
        { Packages = new[] { new BatchPackageSummary("", title.Length == 0 ? game : title, "Application", "0") { BaseTitleId = game } } };
    private sealed class Backend : IPackageMergePreparationBackend
    {
        public bool Fail;
        public CancellationTokenSource? Cancel;
        public string Split(PackageEngineConfiguration config, string source, string destination, string keys, IProgressReporter progress, CancellationToken token)
        {
            Directory.CreateDirectory(destination);
            File.WriteAllText(Path.Combine(destination, "a.nsp"), "temporary game A");
            File.WriteAllText(Path.Combine(destination, "b.nsp"), "temporary game B");
            if (Fail) throw new IOException("fixture split failure");
            Cancel?.Cancel();
            token.ThrowIfCancellationRequested();
            return destination;
        }
        public BatchIntegrityResult Read(string path, CancellationToken token) =>
            Source(path, Path.GetFileName(path) == "a.nsp" ? GameA : GameB);
    }
    private sealed class Reporter : IProgressReporter
    {
        public void SetMode(bool value) { }
        public void SetText(string value) { }
        public void SetPercentage(double value) { }
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SplitPartsFeedMatchingGameGroupsAndCleanupKeepsOriginalsAndFinalOutputs(bool fail)
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var source = Path.Combine(root, "multi.nsz");
            File.WriteAllText(source, "original");
            var multi = Source(source, GameA) with { Packages = new[] { Source("", GameA).Packages[0], Source("", GameB).Packages[0] } };
            var update = Source(Path.Combine(root, "update.xcz"), GameA, "0100000000000800");
            var final = Path.Combine(root, "final.nsp");
            File.WriteAllText(final, "published output");
            using (var preparation = new PackageMergePreparation(new Backend { Fail = fail }, root))
            {
                var plan = preparation.Prepare(new(PackageEngine.Rust, "", ""), new[] { multi, update }, new[] { multi },
                    PackageBatchFormat.Xci, "fixture.keys", new Reporter(), TestContext.Current.CancellationToken);
                if (fail)
                {
                    Assert.Single(preparation.Errors);
                    Assert.Empty(plan.Jobs);
                }
                else
                {
                    var job = Assert.Single(plan.Jobs);
                    Assert.Equal(GameA, job.ExpectedGameId);
                    Assert.Equal("xci", job.Type);
                    Assert.Contains(update.FilePath, job.Sources);
                    Assert.Contains(job.Sources, p => Path.GetFileName(p) == "a.nsp" && File.Exists(p));
                    Assert.DoesNotContain(job.Sources, p => p == source || Path.GetFileName(p) == "b.nsp");
                    Assert.Empty(preparation.Errors);
                }
            }
            Assert.Empty(Directory.GetDirectories(root, ".nxfv-merge-preparation-*"));
            Assert.Equal("original", File.ReadAllText(source));
            Assert.Equal("published output", File.ReadAllText(final));
        }
        finally { Directory.Delete(root, true); }
    }
    [Fact]
    public void CancellationCleansTemporaryPartsAndDoesNotAlterOriginal()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var source = Path.Combine(root, "multi.nsz");
            File.WriteAllText(source, "original");
            using var cancel = new CancellationTokenSource();
            using (var preparation = new PackageMergePreparation(new Backend { Cancel = cancel }, root))
                Assert.ThrowsAny<OperationCanceledException>(() => preparation.Prepare(new(PackageEngine.Rust, "", ""),
                    new[] { Source(source, GameA) }, new[] { Source(source, GameA) }, PackageBatchFormat.Nsp,
                    "fixture.keys", new Reporter(), cancel.Token));
            Assert.Empty(Directory.GetDirectories(root, ".nxfv-merge-preparation-*"));
            Assert.Equal("original", File.ReadAllText(source));
        }
        finally { Directory.Delete(root, true); }
    }
}