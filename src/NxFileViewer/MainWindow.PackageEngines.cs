using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Emignatik.NxFileViewer.Services.Nsz;
using Emignatik.NxFileViewer.Services.PackageOperations;
using Emignatik.NxFileViewer.Settings;

namespace NxFileViewer.Avalonia;

public partial class MainWindow
{
    private static PackageEngine[] PackageEngines(PackageEngine selection)
    {
        var primary = selection.Primary();
        return selection.HasBackup()
            ? [primary, primary == PackageEngine.Rust ? PackageEngine.Kronos : PackageEngine.Rust]
            : [primary];
    }

    private static string ResolvePackageKronosDirectory(ViewerPreferences preferences)
    {
        var configured = preferences.KronosGuiPath;
        var directory = Directory.Exists(configured) ? configured : Path.GetDirectoryName(configured);
        return PackageEnginePaths.ResolveKronos(
            string.IsNullOrWhiteSpace(directory) ? preferences.SharedSettings.KronosPackageDirectory : directory);
    }

    private static bool PackageEngineAvailable(PackageEngine engine, string rust, string kronos) =>
        engine == PackageEngine.Rust ? File.Exists(rust) :
        OperatingSystem.IsWindows() && !string.IsNullOrWhiteSpace(kronos) &&
        File.Exists(Path.Combine(kronos, "runtime", "python.exe")) &&
        File.Exists(Path.Combine(kronos, "ztools", "squirrel.py"));

    private string RunPackageEngines(PackageEngine selection, PackageOperation operation, IReadOnlyList<string> sources,
        string type, string rust, string kronos, string keys, string job, Action<string> validate,
        PackageProgress progress, CancellationToken token)
    {
        var engines = PackageEngines(selection);
        var failures = new List<string>(engines.Length);
        foreach (var engine in engines)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                if (!PackageEngineAvailable(engine, rust, kronos))
                    throw new FileNotFoundException("Package engine not installed: " + engine);

                var attempt = Path.Combine(job, engine.ToString());
                var output = Path.Combine(attempt, "output");
                Directory.CreateDirectory(output);
                var executable = engine == PackageEngine.Rust ? Path.GetFullPath(rust) :
                    Path.Combine(Path.GetFullPath(kronos), "runtime", "python.exe");
                var inputList = Path.Combine(attempt, "inputs.txt");
                if (engine == PackageEngine.Kronos && operation == PackageOperation.Merge)
                    File.WriteAllLines(inputList, sources);
                var arguments = PackageCommandArguments.BuildArguments(engine, operation, sources, output, type, keys, inputList).ToList();
                if (engine == PackageEngine.Kronos)
                {
                    var tools = Path.Combine(attempt, "ztools");
                    CopyKronosDirectory(Path.Combine(kronos, "ztools"), tools, token);
                    var config = Path.Combine(kronos, "zconfig");
                    if (Directory.Exists(config))
                        CopyKronosDirectory(config, Path.Combine(attempt, "zconfig"), token);
                    File.Copy(keys, Path.Combine(tools, "keys.txt"));
                    const string bootstrap = "import sys,runpy,os; script=sys.argv.pop(1); " +
                        "sys.path=[p for p in sys.path if 'ztools' not in p.lower()]; " +
                        "sys.path.insert(0,os.path.dirname(script)); runpy.run_path(script,run_name='__main__')";
                    arguments.InsertRange(0, ["-c", bootstrap, Path.Combine(tools, "squirrel.py")]);
                }

                var phase = $"{engine} · {operation} · {Path.GetFileName(sources[0])}";
                progress(phase);
                QueuePluginLog("INFO", "PACKAGE", phase);
                var reportedError = false;
                NszProcess.Run(executable, arguments, attempt, token, onProgress: update => progress($"{phase} · {update.Details}", update.Fraction),
                    onDiagnostic: line =>
                    {
                        QueuePluginLog("DEBUG", "PACKAGE", line);
                        if (line.Contains("Traceback (", StringComparison.OrdinalIgnoreCase) ||
                            line.Contains("Exception:", StringComparison.OrdinalIgnoreCase)) reportedError = true;
                    });
                token.ThrowIfCancellationRequested();
                if (reportedError || Directory.EnumerateFiles(output, "*", SearchOption.AllDirectories)
                    .Any(path => !Path.GetExtension(path).Equals("." + type, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidDataException("Unexpected package engine output.");
                validate(output);
                return output;
            }
            catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or
                System.ComponentModel.Win32Exception or LibHac.Common.LibHacException or LibHac.NSZ.NczFormatException or ZstdSharp.ZstdException)
            {
                var message = $"{engine} · {error.Message}";
                failures.Add(message);
                QueuePluginLog("WARN", "PACKAGE", message);
                progress(message);
            }
        }
        token.ThrowIfCancellationRequested();
        throw new InvalidDataException(string.Join(Environment.NewLine, failures));
    }

    private static void CopyKronosDirectory(string source, string destination, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if ((File.GetAttributes(source) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Linked directories are not supported in the Kronos package.");
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source))
        {
            token.ThrowIfCancellationRequested();
            var name = Path.GetFileName(file);
            if (name.Equals("keys.txt", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("dev_keys.txt", StringComparison.OrdinalIgnoreCase)) continue;
            if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Linked files are not supported in the Kronos package.");
            File.Copy(file, Path.Combine(destination, name));
        }
        foreach (var directory in Directory.EnumerateDirectories(source))
        {
            var name = Path.GetFileName(directory);
            if (name is not ("__pycache__" or "DB"))
                CopyKronosDirectory(directory, Path.Combine(destination, name), token);
        }
    }
}
