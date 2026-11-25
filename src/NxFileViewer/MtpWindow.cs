using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Emignatik.NxFileViewer.Localization.Keys;
using Emignatik.NxFileViewer.Services.Mtp;
namespace NxFileViewer.Avalonia;

internal sealed record MtpSelection(MtpEntry Entry, string DisplayPath, string CacheDirectory, bool Direct, bool BufferInMemory, bool SwitchMode);
internal sealed class MtpWindow : Window
{
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    internal static Task<MtpSelection?> Choose(Window owner, ILocalizationKeys text, bool batch)
    {
        var backend = new ShellMtpBackend();
        var window = new MtpWindow { Title = text.Mtp_Title, Width = 650, Height = 620, MinWidth = 450, MinHeight = 400, WindowStartupLocation = WindowStartupLocation.CenterOwner, RequestedThemeVariant = owner.ActualThemeVariant };
        var grid = new Grid { Margin = new Thickness(16), RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto,Auto,Auto,*,Auto"), RowSpacing = 8 };
        var entries = new ListBox { DisplayMemberBinding = new global::Avalonia.Data.Binding(nameof(MtpEntry.Name)) };
        var location = new TextBlock { TextWrapping = TextWrapping.Wrap };
        var cache = new TextBox { Text = Path.Combine(Path.GetTempPath(), "NxFileViewer-MTP") };
        var direct = new CheckBox { Content = text.Mtp_Direct }; ToolTip.SetTip(direct, text.Mtp_DirectTip);
        var memory = new CheckBox { Content = text.Mtp_MemoryBuffer, IsEnabled = false, IsChecked = true };
        var mode = new ComboBox { ItemsSource = new[] { "Nintendo Switch", text.Mtp_General }, SelectedIndex = 0 };
        ToolTip.SetTip(mode, text.Mtp_Tip);
        bool SwitchMode() => mode.SelectedIndex == 0;
        var shortcuts = new ComboBox { PlaceholderText = "Contents / Games", HorizontalAlignment = HorizontalAlignment.Stretch, IsVisible = false,
            DisplayMemberBinding = new global::Avalonia.Data.Binding(nameof(Location.Label)) };
        var choose = new Button { Content = text.Mtp_Select, IsDefault = true, IsEnabled = false };
        var cancel = new Button { Content = text.CancelAction, IsCancel = true };
        var parents = new Stack<MtpEntry?>(); MtpEntry? current = null; bool busy = false, closed = false;
        Location? shortcut = null;
        MtpEntry? Choice()
        {
            var entry = entries.SelectedItem as MtpEntry ?? current;
            if (entry is not { IsDevice: false }) return null;
            return (entry.IsDirectory ? batch || (SwitchMode() && (MtpLocations.IsContents(entry) || entry.Name.EndsWith(".nca", StringComparison.OrdinalIgnoreCase)))
                : MtpImportService.Supports(entry.Name)) ? entry : null;
        }
        string DisplayPath(MtpEntry entry) => "MTP: " + string.Join(" / ", parents.Reverse()
            .Append(current).Where(parent => parent != null)
            .Select(parent => shortcut is { } location && location.Entry == parent ? location.Label : parent!.Name)
            .Concat(entry == current ? Array.Empty<string>() : [entry.Name]));
        async Task Browse(MtpEntry? entry)
        {
            if (busy || closed) return;
            busy = true; choose.IsEnabled = entries.IsEnabled = mode.IsEnabled = false; location.Text = text.Mtp_Listing;
            try
            {
                var items = await MtpSta.Run(() => backend.List(entry?.Path));
                if (closed) return;
                current = entry;
                entries.SelectedItem = null;
                entries.ItemsSource = items;
                location.Text = entry != null ? DisplayPath(entry) : items.Count == 0 ? text.Mtp_NoDevice : text.Mtp_Devices;
                if (SwitchMode() && entry is { Name: "Games" } && parents.TryPeek(out var device) && device is { IsDevice: true })
                    direct.IsChecked = true;
                if (entry == null) shortcuts.IsVisible = false;
                if (SwitchMode() && entry is { IsDevice: true })
                {
                    shortcuts.IsVisible = false;
                    shortcuts.ItemsSource = null;
                    var found = await MtpSta.Run(() => MtpLocations.Discover(backend, items));
                    if (closed) return;
                    shortcuts.ItemsSource = found.Select(source => new Location(entry, source.Entry, source.Label)).ToArray();
                    shortcuts.IsVisible = found.Count > 0;
                }
            }
            catch (Exception ex) { if (!closed) location.Text = ex.Message; }
            finally { busy = false; if (!closed) { entries.IsEnabled = mode.IsEnabled = true; choose.IsEnabled = Choice() != null; } }
        }
        void Add(Control control, int row) { Grid.SetRow(control, row); grid.Children.Add(control); }
        Add(mode, 0);
        Add(shortcuts, 1);
        mode.SelectionChanged += async (_, _) =>
        {
            shortcut = null;
            parents.Clear();
            direct.IsChecked = false;
            await Browse(null);
        };
        shortcuts.SelectionChanged += async (_, _) =>
        {
            if (busy || shortcuts.SelectedItem is not Location selected) return;
            shortcuts.SelectedItem = null;
            shortcut = selected;
            parents.Clear();
            parents.Push(null);
            parents.Push(selected.Device);
            await Browse(selected.Entry);
        };
        var navigation = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var devices = new Button { Content = text.Mtp_Devices }; devices.Click += async (_, _) => { if (!busy) { shortcut = null; parents.Clear(); await Browse(null); } };
        var up = new Button { Content = text.Nand_ExplorerUp }; up.Click += async (_, _) => { if (!busy && parents.Count > 0) await Browse(parents.Pop()); };
        navigation.Children.Add(devices); navigation.Children.Add(up); Add(navigation, 2);
        var readOptions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        readOptions.Children.Add(direct); readOptions.Children.Add(memory); Add(readOptions, 3);
        Add(new TextBlock { Text = text.Mtp_DirectTip, TextWrapping = TextWrapping.Wrap }, 4);
        var cachePanel = new StackPanel { Spacing = 4 }; cachePanel.Children.Add(new TextBlock { Text = text.Mtp_Cache });
        var cacheRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8 }; cacheRow.Children.Add(cache);
        var browse = new Button { Content = text.BatchIntegrity_Browse }; Grid.SetColumn(browse, 1); cacheRow.Children.Add(browse);
        browse.Click += async (_, _) => { var folders = await window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = text.Mtp_Cache }); if (folders.FirstOrDefault()?.TryGetLocalPath() is { } path) cache.Text = Path.Combine(path, "NxFileViewer-MTP"); };
        cachePanel.Children.Add(cacheRow); Add(cachePanel, 5);
        direct.IsCheckedChanged += (_, _) => { memory.IsEnabled = direct.IsChecked == true; cachePanel.IsEnabled = !memory.IsEnabled; };
        Add(location, 6); Add(entries, 7);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8 }; actions.Children.Add(choose); actions.Children.Add(cancel); Add(actions, 8);
        void Accept()
        {
            if (busy || Choice() is not { } entry) return;
            var sdContents = SwitchMode() && (MtpLocations.IsContents(entry) ||
                parents.Append(current).OfType<MtpEntry>().Any(MtpLocations.IsContents));
            var useDirect = direct.IsChecked == true && !sdContents &&
                (entry.IsDirectory ? !entry.Name.EndsWith(".nca", StringComparison.OrdinalIgnoreCase) : MtpDirectSource.Supports(entry.Name));
            window.Close(new MtpSelection(entry, DisplayPath(entry), cache.Text?.Trim() ?? "", useDirect, memory.IsChecked == true, SwitchMode()));
        }
        choose.Click += (_, _) => Accept(); cancel.Click += (_, _) => window.Close(null);
        entries.SelectionChanged += (_, _) => choose.IsEnabled = !busy && Choice() != null;
        entries.DoubleTapped += async (_, _) => { if (busy || entries.SelectedItem is not MtpEntry entry) return; if (entry.IsDirectory) { parents.Push(current); await Browse(entry); } else Accept(); };
        window.Content = grid; WpfTheme.Apply(window); window.Closed += (_, _) => closed = true;
        window.Opened += async (_, _) =>
        {
            await Browse(null);
            if (!closed && SwitchMode() && entries.ItemsSource is IReadOnlyList<MtpEntry> { Count: 1 } connected &&
                connected[0].Name.Equals("Nintendo Switch", StringComparison.OrdinalIgnoreCase))
            {
                parents.Push(null);
                await Browse(connected[0]);
            }
        };
        return window.ShowDialog<MtpSelection?>(owner);
    }

    private sealed record Location(MtpEntry Device, MtpEntry Entry, string Label);
}
