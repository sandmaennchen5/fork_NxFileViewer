using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace NxFileViewer.Avalonia;

public partial class MainWindow
{
    internal enum ContentKind { Placeholder, Package, PartitionFile, Nca, Section, Directory, File, Archive, ArchiveDirectory, ArchiveFile }

    internal sealed class ContentNode(string name, ContentKind kind, long size = 0, string? member = null,
        int section = -1, string? innerPath = null) : System.ComponentModel.INotifyPropertyChanged
    {
        public string Name { get; } = name;
        public ContentKind Kind { get; } = kind;
        public long Size { get; } = size;
        public string? Member { get; } = member;
        public int Section { get; } = section;
        public string? InnerPath { get; } = innerPath;
        public string DisplayName => Metadata?.GetValueOrDefault("ContentType") is { } type && Kind == ContentKind.Nca
            ? $"{Name} ({type})" : Name;
        public bool IsRoot { get; init; }
        private Dictionary<string, string>? _metadata;
        public Dictionary<string, string>? Metadata
        {
            get => _metadata;
            set
            {
                _metadata = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(DisplayName)));
            }
        }
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        public string? Error { get; set; }
        private bool _hasErrors;
        public bool HasErrors
        {
            get => _hasErrors;
            set
            {
                if (_hasErrors == value) return;
                _hasErrors = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(HasErrors)));
            }
        }
        public bool Loaded { get; set; }
        public bool Loading { get; set; }
        public ObservableCollection<ContentNode> Children { get; } = [];
        public global::Avalonia.Media.IImage Icon => IconCache.Get(Kind switch
        {
            ContentKind.Package or ContentKind.Archive => "/Icons/NspItem.png",
            ContentKind.Section => "/Icons/SectionItem.png",
            ContentKind.Directory or ContentKind.ArchiveDirectory => "/Icons/FolderDirectoryEntryItem.png",
            ContentKind.File or ContentKind.ArchiveFile => "/Icons/FileDirectoryEntryItem.png",
            _ => "/Icons/PartitionFileEntryItem.png"
        });

        public void AddPlaceholder() => Children.Add(new ContentNode("…", ContentKind.Placeholder));
    }

    private CancellationTokenSource? _ncaContentsCancellation;
    private List<NcaHashResult>? _contentChecks;
    private string? _contentVerificationError;
    private int _contentReads;

    private void ResetNcaContents()
    {
        _ncaContentsCancellation?.Cancel();
        _ncaContentsCancellation?.Dispose();
        _ncaContentsCancellation = null;
        _contentChecks = null;
        _contentVerificationError = null;
    }

    private static bool IsNcaName(string name) => Path.GetExtension(name).Equals(".nca", StringComparison.OrdinalIgnoreCase) ||
        Path.GetExtension(name).Equals(".ncz", StringComparison.OrdinalIgnoreCase);

    internal void ShowContentTree(PackageFile package)
    {
        var name = Path.GetFileName(Nax0Contents.LogicalPath(package.Path));
        var sdContents = package.Structure == Emignatik.NxFileViewer.Services.PackageAnalysis.PackageStructure.Filesystem;
        var standalone = !sdContents && package.Type is ("NCA" or "NCZ" or "NAX0");
        var root = new ContentNode(name, sdContents ? ContentKind.Archive : standalone ? ContentKind.Nca : ContentKind.Package,
            package.Size) { IsRoot = true };
        if (standalone)
            root.AddPlaceholder();
        else
        {
            root.Metadata = new() { ["Format"] = package.Type, ["NumFiles"] = package.Entries.Count.ToString() };
            foreach (var entry in package.Entries)
            {
                var child = new ContentNode(entry.Name, sdContents ? ContentKind.ArchiveFile : IsNcaName(entry.Name) ? ContentKind.Nca : ContentKind.PartitionFile,
                    entry.Size, member: entry.Name);
                if (child.Kind == ContentKind.Nca) child.AddPlaceholder();
                root.Children.Add(child);
            }
        }
        root.Error = package.MetadataError;
        Entries.ItemsSource = new[] { root };
        Entries.SelectedItem = root;
    }

    private async void FileContentTabSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (!ReferenceEquals(e.Source, FileBody) || FileBody.SelectedIndex != 1 || _keysPath == null ||
            Entries.ItemsSource is not ContentNode[] { Length: > 0 } roots) return;
        var version = _openVersion;
        foreach (var node in roots[0].Children.Where(node => node.Kind == ContentKind.Nca))
        {
            if (_closed || version != _openVersion || FileBody.SelectedIndex != 1) return;
            await LoadContentNodeAsync(node);
        }
    }

    private async void EntryExpanded(object? sender, RoutedEventArgs e)
    {
        if (e.Source is TreeViewItem { DataContext: ContentNode node })
            await LoadContentNodeAsync(node);
    }

    private async Task LoadContentNodeAsync(ContentNode node)
    {
        if (node.Loaded || node.Loading || (node.Kind != ContentKind.Nca && node.Kind != ContentKind.Section && node.Kind != ContentKind.Directory &&
            !(node.Kind == ContentKind.PartitionFile && node.Name.EndsWith(".tik", StringComparison.OrdinalIgnoreCase))) ||
            _openedPath is not { } path) return;
        if (node.Kind != ContentKind.PartitionFile && _keysPath is not { Length: > 0 })
        {
            node.Error = _text.WarnNoProdKeysFileFound;
            ShowEntryProperties();
            return;
        }

        var keys = _keysPath;
        var version = _openVersion;
        var cancellation = _ncaContentsCancellation ??= new CancellationTokenSource();
        var token = cancellation.Token;
        node.Loading = true;
        _contentReads++;
        try
        {
            if (node.Kind == ContentKind.Nca)
            {
                var data = await Task.Run(() => NcaContents.Inspect(path, keys!, node.Member, token), token);
                if (_closed || version != _openVersion) return;
                node.Metadata = data.Properties;
                node.Children.Clear();
                foreach (var section in data.Sections)
                {
                    var child = new ContentNode($"Section {section.Index}", ContentKind.Section, member: node.Member, section: section.Index)
                    { Metadata = section.Properties };
                    child.AddPlaceholder();
                    node.Children.Add(child);
                }
            }
            else if (node.Kind is ContentKind.Section or ContentKind.Directory)
            {
                var children = await Task.Run(() => NcaContents.ReadDirectory(path, keys!, node.Member, node.Section, node.InnerPath ?? "/", token), token);
                if (_closed || version != _openVersion) return;
                node.Children.Clear();
                AddDirectoryNodes(node, children);
            }
            else
            {
                var properties = await Task.Run(() => NcaContents.ReadTicket(path, node.Member!), token);
                if (_closed || version != _openVersion) return;
                node.Metadata = properties;
            }
            node.Loaded = true;
            if (node.Kind != ContentKind.Package) node.Error = null;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error) when (IsContentError(error))
        {
            if (!_closed && version == _openVersion)
            {
                node.Error = error.Message;
                if (node.Kind != ContentKind.PartitionFile) node.Children.Clear();
                node.Loaded = true;
            }
        }
        finally
        {
            node.Loading = false;
            _contentReads--;
            if (!_closed && version == _openVersion) ShowEntryProperties();
        }
    }

    private static void AddDirectoryNodes(ContentNode node, IReadOnlyList<NcaDirectoryEntry> children)
    {
        foreach (var entry in children)
        {
            var child = new ContentNode(entry.Name, entry.Directory ? ContentKind.Directory : ContentKind.File,
                entry.Size, node.Member, node.Section, entry.Path);
            if (entry.Directory) child.AddPlaceholder();
            node.Children.Add(child);
        }
    }

    private async void EntrySelected(object? sender, SelectionChangedEventArgs e)
    {
        if (!ReferenceEquals(e.Source, Entries)) return;
        ShowEntryProperties();
        if (Entries.SelectedItem is ContentNode { Kind: ContentKind.Nca or ContentKind.PartitionFile } node)
            await LoadContentNodeAsync(node);
    }

    private void ShowEntryProperties()
    {
        if (Entries.ItemsSource is ContentNode[] roots)
            foreach (var root in roots) UpdateContentErrorMarkers(root);
        RefreshEntryActions();
        if (Entries.SelectedItem is not ContentNode { Kind: not ContentKind.Placeholder } node)
        {
            EntryProperties.ItemsSource = null;
            return;
        }
        var rows = new List<DetailRow> { new("Name", node.Name), new("Type", node.Kind switch
        {
            ContentKind.Package => "PartitionFileSystem",
            ContentKind.Archive or ContentKind.ArchiveDirectory or ContentKind.ArchiveFile => node.Kind.ToString(),
            ContentKind.Section => "NcaFsHeader",
            ContentKind.Directory or ContentKind.File => "DirectoryEntryEx",
            _ => "FileEntry"
        }) };
        if (node.Metadata?.ContainsKey("Format") != true)
            rows.Add(new("Format", node.Kind is ContentKind.Directory or ContentKind.ArchiveDirectory
                ? "" : Path.GetExtension(node.Name).TrimStart('.')));
        if (node.Kind != ContentKind.Section)
            rows.Add(new("Size", Emignatik.NxFileViewer.Utils.FileSizeHelper.ToFileSize(node.Size)));
        if (node.Metadata != null) rows.AddRange(node.Metadata.Select(pair => new DetailRow(pair.Key, pair.Value)));
        if (node.Kind is ContentKind.Directory or ContentKind.File)
            rows.Add(new("DirectoryEntryType", node.Kind.ToString()));
        var check = GetContentCheck(node);
        if (node.Kind == ContentKind.Nca)
            rows.Add(new("HashValid", check?.Matches?.ToString() ?? "Unchecked"));
        var errors = GetContentErrors(node).Distinct().ToArray();
        if (errors.Length > 0) rows.Add(new(_text.BatchIntegrity_Error, string.Join(Environment.NewLine, errors)));
        EntryProperties.ItemsSource = rows;
    }

    private NcaHashResult? GetContentCheck(ContentNode node) =>
        node.Kind == ContentKind.Nca ? _contentChecks?.FirstOrDefault(check => check.Name == node.Name) : null;

    private bool UpdateContentErrorMarkers(ContentNode node)
    {
        var childHasErrors = false;
        foreach (var child in node.Children) childHasErrors |= UpdateContentErrorMarkers(child);
        node.HasErrors = childHasErrors || GetOwnContentErrors(node).Any();
        return node.HasErrors;
    }

    private IEnumerable<string> GetContentErrors(ContentNode node)
    {
        foreach (var error in GetOwnContentErrors(node)) yield return error;
        foreach (var child in node.Children.Where(child => child.HasErrors))
            foreach (var error in GetContentErrors(child))
                yield return $"{child.Name}: {error}";
    }

    private IEnumerable<string> GetOwnContentErrors(ContentNode node)
    {
        if (!string.IsNullOrWhiteSpace(node.Error)) yield return node.Error;
        if (node.IsRoot && _contentVerificationError != null) yield return _contentVerificationError;

        var check = GetContentCheck(node);
        if (check?.Matches == false) yield return _text.NcaHash_NcaItem_Invalid;
        if (check?.Signature == LibHac.Common.Validity.Invalid ||
            node.Metadata?.GetValueOrDefault("HeaderSignatureValidity") == nameof(LibHac.Common.Validity.Invalid))
            yield return _text.NcaHeaderSignature_Invalid;
        if (!string.IsNullOrWhiteSpace(check?.Error)) yield return check.Error;

        if (node.Kind != ContentKind.Package || _contentChecks == null) yield break;
        foreach (var unmatched in _contentChecks.Where(result => result.Error != null &&
            !node.Children.Any(child => child.Name == result.Name)))
            yield return $"{unmatched.Name}: {unmatched.Error}";
    }

    private void OpenContentMenu(object? sender, ContextRequestedEventArgs e)
    {
        if (_workspaceJobs.Count > 0) return;
        var item = (e.Source as global::Avalonia.Visual)?.FindAncestorOfType<TreeViewItem>(includeSelf: true);
        if (item == null && e.TryGetPosition(Entries, out _)) return;
        if (item?.DataContext is ContentNode clicked) Entries.SelectedItem = clicked;
        var menu = new ContextMenu();
        foreach (var (label, action) in GetEntryActions())
        {
            var command = new MenuItem { Header = label };
            command.Click += async (_, _) => await action();
            menu.Items.Add(command);
        }
        if (menu.Items.Count == 0) return;
        e.Handled = true;
        menu.Open((Control?)item ?? Entries);
    }

    private void RefreshEntryActions()
    {
        if (EntryActions == null) return;
        EntryActions.Children.Clear();
        foreach (var (label, action) in GetEntryActions())
        {
            var button = new Button { Content = label, Margin = new global::Avalonia.Thickness(0, 0, 8, 4) };
            button.Click += async (_, _) => await action();
            EntryActions.Children.Add(button);
        }
        EntryActions.IsVisible = EntryActions.Children.Count > 0;
    }

    private IEnumerable<(string Label, Func<Task> Action)> GetEntryActions()
    {
        if (_workspaceJobs.Count > 0 || _openedPath is not { } path ||
            Entries.SelectedItem is not ContentNode { Kind: not ContentKind.Placeholder } node)
            yield break;

        var errors = GetContentErrors(node).Distinct().ToArray();
        if (errors.Length > 0)
            yield return (_text.ContextMenu_ShowItemErrors, () => ShowContentErrorAsync(node.DisplayName, string.Join(Environment.NewLine, errors)));
        var canReadContent = _keysPath != null && node.Error == null;
        switch (node.Kind)
        {
            case ContentKind.PartitionFile:
                yield return (_text.ContextMenu_SavePartitionFileItem, () => ExportContentAsync(path, node, ExportMode.Raw));
                break;
            case ContentKind.Nca:
                var compressed = Path.GetExtension(node.Name).Equals(".ncz", StringComparison.OrdinalIgnoreCase);
                if (!node.IsRoot || !Nax0Contents.IsNax0(path))
                    yield return (compressed ? _text.ContextMenu_SavePartitionFileItem : _text.ContextMenu_SaveNcaFileRaw,
                        () => ExportContentAsync(path, node, ExportMode.Raw));
                if (compressed)
                    yield return (_text is Emignatik.NxFileViewer.Localization.Keys.LocalizationKeys_DE
                        ? "Als NCA speichern (NCZ entpacken)…" : "Save as NCA (decompress NCZ)…",
                        () => ExportContentAsync(path, node, ExportMode.Reconstructed));
                if (canReadContent && node.Metadata != null)
                    yield return (_text.ContextMenu_SaveNcaFilePlaintext, () => ExportContentAsync(path, node, ExportMode.Plaintext));
                break;
            case ContentKind.Section when canReadContent:
                yield return (_text.ContextMenu_SaveSectionItem, () => ExportContentAsync(path, node, ExportMode.Directory));
                break;
            case ContentKind.Directory when canReadContent:
                yield return (_text.ContextMenu_SaveDirectoryItem, () => ExportContentAsync(path, node, ExportMode.Directory));
                break;
            case ContentKind.File when canReadContent:
                yield return (_text.ContextMenu_SaveFileItem, () => ExportContentAsync(path, node, ExportMode.File));
                break;
        }
    }

    private async Task ShowContentErrorAsync(string entry, string error)
    {
        var text = new TextBox { Text = error, IsReadOnly = true, AcceptsReturn = true,
            TextWrapping = global::Avalonia.Media.TextWrapping.Wrap, Margin = new global::Avalonia.Thickness(12) };
        var dialog = new Window { Title = entry, Width = 600, Height = 230,
            Content = text, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        WpfTheme.Apply(dialog);
        await dialog.ShowDialog(this);
    }

    private static bool IsContentError(Exception error) => error is IOException or InvalidDataException or UnauthorizedAccessException or
        ArgumentException or LibHac.Common.LibHacException or LibHac.NSZ.NczFormatException or ZstdSharp.ZstdException or NotSupportedException;
}

