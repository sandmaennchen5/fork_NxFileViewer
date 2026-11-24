using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Emignatik.NxFileViewer.Services.Nsz;
using Emignatik.NxFileViewer.Services.PackageOperations;
using Emignatik.NxFileViewer.Settings;
namespace NxFileViewer.Avalonia;
public partial class MainWindow
{
    private async void RunOpenedPackageAction(object? sender, RoutedEventArgs e)
    {
        if (_fileVerificationCancellation != null || _overviewPackage is not { } package ||
            sender is not Button { Tag: string action } button) return;
        await RunPackageActionAsync(action, [package.Path], package.Path, button.Content?.ToString(), workspace: FileTab);
    }

    private async void RunBatchPackageAction(object? sender, RoutedEventArgs e)
    {
        if (_batchCancellation != null || (sender as Button)?.Tag is not string action) return;

        var rows = FilteredBatchRows().DistinctBy(row => row.Path).Where(row => !Emignatik.NxFileViewer.Services.Mtp.MtpDirectSource.IsDirect(row.Path)).Where(row => !row.HasError && row.FileType is "NSP" or "NSZ" or "XCI" or "XCZ").ToArray();
        rows = action switch { "Compress" => rows.Where(row => row.FileType is "NSP" or "XCI").ToArray(), "Decompress" => rows.Where(row => row.FileType is "NSZ" or "XCZ").ToArray(), "Split" => rows.Where(row => row.HasMultiplePackages).ToArray(), "Merge" when rows.Length < 2 => [], _ => rows };
        await RunPackageActionAsync(action, rows.Select(row => row.Path).ToArray(),
            rows.FirstOrDefault()?.DisplayPath.Split(" :: ")[0], (sender as Button)?.Content?.ToString());
    }

