using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using System.Text.RegularExpressions;
using Emignatik.NxFileViewer.Localization;
namespace Emignatik.NxFileViewer.Services.Nsz;
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
