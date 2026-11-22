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
        NszOperation operation = NszOperation.Compress) => NszCommandArguments.BuildRustArguments(source, output, keys, level, operation);
    public static IReadOnlyList<string> BuildArguments(string source, string output, string keys, NszOperation operation,
        int level, NszCompressionMode mode = NszCompressionMode.Auto, int blockSizeExponent = 20, bool useRelativePaths = false) =>
        NszCommandArguments.BuildArguments(source, output, keys, operation, level, mode, blockSizeExponent, useRelativePaths);
}
