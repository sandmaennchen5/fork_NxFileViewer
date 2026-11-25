using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Input;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using LibHac.Common;
using Emignatik.NxFileViewer.Services.PackageAnalysis;
using Emignatik.NxFileViewer.Utils;
using Emignatik.NxFileViewer.FileLoading;
using Emignatik.NxFileViewer.FileLoading.QuickFileInfoLoading;
using Emignatik.NxFileViewer.Services.Integrity;
using LibHac.Ncm;
namespace NxFileViewer.Avalonia;
public partial class MainWindow
{
    private sealed record BatchRow(string Path, string Summary, bool HasError = false)
    {
        public PackageFile? Package { get; init; }
        public Dictionary<string, string>? StoredMetadata { get; init; }
        public BatchRow[]? StoredParts { get; init; }
        public OverviewLanguage[]? StoredLanguages { get; init; }
        public bool? StoredDemo { get; init; }
        public long? StoredSize { get; init; }
        private string? Stored(string name) => StoredMetadata?.GetValueOrDefault(name);
        public string DisplayPath { get; init; } = Path;
        public string SourceGroupKey
        {
            get
            {
                var parts = DisplayPath.Split(" :: ", 2);
                var source = parts[0];
                return parts.Length == 2 || System.IO.Path.GetExtension(source).ToLowerInvariant() is ".zip" or ".7z"
                    ? source
                    : System.IO.Path.GetDirectoryName(source) ?? source;
            }
        }
        public string FileName
        {
            get
            {
                var parts = DisplayPath.Split(" :: ", 2);
                var name = System.IO.Path.GetFileName(parts[0]);
                if (parts.Length == 2) name += " :: " + parts[1];
                return name + (PartNumber > 0 ? "::" + PartNumber : "");
            }
        }
        public string? FirmwareArchiveType { get; init; }
        public bool IsSaveBackup { get; init; }
        public string RawFileType => Stored(nameof(RawFileType)) ?? Stored(nameof(FileType))?.Split(' ', 2)[0] ??
            FirmwareArchiveType ?? (IsSaveBackup ? System.IO.Path.GetExtension(Path).TrimStart('.').ToUpperInvariant()
                : Nand != null ? "NAND" : Package?.Type ?? "—");
        public string FileType => DisplayPath.Contains(" :: ", StringComparison.Ordinal)
            ? System.IO.Path.GetExtension(DisplayPath.Split(" :: ", 2)[0]).ToLowerInvariant() switch
            {
                ".zip" => $"{RawFileType} (ZIP)",
                ".7z" => $"{RawFileType} (7z)",
                _ => RawFileType
            }
            : RawFileType;
        public string ContainerType => Stored(nameof(ContainerType)) ?? (IsSaveBackup ? Summary : FirmwareArchiveType != null ? "FIRMWARE" : Nand != null ? "NAND" : Package?.DisplayType ?? "—");
        public bool HasMultiplePackages => Package?.IsMultiPackage == true || StoredParts is { Length: > 1 };
        public bool ShowPackageDetails => HasMultiplePackages && PartNumber == 0;
        [System.Text.Json.Serialization.JsonIgnore]
        public IEnumerable<BatchRow> PackageDetails => StoredParts ??
            Package?.Titles.Select((content, index) => this with { ContentOverride = content, PartNumber = index + 1 }) ??
            Enumerable.Empty<BatchRow>();
        public Emignatik.NxFileViewer.Services.Nand.NandDetectionResult? Nand { get; init; }
        public string? NandDetails { get; init; }
        [System.Text.Json.Serialization.JsonIgnore]
        public IReadOnlyList<NcaHashResult>? VerificationChecks { get; init; }
        public string Structure { get; init; } = "—";
        public string Signature { get; init; } = "—";
        public string Integrity { get; init; } = "—";
        public string Size => (Package?.Size ?? Nand?.Size ?? StoredSize)?.ToFileSize() ?? "—";
        public Content? ContentOverride { get; init; }
        public int PartNumber { get; init; }
        public bool PreservePackageOrder { get; init; }
        private IEnumerable<Content> Contents => ContentOverride != null ? new[] { ContentOverride } : Package?.Titles ?? Array.Empty<Content>();
        private string SummaryValue(Func<Content,string> selector, bool versions = false, [System.Runtime.CompilerServices.CallerMemberName] string property = "")
        {
            if (Stored(property) is { } saved) return saved;
            var values = Contents.Select(selector).Where(value => !string.IsNullOrWhiteSpace(value) && value != "—").Distinct().ToArray();
            return values.Length == 0 ? "—" : PreservePackageOrder ? string.Join(" / ", values) : versions ? BatchSummaryValues.Versions(values) : BatchSummaryValues.Text(values);
        }
        public string Title => Package?.Homebrew?.Titles.FirstOrDefault()?.AppName ??
            SummaryValue(content => content.NacpData?.Titles.FirstOrDefault(title => !string.IsNullOrWhiteSpace(title?.Name))?.Name ?? "");
        public string TitleId => SummaryValue(content => content.TitleId);
        public string Version => SummaryValue(content => content.Version?.Version.ToString() ?? "", true);
        public BatchVersionSortKey VersionSortKey => new(Version);
        public string ContentType => Stored(nameof(ContentType)) ?? (FirmwareArchiveType != null ? "FIRMWARE" : PreservePackageOrder ? BatchContentMetadata.Types(Contents.Select(content => content.Type)) : BatchSummaryValues.Types(Contents.Select(content => content.Type switch { ContentMetaType.Application => "Base", ContentMetaType.Patch => "Update", ContentMetaType.AddOnContent => "DLC", _ => content.Type.ToString() })));
        public string CompressionRatio => Stored(nameof(CompressionRatio)) ??
            (Package?.UncompressedSize is > 0 and var original ? $"{(double)Package.Size / original:P1}" : "—");
        public string MissingKeys => Stored(nameof(MissingKeys)) ?? string.Join(Environment.NewLine, Package?.MissingKeys ?? []);
        public string OutputPath { get; init; } = "";
        public string SourceSizeDisplay => Stored(nameof(SourceSizeDisplay)) ?? (Package?.Size ?? Nand?.Size ?? StoredSize)?.ToString("N0") ?? "—";
        public long? OutputSize { get; init; }
        public string OutputSizeDisplay => OutputSize?.ToString("N0") ?? "—";
        public string SystemUpdateVersion => Stored(nameof(SystemUpdateVersion)) ?? Package?.SystemUpdateVersion ?? "—";
        public BatchVersionSortKey SystemUpdateVersionSortKey => new(SystemUpdateVersion);
        public string Demo => SummaryValue(content => content.NacpData?.IsDemo.ToString() ?? "");
        public string ConversionStatus { get; init; } = "";
        public string Compression => Stored(nameof(Compression)) ?? Package?.Compression ?? (RawFileType is "NSZ" or "XCZ" or "NCZ" ? "Zstandard" : "—");
        public string MinimumSystemVersion => SummaryValue(content => content.MinimumSystemVersion?.Version.ToString() ?? "", true);
        public BatchVersionSortKey MinimumSystemVersionSortKey => new(MinimumSystemVersion);
        public string BaseTitleId => SummaryValue(content => content.ApplicationTitleId);
        public string MasterKey => SummaryValue(content => content.KeyGeneration?.ToString() ?? "", true);
        public string MinimumApplicationVersion => SummaryValue(content => content.MinimumApplicationVersion?.Version.ToString() ?? "", true);
        public string Distribution => Package?.Homebrew != null ? "Homebrew" : SummaryValue(content => content.Distribution ?? "");
        public string BuildId => Package?.Homebrew?.BuildId ?? SummaryValue(content => content.BuildId ?? "");
        public string Permissions => SummaryValue(content => content.FileSystemPermissions?.ToString("X16") ?? "");
        public string Security => SummaryValue(content => content.SecurityLevel ?? "");
        public string AcidSignature => SummaryValue(content => content.AcidSignature ?? "");
        public string Publisher => Package?.Homebrew?.Titles.FirstOrDefault()?.Publisher ??
            SummaryValue(content => content.NacpData?.Titles.FirstOrDefault(title => title != null)?.Publisher ?? "");
        public string DisplayVersion => Package?.Homebrew?.DisplayVersion ?? SummaryValue(content => content.NacpData?.DisplayVersion ?? "", true);
        public BatchVersionSortKey DisplayVersionSortKey => new(DisplayVersion);
        public string Languages => Stored(nameof(Languages)) ?? (Package?.Homebrew is { } homebrew
            ? string.Join(", ", homebrew.Titles.Select(title => title.Language))
            : string.Join(", ", Contents.Where(content => content.NacpData != null).SelectMany(content => content.NacpData!.Titles.Select((title, index) => (title, index)).Where(item => item.title != null).Select(item => ((Emignatik.NxFileViewer.Models.Overview.NacpLanguage)item.index).ToString())).Distinct()));
        public string NewName { get; init; } = "";
        public string NamingError { get; init; } = "";
        public string Naming { get; init; } = "—";
        public string Error { get; init; } = "";
        public long Modified { get; init; }
        public string? SourceFingerprint { get; init; }
        public bool Verified { get; init; }
        public bool Original { get; init; }
    }
    private readonly ObservableCollection<BatchRow> _batchRows = new();
    private CancellationTokenSource? _batchCancellation;
    private string? _batchVerifyingPath;
    private string? _batchFolder;
    private bool _batchReady;
    private void InitializeBatch()
    {
        BatchSourceMode.ItemsSource = new[] { _text.SdBatch_SourceAuto, _text.SdBatch_SourceFolder, _text.SdBatch_SourceSd };
        BatchSourceMode.SelectedIndex = System.Math.Clamp((int)_preferences.SharedSettings.SdBatchSourceMode, 0, 2);
        BatchTypeFilter.ItemsSource = new[] { _text.BatchTable_All, _text.BatchTable_Games, "NSP", "NSZ", "XCI", "XCZ",
            "SuperNSP", "SuperNSZ", "SuperXCI", "SuperXCZ", "NCA", "NCZ", "NRO", "NUP", "NAX0", "NAND", "ZIP", "7z", "Folder" };
        BatchIntegrityFilter.ItemsSource = new[] { _text.BatchTable_All, _text.NcasIntegrity_Unchecked, _text.NcasIntegrity_InProgress,
            _text.NcasIntegrity_Original, _text.NcasIntegrity_Incomplete, _text.NcasIntegrity_Modified, _text.NcasIntegrity_Corrupted,
            _text.NcasIntegrity_Error, _text.NcasIntegrity_NoNca, _text.NcasIntegrity_Unknown };
        BatchTypeFilter.SelectedIndex = BatchIntegrityFilter.SelectedIndex = 0;
        BatchNamingFilter.ItemsSource = new[] { _text.BatchTable_All, _text.BatchNaming_Unchecked, _text.BatchNaming_Matches, _text.BatchNaming_Differs, _text.BatchNaming_Error };
        BatchNamingFilter.SelectedIndex = 0;
        BatchSeparatePackages.IsChecked = _preferences.SharedSettings.BatchSeparatePackages;
        BatchGroupFiles.IsChecked = _preferences.SharedSettings.BatchGroupFiles;
        InitializeBatchColumns();
        RestoreBatchLayout();
        _batchReady = true;
        LoadBatchHistory();
        RefreshBatchFilter();
    }
    private async void ChooseBatchFolder(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = _text.BatchIntegrity_SelectDirectory, SuggestedStartLocation = await GetStartFolderAsync(_batchFolder) });
        if (!_closed && folders.FirstOrDefault()?.TryGetLocalPath() is { } folder) BatchInputPath.Text = folder;
    }
    private async void ChooseBatchArchive(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = _text.Firmware_BrowseZip, SuggestedStartLocation = await GetStartFolderAsync(_batchFolder), FileTypeFilter = new[] { new FilePickerFileType("ZIP / 7z") { Patterns = new[] { "*.zip", "*.7z" } } } });
        if (!_closed && files.FirstOrDefault()?.TryGetLocalPath() is { } archive) BatchInputPath.Text = archive;
    }
    private void BatchPathChanged(object? sender, TextChangedEventArgs e)
    {
        if (BatchStartButton == null || BatchHashButton == null) return;
        var input = BatchInputPath.Text?.Trim();
        _batchFolder = _mtpBatchPath is { } mtp && input == mtp.Display ? mtp.Local : input;
        BatchStartButton.IsEnabled = BatchHashButton.IsEnabled = _batchCancellation == null && _mtpCancellation == null && (Directory.Exists(_batchFolder) || File.Exists(_batchFolder));
    }
    private void SetBatchBusy(bool busy)
    {
        BatchStartButton.IsEnabled = BatchHashButton.IsEnabled = !busy && (Directory.Exists(_batchFolder) || File.Exists(_batchFolder));
        BatchVerifyAllButton.IsEnabled = !busy && _batchRows.Count > 0;
        BatchCancelButton.IsEnabled = busy;
        BatchSourceMode.IsEnabled = !busy;
        BatchProgress.IsVisible = busy;
        BatchProgress.IsIndeterminate = true;
        BatchProgress.Value = 0;
        UpdateBatchActionStates();
    }
    private async void StartBatch(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_batchFolder)) return;
        await RunBatchAsync([_batchFolder], ReferenceEquals(sender, BatchHashButton));
    }

    private async Task RunBatchAsync(string[] sources, bool verify, bool forceRecursive = false, BatchHistoryEntry? checkpoint = null)
    {
        if (_batchCancellation != null || _mtpCancellation != null) return;
        using var workspaceJob = LockWorkspace(BatchTab);
        using var cancellation = new CancellationTokenSource(); _batchCancellation = cancellation;
        ResetBatchWorkingFiles();
        if (checkpoint is { Id: var id } && id != Guid.Empty) _batchScanId = id;
        _batchRows.Clear(); RefreshBatchFilter(); SetBatchBusy(true);
        StatusText.Text = _text.LoadingFile_PleaseWait;
        var savedOptions = checkpoint?.Options;
        var options = new BatchResumeOptions(sources, verify,
            savedOptions?.Recursive ?? (forceRecursive || BatchRecursive.IsChecked == true),
            savedOptions?.IncludeArchives ?? (forceRecursive || BatchIncludeArchives.IsChecked == true),
            _preferences.SharedSettings.IgnoreMissingDeltaFragments,
            savedOptions?.Areas ?? SdBatchAreas.From(_preferences.SharedSettings) with { SourceMode = _batchSourceMode });
        var recursive = options.Recursive;
        var includeArchives = options.IncludeArchives;
        var reusable = savedOptions?.IgnoreMissingDeltaFragments == options.IgnoreMissingDeltaFragments
            ? checkpoint?.Results ?? [] : [];
        var savedRows = reusable.Where(row => row.SourceFingerprint != null && !row.HasError &&
                string.IsNullOrWhiteSpace(row.MissingKeys) && (!verify || row.Verified))
            .DistinctBy(row => row.DisplayPath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(row => row.DisplayPath, StringComparer.OrdinalIgnoreCase);
        var lastCheckpoint = System.Diagnostics.Stopwatch.StartNew();
        var trackHistory = _preferences.BatchHistoryEnabled;
        var keys = _keysPath;
        LogSession("INFO", verify ? _text.Batch_ScanAndVerify : _text.Batch_Scan);
        LogSession("DEBUG", $"Batch options: recursive={recursive}, archives={includeArchives}, sourceMode={_batchSourceMode}, sources={sources.Length}");
        RecordBatchMemory("batch-started");
        bool IsCurrentScan() => !_closed && ReferenceEquals(_batchCancellation, cancellation);
        IProgress<BatchRow> progress = new Progress<BatchRow>(row =>
        {
            if (_closed || !ReferenceEquals(_batchCancellation, cancellation)) return;
            BatchProgress.IsIndeterminate = true;
            LogBatchFile(row);
            var result = StoreActiveBatchArtwork(row) with { VerificationChecks = null };
            _batchRows.Add(result);
            if (_batchRows.Count % 32 == 0) FlushBatchArtwork();
            RefreshBatchFilter(followResult: result);
            LogFileSection(row.DisplayPath, [("TRACE", $"Batch file completed: {row.Integrity}")]);
            if (trackHistory && (_batchRows.Count == 1 || lastCheckpoint.ElapsedMilliseconds >= 5000))
            {
                SaveBatchHistory(sources[0], options);
                lastCheckpoint.Restart();
            }
        });
        IProgress<string> reading = new Progress<string>(path =>
        {
            if (!IsCurrentScan()) return;
            LogFileSection(path, [("TRACE", "Batch file started")]);
            BatchProgress.IsIndeterminate = true;
            StatusText.Text = $"{(verify ? _text.NcasIntegrity_InProgress : _text.LoadingFile_PleaseWait)} {BatchDisplayName(path)}";
        });
        IProgress<BatchArchiveSources.CopyProgress> copying = new Progress<BatchArchiveSources.CopyProgress>(step =>
        {
            if (!IsCurrentScan()) return;
            var message = ReportArchiveCopy(step);
            StatusText.Text = message;
            BatchProgress.Maximum = 100;
            BatchProgress.IsIndeterminate = step.Total <= 0;
            if (step.Total > 0) BatchProgress.Value = 100d * step.Copied / step.Total;
        });
        IProgress<(string File, double Fraction)> firmwareProgress = new Progress<(string File, double Fraction)>(step =>
        {
            if (_closed || !ReferenceEquals(_batchCancellation, cancellation)) return;
            BatchProgress.Maximum = 100;
            BatchProgress.IsIndeterminate = false;
            BatchProgress.Value = step.Fraction * 100;
            StatusText.Text = $"{_text.Firmware_Title} · {step.File} · {step.Fraction:P0}";
        });
        IProgress<(string Message, double Fraction)> verifyProgress = new Progress<(string Message, double Fraction)>(step =>
        {
            if (!IsCurrentScan()) return;
            ShowBatchVerificationProgress(step.Message, step.Fraction);
        });
        try
        {
            if (checkpoint != null) PreserveResumeArtwork(checkpoint.Id);
            await Task.Run(() =>
            {
                using var archiveOperation = PackageZip.BeginOperation();
                foreach (var source in sources)
                {
                    try
                    {
                        foreach (var file in EnumerateBatchSources(source, recursive, includeArchives || File.Exists(source), cancellation.Token, copying.Report, options.Areas))
                        {
                            cancellation.Token.ThrowIfCancellationRequested();
                            if (file.Error != null) { progress.Report(BatchSourceError(file.Path, file.DisplayPath, file.Error)); continue; }
                            reading.Report(file.DisplayPath);
                            try
                            {
                                var fingerprint = trackHistory || savedRows.Count > 0 ? BatchSourceFingerprint.Read(file.DisplayPath) : null;
                                if (fingerprint != null && savedRows.TryGetValue(file.DisplayPath, out var stored) && stored.SourceFingerprint == fingerprint)
                                {
                                    progress.Report(stored with { Path = PersistentBatchPath(stored) });
                                    continue;
                                }
                                var row = ReadBatchRow(file.Path, file.DisplayPath, keys, verify, cancellation.Token,
                                    fraction => firmwareProgress.Report((BatchDisplayName(file.DisplayPath), fraction)),
                                    (message, fraction) => verifyProgress.Report((message, fraction)));
                                progress.Report(row with { SourceFingerprint = fingerprint != null && fingerprint == BatchSourceFingerprint.Read(file.DisplayPath)
                                    ? fingerprint : null });
                            }
                            finally { ReleaseScannedArchiveMember(file); }
                        }
                    }
                    catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or SharpCompress.Common.SharpCompressException)
                    {
                        progress.Report(BatchSourceError(source, source, error.Message));
                    }
                }
            });
            if (!_closed)
            {
                FlushBatchArtwork();
                if (_batchRows.Count > 0) SaveBatchHistory(sources[0], options);
                StatusText.Text = _batchRows.Count > 0 ? _text.Status_Ready : string.Format(_text.FileNotSupported_Log, string.Join(", ", sources));
                LogSession(_batchRows.Count > 0 ? "INFO" : "WARN", _batchRows.Count > 0
                    ? $"{_text.BatchIntegrity_Title} · {_batchRows.Count} · {StatusText.Text}" : StatusText.Text!);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            if (!_closed)
            {
                if (_batchRows.Count > 0) SaveBatchHistory(sources[0], options);
                StatusText.Text = _text.Log_NcasIntegrityCanceled;
                LogSession("INFO", _text.Log_NcasIntegrityCanceled);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or SharpCompress.Common.SharpCompressException) { if (!_closed) { StatusText.Text = ex.Message; LogSession("ERROR", ex.Message); } }
        finally
        {
            ReleaseResumeArtwork();
            _batchCancellation = null;
            ClearBatchExtraction();
            if (_closed) ResetBatchWorkingFiles();
            if (!_closed)
            {
                SetBatchBusy(false);
                RecordBatchMemory("batch-finished");
            }
        }
    }

    private static string BatchDisplayName(string path) => Path.GetFileName(path.Split(" :: ", 2)[^1].Replace('\\', '/'));

    private string ReportArchiveCopy(BatchArchiveSources.CopyProgress step)
    {
        var message = string.Format(_text.Batch_CopyArchiveMember, BatchDisplayName(step.DisplayPath),
            step.Copied.ToFileSize(), step.Total.ToFileSize());
        if (step.Copied == 0 || step.Copied == step.Total) LogFileSection(step.DisplayPath, [("DEBUG", message)]);
        return message;
    }

    private void ShowBatchVerificationProgress(string message, double fraction)
    {
        StatusText.Text = message;
        BatchProgress.IsIndeterminate = false;
        BatchProgress.Maximum = 100;
        BatchProgress.Value = Math.Clamp(fraction, 0, 1) * 100;
    }

    private BatchRow BatchSourceError(string path, string display, string error) => new(path, error, true)
    {
        DisplayPath = display, Error = error, Integrity = _text.NcasIntegrity_Error, Naming = _text.BatchNaming_Unchecked
    };
    private BatchRow ReadBatchRow(string path, string display, string? keys, bool verify, CancellationToken token,
        Action<double>? firmwareProgress = null, Action<string, double>? verificationProgress = null,
        Action<BatchArchiveSources.CopyProgress>? copyProgress = null)
    {
        var originalPath = path;
        try
        {
            path = MaterializeBatchSource(path, token, copyProgress);
            QueueBatchFileTrace(display, "Detecting content type and reading metadata");
            var sdContents = Directory.Exists(Path.Combine(path, "registered"));
            if (SaveBackupDetection.IsBackup(path, token))
            {
                var label = SaveBackupDetection.IsLikelyLegacyBackup(path, token)
                    ? _text.File_SaveBackupSuspected : _text.File_SaveBackup;
                return new BatchRow(path, label)
                {
                    IsSaveBackup = true, DisplayPath = display, Structure = "SaveBackup",
                    StoredSize = new FileInfo(path).Length, Signature = _text.Signature_Unchecked,
                    Integrity = _text.NcasIntegrity_NoNca
                };
            }
            if (Emignatik.NxFileViewer.FileLoading.NupReader.IsNup(path) && !_preferences.SharedSettings.EnableNup)
                throw new InvalidDataException(_text.Features_NupDisabled);
            if (!sdContents && _preferences.FirmwareActive && (Directory.Exists(path) || Path.GetExtension(path).ToLowerInvariant() is ".zip" or ".7z"))
                return ReadBatchFirmware(path, token, firmwareProgress);

            if (_preferences.NandActive && !Nax0Contents.IsCandidate(path) &&
                Emignatik.NxFileViewer.Services.Nand.NandDetection.IsCandidateName(path) &&
                Emignatik.NxFileViewer.Services.Nand.NandDetection.Detect(path, token) is { } nand)
            {
                var report = ReadNandReport(path, nand, requestPlugin: false,
                    new MtpProgress(new Progress<string>()), token);
                return new BatchRow(path, nand.Information)
                {
                    Nand = nand, NandDetails = report.Information, DisplayPath = display, Structure = nand.Type,
                    Signature = _text.Signature_Unchecked, Integrity = _text.NcasIntegrity_Unchecked
                };
            }

            using var sourceLock = sdContents ? null : new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var checks = verify ? new List<NcaHashResult>() : null;
            var name = BatchDisplayName(display);
            var package = PackageFile.Read(path, keys, checks, token, includeDetails: true,
                hashProgress: (entry, index, count, completed) =>
                {
                    if (!completed) QueueBatchFileTrace(display, $"{string.Format(_text.NcaHash_ProgressText, index, count)} {entry}");
                    verificationProgress?.Invoke($"{name} · {string.Format(_text.NcaHash_ProgressText, index, count)} {entry}",
                        (double)(index - (completed ? 0 : 1)) / count);
                },
                nupProgress: fraction => verificationProgress?.Invoke($"{_text.NcasIntegrity_InProgress} {name} · {fraction:P0}", fraction));
            QueueBatchFileTrace(display, $"Metadata read: type={package.DisplayType}; entries={package.Entries.Count}; titles={package.Titles.Count}");
            var error = package.MetadataError ?? (package.NeedsProdKeys ? _text.WarnNoProdKeysFileFound
                : sdContents && package.MissingKeys.Length > 0 ? string.Join(Environment.NewLine, package.MissingKeys) : null);
            if (package.Type == "NUP")
                return new BatchRow(path, package.HeaderDetails ?? package.Type, false)
                {
                    Package = package, DisplayPath = display, Modified = File.GetLastWriteTimeUtc(path).Ticks,
                    Verified = verify, Signature = _text.Signature_Unchecked,
                    Integrity = verify ? _text.NcasIntegrity_Original : _text.NcasIntegrity_Unchecked, Naming = _text.BatchNaming_Unchecked
                };
            var original = checks is { Count: > 0 } && checks.All(c => c.Matches == true && c.Signature == Validity.Valid && c.Error == null);
            var corrupt = checks?.Any(c => c.Matches == false) == true;
            var invalid = checks?.Any(c => c.Error != null && !c.Missing) == true;
            var missing = checks?.Any(c => c.Missing) == true;
            var present = checks?.Where(c => !c.Missing).ToArray();
            var incomplete = missing && present is { Length: > 0 } &&
                present.All(c => c.Matches == true && c.Signature == Validity.Valid && c.Error == null);
            var modified = !missing && present is { Length: > 0 } &&
                present.All(c => c.Matches == true && c.Error == null) && present.Any(c => c.Signature == Validity.Invalid);
            var signature = checks == null ? _text.Signature_Unchecked : checks.Count > 0 && checks.All(c => c.Signature == Validity.Valid) ? _text.Signature_Passed : _text.Signature_NotPassed;
            var integrity = !verify ? _text.NcasIntegrity_Unchecked : corrupt ? _text.NcasIntegrity_Corrupted :
                incomplete ? _text.NcasIntegrity_Incomplete : invalid || error != null ? _text.NcasIntegrity_Error :
                modified ? _text.NcasIntegrity_Modified : original ? _text.NcasIntegrity_Original :
                checks is { Count: 0 } ? _text.NcasIntegrity_NoNca : _text.NcasIntegrity_Unknown;
            var summary = package.Type + " · " + package.Size.ToFileSize() + (error == null ? "" : " · " + error);
            if (checks != null) summary += Environment.NewLine + string.Join(Environment.NewLine, checks.Select(FormatCheck));
            return new BatchRow(path, summary, error != null || corrupt || invalid || modified) { PreservePackageOrder = _preferences.SharedSettings.BatchSummaryPreservePackageOrder, Package = package, DisplayPath = display, Modified = File.GetLastWriteTimeUtc(path).Ticks, Verified = verify, VerificationChecks = checks, Original = original, Signature = signature, Integrity = integrity, Error = error ?? string.Join("; ", checks?.Where(c => c.Error != null).Select(c => c.Error) ?? Array.Empty<string>()), Structure = PackageStructureAnalyzer.GetDisplayName(package.Structure.ToString(), _text), Naming = _text.BatchNaming_Unchecked };
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or OverflowException or LibHac.Common.LibHacException or LibHac.NSZ.NczFormatException or ZstdSharp.ZstdException)
        { return BatchSourceError(originalPath, display, ex.Message); }
    }
    private async void VerifyBatchRows(object? sender, RoutedEventArgs e)
    {
        await VerifyBatchSelectionAsync(_batchRows.Where(row => row.Nand == null).ToArray());
    }

    private async Task VerifyBatchSelectionAsync(BatchRow[] rows)
    {
        if (_batchCancellation != null || _mtpCancellation != null || rows.Length == 0) return;
        using var workspaceJob = LockWorkspace(BatchTab);
        using var cancellation = new CancellationTokenSource(); _batchCancellation = cancellation; SetBatchBusy(true);
        var keys = _keysPath;
        BatchProgress.IsIndeterminate = false;
        BatchProgress.Maximum = rows.Length;
        LogSession("INFO", $"{_text.MenuItem_CheckIntegrity} · {rows.Length}");
        RecordBatchMemory("batch-verification-started");
        try
        {
            foreach (var row in rows)
            {
                cancellation.Token.ThrowIfCancellationRequested();
                StatusText.Text = $"{_text.NcasIntegrity_InProgress} {row.FileName}";
                LogFileSection(row.DisplayPath, [("TRACE", "Batch verification started")]);
                _batchVerifyingPath = row.Path;
                if (BatchIntegrityFilter.SelectedItem as string == _text.NcasIntegrity_InProgress) RefreshBatchFilter();
                var currentIndex = BatchProgress.Value;
                IProgress<string> progress = new Progress<string>(message =>
                {
                    if (!_closed && ReferenceEquals(_batchCancellation, cancellation) && _batchVerifyingPath == row.Path)
                        StatusText.Text = message;
                });
                IProgress<BatchArchiveSources.CopyProgress> copying = new Progress<BatchArchiveSources.CopyProgress>(step =>
                {
                    if (!_closed && ReferenceEquals(_batchCancellation, cancellation) && _batchVerifyingPath == row.Path)
                        StatusText.Text = ReportArchiveCopy(step);
                });
                var updated = await Task.Run(() => ReadBatchRow(row.Path, row.DisplayPath, keys, true, cancellation.Token,
                    fraction => progress.Report($"{_text.Firmware_Title} · {row.FileName} · {fraction:P0}"),
                    (message, _) => progress.Report(message), copying.Report));
                _batchVerifyingPath = null;
                if (_closed) return;
                LogBatchFile(updated);
                updated = StoreActiveBatchArtwork(updated) with { VerificationChecks = null };
                var index = _batchRows.IndexOf(row); if (index >= 0) _batchRows[index] = updated;
                RefreshBatchFilter(followResult: updated);
                LogFileSection(row.DisplayPath, [("TRACE", $"Batch verification completed: {updated.Integrity}")]);
                BatchProgress.Value = currentIndex + 1;
            }
            if (!_closed)
            {
                FlushBatchArtwork();
                if (!string.IsNullOrWhiteSpace(_batchFolder)) SaveBatchHistory(_batchFolder);
            }
            StatusText.Text = _text.Status_Ready;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { if (!_closed) StatusText.Text = _text.Log_NcasIntegrityCanceled; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            if (!_closed) { StatusText.Text = error.Message; LogSession("ERROR", error.Message); }
        }
        finally
        {
            var wasVerifying = _batchVerifyingPath != null;
            _batchVerifyingPath = null;
            _batchCancellation = null;
            ClearBatchExtraction();
            if (_closed) ResetBatchWorkingFiles();
            if (!_closed)
            {
                if (wasVerifying) RefreshBatchFilter();
                SetBatchBusy(false);
                RecordBatchMemory("batch-verification-finished");
            }
        }
    }
    private readonly Dictionary<DataGridColumn, (bool Visible, DataGridLength Width)> _defaultBatchColumns = new();
    private void InitializeBatchColumns()
    {
        foreach (var (field, title, width, visible) in new[]
        {
            (nameof(BatchRow.FileName), _text.BatchIntegrity_File, 220, true),
            (nameof(BatchRow.FileType), _text.BatchIntegrity_FileType, 120, true),
            (nameof(BatchRow.ContainerType), _text.BatchTable_Type, 120, true),
            (nameof(BatchRow.Structure), _text.PackageStructure_Title, 110, true),
            (nameof(BatchRow.Signature), _text.Signature_Title, 125, true),
            (nameof(BatchRow.Integrity), _text.Title_FileInfo_Integrity, 110, true),
            (nameof(BatchRow.Size), _text.FileInfo_FileSize, 110, true),
            (nameof(BatchRow.Compression), _text.Title_FileInfo_Compression, 110, true),
            (nameof(BatchRow.CompressionRatio), _text.FileInfo_CompressionRatio, 140, false),
            (nameof(BatchRow.SystemUpdateVersion), _text.FileInfo_SystemUpdate, 160, false),
            (nameof(BatchRow.ContentType), _text.BatchIntegrity_PackageType, 130, true),
            (nameof(BatchRow.TitleId), _text.CnmtOverview_TitleId, 165, true),
            (nameof(BatchRow.Version), _text.CnmtOverview_TitleVersion, 110, true),
            (nameof(BatchRow.MinimumSystemVersion), _text.CnmtOverview_MinimumSystemVersion, 150, false),
            (nameof(BatchRow.BaseTitleId), _text.CnmtOverview_BaseTitleId, 165, false),
            (nameof(BatchRow.MasterKey), _text.CnmtOverview_MasterKey, 200, false),
            (nameof(BatchRow.MinimumApplicationVersion), _text.CnmtOverview_MinimumApplicationVersion, 170, false),
            (nameof(BatchRow.Distribution), _text.CnmtOverview_Distribution, 140, false),
            (nameof(BatchRow.BuildId), _text.CnmtOverview_BuildID, 220, false),
            (nameof(BatchRow.Demo), _text.CnmtOverview_IsDemo, 90, false),
            (nameof(BatchRow.Security), _text.Security_Title, 150, true),
            (nameof(BatchRow.Permissions), _text.Security_FileSystemPermissions, 185, false),
            (nameof(BatchRow.AcidSignature), _text.Security_AcidSignature, 130, false),
            (nameof(BatchRow.Title), _text.AppTitle, 260, true),
            (nameof(BatchRow.Publisher), _text.Publisher, 170, false),
            (nameof(BatchRow.DisplayVersion), _text.DisplayVersion, 130, false),
            (nameof(BatchRow.Languages), _text.AvailableLanguages, 180, false),
            (nameof(BatchRow.Error), _text.BatchIntegrity_Error, 250, true),
            (nameof(BatchRow.MissingKeys), _text.File_MissingKeysHeader, 240, false),
            (nameof(BatchRow.DisplayPath), _text.BatchIntegrity_Path, 280, false),
            (nameof(BatchRow.Naming), _text.BatchNaming_Title, 130, false),
            (nameof(BatchRow.NewName), _text.RenamingTool_NewName, 280, false),
            (nameof(BatchRow.NamingError), _text.BatchNaming_Error, 240, false),
            (nameof(BatchRow.ConversionStatus), _text.BatchTable_ConversionStatus, 200, false),
            (nameof(BatchRow.SourceSizeDisplay), _text.Nsz_SourceSize, 130, false),
            (nameof(BatchRow.OutputSizeDisplay), _text.Nsz_OutputSize, 130, false),
            (nameof(BatchRow.OutputPath), _text.BatchTable_OutputPath, 280, false),
        })
        {
            BatchResults.Columns.Add(new DataGridTextColumn
            {
                Header = title, Binding = new global::Avalonia.Data.Binding(field),
                SortMemberPath = BatchSortPath(field), IsVisible = visible, Width = new DataGridLength(width)
            });
        }
        foreach (var column in BatchResults.Columns)
        {
            _defaultBatchColumns[column] = (column.IsVisible, column.Width);
            column.PropertyChanged += (_, change) =>
            {
                if (change.Property == DataGridColumn.IsVisibleProperty)
                    RefreshBatchPackageTables();
            };
        }
        BatchResults.ColumnDisplayIndexChanged += (_, _) => RefreshBatchPackageTables();
    }
    private void BatchFilterChanged(object? sender, RoutedEventArgs e) { if (_batchReady) RefreshBatchFilter(); }
    private void BatchChoiceChanged(object? sender, SelectionChangedEventArgs e) { if (_batchReady) RefreshBatchFilter(); }
    private static IEnumerable<BatchRow> ExpandedBatchRows(IEnumerable<BatchRow> rows, bool separate)
    {
        foreach (var row in rows)
        {
            if (separate && row.HasMultiplePackages)
                foreach (var part in row.PackageDetails) yield return part;
            else yield return row;
        }
    }
    private BatchRow[] FilteredBatchRows()
    {
        var search = BatchSearch.Text?.Trim() ?? "";
        var words = search.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var fileType = BatchTypeFilter.SelectedIndex > 0 ? BatchTypeFilter.SelectedItem as string : null;
        var sourceRows = _batchVerifyingPath == null ? (IEnumerable<BatchRow>)_batchRows :
            _batchRows.Select(row => row.Path == _batchVerifyingPath ? row with { Integrity = _text.NcasIntegrity_InProgress } : row);
        var rows = ExpandedBatchRows(sourceRows, BatchSeparatePackages.IsChecked == true).Where(row =>
            (BatchErrorsOnly.IsChecked != true || row.HasError || row.Integrity != _text.NcasIntegrity_Original || row.NamingError.Length > 0) &&
            (BatchNamingFilter.SelectedIndex <= 0 || row.Naming == BatchNamingFilter.SelectedItem as string) &&
            MatchesBatchType(row, fileType) &&
            (BatchIntegrityFilter.SelectedIndex <= 0 || row.Integrity == BatchIntegrityFilter.SelectedItem as string) &&
            MatchesBatchSearch(row, words));
        if (BatchAlphabetical.IsChecked == true) rows = rows.OrderBy(row => row.Title, StringComparer.CurrentCultureIgnoreCase).ThenBy(row => row.TitleId);
        return rows.ToArray();
    }
    private bool MatchesBatchType(BatchRow row, string? type)
    {
        if (type == null) return true;
        if (type == _text.BatchTable_Games) return row.RawFileType is "NSP" or "NSZ" or "XCI" or "XCZ" or "NUP";
        return row.RawFileType.Equals(type, StringComparison.OrdinalIgnoreCase) ||
            row.ContainerType.Equals(type, StringComparison.OrdinalIgnoreCase) ||
            row.FileType.EndsWith(" (" + type + ")", StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesBatchSearch(BatchRow row, string[] words)
    {
        if (words.Length == 0) return true;
        var text = string.Join(" ", row.FileName, row.DisplayPath, row.Title, row.TitleId, row.Publisher, row.Version,
            row.DisplayVersion, row.MinimumSystemVersion, row.MasterKey, row.BuildId, row.Distribution, row.Languages,
            row.FileType, row.ContentType, row.Structure, row.Compression, row.Integrity, row.Error, row.ConversionStatus,
            row.Summary, row.Naming, row.NamingError, row.NewName);
        return words.All(word => text.Contains(word, StringComparison.OrdinalIgnoreCase));
    }

    private void UpdateBatchActionStates()
    {
        var ready = _batchCancellation == null && _mtpCancellation == null && FilteredBatchRows().Length > 0;
        foreach (var button in BatchActionsPanel.Children.OfType<Button>())
        {
            if (button == BatchStartButton || button == BatchHashButton || button == BatchCancelButton) continue;
            button.IsEnabled = ready;
            if (button.Tag is string action)
                button.IsVisible = action is "Compress" or "Decompress" ? _preferences.NszActive : _preferences.PackageOperationsActive;
        }
    }
    private BatchRow? _lastFollowedBatchRow;

    private void RefreshBatchFilter(BatchRow? followResult = null)
    {
        UpdateBatchActionStates();
        var previous = BatchResults.SelectedItem as BatchRow;
        var rows = FilteredBatchRows();
        SetBatchItems(rows);
        var oldSelection = previous == null ? null : rows.FirstOrDefault(row => row.Path == previous.Path &&
            row.DisplayPath == previous.DisplayPath && row.PartNumber == previous.PartNumber);
        var followed = followResult != null && (previous == null || ReferenceEquals(previous, _lastFollowedBatchRow))
            ? rows.FirstOrDefault(row => row.Path == followResult.Path && row.DisplayPath == followResult.DisplayPath)
            : null;
        BatchResults.SelectedItem = followed ?? oldSelection;
        if (followed != null) _lastFollowedBatchRow = followed;
        BatchExportButton.IsEnabled = _batchRows.Count > 0;
        BatchVerifyAllButton.IsEnabled = _batchRows.Count > 0 && _batchCancellation == null;
    }
    private async void ResetBatchFilters(object? sender, RoutedEventArgs e)
    {
        if (!await ConfirmPluginCleanupAsync(_text.BatchTable_ResetFiltersAndSort, _text.BatchTable_ConfirmResetFiltersAndSort,
            _text.Dialog_Yes, _text.Dialog_No)) return;

        var wasReady = _batchReady;
        _batchReady = false;
        try
        {
            BatchSearch.Text = "";
            BatchErrorsOnly.IsChecked = false;
            BatchTypeFilter.SelectedIndex = BatchIntegrityFilter.SelectedIndex = BatchNamingFilter.SelectedIndex = 0;
            if (BatchAlphabetical.IsChecked != true)
            {
                foreach (var column in BatchResults.Columns) column.ClearSort();
                if (BatchResults.ItemsSource is global::Avalonia.Collections.DataGridCollectionView view)
                    view.SortDescriptions.Clear();
                _batchSort = [];
            }
        }
        finally { _batchReady = wasReady; }
        RefreshBatchFilter();
        if (BatchAlphabetical.IsChecked != true) SaveResetBatchLayout();
    }
    private async void ExportBatch(object? sender, RoutedEventArgs e)
    {
        var rows = ExpandedBatchRows(FilteredBatchRows().DistinctBy(row => row.Path).Select(row => row with { ContentOverride = null, PartNumber = 0 }), _preferences.SharedSettings.BatchExportSeparatePackages).ToArray(); if (rows.Length == 0) return;
        try
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { Title = _text.BatchIntegrity_Export, SuggestedFileName = _preferences.SharedSettings.BatchExportExcel ? "NxFileViewer-Batch.xlsx" : "NxFileViewer-Batch.csv", DefaultExtension = _preferences.SharedSettings.BatchExportExcel ? "xlsx" : "csv", FileTypeChoices = new[] { new FilePickerFileType("Excel") { Patterns = new[] { "*.xlsx" } }, new FilePickerFileType("CSV") { Patterns = new[] { "*.csv" } }, new FilePickerFileType("JSON") { Patterns = new[] { "*.json" } } }, ShowOverwritePrompt = true });
            if (file?.TryGetLocalPath() is not { } path) return;
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                var fields = typeof(BatchRow).GetProperties().Where(property => property.PropertyType == typeof(string)).Select(property => property.Name).ToArray();
                var columns = BatchResults.Columns.OfType<DataGridTextColumn>().Where(column => !_preferences.ExportVisibleColumnsOnly || column.IsVisible)
                    .OrderBy(column => _preferences.ExportVisibleColumnsOnly ? column.DisplayIndex : BatchResults.Columns.IndexOf(column)).Select(column => (Name: (column.Binding as global::Avalonia.Data.Binding)?.Path, Header: column.Header?.ToString() ?? ""))
                    .Where(column => column.Name != null && fields.Contains(column.Name)).ToArray();
                string Value(BatchRow row, string name) => name == "FileName" ? row.DisplayPath : typeof(BatchRow).GetProperty(name)?.GetValue(row)?.ToString() ?? "";
                if (System.IO.Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase))
                    File.WriteAllText(temporary, System.Text.Json.JsonSerializer.Serialize(rows.Select(row => columns.ToDictionary(column => column.Name!, column => Value(row, column.Name!))), new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                else if (System.IO.Path.GetExtension(path).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
                {
                    using var stream = new FileStream(temporary, FileMode.CreateNew);
                    Emignatik.NxFileViewer.Services.Integrity.BatchExcelWriter.Write(stream, columns.Select(column => new Emignatik.NxFileViewer.Services.Integrity.BatchExportColumn(column.Name!, column.Header, column.Name!)).ToArray(), rows.Select(row => columns.Select(column => Value(row, column.Name!))));
                }
                else using (var stream = new FileStream(temporary, FileMode.CreateNew))
                    Emignatik.NxFileViewer.Services.Integrity.BatchCsvWriter.Write(stream, columns.Select(column => column.Header).ToArray(), rows.Select(row => columns.Select(column => Value(row, column.Name!)).ToArray()));
                File.Move(temporary, path, true);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            StatusText.Text = path;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException) { StatusText.Text = ex.Message; }
    }
    private string FormatCheck(NcaHashResult result) => string.Join(" · ", new[] { result.Name, result.Matches == true ? string.Format(_text.NcaHash_Valid_Log, result.Name) : result.Matches == false ? _text.NcaHash_NcaItem_Invalid : _text.NcasIntegrity_Unknown, result.Signature == Validity.Valid ? string.Format(_text.NcaHeaderSignature_Valid_Log, result.Name) : result.Signature?.ToString(), result.Error }.Where(value => !string.IsNullOrWhiteSpace(value)));
    private void CancelBatch(object? sender, RoutedEventArgs e) { _batchCancellation?.Cancel(); _mtpCancellation?.Cancel(); }
    private async void OpenBatchResult(object? sender, TappedEventArgs e)
    {
        if (!IsBatchPackageTableEvent(e.Source) && _workspaceJobs.Count == 0 && BatchResults.SelectedItem is BatchRow row)
            await OpenBatchFileAsync(row);
    }
}
