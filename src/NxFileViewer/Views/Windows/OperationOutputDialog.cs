using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Emignatik.NxFileViewer.Localization;
using Emignatik.NxFileViewer.Services.Prompting;
using Emignatik.NxFileViewer.Settings;

namespace Emignatik.NxFileViewer.Views.Windows;

public sealed record OutputLocationOption(OutputLocation Value, string Label);
public sealed record OperationOutputSelection(OutputLocation Location, string Directory, string Action, PackageEngine Engine)
{
    public string ForSource(string source) => Resolve(Location, Directory, Action, source);
    public static string Resolve(OutputLocation location, string custom, string action, string source)
    {
        var parent = Path.GetDirectoryName(Path.GetFullPath(source))!;
        return location switch {
            OutputLocation.NextToSource => parent,
            OutputLocation.ActionSubdirectory => Path.Combine(parent, action switch {
                "Compression" => LocalizationManager.Instance.Current.Keys.Output_Compression,
                "Decompression" => LocalizationManager.Instance.Current.Keys.Output_Decompression,
                _ => action }),
            OutputLocation.Custom when !string.IsNullOrWhiteSpace(custom) => Path.GetFullPath(custom),
            _ => throw new ArgumentException("Select an output directory.")
        };
    }
}

public static class OperationOutputDialog
{
    private sealed record EngineOption(PackageEngine Value, string Label);
    public static OutputLocationOption[] Locations => new[] {
        new OutputLocationOption(OutputLocation.NextToSource, LocalizationManager.Instance.Current.Keys.Output_NextToSource),
        new OutputLocationOption(OutputLocation.ActionSubdirectory, LocalizationManager.Instance.Current.Keys.Output_ActionSubdirectory),
        new OutputLocationOption(OutputLocation.Custom, LocalizationManager.Instance.Current.Keys.Output_Custom)
    };
    public static OperationOutputSelection? Select(IAppSettings settings, string action, string firstSource,
        IPromptService prompts, bool packageEngine = false)
    {
        var keys = LocalizationManager.Instance.Current.Keys;
        var initial = action switch {
            "Merge" or "Convert" => settings.MergeOutputLocation,
            "Split" => settings.SplitOutputLocation,
            "Compression" => settings.CompressionOutputLocation,
            _ => settings.DecompressionOutputLocation
        };
        var panel = new StackPanel { Margin = new Thickness(20) };
        var dialog = new Window { Title = keys.Output_Title, Content = panel, Width = 660,
            SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = Application.Current.MainWindow };
        dialog.Style = Application.Current.TryFindResource("WindowStyle") as Style;
        var location = new ComboBox { ItemsSource = Locations, DisplayMemberPath = "Label", SelectedValuePath = "Value", SelectedValue = initial };
        panel.Children.Add(location);
        var row = new DockPanel { Margin = new Thickness(0, 12, 0, 12) };
        var browse = new Button { Content = keys.Output_Browse, Padding = new Thickness(10, 4, 10, 4), Margin = new Thickness(8, 0, 0, 0) };
        DockPanel.SetDock(browse, Dock.Right); row.Children.Add(browse);
        var path = new TextBox { Text = settings.CustomOutputDirectory };
        row.Children.Add(path); panel.Children.Add(row);
        var engine = new ComboBox { ItemsSource = new[] {
            new EngineOption(PackageEngine.Rust, "cxfcxf / nscb_rust"),
            new EngineOption(PackageEngine.Kronos, "Kronos / NSC_BUILDER"),
            new EngineOption(PackageEngine.RustWithKronosBackup, "cxfcxf / nscb_rust (Backup: Kronos)"),
            new EngineOption(PackageEngine.KronosWithRustBackup, "Kronos (Backup: cxfcxf / nscb_rust)")
        }, DisplayMemberPath = "Label", SelectedValuePath = "Value", SelectedValue = settings.PackageEngine };
        if (packageEngine) {
            panel.Children.Add(new TextBlock { Text = keys.Output_Engine, Margin = new Thickness(0, 0, 0, 4) });
            panel.Children.Add(engine);
        }
        var custom = settings.CustomOutputDirectory;
        void UpdatePath()
        {
            var mode = (OutputLocation)location.SelectedValue;
            path.IsReadOnly = mode != OutputLocation.Custom;
            path.Text = mode == OutputLocation.Custom ? custom : OperationOutputSelection.Resolve(mode, "", action, firstSource);
        }
        path.TextChanged += (_, _) => { if (location.SelectedValue is OutputLocation.Custom) custom = path.Text; };
        location.SelectionChanged += (_, _) => UpdatePath();
        browse.Click += (_, _) => { var selected = prompts.PromptSelectDir(keys.Output_Title); if (selected == null) return;
            custom = selected; location.SelectedValue = OutputLocation.Custom; UpdatePath(); };
        UpdatePath();
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        var ok = new Button { Content = "OK", IsDefault = true, MinWidth = 90, Padding = new Thickness(12, 5, 12, 5) };
        var cancel = new Button { Content = keys.Nsz_Cancel, IsCancel = true, MinWidth = 90, Padding = new Thickness(12, 5, 12, 5), Margin = new Thickness(8, 0, 0, 0) };
        OperationOutputSelection? result = null;
        ok.Click += (_, _) => {
            try {
                var mode = (OutputLocation)location.SelectedValue;
                var directory = OperationOutputSelection.Resolve(mode, path.Text, action, firstSource);
                result = new(mode, directory, action, (PackageEngine)engine.SelectedValue);
                dialog.DialogResult = true;
            } catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) {
                ThemedDialog.Notice(ex.Message, keys.Output_Title, MessageBoxImage.Warning);
            }
        };
        buttons.Children.Add(ok); buttons.Children.Add(cancel); panel.Children.Add(buttons);
        App.ServiceProvider.GetRequiredService<Emignatik.NxFileViewer.Styling.Theme.IThemeService>().RegisterWindow(dialog);
        dialog.ShowDialog();
        return result;
    }

    public static void OpenInBatch(IEnumerable<string> paths)
    {
        if (Application.Current.MainWindow is MainWindow main)
            main.OpenExplorerBatch(System.Linq.Enumerable.ToArray(paths), false);
    }
}