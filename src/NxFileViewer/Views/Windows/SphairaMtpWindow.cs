using System;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Emignatik.NxFileViewer.Localization;
using Emignatik.NxFileViewer.Services.Mtp;

namespace Emignatik.NxFileViewer.Views.Windows;

public sealed class SphairaMtpWindow : Window
{
    private readonly ILogger<SphairaMtpWindow>? _logger;
    private readonly IMtpBackend _backend = new ShellMtpBackend();
    private readonly ListBox _items = new() { DisplayMemberPath = nameof(MtpEntry.Name), Margin = new Thickness(0, 8, 0, 8) };
    private readonly TextBlock _location = new() { TextWrapping = TextWrapping.Wrap };
    private readonly Button _open = new() { Padding = new Thickness(12, 5, 12, 5), IsDefault = true };
    private readonly Stack<MtpEntry?> _parents = new();
    private MtpEntry? _current;
    private bool _busy;
    private readonly TextBox _cache = new() { Text = System.IO.Path.Combine(AppContext.BaseDirectory, "Temp", "MTP"), Margin = new Thickness(0, 4, 8, 8), MinWidth = 350 };
    private readonly CheckBox _direct = new();
    public bool DirectRead => _direct.IsChecked == true;
    public string CacheDirectory => _cache.Text.Trim();
    public MtpEntry? Selection { get; private set; }
    public SphairaMtpWindow(ILogger<SphairaMtpWindow>? logger = null)
    {
        _logger = logger;
        var keys = LocalizationManager.Instance.Current.Keys;
        Title = keys.Mtp_Title; Width = 650; Height = 620; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var panel = new DockPanel { Margin = new Thickness(16) };
        var top = new StackPanel();
        top.Children.Add(new TextBlock { Text = keys.Mtp_Tip, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) });
        var navigation = new WrapPanel();
        var devices = new Button { Content = keys.Mtp_Devices, Padding = new Thickness(10, 4, 10, 4), Margin = new Thickness(0, 0, 8, 8) };
        devices.Click += (_, _) => { if (!_busy) { _parents.Clear(); Browse(null); } };
        var up = new Button { Content = keys.Nand_ExplorerUp, Padding = new Thickness(10, 4, 10, 4), Margin = new Thickness(0, 0, 8, 8) };
        up.Click += (_, _) => { if (!_busy && _parents.Count > 0) Browse(_parents.Pop()); };
        navigation.Children.Add(devices); navigation.Children.Add(up); top.Children.Add(navigation);
        _direct.Content = keys.Mtp_Direct; _direct.ToolTip = keys.Mtp_DirectTip;
        _direct.Margin = new Thickness(0, 0, 0, 8); top.Children.Add(_direct);
        top.Children.Add(new TextBlock { Text = keys.Mtp_DirectTip, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) });
        top.Children.Add(new TextBlock { Text = keys.Mtp_Cache });
        var cachePanel = new DockPanel();
        var cacheBrowse = new Button { Content = keys.BatchIntegrity_Browse, Padding = new Thickness(8, 3, 8, 3), Margin = new Thickness(0, 4, 0, 8) };
        cacheBrowse.Click += (_, _) =>
        {
            var picker = new Microsoft.Win32.OpenFolderDialog { Title = keys.Mtp_Cache };
            if (picker.ShowDialog() == true) _cache.Text = System.IO.Path.Combine(picker.FolderName, "NxFileViewer-MTP");
        };
        DockPanel.SetDock(cacheBrowse, Dock.Right); cachePanel.Children.Add(cacheBrowse); cachePanel.Children.Add(_cache);
        _direct.Checked += (_, _) => cachePanel.IsEnabled = false;
        _direct.Unchecked += (_, _) => cachePanel.IsEnabled = true;
        top.Children.Add(cachePanel); top.Children.Add(_location);
        DockPanel.SetDock(top, Dock.Top); panel.Children.Add(top);
        _open.Content = keys.Mtp_Select;
        _open.Click += (_, _) => { var choice = Choice(); if (choice != null && !_busy) { Selection = choice; DialogResult = true; } };
        var bottom = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = keys.CancelAction, IsCancel = true, Margin = new Thickness(8, 0, 0, 0), Padding = new Thickness(12, 5, 12, 5) };
        bottom.Children.Add(_open); bottom.Children.Add(cancel); DockPanel.SetDock(bottom, Dock.Bottom); panel.Children.Add(bottom);
        _items.SelectionChanged += (_, _) => _open.IsEnabled = !_busy && Choice() != null;
        _items.MouseDoubleClick += (_, _) =>
        {
            if (_busy || _items.SelectedItem is not MtpEntry entry) return;
            if (entry.IsDirectory) { _parents.Push(_current); Browse(entry); }
            else if (Choice() != null) { Selection = entry; DialogResult = true; }
        };
        panel.Children.Add(_items); Content = panel;
        Loaded += (_, _) => Browse(null);
    }
    private MtpEntry? Choice()
    {
        var entry = _items.SelectedItem as MtpEntry ?? _current;
        return entry is { IsDevice: false } && (entry.IsDirectory || MtpImportService.Supports(entry.Name)) ? entry : null;
    }
    private async void Browse(MtpEntry? entry)
    {
        if (_busy) return;
        _busy = true; _open.IsEnabled = false; _items.IsEnabled = false;
        _location.Text = LocalizationManager.Instance.Current.Keys.Mtp_Listing;
        try
        {
            var items = await MtpSta.Run(() => _backend.List(entry?.Path));
            _current = entry; _items.ItemsSource = items;
            _location.Text = items.Count == 0 ? LocalizationManager.Instance.Current.Keys.Mtp_NoDevice
                : entry?.Name ?? LocalizationManager.Instance.Current.Keys.Mtp_Devices;
        }
        catch (Exception ex) { _logger?.LogWarning(ex, "MTP browsing failed."); _location.Text = ex.Message; }
        finally { _busy = false; _items.IsEnabled = true; _open.IsEnabled = Choice() != null; }
    }
}
