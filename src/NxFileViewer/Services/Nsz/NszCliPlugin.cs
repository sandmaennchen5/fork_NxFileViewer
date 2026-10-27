using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Text;
using Microsoft.Extensions.Logging;
using Emignatik.NxFileViewer.Localization;
using Emignatik.NxFileViewer.Services.BackgroundTask;
using Emignatik.NxFileViewer.Services.KeysManagement;
using Emignatik.NxFileViewer.Settings;
using Emignatik.NxFileViewer.Services.PackageOperations;

namespace Emignatik.NxFileViewer.Services.Nsz;

public sealed class NszCliPlugin(NszPluginManager manager, IKeySetProviderService keys, IAppSettings settings, ILogger<NszCliPlugin>? logger = null) : INszEnginePlugin
{
    public void Convert(string source, string outputDirectory, NszOperation operation,
        IProgressReporter progress, CancellationToken cancellationToken) =>
        ConvertUsingEngine(settings.NszCompressionEngine.PrimaryRust(), source, outputDirectory, operation, progress, cancellationToken);

    public void ConvertUsingEngine(bool useRust, string source, string outputDirectory, NszOperation operation,
        IProgressReporter progress, CancellationToken cancellationToken)
    {
        if (!settings.NszPluginActive) throw new InvalidOperationException(LocalizationManager.Instance.Current.Keys.Features_PluginsDisabled);
        var engineName = useRust ? "nscb_rust" : "nicoboss/nsz";
        var executable = useRust ? PackageEnginePaths.ResolveRust(settings.RustPackageExecutablePath) : manager.ExecutablePath;
        if (!File.Exists(executable))
            throw new FileNotFoundException(useRust ? $"{engineName}: {LocalizationManager.Instance.Current.Keys.Package_Missing} ({executable})" : LocalizationManager.Instance.Current.Keys.Nsz_NotInstalled, executable);
        executable = Path.GetFullPath(executable);
        var keyPath = keys.ActualProdKeysFilePath;
        if (keyPath == null) throw new InvalidOperationException(LocalizationManager.Instance.Current.Keys.Nsz_KeysMissing);
        progress.SetMode(true);
        progress.SetText((operation == NszOperation.Compress ? LocalizationManager.Instance.Current.Keys.Nsz_Compress :
            LocalizationManager.Instance.Current.Keys.Nsz_Decompress) + " " + Path.GetFileName(source));
        using var alias = new NszInputAlias(source, outputDirectory, keyPath, cancellationToken, logger);
        var arguments = useRust
            ? BuildRustArguments(alias.SourceArgument, ".", alias.KeysArgument, settings.NszCompressionLevel, operation)
            : BuildArguments(alias.SourceArgument, ".", alias.KeysArgument, operation, settings.NszCompressionLevel,
            settings.NszCompressionMode, settings.NszBlockSizeExponent, useRelativePaths: true);
        var action = (operation == NszOperation.Compress ? LocalizationManager.Instance.Current.Keys.Nsz_Compress :
            LocalizationManager.Instance.Current.Keys.Nsz_Decompress) + " " + Path.GetFileName(source);
        logger?.LogInformation("{Engine} {Operation}: source={Source}; output={Output}; keys file={Keys}; executable={Executable}; arguments={Arguments}",
            engineName, operation, source, outputDirectory, keyPath, executable, string.Join(" ", arguments));
        var timer = Stopwatch.StartNew();
        var diagnostics = new Queue<string>();
        var diagnosticGate = new object();
        try
        {
            NszProcess.Run(executable, arguments, outputDirectory, cancellationToken, update =>
            {
                progress.SetMode(false);
                progress.SetPercentage(update.Fraction);
                progress.SetText(action + " â€” " + update.Details);
            }, line =>
            {
                lock (diagnosticGate)
                {
                    diagnostics.Enqueue(line);
                    while (diagnostics.Count > 30) diagnostics.Dequeue();
                }
                logger?.LogDebug("{Engine}: {Output}", engineName, line);
            });
            cancellationToken.ThrowIfCancellationRequested();
            alias.RestoreOutputName(operation);
            if (Path.GetExtension(source).Equals(".xci", StringComparison.OrdinalIgnoreCase) ||
                Path.GetExtension(source).Equals(".xcz", StringComparison.OrdinalIgnoreCase))
            {
                var extension = operation == NszOperation.Compress ? ".xcz" : ".xci";
                XciPartitionIntegrity.PreserveStagedPrefix(source,
                    Path.Combine(outputDirectory, Path.GetFileNameWithoutExtension(source) + extension), cancellationToken);
            }
            if (operation == NszOperation.Decompress && Path.GetExtension(source).Equals(".xcz", StringComparison.OrdinalIgnoreCase))
            {
                progress.SetMode(true);
                progress.SetText(LocalizationManager.Instance.Current.Keys.Nsz_PhaseLayout);
                var output = Path.Combine(outputDirectory, Path.GetFileNameWithoutExtension(source) + ".xci");
                var corrected = XciPartitionIntegrity.CorrectStagedLayout(source, output, cancellationToken);
                var rebuilt = XciPartitionIntegrity.NormalizeStagedGamecardHeader(output, cancellationToken);
                logger?.LogInformation("XCI Gamecard layout rebuilt: {Rebuilt}", rebuilt);
                logger?.LogInformation("XCZ output layout checked; corrected {Count} HFS0 entries after validating their content hashes.", corrected);
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException)
        {
            lock (diagnosticGate)
                logger?.LogError("{Engine} process failed after {Seconds:F1} seconds. Last diagnostic messages:\n{Diagnostics}",
                    engineName, timer.Elapsed.TotalSeconds, string.Join(Environment.NewLine, diagnostics));
            throw;
        }
        logger?.LogInformation("{Engine} process completed successfully after {Seconds:F1} seconds.", engineName, timer.Elapsed.TotalSeconds);
        progress.SetMode(false);
    }

    public static IReadOnlyList<string> BuildRustArguments(string source, string output, string keys, int level,
        NszOperation operation = NszOperation.Compress)
    {
        var args = new List<string> { operation == NszOperation.Compress ? "--compress" : "--decompress",
            source, "--keys", keys, "-o", output };
        if (operation == NszOperation.Compress)
            args.AddRange(new[] { "--level", Math.Clamp(level, 1, 22).ToString(System.Globalization.CultureInfo.InvariantCulture) });
        return args;
    }

    public static IReadOnlyList<string> BuildArguments(string source, string output, string keys,
        NszOperation operation, int level, NszCompressionMode mode = NszCompressionMode.Auto, int blockSizeExponent = 20, bool useRelativePaths = false)
    {
        var args = new List<string> { operation == NszOperation.Compress ? "-C" : "-D", "--keys", keys, "-o", output };
        if (operation == NszOperation.Compress)
        {
            if (mode == NszCompressionMode.Solid) args.Add("--solid");
            else if (mode == NszCompressionMode.Block)
                args.AddRange(new[] { "--block", "--bs", Math.Clamp(blockSizeExponent, 14, 32).ToString(System.Globalization.CultureInfo.InvariantCulture) });
            // Preserve extra partitions/files; NSZ additionally verifies its own round trip.
            args.AddRange(new[] { "-K", "-V", "-l", Math.Clamp(level, 1, 22).ToString(System.Globalization.CultureInfo.InvariantCulture) });
        }
        args.Add(useRelativePaths ? source : Path.GetFullPath(source));
        return args;
    }
}

public static class NszProcess
{
    public static string Run(string executable, IEnumerable<string> arguments, string workingDirectory,
        CancellationToken cancellationToken, Action<NszProgressUpdate>? onProgress = null, Action<string>? onDiagnostic = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var temporary = new NszTemporaryDirectory();
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true,
            RedirectStandardError = true, RedirectStandardInput = true, WorkingDirectory = workingDirectory,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
        };
        start.Environment["PYTHONUNBUFFERED"] = "1";
        start.Environment["PYTHONUTF8"] = "1";
        start.Environment["PYTHONIOENCODING"] = "utf-8";
        start.Environment["TEMP"] = temporary.Path;
        start.Environment["TMP"] = temporary.Path;
        start.Environment["TMPDIR"] = temporary.Path;
        start.Environment["KIVY_HOME"] = System.IO.Path.Combine(AppContext.BaseDirectory, "Cache", "NSZ", "Kivy");
        start.Environment["XDG_CACHE_HOME"] = System.IO.Path.Combine(AppContext.BaseDirectory, "Cache", "NSZ");
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = new Process { StartInfo = start };
        cancellationToken.ThrowIfCancellationRequested();
        process.Start();
        process.StandardInput.Close(); // Avoid an interactive prompt in older releases.
        // Drain both streams; retain a bounded help response and sanitize diagnostic output.
        var progressGate = new object();
        var timer = Stopwatch.StartNew();
        long lastUpdate = -1000;
        void Report(NszProgressUpdate update)
        {
            if (onProgress == null) return;
            lock (progressGate)
            {
                if (timer.ElapsedMilliseconds - lastUpdate < 200 && update.Fraction < 1) return;
                lastUpdate = timer.ElapsedMilliseconds;
                onProgress(update);
            }
        }
        var stdout = Drain(process.StandardOutput, Report, onDiagnostic, "stdout");
        var stderr = Drain(process.StandardError, Report, onDiagnostic, "stderr");
        using var registration = cancellationToken.Register(() =>
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { }
        });
        try
        {
            process.WaitForExitAsync(CancellationToken.None).GetAwaiter().GetResult();
            Task.WhenAll(stdout, stderr).GetAwaiter().GetResult();
            onDiagnostic?.Invoke("Exit code: " + process.ExitCode);
            cancellationToken.ThrowIfCancellationRequested();
            if (process.ExitCode != 0)
            {
                // Classify only a known bootloader failure. Raw output can contain key values.
                var message = "NSZ exit code: " + process.ExitCode;
                if (stderr.Result.Contains("Failed to load Python DLL", StringComparison.OrdinalIgnoreCase) &&
                    stderr.Result.Contains("[PYI-", StringComparison.Ordinal))
                    throw new NszRuntimeStartupException(message + ". " + LocalizationManager.Instance.Current.Keys.Nsz_PythonRuntimeFailed);
                throw new IOException(message);
            }
            return stdout.Result;
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
            }
        }
    }

    private sealed class NszTemporaryDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(AppContext.BaseDirectory, "Temp", "NSZ", Guid.NewGuid().ToString("N"));
        public NszTemporaryDirectory() => Directory.CreateDirectory(Path);
        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    public static string SanitizeDiagnostic(string line) =>
        Regex.Replace(line, @"\b[0-9a-fA-F]{32,}\b", "[redacted]");

    private static async Task<string> Drain(StreamReader reader, Action<NszProgressUpdate> report, Action<string>? diagnostic, string stream)
    {
        var retained = new System.Text.StringBuilder();
        var line = new System.Text.StringBuilder();
        var buffer = new char[4096];
        void Emit()
        {
            var text = line.ToString();
            if (NszProgressUpdate.TryParse(text, out var update)) report(update!);
            else if (!string.IsNullOrWhiteSpace(text)) diagnostic?.Invoke(stream + ": " + SanitizeDiagnostic(text));
            line.Clear();
        }
        int count;
        while ((count = await reader.ReadAsync(buffer.AsMemory()).ConfigureAwait(false)) > 0)
        {
            if (retained.Length < 65536) retained.Append(buffer, 0, Math.Min(count, 65536 - retained.Length));
            for (var i = 0; i < count; i++)
            {
                if (buffer[i] is '\r' or '\n') Emit();
                else { if (line.Length >= 2048) line.Clear(); line.Append(buffer[i]); }
            }
        }
        if (line.Length > 0) Emit();
        return retained.ToString();
    }
}

public sealed class NszRuntimeStartupException(string message) : IOException(message);
