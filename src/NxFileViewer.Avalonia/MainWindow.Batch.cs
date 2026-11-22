using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Input;
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
        public string DisplayPath { get; init; } = Path;
        public string FileName => System.IO.Path.GetFileName(DisplayPath) + (PartNumber > 0 ? "::" + PartNumber : "");
        public string FileType => Package?.Type ?? "—";
        public string Structure { get; init; } = "—";
        public string Signature { get; init; } = "—";
        public string Integrity { get; init; } = "—";
        public string Size => Package?.Size.ToFileSize() ?? "—";
        public Content? ContentOverride { get; init; }
        public int PartNumber { get; init; }
        public bool PreservePackageOrder { get; init; }
        private IEnumerable<Content> Contents => ContentOverride != null ? new[] { ContentOverride } : Package?.Titles ?? Array.Empty<Content>();
        private string SummaryValue(Func<Content,string> selector, bool versions = false)
        {
            var values = Contents.Select(selector).Where(value => !string.IsNullOrWhiteSpace(value) && value != "—").Distinct().ToArray();
            return values.Length == 0 ? "—" : PreservePackageOrder ? string.Join(" / ", values) : versions ? BatchSummaryValues.Versions(values) : BatchSummaryValues.Text(values);
        }
        public string Title => SummaryValue(content => content.NacpData?.Titles.FirstOrDefault(title => !string.IsNullOrWhiteSpace(title?.Name))?.Name ?? "");
        public string TitleId => SummaryValue(content => content.TitleId);
        public string Version => SummaryValue(content => content.Version.Version.ToString(), true);
        public string ContentType => PreservePackageOrder ? BatchContentMetadata.Types(Contents.Select(content => content.Type)) : BatchSummaryValues.Types(Contents.Select(content => content.Type switch { ContentMetaType.Application => "Base", ContentMetaType.Patch => "Update", ContentMetaType.AddOnContent => "DLC", _ => content.Type.ToString() }));
        public string PackageType => Package == null ? "—" : Package.Titles.Count > 1 ? "Multi" : "Single";
        public string Compression => FileType is "NSZ" or "XCZ" or "NCZ" ? "Zstandard" : "—";
        public string MinimumSystemVersion => SummaryValue(content => content.MinimumSystemVersion.Version.ToString(), true);
        public string BaseTitleId => SummaryValue(content => content.ApplicationTitleId);
        public string MasterKey => SummaryValue(content => content.KeyGeneration?.ToString() ?? "", true);
        public string MinimumApplicationVersion => SummaryValue(content => content.MinimumApplicationVersion.Version.ToString(), true);
        public string Distribution => SummaryValue(content => content.Distribution ?? "");
        public string BuildId => SummaryValue(content => content.BuildId ?? "");
        public string Permissions => SummaryValue(content => content.FileSystemPermissions?.ToString("X16") ?? "");
        public string Security => SummaryValue(content => content.SecurityLevel ?? "");
        public string AcidSignature => SummaryValue(content => content.AcidSignature ?? "");
        public string Publisher => SummaryValue(content => content.NacpData?.Titles.FirstOrDefault(title => title != null)?.Publisher ?? "");
        public string DisplayVersion => SummaryValue(content => content.NacpData?.DisplayVersion ?? "", true);
        public string Languages => string.Join(", ", Contents.Where(content => content.NacpData != null).SelectMany(content => content.NacpData!.Titles.Select((title, index) => (title, index)).Where(item => item.title != null).Select(item => ((Emignatik.NxFileViewer.Models.Overview.NacpLanguage)item.index).ToString())).Distinct());
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
        BatchTypeFilter.ItemsSource = new[] { _text.BatchTable_All, "NSP", "NSZ", "XCI", "XCZ", "NCA", "NCZ", "NRO" };
        BatchIntegrityFilter.ItemsSource = new[] { _text.BatchTable_All, _text.NcasIntegrity_Unchecked, _text.NcasIntegrity_Original, _text.NcasIntegrity_Corrupted, _text.NcasIntegrity_Error };
        BatchTypeFilter.SelectedIndex = BatchIntegrityFilter.SelectedIndex = 0;
        BatchNamingFilter.ItemsSource = new[] { _text.BatchTable_All, _text.BatchNaming_Unchecked, _text.BatchNaming_Matches, _text.BatchNaming_Differs, _text.BatchNaming_Error };
        BatchNamingFilter.SelectedIndex = 0;
        BatchSeparatePackages.IsChecked = _preferences.SharedSettings.BatchSeparatePackages;
        AddBatchMetadataColumns();
        _batchReady = true;
        LoadBatchHistory();
        RefreshBatchFilter();
    }
    private async void ChooseBatchFolder(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = _text.BatchIntegrity_SelectDirectory });
        if (!_closed && folders.FirstOrDefault()?.TryGetLocalPath() is { } folder) BatchInputPath.Text = folder;
    }
    private async void ChooseBatchArchive(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = _text.Firmware_BrowseZip, FileTypeFilter = new[] { new FilePickerFileType("ZIP / 7z") { Patterns = new[] { "*.zip", "*.7z" } } } });
        if (!_closed && files.FirstOrDefault()?.TryGetLocalPath() is { } archive) BatchInputPath.Text = archive;
    }
    private void BatchPathChanged(object? sender, TextChangedEventArgs e)
    {
        if (BatchStartButton == null || BatchHashButton == null) return;
        _batchFolder = BatchInputPath.Text;
        BatchStartButton.IsEnabled = BatchHashButton.IsEnabled = _batchCancellation == null && _mtpCancellation == null && (Directory.Exists(_batchFolder) || File.Exists(_batchFolder));
    }
    private void SetBatchBusy(bool busy)
    {
        BatchStartButton.IsEnabled = BatchHashButton.IsEnabled = !busy && (Directory.Exists(_batchFolder) || File.Exists(_batchFolder));
        BatchVerifyAllButton.IsEnabled = !busy && _batchRows.Count > 0;
        BatchCancelButton.IsEnabled = busy;
        UpdateBatchActionStates();
    }
    private async void StartBatch(object? sender, RoutedEventArgs e)
    {
        if (_batchCancellation != null || _mtpCancellation != null || string.IsNullOrWhiteSpace(_batchFolder)) return;
        var source = _batchFolder;
        using var cancellation = new CancellationTokenSource(); _batchCancellation = cancellation;
        _batchRows.Clear(); RefreshBatchFilter(); SetBatchBusy(true);
        var recursive = BatchRecursive.IsChecked == true;
        var includeArchives = BatchIncludeArchives.IsChecked == true || File.Exists(source);
        var verify = ReferenceEquals(sender, BatchHashButton);
        var keys = _keysPath;
        var progress = new Progress<BatchRow>(row => { if (_closed || !ReferenceEquals(_batchCancellation, cancellation)) return; _batchRows.Add(row); LogSession(row.HasError ? "ERROR" : "INFO", row.DisplayPath + " : " + row.Summary); RefreshBatchFilter(); StatusText.Text = _batchRows.Count.ToString("N0"); });
        try
        {
            await Task.Run(() =>
            {
                foreach (var file in EnumerateBatchSources(source, recursive, includeArchives, cancellation.Token))
                { cancellation.Token.ThrowIfCancellationRequested(); ((IProgress<BatchRow>)progress).Report(ReadBatchRow(file.Path, file.DisplayPath, keys, verify, cancellation.Token)); }
            });
            if (!_closed) SaveBatchHistory(source);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { if (!_closed) StatusText.Text = _text.Log_NcasIntegrityCanceled; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { if (!_closed) { StatusText.Text = ex.Message; LogSession("ERROR", ex.Message); } }
        finally { _batchCancellation = null; if (!_closed) SetBatchBusy(false); }
    }
    private BatchRow ReadBatchRow(string path, string display, string? keys, bool verify, CancellationToken token)
    {
        try
        {
            using var sourceLock = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var checks = verify ? new List<NcaHashResult>() : null;
            var package = PackageFile.Read(path, keys, checks, token, includeDetails: true);
            var error = package.MetadataError ?? (package.NeedsProdKeys ? _text.WarnNoProdKeysFileFound : null);
            var original = checks is { Count: > 0 } && checks.All(c => c.Matches == true && c.Signature == Validity.Valid && c.Error == null);
            var corrupt = checks?.Any(c => c.Matches == false) == true;
            var invalid = checks?.Any(c => c.Error != null || c.Signature == Validity.Invalid) == true;
            var signature = checks == null ? _text.Signature_Unchecked : checks.Count > 0 && checks.All(c => c.Signature == Validity.Valid) ? _text.Signature_Passed : _text.Signature_NotPassed;
            var integrity = !verify ? _text.NcasIntegrity_Unchecked : corrupt ? _text.NcasIntegrity_Corrupted : invalid || error != null ? _text.NcasIntegrity_Error : original ? _text.NcasIntegrity_Original : _text.NcasIntegrity_Unknown;
            var summary = package.Type + " · " + package.Size.ToFileSize() + (error == null ? "" : " · " + error);
            if (checks != null) summary += Environment.NewLine + string.Join(Environment.NewLine, checks.Select(FormatCheck));
            return new BatchRow(path, summary, error != null || corrupt || invalid) { PreservePackageOrder = _preferences.SharedSettings.BatchSummaryPreservePackageOrder, Package = package, DisplayPath = display, Modified = File.GetLastWriteTimeUtc(path).Ticks, Verified = verify, Original = original, Signature = signature, Integrity = integrity, Error = error ?? string.Join("; ", checks?.Where(c => c.Error != null).Select(c => c.Error) ?? Array.Empty<string>()), Structure = PackageStructureAnalyzer.GetDisplayName(PackageStructureAnalyzer.AnalyzeNspNames(package.Entries.Select(entry => entry.Name)).ToString(), _text), Naming = _text.BatchNaming_Unchecked };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or OverflowException or LibHac.Common.LibHacException or LibHac.NSZ.NczFormatException or ZstdSharp.ZstdException)
        { return new BatchRow(path, ex.Message, true) { DisplayPath = display, Error = ex.Message, Integrity = _text.NcasIntegrity_Error, Naming = _text.BatchNaming_Unchecked }; }
    }
    private async void VerifyBatchRows(object? sender, RoutedEventArgs e)
    {
        if (_batchCancellation != null) return;
        using var cancellation = new CancellationTokenSource(); _batchCancellation = cancellation; SetBatchBusy(true);
        var rows = _batchRows.ToArray(); var keys = _keysPath;
        try
        {
            foreach (var row in rows)
            {
                cancellation.Token.ThrowIfCancellationRequested();
                var updated = await Task.Run(() => ReadBatchRow(row.Path, row.DisplayPath, keys, true, cancellation.Token));
                if (_closed) return;
                var index = _batchRows.IndexOf(row); if (index >= 0) _batchRows[index] = updated;
                LogSession(updated.HasError ? "ERROR" : "INFO", updated.DisplayPath + " : " + updated.Summary); RefreshBatchFilter();
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { if (!_closed) StatusText.Text = _text.Log_NcasIntegrityCanceled; }
        finally { _batchCancellation = null; if (!_closed) SetBatchBusy(false); }
    }
    private readonly Dictionary<DataGridColumn, (bool Visible, DataGridLength Width)> _defaultBatchColumns = new();
    private void AddBatchMetadataColumns()
    {
        foreach (var (field, title, visible) in new[] {
            ("ContentType", "Typ", true), ("Compression", "Kompression", true), ("PackageType", "Pakettyp", true), ("Security", "Sicherheit", true),
            ("MinimumSystemVersion", "Min. Systemversion", false), ("BaseTitleId", "Basis-TitelID", false), ("MasterKey", "Master-Key", false),
            ("MinimumApplicationVersion", "Min. Anwendungsversion", false), ("Distribution", "Distribution", false), ("BuildId", "Build-ID", false),
            ("Permissions", "Dateisystemberechtigungen", false), ("AcidSignature", "ACID-Signatur", false), ("Publisher", "Herausgeber", false),
            ("DisplayVersion", "Angezeigte Version", false), ("Languages", "Sprachen", false), ("DisplayPath", "Pfad", false),
            ("NewName", "Neuer Name", false), ("NamingError", "Benennungsfehler", false) })
            BatchResults.Columns.Add(new DataGridTextColumn { Header = title, Binding = new global::Avalonia.Data.Binding(field), IsVisible = visible, Width = new DataGridLength(150) });
        foreach (var column in BatchResults.Columns) _defaultBatchColumns[column] = (column.IsVisible, column.Width);
    }
    private void BatchFilterChanged(object? sender, RoutedEventArgs e) { if (_batchReady) RefreshBatchFilter(); }
    private void BatchChoiceChanged(object? sender, SelectionChangedEventArgs e) { if (_batchReady) RefreshBatchFilter(); }
    private static IEnumerable<BatchRow> ExpandedBatchRows(IEnumerable<BatchRow> rows, bool separate)
    {
        foreach (var row in rows)
        {
            if (separate && row.Package?.Titles.Count > 1)
                for (var index = 0; index < row.Package.Titles.Count; index++) yield return row with { ContentOverride = row.Package.Titles[index], PartNumber = index + 1 };
            else yield return row;
        }
    }
    private BatchRow[] FilteredBatchRows()
    {
        var search = BatchSearch.Text?.Trim() ?? "";
        var rows = ExpandedBatchRows(_batchRows, BatchSeparatePackages.IsChecked == true).Where(row => (BatchErrorsOnly.IsChecked != true || row.HasError) && (BatchNamingFilter.SelectedIndex <= 0 || row.Naming == BatchNamingFilter.SelectedItem as string) && (BatchTypeFilter.SelectedIndex <= 0 || row.FileType == BatchTypeFilter.SelectedItem as string) && (BatchIntegrityFilter.SelectedIndex <= 0 || row.Integrity == BatchIntegrityFilter.SelectedItem as string) && (search.Length == 0 || (row.DisplayPath + " " + row.Summary + " " + row.Title + " " + row.TitleId).Contains(search, StringComparison.CurrentCultureIgnoreCase)));
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
    private void RefreshBatchFilter() { UpdateBatchActionStates(); BatchResults.ItemsSource = FilteredBatchRows(); BatchExportButton.IsEnabled = _batchRows.Count > 0; BatchVerifyAllButton.IsEnabled = _batchRows.Count > 0 && _batchCancellation == null; }
    private void ResetBatchFilters(object? sender, RoutedEventArgs e) { BatchSearch.Text = ""; BatchErrorsOnly.IsChecked = BatchAlphabetical.IsChecked = false; BatchTypeFilter.SelectedIndex = BatchIntegrityFilter.SelectedIndex = BatchNamingFilter.SelectedIndex = 0; foreach (var column in BatchResults.Columns) column.ClearSort(); RefreshBatchFilter(); }
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
    private async void OpenBatchResult(object? sender, TappedEventArgs e) { if (BatchResults.SelectedItem is BatchRow row) await OpenFileAsync(row.Path); }
}