    private async Task RunPackageActionAsync(string action, string[] paths, string? firstDisplayPath, string? title, string? targetType = null, TabItem? workspace = null)
    {
        if (_batchCancellation != null || _mtpCancellation != null || paths.Length == 0) return;
        if (action is "Compress" or "Decompress" ? !_preferences.NszActive : !_preferences.PackageOperationsActive)
        {
            StatusText.Text = action is "Compress" or "Decompress" ? _text.Features_PluginsDisabled : _text.Features_PackageOperationsDisabled;
            return;
        }
        var preferences = _preferences;
        var compression = action is "Compress" or "Decompress";
        var executable = PackageEnginePaths.ResolveRust(preferences.RustBackendPath ?? "");
        var kronos = ResolvePackageKronosDirectory(preferences);
        var engines = PackageEngines(preferences.SharedSettings.PackageEngine);
        var engineAvailable = compression
            ? (preferences.CompressionEngine != 1 && File.Exists(executable)) ||
                (preferences.CompressionEngine != 0 && File.Exists(preferences.NszCliPath))
            : engines.Any(engine => PackageEngineAvailable(engine, executable, kronos));
        if (!File.Exists(_keysPath) || !engineAvailable)
        {
            StatusText.Text = !File.Exists(_keysPath) ? _text.Nsz_KeysMissing : _text.Package_Missing;
            return;
        }
        var outputMode = _preferences.OutputModes?.GetValueOrDefault(action, 1) ?? 1;
        var initialFolder = outputMode == 2 ? _preferences.CustomOutputFolder : Path.GetDirectoryName(firstDisplayPath ?? paths[0]);
        var suggested = !string.IsNullOrWhiteSpace(initialFolder) && Directory.Exists(initialFolder) ? await StorageProvider.TryGetFolderFromPathAsync(new Uri(Path.GetFullPath(initialFolder))) : null;
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = title, SuggestedStartLocation = suggested });
        if (folders.FirstOrDefault()?.TryGetLocalPath() is not { } destination || _closed) return;
        if (outputMode == 1) destination = Path.Combine(destination, action);
        using var workspaceJob = LockWorkspace(workspace ?? BatchTab);
        using var cancellation = new CancellationTokenSource(); _batchCancellation = cancellation; SetBatchBusy(true);
        FilePackageCancelButton.IsVisible = workspace == FileTab;
        RefreshFilePackageActions();
        var keys = Path.GetFullPath(_keysPath!);
        var progress = new Progress<string>(message => { if (!_closed) { StatusText.Text = message; LogSession("INFO", message, action is "Compress" or "Decompress" && preferences.CompressionEngine is 1 or 3 ? "NSZ" : "PACKAGE"); } });
        try
        {
            var groups = action == "Merge" ? new[] { paths } : paths.Select(path => new[] { path }).ToArray();
            foreach (var sources in groups)
            {
                cancellation.Token.ThrowIfCancellationRequested();
                var result = await Task.Run(() => RunPackageJob(action, sources, destination, executable, kronos, keys, preferences, progress, cancellation.Token, targetType));
                if (_closed) return;
                if (workspace == null || workspace == BatchTab)
                {
                    for (var index = 0; index < _batchRows.Count; index++)
                        if (sources.Contains(_batchRows[index].Path))
                            _batchRows[index] = _batchRows[index] with { OutputPath = result, ConversionStatus = _text.Status_Ready };
                    RefreshBatchFilter();
                }
                StatusText.Text = result;
                LogSession("INFO", result, "PACKAGE");
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { if (!_closed) StatusText.Text = _text.Log_SaveFileCanceled; }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or LibHac.Common.LibHacException or LibHac.NSZ.NczFormatException or ZstdSharp.ZstdException or System.ComponentModel.Win32Exception)
        { if (!_closed) { StatusText.Text = ex.Message; LogSession("ERROR", ex.Message, "PACKAGE"); } }
        finally
        {
            _batchCancellation = null;
            if (!_closed)
            {
                SetBatchBusy(false);
                FilePackageCancelButton.IsVisible = false;
                RefreshFilePackageActions();
            }
        }
    }
    private static PackageFile VerifyPackageJobFile(string path, string keys, CancellationToken token)
    {
        var checks = new List<NcaHashResult>(); var package = PackageFile.Read(path, keys, checks, token, includeDetails: false);
        if (package.MetadataError != null || package.Titles.Count == 0 || checks.Count == 0 || checks.Any(check => check.Matches != true || check.Signature != LibHac.Common.Validity.Valid || check.Error != null))
            throw new InvalidDataException("Package integrity verification failed: " + path);
        if (package.Type == "XCI") XciPartitionIntegrity.Verify(path, token);
        return package;
    }
    private static string RunPackageJob(string action, string[] sources, string destination, string executable, string kronos, string keys, ViewerPreferences preferences, IProgress<string> progress, CancellationToken token, string? targetType)
    {
        var job = Path.Combine(Path.GetFullPath(destination), ".nxfv-package-" + Guid.NewGuid().ToString("N"));
        var output = Path.Combine(job, "output"); Directory.CreateDirectory(output);
        var locks = new List<FileStream>();
        try
        {
            var before = new List<PackageFile>(); var prepared = new List<string>();
            var titleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var expectedTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var expectedContents = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var compression = action is "Compress" or "Decompress";
            foreach (var source in sources)
            {
                token.ThrowIfCancellationRequested(); locks.Add(new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read));
                progress.Report("1/3 · " + Path.GetFileName(source)); var package = VerifyPackageJobFile(source, keys, token); before.Add(package);
                foreach (var title in package.Titles)
                    if (!titleIds.Add(title.TitleId) || !expectedTitles.Add(title.TitleId + ":" + title.Version.Version)) throw new InvalidDataException("Duplicate title/version: " + title.TitleId);
                foreach (var entry in PackageFile.ContentDigests(source, !compression, token))
                {
                    if (expectedContents.TryGetValue(entry.Key, out var previous) && previous != entry.Value) throw new InvalidDataException("Conflicting contents: " + entry.Key);
                    expectedContents[entry.Key] = entry.Value;
                }
                var input = source;
                if (!compression && package.Type is "NSZ" or "XCZ")
                {
                    var folder = Path.Combine(job, "inputs", prepared.Count.ToString()); Directory.CreateDirectory(folder);
                    ConvertWithRust(source, folder, executable, keys, NszOperation.Decompress, preferences, progress, token);
                    input = Directory.GetFiles(folder).Single(); VerifyPackageJobFile(input, keys, token);
                }
                prepared.Add(input);
            }
            if (action == "Split" && before.Single().Titles.Count <= 1) throw new InvalidDataException("The source contains only one title.");
            progress.Report("2/3 · " + action);
            if (compression)
            {
                ConvertWithRust(sources.Single(), output, executable, keys, action == "Compress" ? NszOperation.Compress : NszOperation.Decompress, preferences, progress, token);
                ValidateOutputs(output);
            }
            else
            {
                var operation = action == "Merge" ? PackageOperation.Merge : action == "Split" ? PackageOperation.Split : PackageOperation.Convert;
                var type = targetType ?? (action == "Xci" ? "xci" : "nsp");
                output = RunPackageEngines(preferences.SharedSettings.PackageEngine, operation, prepared, type,
                    executable, kronos, keys, job, ValidateOutputs, progress, token);
            }

            void ValidateOutputs(string candidate)
            {
                var outputs = Directory.GetFiles(candidate, "*", SearchOption.AllDirectories);
                if (outputs.Length == 0 || action != "Split" && outputs.Length != 1) throw new InvalidDataException("Unexpected package output count.");
                var actualTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var actualContents = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var path in outputs)
                {
                    progress.Report("3/3 · " + Path.GetFileName(path));
                    var package = VerifyPackageJobFile(path, keys, token);
                    if (action == "Split" && package.Titles.Count != 1) throw new InvalidDataException("Split output contains multiple titles.");
                    foreach (var title in package.Titles) if (!actualTitles.Add(title.TitleId + ":" + title.Version.Version)) throw new InvalidDataException("Duplicate output title.");
                    foreach (var entry in PackageFile.ContentDigests(path, !compression, token))
                    {
                        if (actualContents.TryGetValue(entry.Key, out var previous) && previous != entry.Value) throw new InvalidDataException("Conflicting output contents.");
                        actualContents[entry.Key] = entry.Value;
                    }
                }
                if (!expectedTitles.SetEquals(actualTitles) || expectedContents.Count != actualContents.Count || expectedContents.Any(entry => !actualContents.TryGetValue(entry.Key, out var hash) || hash != entry.Value))
                    throw new InvalidDataException("The output does not preserve the source contents and title versions.");
            }
            token.ThrowIfCancellationRequested();
            var published = Path.Combine(Path.GetFullPath(destination), "NxFileViewer-" + action + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..8]);
            Directory.Move(output, published); return published;
        }
        finally
        {
            foreach (var handle in locks) handle.Dispose();
            try { if (Directory.Exists(job)) Directory.Delete(job, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
    private static void ConvertWithRust(string source, string output, string executable, string keys, NszOperation operation, ViewerPreferences preferences, IProgress<string> progress, CancellationToken token)
    {
        var primaryNsz = preferences.CompressionEngine is 1 or 3;
        var attempts = preferences.CompressionEngine is 2 or 3 ? new[] { primaryNsz, !primaryNsz } : new[] { primaryNsz };
        for (var index = 0; index < attempts.Length; index++)
        {
            token.ThrowIfCancellationRequested();
            var attempt = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(output))!, ".nxfv-engine-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(attempt);
            try
            {
                var engine = attempts[index] ? preferences.NszCliPath : executable;
                if (string.IsNullOrWhiteSpace(engine) || !File.Exists(engine)) throw new FileNotFoundException("Compression engine not installed.", engine);
                {
                    using var alias = new NszInputAlias(source, attempt, keys, token);
                    var args = attempts[index] ? NszCommandArguments.BuildArguments(alias.SourceArgument, ".", alias.KeysArgument, operation, preferences.CompressionLevel, (NszCompressionMode)preferences.CompressionMode, preferences.CompressionBlockSize, true) : NszCommandArguments.BuildRustArguments(alias.SourceArgument, ".", alias.KeysArgument, preferences.CompressionLevel, operation);
                    NszProcess.Run(Path.GetFullPath(engine), args, attempt, token, update => progress.Report(update.Details));
                    alias.RestoreOutputName(operation);
                }
                if (Path.GetExtension(source).ToLowerInvariant() is ".xci" or ".xcz")
                {
                    var target = Path.Combine(attempt, Path.GetFileNameWithoutExtension(source) + (operation == NszOperation.Compress ? ".xcz" : ".xci"));
                    XciPartitionIntegrity.PreserveStagedPrefix(source, target, token);
                    if (operation == NszOperation.Decompress) { XciPartitionIntegrity.CorrectStagedLayout(source, target, token); XciPartitionIntegrity.NormalizeStagedGamecardHeader(target, token); }
                }
                var files = Directory.GetFiles(attempt);
                if (files.Length != 1) throw new InvalidDataException("Unexpected compression output count.");
                token.ThrowIfCancellationRequested();
                File.Move(files[0], Path.Combine(output, Path.GetFileName(files[0])), false);
                return;
            }
            catch (Exception ex) when (index + 1 < attempts.Length && !token.IsCancellationRequested && ex is IOException or System.ComponentModel.Win32Exception)
            { progress.Report(ex.Message + " · Backup: " + (attempts[index + 1] ? "nicoboss/nsz" : "nscb_rust")); }
            finally { if (Directory.Exists(attempt)) Directory.Delete(attempt, true); }
        }
    }
}
