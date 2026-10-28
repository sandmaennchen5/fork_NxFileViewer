using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Emignatik.NxFileViewer.Settings;
using Emignatik.NxFileViewer.Services.Nsz;
using Emignatik.NxFileViewer.Services.Integrity;
using Emignatik.NxFileViewer.Services.BackgroundTask;
using Emignatik.NxFileViewer.Models.Overview;
using Xunit;
namespace Emignatik.NxFileViewer.Test.Services.Nsz;
public sealed class NszFallbackTest
{
    private sealed class Reporter : IProgressReporter { public void SetMode(bool b) { } public void SetText(string s) { } public void SetPercentage(double p) { } }
    private sealed class Engine(string fault, CancellationTokenSource? cancel) : INszEnginePlugin
    {
        public readonly List<bool> Calls = new();
        public void Convert(string s, string d, NszOperation o, IProgressReporter p, CancellationToken t) => throw new Exception("Explicit engine required.");
        public void ConvertUsingEngine(bool rust, string source, string output, NszOperation operation, IProgressReporter p, CancellationToken token)
        {
            Calls.Add(rust);
            if (Calls.Count == 1 || fault == "both")
            {
                File.WriteAllText(Path.Combine(output, "partial.tmp"), "partial");
                if (fault == "cancel") { cancel!.Cancel(); throw new OperationCanceledException(token); }
                if (fault is "process" or "both") throw new IOException("engine failed");
                if (fault == "missing") return;
            }
            Assert.False(Calls.Count > 1 && File.Exists(Path.Combine(output, "partial.tmp")));
            File.WriteAllText(Path.Combine(output, Path.GetFileNameWithoutExtension(source) + ".nsz"), "converted");
        }
    }
    private sealed class Verifier(string source, bool invalid) : IConversionVerifier
    {
        private int _outputs;
        public BatchIntegrityResult Verify(string path, IProgressReporter p, CancellationToken t)
        {
            var result = path == source || !invalid || ++_outputs > 1 ? NcasIntegrity.Original : NcasIntegrity.Corrupted;
            return new(path, "NSP", "", "", "", result, null);
        }
    }
    [Theory]
    [InlineData(NszCompressionEngine.RustWithNszBackup, "process")]
    [InlineData(NszCompressionEngine.NszWithRustBackup, "missing")]
    [InlineData(NszCompressionEngine.RustWithNszBackup, "invalid")]
    public void BackupUsesFreshWorkspaceAndOnlyPublishesVerifiedOutput(NszCompressionEngine mode, string fault)
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var source = Path.Combine(root, "game.nsp"); File.WriteAllText(source, "original");
            var engine = new Engine(fault, null);
            var service = new PackageConversionService(engine, new Verifier(source, fault == "invalid"), settings: new AppSettings { NszCompressionEngine = mode });
            var result = service.Convert(source, Path.Combine(root, "output"), NszOperation.Compress, new Reporter(), TestContext.Current.CancellationToken);
            Assert.Equal(new[] { mode.PrimaryRust(), !mode.PrimaryRust() }, engine.Calls);
            Assert.Equal("converted", File.ReadAllText(result.OutputPath));
            Assert.Equal("original", File.ReadAllText(source));
            Assert.Empty(Directory.GetDirectories(Path.Combine(root, "output")));
        } finally { Directory.Delete(root, true); }
    }
    [Theory]
    [InlineData("cancel")]
    [InlineData("both")]
    public void CancellationAndDoubleFailurePreserveOriginalAndExistingDestination(string fault)
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var source = Path.Combine(root, "game.nsp"); File.WriteAllText(source, "original");
            var output = Path.Combine(root, "output"); Directory.CreateDirectory(output);
            var old = Path.Combine(output, "game.nsz"); File.WriteAllText(old, "previous output");
            using var cancel = new CancellationTokenSource();
            var engine = new Engine(fault, cancel);
            var service = new PackageConversionService(engine, new Verifier(source, false), settings: new AppSettings { NszCompressionEngine = NszCompressionEngine.RustWithNszBackup });
            if (fault == "cancel")
                Assert.ThrowsAny<OperationCanceledException>(() => service.Convert(source, output, NszOperation.Compress, new Reporter(), cancel.Token, _ => OutputConflictResolution.Replace, true));
            else Assert.Throws<AggregateException>(() => service.Convert(source, output, NszOperation.Compress, new Reporter(), cancel.Token, _ => OutputConflictResolution.Replace, true));
            Assert.Equal(fault == "cancel" ? 1 : 2, engine.Calls.Count);
            Assert.Equal("original", File.ReadAllText(source)); Assert.Equal("previous output", File.ReadAllText(old));
            Assert.Empty(Directory.GetDirectories(output));
        } finally { Directory.Delete(root, true); }
    }
}