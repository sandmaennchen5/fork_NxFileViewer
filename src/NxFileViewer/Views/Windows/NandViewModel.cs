using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using Emignatik.NxFileViewer.Localization;
using Emignatik.NxFileViewer.Services.BackgroundTask;
using Emignatik.NxFileViewer.Services.BackgroundTask.RunnableImpl;
using Emignatik.NxFileViewer.Services.Nand;
using Emignatik.NxFileViewer.Utils.MVVM;
using Emignatik.NxFileViewer.Utils.MVVM.Commands;
using Microsoft.Win32;
using System.ComponentModel;
using Emignatik.NxFileViewer.FileLoading;
using Emignatik.NxFileViewer.Models;
using Emignatik.NxFileViewer.Services.FileOpening;
using System.Linq;
using System.Collections.ObjectModel;

namespace Emignatik.NxFileViewer.Views.Windows;

public sealed class NandViewModel : NotifyPropertyChangedBase
{
    private readonly NandCliPlugin _plugin;
    private readonly IFileOpeningService? _opening;
    private NandExplorerEntry? _save;
    private string _physicalSource = "";
    private string _source = "";
    private string _information = "";
    private string? _partition;
    private IReadOnlyList<string> _partitions = Array.Empty<string>();
    public NandViewModel(NandCliPlugin plugin, IMainBackgroundTaskRunnerService runner, IFileOpeningService? opening = null)
    {
        _plugin = plugin;
        _opening = opening;
        Runner = runner;
        OpenCommand = new RelayCommand(Open, () => !Runner.IsRunning);
        ExportCommand = new RelayCommand(Export, () => !Runner.IsRunning && !string.IsNullOrEmpty(SelectedPartition) && File.Exists(_physicalSource));
        ReadInformationCommand = new RelayCommand(ReadInformation, () => !Runner.IsRunning && File.Exists(_physicalSource));
        OpenDriveCommand = new RelayCommand(OpenDrive, () => !Runner.IsRunning);
        BrowseExplorerCommand = new RelayCommand(BrowseExplorer, () => !Runner.IsRunning && !string.IsNullOrEmpty(_physicalSource));
        UpExplorerCommand = new RelayCommand(() => LoadExplorer(Path.GetDirectoryName(ExplorerPath.Replace('/', '\\'))?.Replace('\\', '/') ?? "/"), () => !Runner.IsRunning && ExplorerPath != "/");
        ExportExplorerCommand = new RelayCommand(ExportExplorer, () => !Runner.IsRunning && SelectedExplorerEntry is { IsDirectory: false });
        OpenSaveCommand = new RelayCommand(() => LoadExplorerContent("/", SelectedExplorerEntry), () => !Runner.IsRunning && _save == null && SelectedExplorerEntry is { IsSave: true });
        LeaveSaveCommand = new RelayCommand(() => LoadExplorerContent("/save", null), () => !Runner.IsRunning && _save != null);
        ExportSaveCommand = new RelayCommand(ExportSave, () => !Runner.IsRunning && _save != null);
        PropertyChangedEventManager.AddHandler(Runner, OnTaskChanged, "");
    }
    private void OnTaskChanged(object? sender, PropertyChangedEventArgs e)
    {
        OpenCommand.TriggerCanExecuteChanged(true); ExportCommand.TriggerCanExecuteChanged(true); ReadInformationCommand.TriggerCanExecuteChanged(true);
        OpenSaveCommand.TriggerCanExecuteChanged(true); LeaveSaveCommand.TriggerCanExecuteChanged(true); ExportSaveCommand.TriggerCanExecuteChanged(true);
        OpenDriveCommand.TriggerCanExecuteChanged(true); BrowseExplorerCommand.TriggerCanExecuteChanged(true); UpExplorerCommand.TriggerCanExecuteChanged(true); ExportExplorerCommand.TriggerCanExecuteChanged(true);
    }
    public bool IsStandalone { get; private set; } = true;
    public RelayCommand ReadInformationCommand { get; }
    public RelayCommand OpenDriveCommand { get; }
    public RelayCommand BrowseExplorerCommand { get; }
    public RelayCommand UpExplorerCommand { get; }
    public RelayCommand ExportExplorerCommand { get; }
    public RelayCommand OpenSaveCommand { get; }
    public RelayCommand LeaveSaveCommand { get; }
    public RelayCommand ExportSaveCommand { get; }
    public string ExplorerDisplayPath => _save == null ? ExplorerPath : _save.Name + ":" + ExplorerPath;
    public IReadOnlyList<NandExplorerPartition> ExplorerPartitions { get; private set; } = Array.Empty<NandExplorerPartition>();
    private NandExplorerPartition? _explorerPartition;
    public NandExplorerPartition? ExplorerPartition
    {
        get => _explorerPartition;
        set
        {
            if (ReferenceEquals(_explorerPartition, value)) return;
            _save = null;
            _explorerPartition = value; NotifyPropertyChanged();
            if (value != null) { _partition = value.Name; NotifyPropertyChanged(nameof(SelectedPartition)); ExportCommand.TriggerCanExecuteChanged(); }
            ExplorerEntries = Array.Empty<NandExplorerEntry>(); ExplorerFolders.Clear(); ExplorerPath = "/";
            SelectedExplorerEntry = null;
            NotifyPropertyChanged(nameof(ExplorerEntries)); NotifyPropertyChanged(nameof(ExplorerPath)); NotifyPropertyChanged(nameof(ExplorerDisplayPath));
            UpExplorerCommand.TriggerCanExecuteChanged();
            LeaveSaveCommand.TriggerCanExecuteChanged(); ExportSaveCommand.TriggerCanExecuteChanged();
            if (value != null) LoadExplorer("/");
        }
    }
    public string ExplorerPath { get; private set; } = "/";
    public int ExplorerTabIndex { get; set; }
    public IReadOnlyList<NandExplorerEntry> ExplorerEntries { get; private set; } = Array.Empty<NandExplorerEntry>();
    public ObservableCollection<NandExplorerFolder> ExplorerFolders { get; } = new();
    private NandExplorerEntry? _selectedExplorerEntry;
    public NandExplorerEntry? SelectedExplorerEntry
    {
        get => _selectedExplorerEntry;
        set { _selectedExplorerEntry = value; NotifyPropertyChanged(); ExportExplorerCommand.TriggerCanExecuteChanged(); OpenSaveCommand.TriggerCanExecuteChanged(); }
    }
    private void ResetExplorer()
    {
        ExplorerPartition = null; ExplorerPartitions = Array.Empty<NandExplorerPartition>();
        ExplorerPath = "/"; SelectedExplorerEntry = null;
        ExplorerFolders.Clear(); ExplorerTabIndex = 0; NotifyPropertyChanged(nameof(ExplorerTabIndex));
        NotifyPropertyChanged(nameof(ExplorerPartitions)); NotifyPropertyChanged(nameof(ExplorerPath));
        BrowseExplorerCommand.TriggerCanExecuteChanged();
    }
    private async void BrowseExplorer()
    {
        try
        {
            if (ExplorerPartitions.Count == 0)
            {
                var source = _physicalSource;
                ExplorerPartitions = await Runner.RunAsync(new RunnableRelay<IReadOnlyList<NandExplorerPartition>>((_, token) => _plugin.ExplorerPartitions(source, token)) { SupportsCancellation = true });
                NotifyPropertyChanged(nameof(ExplorerPartitions));
                ExplorerPartition = ExplorerPartitions.FirstOrDefault(p => p.Name == "USER") ?? ExplorerPartitions.FirstOrDefault(p => p.Name == "SYSTEM") ?? ExplorerPartitions.FirstOrDefault();
            }
            else LoadExplorerContent("/", null);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { ThemedDialog.Notice(ex.Message, "NAND Explorer", MessageBoxImage.Error); }
    }
    public void LoadExplorer(string path) => LoadExplorerContent(path, _save);
    private async void LoadExplorerContent(string path, NandExplorerEntry? save)
    {
        if (Runner.IsRunning || ExplorerPartition == null) return;
        try
        {
            var source = _physicalSource; var partition = ExplorerPartition;
            var entries = await Runner.RunAsync(new RunnableRelay<IReadOnlyList<NandExplorerEntry>>((_, token) => save == null ? _plugin.ListExplorer(source, partition, path, token) : _plugin.ListSave(source, partition, save, path, token)) { SupportsCancellation = true });
            if (source != _physicalSource || !ReferenceEquals(partition, ExplorerPartition)) return;
            var contextChanged = !ReferenceEquals(_save, save);
            _save = save;
            ExplorerEntries = entries; ExplorerPath = path; SelectedExplorerEntry = null;
            if (path == "/" || contextChanged) { ExplorerFolders.Clear(); ExplorerFolders.Add(new NandExplorerFolder(path, path)); }
            var folder = FindFolder(ExplorerFolders, path);
            if (folder != null)
            {
                folder.Children.Clear();
                foreach (var entry in entries.Where(e => e.IsDirectory)) folder.Children.Add(new NandExplorerFolder(entry.Name, entry.Path));
            }
            ExplorerTabIndex = 1; NotifyPropertyChanged(nameof(ExplorerTabIndex));
            NotifyPropertyChanged(nameof(ExplorerEntries)); NotifyPropertyChanged(nameof(ExplorerPath)); NotifyPropertyChanged(nameof(ExplorerDisplayPath)); UpExplorerCommand.TriggerCanExecuteChanged();
            OpenSaveCommand.TriggerCanExecuteChanged(); LeaveSaveCommand.TriggerCanExecuteChanged(); ExportSaveCommand.TriggerCanExecuteChanged();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { ThemedDialog.Notice(ex.Message, "NAND Explorer", MessageBoxImage.Error); }
    }
    private static NandExplorerFolder? FindFolder(IEnumerable<NandExplorerFolder> folders, string path)
    {
        foreach (var folder in folders)
        {
            if (folder.Path == path) return folder;
            if (FindFolder(folder.Children, path) is { } found) return found;
        }
        return null;
    }
    private async void ExportExplorer()
    {
        if (SelectedExplorerEntry == null || ExplorerPartition == null) return;
        var entry = SelectedExplorerEntry; var partition = ExplorerPartition; var source = _physicalSource; var save = _save;
        var dialog = new SaveFileDialog { FileName = entry.Name, OverwritePrompt = true };
        if (dialog.ShowDialog() != true) return;
        if (File.Exists(dialog.FileName)) { ThemedDialog.Notice(LocalizationManager.Instance.Current.Keys.Nand_NewTarget, "NAND Explorer", MessageBoxImage.Information); return; }
        try
        {
            await Runner.RunAsync(new RunnableRelay((_, token) => { if (save == null) _plugin.ExportExplorer(source, partition, entry, dialog.FileName, token); else _plugin.ExportSaveFile(source, partition, save, entry.Path, dialog.FileName, token); }) { SupportsCancellation = true });
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { ThemedDialog.Notice(ex.Message, "NAND Explorer", MessageBoxImage.Error); }
    }
    private async void ExportSave()
    {
        if (_save == null || ExplorerPartition == null || Runner.IsRunning) return;
        var save = _save; var partition = ExplorerPartition; var source = _physicalSource;
        var dialog = new OpenFolderDialog { Title = LocalizationManager.Instance.Current.Keys.Nand_ExportSave };
        if (dialog.ShowDialog() != true) return;
        var destination = Path.Combine(dialog.FolderName, save.Name + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        try
        {
            await Runner.RunAsync(new RunnableRelay((_, token) => _plugin.ExportSave(source, partition, save, destination, token)) { SupportsCancellation = true });
            ThemedDialog.Notice(LocalizationManager.Instance.Current.Keys.Nand_ExportDone + Environment.NewLine + destination, "NAND Explorer", MessageBoxImage.Information);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { ThemedDialog.Notice(ex.Message, "NAND Explorer", MessageBoxImage.Error); }
    }
    private void OpenDrive()
    {
        var picker = new NandDriveWindow { Owner = Application.Current?.MainWindow };
        if (picker.ShowDialog() != true) return;
        try
        {
            var partitions = _plugin.ExplorerPartitions(picker.Source, default);
            _physicalSource = picker.Source; Source = picker.Source;
            ResetExplorer(); ExplorerPartitions = partitions;
            NotifyPropertyChanged(nameof(ExplorerPartitions));
            ExplorerPartition = partitions.FirstOrDefault(p => p.Name == "USER") ?? partitions.FirstOrDefault(p => p.Name == "SYSTEM");
            Information = string.Join(Environment.NewLine, partitions.Select(p => $"{p.Name}: {p.Size:N0} bytes"));
            Partitions = partitions.Select(p => p.Name).ToArray(); SelectedPartition = Partitions.FirstOrDefault();
        }
        catch (Exception ex) { ThemedDialog.Notice(ex.Message, "NAND Explorer", MessageBoxImage.Error); }
    }
    public void SetFile(NxFile file)
    {
        IsStandalone = false;
        NotifyPropertyChanged(nameof(IsStandalone));
        Source = file.FilePath;
        _physicalSource = file.NandPhysicalPath ?? "";
        ResetExplorer();
        Information = file.NandResult?.Information ?? "";
        Partitions = file.NandResult?.NameOnly == false ? file.NandResult.Partitions : Array.Empty<string>();
        SelectedPartition = Partitions.Count > 0 ? Partitions[0] : null;
        ReadInformationCommand.TriggerCanExecuteChanged();
    }
    public IMainBackgroundTaskRunnerService Runner { get; }
    public RelayCommand OpenCommand { get; }
    public RelayCommand ExportCommand { get; }
    public string Source { get => _source; private set { _source = value; NotifyPropertyChanged(); } }
    public string Information { get => _information; private set { _information = value; NotifyPropertyChanged(); } }
    public IReadOnlyList<string> Partitions { get => _partitions; private set { _partitions = value; NotifyPropertyChanged(); } }
    public string? SelectedPartition
    {
        get => _partition;
        set
        {
            _partition = value; NotifyPropertyChanged(); ExportCommand.TriggerCanExecuteChanged();
            if (ExplorerTabIndex == 1 && ExplorerPartitions.FirstOrDefault(p => p.Name == value) is { } partition)
                ExplorerPartition = partition;
        }
    }

    private async void Open()
    {
        var keys = LocalizationManager.Instance.Current.Keys;
        var dialog = new OpenFileDialog { Title = keys.Nand_Open, Filter = "NAND / ZIP / 7z (*.*)|*.*", CheckFileExists = true };
        if (dialog.ShowDialog() != true) return;
        if (PackageZip.IsArchive(dialog.FileName) && _opening != null)
        {
            await _opening.SafeOpenFile(dialog.FileName);
            return;
        }
        Source = "";
        _physicalSource = "";
        ResetExplorer();
        Information = "";
        Partitions = Array.Empty<string>();
        SelectedPartition = null;
        try
        {
            var information = await Runner.RunAsync(new RunnableRelay<string>((progress, token) =>
                NandDetection.Detect(dialog.FileName, token)?.Information ?? _plugin.ReadInformation(dialog.FileName, progress, token)) { SupportsCancellation = true });
            Source = dialog.FileName;
            _physicalSource = dialog.FileName;
            ResetExplorer();
            Information = information;
            Partitions = NandCliPlugin.FindPartitions(information);
            SelectedPartition = Partitions.Count > 0 ? Partitions[0] : null;
            ReadInformationCommand.TriggerCanExecuteChanged();
        }
        catch (OperationCanceledException) { Information = keys.Nand_Cancelled; }
        catch (Exception ex) { ThemedDialog.Notice(ex.Message, "NxNandManager", MessageBoxImage.Error); }
    }

    private async void ReadInformation()
    {
        try
        {
            Information = await Runner.RunAsync(new RunnableRelay<string>((progress, token) => _plugin.ReadInformation(_physicalSource, progress, token)) { SupportsCancellation = true });
            Partitions = NandCliPlugin.FindPartitions(Information);
            SelectedPartition = Partitions.Count > 0 ? Partitions[0] : null;
        }
        catch (OperationCanceledException) { ThemedDialog.Notice(LocalizationManager.Instance.Current.Keys.Nand_Cancelled, "NxNandManager", MessageBoxImage.Information); }
        catch (Exception ex) { ThemedDialog.Notice(ex.Message, "NxNandManager", MessageBoxImage.Error); }
    }

    private async void Export()
    {
        if (SelectedPartition == null) return;
        var keys = LocalizationManager.Instance.Current.Keys;
        var source = _physicalSource;
        var partition = SelectedPartition;
        var dialog = new SaveFileDialog { Title = keys.Nand_Export, FileName = partition + ".bin", Filter = "NAND (*.bin)|*.bin", OverwritePrompt = true };
        if (dialog.ShowDialog() != true) return;
        try
        {
            await Runner.RunAsync(new RunnableRelay((progress, token) => _plugin.Export(source, dialog.FileName, partition, progress, token)) { SupportsCancellation = true });
            ThemedDialog.Notice(keys.Nand_ExportDone + Environment.NewLine + dialog.FileName, "NxNandManager", MessageBoxImage.Information);
        }
        catch (OperationCanceledException) { ThemedDialog.Notice(keys.Nand_Cancelled, "NxNandManager", MessageBoxImage.Information); }
        catch (Exception ex) { ThemedDialog.Notice(ex.Message, "NxNandManager", MessageBoxImage.Error); }
    }
}

public sealed record NandExplorerFolder(string Name, string Path)
{
    public ObservableCollection<NandExplorerFolder> Children { get; } = new();
}
