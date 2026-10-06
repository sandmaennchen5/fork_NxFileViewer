using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Emignatik.NxFileViewer.Localization;
using Emignatik.NxFileViewer.Services.BackgroundTask;
using Emignatik.NxFileViewer.Services.KeysManagement;
using Emignatik.NxFileViewer.Settings;

namespace Emignatik.NxFileViewer.Services.Nsz;

public sealed class NszCliPlugin(NszPluginManager manager, IKeySetProviderService keys, IAppSettings settings) : INszPlugin
{
    public void Convert(string source, string outputDirectory, NszOperation operation,
        IProgressReporter progress, CancellationToken cancellationToken)
    {
        var executable = manager.ExecutablePath;
        if (!File.Exists(executable))
            throw new FileNotFoundException(LocalizationManager.Instance.Current.Keys.Nsz_NotInstalled);
        var keyPath = keys.ActualProdKeysFilePath;
        if (keyPath == null) throw new InvalidOperationException(LocalizationManager.Instance.Current.Keys.Nsz_KeysMissing);
        progress.SetMode(true);
        progress.SetText((operation == NszOperation.Compress ? LocalizationManager.Instance.Current.Keys.Nsz_Compress :
            LocalizationManager.Instance.Current.Keys.Nsz_Decompress) + " " + Path.GetFileName(source));
        var arguments = BuildArguments(source, outputDirectory, keyPath, operation, settings.NszCompressionLevel,
            settings.NszCompressionMode, settings.NszBlockSizeExponent);
        var action = (operation == NszOperation.Compress ? LocalizationManager.Instance.Current.Keys.Nsz_Compress :
            LocalizationManager.Instance.Current.Keys.Nsz_Decompress) + " " + Path.GetFileName(source);
        NszProcess.Run(executable, arguments, outputDirectory, cancellationToken, update =>
        {
            progress.SetMode(false);
            progress.SetPercentage(update.Fraction);
            progress.SetText(action + " — " + update.Details);
        });
        progress.SetMode(false);
    }

    public static IReadOnlyList<string> BuildArguments(string source, string output, string keys,
        NszOperation operation, int level, NszCompressionMode mode = NszCompressionMode.Auto, int blockSizeExponent = 20)
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
        args.Add(Path.GetFullPath(source));
        return args;
    }
}

public static class NszProcess
{
    public static string Run(string executable, IEnumerable<string> arguments, string workingDirectory,
        CancellationToken cancellationToken, Action<NszProgressUpdate>? onProgress = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var temporary = new NszTemporaryDirectory();
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true,
            RedirectStandardError = true, RedirectStandardInput = true, WorkingDirectory = workingDirectory
        };
        start.Environment["PYTHONUNBUFFERED"] = "1";
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
        // Always drain both streams; retain a bounded help response, never log keys or raw output.
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
        var stdout = Drain(process.StandardOutput, Report);
        var stderr = Drain(process.StandardError, Report);
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

    private static async Task<string> Drain(StreamReader reader, Action<NszProgressUpdate> report)
    {
        var retained = new System.Text.StringBuilder();
        var line = new System.Text.StringBuilder();
        var buffer = new char[4096];
        void Emit()
        {
            if (NszProgressUpdate.TryParse(line.ToString(), out var update)) report(update!);
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
