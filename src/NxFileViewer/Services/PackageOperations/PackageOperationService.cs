using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Emignatik.NxFileViewer.FileLoading;
using Emignatik.NxFileViewer.Localization;
using Emignatik.NxFileViewer.Models.Overview;
using Emignatik.NxFileViewer.Services.BackgroundTask;
using Emignatik.NxFileViewer.Services.Nsz;
using Emignatik.NxFileViewer.Settings;

namespace Emignatik.NxFileViewer.Services.PackageOperations;

public enum PackageOperation { Merge, Convert, Split }
public sealed record PackageEngineConfiguration(PackageEngine Engine, string RustExecutable, string KronosDirectory)
{
    public static PackageEngineConfiguration Capture(IAppSettings settings) =>
        new(settings.PackageEngine, settings.RustPackageExecutablePath, settings.KronosPackageDirectory);
    public bool IsAvailable => Engine switch
    {
        PackageEngine.Rust => File.Exists(RustExecutable),
        PackageEngine.Kronos => File.Exists(Path.Combine(KronosDirectory, "runtime", "python.exe")) &&
            File.Exists(Path.Combine(KronosDirectory, "ztools", "squirrel.py")),
        _ => false
    };
}

/// <summary>Both engines write to an isolated job. Only validated outputs are published.</summary>
public sealed class PackageOperationService(IFileLoader loader, IConversionVerifier verifier)
{
    public static IReadOnlyList<string> BuildArguments(PackageEngine engine, PackageOperation operation,
        IReadOnlyList<string> sources, string output, string type, string keys, string? inputList = null)
    {
        if (type is not ("nsp" or "xci")) throw new ArgumentException("Unsupported target type.");
        var args = new List<string>();
        args.Add((engine, operation) switch
        {
            (PackageEngine.Rust, PackageOperation.Merge) => "--direct_multi",
            (PackageEngine.Rust, PackageOperation.Convert) => "--direct_creation",
            (PackageEngine.Rust, PackageOperation.Split) => "--dspl",
            (PackageEngine.Kronos, PackageOperation.Merge) => "--direct_multi",
            (PackageEngine.Kronos, PackageOperation.Convert) => "--direct_creation",
            (PackageEngine.Kronos, PackageOperation.Split) => "--direct_splitter",
            _ => throw new ArgumentOutOfRangeException(nameof(engine))
        });
        if (engine == PackageEngine.Kronos && operation == PackageOperation.Merge)
        {
            if (inputList == null) throw new ArgumentException("Kronos merge requires an input list.");
            args.AddRange(new[] { "calculate", "-tfile", inputList });
        }
        else args.AddRange(sources);
        args.AddRange(new[] { "-o", output, "-t", type });
        if (engine == PackageEngine.Kronos) args.AddRange(new[] { "-pv", "false", "-kp", "false" });
        if (engine == PackageEngine.Rust) args.AddRange(new[] { "--keys", keys });
        return args;
    }

