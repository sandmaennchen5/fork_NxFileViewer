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

internal sealed record MtpSelection(MtpEntry Entry, string CacheDirectory, bool Direct);
internal sealed class MtpWindow : Window
{
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    internal static Task<MtpSelection?> Choose(Window owner, ILocalizationKeys text)
    {
        var backend = new ShellMtpBackend();
        var window = new MtpWindow { Title = text.Mtp_Title, Width = 650, Height = 620, MinWidth = 450, MinHeight = 400, WindowStartupLocation = WindowStartupLocation.CenterOwner, RequestedThemeVariant = owner.ActualThemeVariant };
        var grid = new Grid { Margin = new Thickness(16), RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto,Auto,*,Auto"), RowSpacing = 8 };
        var entries = new ListBox { DisplayMemberBinding = new global::Avalonia.Data.Binding(nameof(MtpEntry.Name)) };
        var location = new TextBlock { TextWrapping = TextWrapping.Wrap };
        var cache = new TextBox { Text = Path.Combine(AppContext.BaseDirectory, "Temp", "MTP") };
        var direct = new CheckBox { Content = text.Mtp_Direct }; ToolTip.SetTip(direct, text.Mtp_DirectTip);
        var choose = new Button { Content = text.Mtp_Select, IsDefault = true, IsEnabled = false };
        var cancel = new Button { Content = text.CancelAction, IsCancel = true };
        var parents = new Stack<MtpEntry?>(); MtpEntry? current = null; bool busy = false, closed = false;
        MtpEntry? Choice() { var entry = entries.SelectedItem as MtpEntry ?? current; return entry is { IsDevice: false } && (entry.IsDirectory || MtpImportService.Supports(entry.Name)) ? entry : null; }
        async Task Browse(MtpEntry? entry)
        {
            if (busy || closed) return;
            busy = true; choose.IsEnabled = entries.IsEnabled = false; location.Text = text.Mtp_Listing;
            try { var items = await MtpSta.Run(() => backend.List(entry?.Path)); if (closed) return; current = entry; entries.ItemsSource = items; location.Text = items.Count == 0 ? text.Mtp_NoDevice : entry?.Name ?? text.Mtp_Devices; }
            catch (Exception ex) { if (!closed) location.Text = ex.Message; }
            finally { busy = false; if (!closed) { entries.IsEnabled = true; choose.IsEnabled = Choice() != null; } }
        }
        void Add(Control control, int row) { Grid.SetRow(control, row); grid.Children.Add(control); }
        Add(new TextBlock { Text = text.Mtp_Tip, TextWrapping = TextWrapping.Wrap }, 0);
        var navigation = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var devices = new Button { Content = text.Mtp_Devices }; devices.Click += async (_, _) => { if (!busy) { parents.Clear(); await Browse(null); } };
        var up = new Button { Content = text.Nand_ExplorerUp }; up.Click += async (_, _) => { if (!busy && parents.Count > 0) await Browse(parents.Pop()); };
        navigation.Children.Add(devices); navigation.Children.Add(up); Add(navigation, 1); Add(direct, 2);
        Add(new TextBlock { Text = text.Mtp_DirectTip, TextWrapping = TextWrapping.Wrap }, 3);
        var cachePanel = new StackPanel { Spacing = 4 }; cachePanel.Children.Add(new TextBlock { Text = text.Mtp_Cache });
        var cacheRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8 }; cacheRow.Children.Add(cache);
        var browse = new Button { Content = text.BatchIntegrity_Browse }; Grid.SetColumn(browse, 1); cacheRow.Children.Add(browse);
        browse.Click += async (_, _) => { var folders = await window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = text.Mtp_Cache }); if (folders.FirstOrDefault()?.TryGetLocalPath() is { } path) cache.Text = Path.Combine(path, "NxFileViewer-MTP"); };
        cachePanel.Children.Add(cacheRow); Add(cachePanel, 4); direct.IsCheckedChanged += (_, _) => cachePanel.IsEnabled = direct.IsChecked != true;
        Add(location, 5); Add(entries, 6);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8 }; actions.Children.Add(choose); actions.Children.Add(cancel); Add(actions, 7);
        void Accept() { if (!busy && Choice() is { } entry) window.Close(new MtpSelection(entry, cache.Text?.Trim() ?? "", direct.IsChecked == true)); }
        choose.Click += (_, _) => Accept(); cancel.Click += (_, _) => window.Close(null);
        entries.SelectionChanged += (_, _) => choose.IsEnabled = !busy && Choice() != null;
        entries.DoubleTapped += async (_, _) => { if (busy || entries.SelectedItem is not MtpEntry entry) return; if (entry.IsDirectory) { parents.Push(current); await Browse(entry); } else Accept(); };
        window.Content = grid; WpfTheme.Apply(window); window.Closed += (_, _) => closed = true; window.Opened += async (_, _) => await Browse(null);
        return window.ShowDialog<MtpSelection?>(owner);
    }
}
