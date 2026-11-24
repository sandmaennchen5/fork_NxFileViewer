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
        public string FileType => Stored(nameof(FileType)) ?? FirmwareArchiveType ?? (Nand != null ? "NAND" : Package?.Type ?? "—");
        public string ContainerType => Stored(nameof(ContainerType)) ?? (FirmwareArchiveType != null ? "FIRMWARE" : Nand != null ? "NAND" : Package?.DisplayType ?? "—");
        public bool HasMultiplePackages => Package?.IsMultiPackage == true || StoredParts is { Length: > 1 };
        public bool ShowPackageDetails => HasMultiplePackages && PartNumber == 0;
        [System.Text.Json.Serialization.JsonIgnore]
        public IEnumerable<BatchRow> PackageDetails => StoredParts ??
            Package?.Titles.Select((content, index) => this with { ContentOverride = content, PartNumber = index + 1 }) ??
            Enumerable.Empty<BatchRow>();
        public Emignatik.NxFileViewer.Services.Nand.NandDetectionResult? Nand { get; init; }
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
        public string Title => SummaryValue(content => content.NacpData?.Titles.FirstOrDefault(title => !string.IsNullOrWhiteSpace(title?.Name))?.Name ?? "");
        public string TitleId => SummaryValue(content => content.TitleId);
        public string Version => SummaryValue(content => content.Version?.Version.ToString() ?? "", true);
        public string ContentType => Stored(nameof(ContentType)) ?? (FirmwareArchiveType != null ? "FIRMWARE" : PreservePackageOrder ? BatchContentMetadata.Types(Contents.Select(content => content.Type)) : BatchSummaryValues.Types(Contents.Select(content => content.Type switch { ContentMetaType.Application => "Base", ContentMetaType.Patch => "Update", ContentMetaType.AddOnContent => "DLC", _ => content.Type.ToString() })));
        public string CompressionRatio => Stored(nameof(CompressionRatio)) ??
            (Package?.UncompressedSize is > 0 and var original ? $"{(double)Package.Size / original:P1}" : "—");
        public string MissingKeys => Stored(nameof(MissingKeys)) ?? string.Join(Environment.NewLine, Package?.MissingKeys ?? []);
        public string OutputPath { get; init; } = "";
        public string ConversionStatus { get; init; } = "";
        public string Compression => Stored(nameof(Compression)) ?? Package?.Compression ?? (FileType is "NSZ" or "XCZ" or "NCZ" ? "Zstandard" : "—");
        public string MinimumSystemVersion => SummaryValue(content => content.MinimumSystemVersion?.Version.ToString() ?? "", true);
        public string BaseTitleId => SummaryValue(content => content.ApplicationTitleId);
        public string MasterKey => SummaryValue(content => content.KeyGeneration?.ToString() ?? "", true);
        public string MinimumApplicationVersion => SummaryValue(content => content.MinimumApplicationVersion?.Version.ToString() ?? "", true);
        public string Distribution => SummaryValue(content => content.Distribution ?? "");
        public string BuildId => SummaryValue(content => content.BuildId ?? "");
        public string Permissions => SummaryValue(content => content.FileSystemPermissions?.ToString("X16") ?? "");
        public string Security => SummaryValue(content => content.SecurityLevel ?? "");
        public string AcidSignature => SummaryValue(content => content.AcidSignature ?? "");
        public string Publisher => SummaryValue(content => content.NacpData?.Titles.FirstOrDefault(title => title != null)?.Publisher ?? "");
        public string DisplayVersion => SummaryValue(content => content.NacpData?.DisplayVersion ?? "", true);
        public string Languages => Stored(nameof(Languages)) ?? string.Join(", ", Contents.Where(content => content.NacpData != null).SelectMany(content => content.NacpData!.Titles.Select((title, index) => (title, index)).Where(item => item.title != null).Select(item => ((Emignatik.NxFileViewer.Models.Overview.NacpLanguage)item.index).ToString())).Distinct());
        public string NewName { get; init; } = "";
        public string NamingError { get; init; } = "";
        public string Naming { get; init; } = "—";
        public string Error { get; init; } = "";
        public long Modified { get; init; }
        public bool Verified { get; init; }
        public bool Original { get; init; }
    }
    private readonly ObservableCollection<BatchRow> _batchRows = new();
    private CancellationTokenSource? _batchCancellation;
    private string? _batchFolder;
    private bool _batchReady;
    private void InitializeBatch()
    {
        BatchTypeFilter.ItemsSource = new[] { _text.BatchTable_All, "NSP", "NSZ", "XCI", "XCZ", "SuperNSP", "SuperNSZ", "SuperXCI", "SuperXCZ", "NCA", "NCZ", "NRO", "NUP", "ZIP", "7z", "NAND" };
        BatchIntegrityFilter.ItemsSource = new[] { _text.BatchTable_All, _text.NcasIntegrity_Unchecked, _text.NcasIntegrity_Original, _text.NcasIntegrity_Corrupted, _text.NcasIntegrity_Error };
        BatchTypeFilter.SelectedIndex = BatchIntegrityFilter.SelectedIndex = 0;
        BatchNamingFilter.ItemsSource = new[] { _text.BatchTable_All, _text.BatchNaming_Unchecked, _text.BatchNaming_Matches, _text.BatchNaming_Differs, _text.BatchNaming_Error };
        BatchNamingFilter.SelectedIndex = 0;
        BatchSeparatePackages.IsChecked = _preferences.SharedSettings.BatchSeparatePackages;
        BatchGroupFiles.IsChecked = _preferences.SharedSettings.BatchGroupFiles;
        AddBatchMetadataColumns();
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
        _batchFolder = BatchInputPath.Text?.Trim();
        BatchStartButton.IsEnabled = BatchHashButton.IsEnabled = _batchCancellation == null && _mtpCancellation == null && (Directory.Exists(_batchFolder) || File.Exists(_batchFolder));
    }
    private void SetBatchBusy(bool busy)
    {
        BatchStartButton.IsEnabled = BatchHashButton.IsEnabled = !busy && (Directory.Exists(_batchFolder) || File.Exists(_batchFolder));
        BatchVerifyAllButton.IsEnabled = !busy && _batchRows.Count > 0;
        BatchCancelButton.IsEnabled = busy;
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

    private async Task RunBatchAsync(string[] sources, bool verify)
    {
        if (_batchCancellation != null || _mtpCancellation != null) return;
        using var workspaceJob = LockWorkspace(BatchTab);
        using var cancellation = new CancellationTokenSource(); _batchCancellation = cancellation;
        _batchRows.Clear(); RefreshBatchFilter(); SetBatchBusy(true);
        StatusText.Text = _text.LoadingFile_PleaseWait;
        var recursive = BatchRecursive.IsChecked == true;
        var includeArchives = BatchIncludeArchives.IsChecked == true;
        var keys = _keysPath;
        var progress = new Progress<BatchRow>(row => { if (_closed || !ReferenceEquals(_batchCancellation, cancellation)) return; _batchRows.Add(row); LogBatchFile(row); RefreshBatchFilter(followResult: row); StatusText.Text = $"{_text.LoadingFile_PleaseWait} {row.FileName}"; });
        try
        {
            await Task.Run(() =>
            {
                foreach (var source in sources)
                foreach (var file in EnumerateBatchSources(source, recursive, includeArchives || File.Exists(source), cancellation.Token))
                { cancellation.Token.ThrowIfCancellationRequested(); ((IProgress<BatchRow>)progress).Report(ReadBatchRow(file.Path, file.DisplayPath, keys, verify, cancellation.Token)); }
            });
            if (!_closed)
            {
                if (sources.Length == 1 && _batchRows.Count > 0) SaveBatchHistory(sources[0]);
                StatusText.Text = _batchRows.Count > 0 ? _text.Status_Ready : string.Format(_text.FileNotSupported_Log, string.Join(", ", sources));
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { if (!_closed) StatusText.Text = _text.Log_NcasIntegrityCanceled; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or SharpCompress.Common.SharpCompressException) { if (!_closed) { StatusText.Text = ex.Message; LogSession("ERROR", ex.Message); } }
        finally { _batchCancellation = null; if (!_closed) SetBatchBusy(false); }
    }
    private BatchRow ReadBatchRow(string path, string display, string? keys, bool verify, CancellationToken token)
    {
        try
        {
            if (Emignatik.NxFileViewer.FileLoading.NupReader.IsNup(path) && !_preferences.SharedSettings.EnableNup)
                throw new InvalidDataException(_text.Features_NupDisabled);
            if (_preferences.FirmwareActive && (Path.GetExtension(path).ToLowerInvariant() is ".zip" or ".7z"))
                return ReadBatchFirmware(path, token);

            if (_preferences.NandActive && Emignatik.NxFileViewer.Services.Nand.NandDetection.IsCandidateName(path) &&
                Emignatik.NxFileViewer.Services.Nand.NandDetection.Detect(path, token) is { } nand)
                return new BatchRow(path, nand.Information)
                {
                    Nand = nand, DisplayPath = display, Structure = nand.Type,
                    Signature = _text.Signature_Unchecked, Integrity = _text.NcasIntegrity_Unchecked
                };

            using var sourceLock = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var checks = verify ? new List<NcaHashResult>() : null;
            var package = PackageFile.Read(path, keys, checks, token, includeDetails: true);
            var error = package.MetadataError ?? (package.NeedsProdKeys ? _text.WarnNoProdKeysFileFound : null);
            if (package.Type == "NUP")
                return new BatchRow(path, package.HeaderDetails ?? package.Type, false)
                {
                    Package = package, DisplayPath = display, Modified = File.GetLastWriteTimeUtc(path).Ticks,
                    Verified = verify, Signature = _text.Signature_Unchecked,
                    Integrity = verify ? _text.Signature_Passed : _text.NcasIntegrity_Unchecked, Naming = _text.BatchNaming_Unchecked
                };
            var original = checks is { Count: > 0 } && checks.All(c => c.Matches == true && c.Signature == Validity.Valid && c.Error == null);
            var corrupt = checks?.Any(c => c.Matches == false) == true;
            var invalid = checks?.Any(c => c.Error != null || c.Signature == Validity.Invalid) == true;
            var signature = checks == null ? _text.Signature_Unchecked : checks.Count > 0 && checks.All(c => c.Signature == Validity.Valid) ? _text.Signature_Passed : _text.Signature_NotPassed;
            var integrity = !verify ? _text.NcasIntegrity_Unchecked : corrupt ? _text.NcasIntegrity_Corrupted : invalid || error != null ? _text.NcasIntegrity_Error : original ? _text.NcasIntegrity_Original : _text.NcasIntegrity_Unknown;
            var summary = package.Type + " · " + package.Size.ToFileSize() + (error == null ? "" : " · " + error);
            if (checks != null) summary += Environment.NewLine + string.Join(Environment.NewLine, checks.Select(FormatCheck));
            return new BatchRow(path, summary, error != null || corrupt || invalid) { PreservePackageOrder = _preferences.SharedSettings.BatchSummaryPreservePackageOrder, Package = package, DisplayPath = display, Modified = File.GetLastWriteTimeUtc(path).Ticks, Verified = verify, Original = original, Signature = signature, Integrity = integrity, Error = error ?? string.Join("; ", checks?.Where(c => c.Error != null).Select(c => c.Error) ?? Array.Empty<string>()), Structure = PackageStructureAnalyzer.GetDisplayName(PackageStructureAnalyzer.AnalyzeNspNames(package.Entries.Select(entry => entry.Name)).ToString(), _text), Naming = _text.BatchNaming_Unchecked };
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or OverflowException or LibHac.Common.LibHacException or LibHac.NSZ.NczFormatException or ZstdSharp.ZstdException)
        { return new BatchRow(path, ex.Message, true) { DisplayPath = display, Error = ex.Message, Integrity = _text.NcasIntegrity_Error, Naming = _text.BatchNaming_Unchecked }; }
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
        try
        {
            foreach (var row in rows)
            {
                cancellation.Token.ThrowIfCancellationRequested();
                StatusText.Text = $"{_text.NcasIntegrity_InProgress} {row.FileName}";
                var updated = await Task.Run(() => ReadBatchRow(row.Path, row.DisplayPath, keys, true, cancellation.Token));
                if (_closed) return;
                var index = _batchRows.IndexOf(row); if (index >= 0) _batchRows[index] = updated;
                LogBatchFile(updated); RefreshBatchFilter(followResult: updated);
                BatchProgress.Value++;
            }
            if (!_closed && !string.IsNullOrWhiteSpace(_batchFolder)) SaveBatchHistory(_batchFolder);
            StatusText.Text = _text.Status_Ready;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { if (!_closed) StatusText.Text = _text.Log_NcasIntegrityCanceled; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            if (!_closed) { StatusText.Text = error.Message; LogSession("ERROR", error.Message); }
        }
        finally { _batchCancellation = null; if (!_closed) SetBatchBusy(false); }
    }
    private readonly Dictionary<DataGridColumn, (bool Visible, DataGridLength Width)> _defaultBatchColumns = new();
    private void AddBatchMetadataColumns()
    {
        foreach (var (field, title, visible) in new[] {
            ("Compression", "Kompression", true), ("ContentType", "Pakettyp", true), ("Security", "Sicherheit", true),
            ("MinimumSystemVersion", "Min. Systemversion", false), ("BaseTitleId", "Basis-TitelID", false), ("MasterKey", "Master-Key", false),
            ("MinimumApplicationVersion", "Min. Anwendungsversion", false), ("Distribution", "Distribution", false), ("BuildId", "Build-ID", false),
            ("Permissions", "Dateisystemberechtigungen", false), ("AcidSignature", "ACID-Signatur", false), ("Publisher", "Herausgeber", false),
            ("DisplayVersion", "Angezeigte Version", false), ("Languages", "Sprachen", false), ("DisplayPath", "Pfad", false),
            ("NewName", "Neuer Name", false), ("NamingError", "Benennungsfehler", false),
            (nameof(BatchRow.MissingKeys), _text.File_MissingKeysHeader, false),
            (nameof(BatchRow.CompressionRatio), _text.FileInfo_CompressionRatio, false),
            (nameof(BatchRow.ConversionStatus), _text.BatchTable_ConversionStatus, false),
            (nameof(BatchRow.OutputPath), _text.BatchTable_OutputPath, false) })
            BatchResults.Columns.Add(new DataGridTextColumn { Header = title, Binding = new global::Avalonia.Data.Binding(field), IsVisible = visible, Width = new DataGridLength(150) });
        foreach (var column in BatchResults.Columns) _defaultBatchColumns[column] = (column.IsVisible, column.Width);
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
        var rows = ExpandedBatchRows(_batchRows, BatchSeparatePackages.IsChecked == true).Where(row => (BatchErrorsOnly.IsChecked != true || row.HasError) && (BatchNamingFilter.SelectedIndex <= 0 || row.Naming == BatchNamingFilter.SelectedItem as string) && (BatchTypeFilter.SelectedIndex <= 0 || (row.FileType == BatchTypeFilter.SelectedItem as string || row.ContainerType == BatchTypeFilter.SelectedItem as string)) && (BatchIntegrityFilter.SelectedIndex <= 0 || row.Integrity == BatchIntegrityFilter.SelectedItem as string) && (search.Length == 0 || (row.DisplayPath + " " + row.Summary + " " + row.Title + " " + row.TitleId).Contains(search, StringComparison.CurrentCultureIgnoreCase)));
        if (BatchAlphabetical.IsChecked == true) rows = rows.OrderBy(row => row.Title, StringComparer.CurrentCultureIgnoreCase).ThenBy(row => row.TitleId);
        return rows.ToArray();
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
    private void ResetBatchFilters(object? sender, RoutedEventArgs e) { BatchSearch.Text = ""; BatchErrorsOnly.IsChecked = BatchAlphabetical.IsChecked = false; BatchTypeFilter.SelectedIndex = BatchIntegrityFilter.SelectedIndex = BatchNamingFilter.SelectedIndex = 0; foreach (var column in BatchResults.Columns) column.ClearSort(); _batchSort = []; RefreshBatchFilter(); }
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
    private async void OpenBatchResult(object? sender, TappedEventArgs e) { if (_workspaceJobs.Count == 0 && BatchResults.SelectedItem is BatchRow row) await OpenFileAsync(row.Path); }
}