    public string Run(PackageEngineConfiguration configuration, PackageOperation operation, string[] sources,
        string destination, string type, string keyPath, IProgressReporter progress, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var text = LocalizationManager.Instance.Current.Keys;
        if (!configuration.IsAvailable) throw new FileNotFoundException(text.Package_Missing);
        if (!File.Exists(keyPath)) throw new FileNotFoundException(text.Nsz_KeysMissing);
        if (sources.Length == 0 || (operation != PackageOperation.Merge && sources.Length != 1))
            throw new ArgumentException("Select one source for convert/split, or multiple sources for merge.");
        sources = sources.Select(Path.GetFullPath).ToArray();
        if (sources.Distinct(StringComparer.OrdinalIgnoreCase).Count() != sources.Length ||
            sources.Any(p => !File.Exists(p) || Path.GetExtension(p).ToLowerInvariant() is not (".nsp" or ".xci")))
            throw new ArgumentException(text.Package_Inputs);
        var expected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var titleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var locks = new List<FileStream>();
        Directory.CreateDirectory(destination);
        var job = Path.Combine(Path.GetFullPath(destination), ".nxfv-package-" + Guid.NewGuid().ToString("N"));
        var output = Path.Combine(job, "output");
        Directory.CreateDirectory(output);
        var preserveValidatedOutputs = false;
        string? publicationDirectory = null;
        try
        {
            foreach (var source in sources)
            {
                token.ThrowIfCancellationRequested();
                locks.Add(new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read));
                CheckIntegrity(source, progress, token);
                using var file = loader.Load(source, token);
                foreach (var cnmt in file.Overview.CnmtContainers.Select(c => c.CnmtItem))
                {
                    if (!titleIds.Add(cnmt.TitleId)) throw new InvalidDataException("Select only one version of title " + cnmt.TitleId);
                    expected.Add(cnmt.TitleId + ":" + cnmt.TitleVersion);
                }
            }
            if (expected.Count == 0) throw new InvalidDataException(text.Package_Invalid);
            progress.SetMode(true);
            progress.SetText(configuration.Engine + " / " + operation);
            var executable = Path.GetFullPath(configuration.RustExecutable.Length == 0 ? "." : configuration.RustExecutable);
            var inputList = Path.Combine(job, "inputs.txt");
            File.WriteAllLines(inputList, sources, new System.Text.UTF8Encoding(false));
            var args = BuildArguments(configuration.Engine, operation, sources, output, type, Path.GetFullPath(keyPath), inputList).ToList();
            if (configuration.Engine == PackageEngine.Kronos)
            {
                var root = Path.GetFullPath(configuration.KronosDirectory);
                executable = Path.Combine(root, "runtime", "python.exe");
                // Python NSCB changes cwd and uses fixed key/config paths. Keep these job-local.
                CopyTree(Path.Combine(root, "ztools"), Path.Combine(job, "ztools"), token);
                if (Directory.Exists(Path.Combine(root, "zconfig")))
                    CopyTree(Path.Combine(root, "zconfig"), Path.Combine(job, "zconfig"), token);
                File.Copy(keyPath, Path.Combine(job, "ztools", "keys.txt"), true);
                const string bootstrap = "import sys,runpy,os; script=sys.argv.pop(1); " +
                    "sys.path=[p for p in sys.path if 'ztools' not in p.lower()]; " +
                    "sys.path.insert(0,os.path.dirname(script)); runpy.run_path(script,run_name='__main__')";
                args.InsertRange(0, new[] { "-c", bootstrap, Path.Combine(job, "ztools", "squirrel.py") });
            }
            var reportedError = false;
            NszProcess.Run(executable, args, job, token, onDiagnostic: line =>
            {
                if (line.Contains("Exception:", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("Traceback (", StringComparison.OrdinalIgnoreCase)) reportedError = true;
            });
            token.ThrowIfCancellationRequested();
            if (reportedError) throw new InvalidDataException(text.Package_Invalid);
            var outputs = Directory.GetFiles(output, "*", SearchOption.AllDirectories)
                .Where(p => Path.GetExtension(p).Equals("." + type, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (outputs.Length == 0 || (operation != PackageOperation.Split && outputs.Length != 1))
                throw new InvalidDataException(text.Package_Invalid);
            var actual = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var path in outputs)
            {
                CheckIntegrity(path, progress, token);
                using var file = loader.Load(path, token);
                var titles = file.Overview.CnmtContainers.Select(c => c.CnmtItem).ToArray();
                if (titles.Length == 0 || (operation == PackageOperation.Split && titles.Length != 1))
                    throw new InvalidDataException(text.Package_Invalid);
                foreach (var title in titles)
                    if (!actual.Add(title.TitleId + ":" + title.TitleVersion)) throw new InvalidDataException(text.Package_Invalid);
            }
            if (!expected.SetEquals(actual)) throw new InvalidDataException(text.Package_Invalid);
            token.ThrowIfCancellationRequested();
            var published = Path.Combine(Path.GetFullPath(destination), "NXFV-" + operation + "-" + Guid.NewGuid().ToString("N"));
            publicationDirectory = published;
            preserveValidatedOutputs = true;
            PackageOutputPublisher.Publish(output, published, token, files: outputs);
            preserveValidatedOutputs = false;
            return published;
        }
        catch (Exception ex) when (preserveValidatedOutputs && ex is IOException or UnauthorizedAccessException or OperationCanceledException)
        {
            // Do not delete validated large outputs when publication is blocked or cancelled.
            throw new IOException(text.Package_PublicationRecovery + Environment.NewLine + job +
                Environment.NewLine + publicationDirectory + Environment.NewLine + ex.Message, ex);
        }
        finally
        {
            foreach (var stream in locks) stream.Dispose();
            if (preserveValidatedOutputs)
            {
                // Keep only result data; the private engine workspace contains the temporary keys.
                CleanupDirectory(Path.Combine(job, "ztools"));
                CleanupDirectory(Path.Combine(job, "zconfig"));
            }
            else CleanupDirectory(job);
        }
    }

    private static void CleanupDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private void CheckIntegrity(string path, IProgressReporter progress, CancellationToken token)
    {
        var result = verifier.Verify(path, progress, token);
        // Repacked NCAs may have changed signatures, but their hashes must be valid.
        if (result.Integrity is not (NcasIntegrity.Original or NcasIntegrity.Modified))
            throw new InvalidDataException(LocalizationManager.Instance.Current.Keys.Package_Invalid + " " + result.Integrity);
    }

    private static void CopyTree(string source, string target, CancellationToken token)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.GetFiles(source))
        {
            token.ThrowIfCancellationRequested();
            if (Path.GetFileName(file).Equals("keys.txt", StringComparison.OrdinalIgnoreCase) ||
                Path.GetFileName(file).Equals("dev_keys.txt", StringComparison.OrdinalIgnoreCase)) continue;
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)), false);
        }
        foreach (var directory in Directory.GetDirectories(source))
        {
            if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Linked directories are not supported in the Kronos package.");
            if (Path.GetFileName(directory) is "__pycache__" or "DB") continue;
            CopyTree(directory, Path.Combine(target, Path.GetFileName(directory)), token);
        }
    }
}
