using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace NxFileViewer.Avalonia;

public partial class MainWindow
{
    internal sealed record SinglePackageOutput(string Path, bool Replace);

    private async Task<SinglePackageOutput?> ChooseSinglePackageOutputAsync(string source, string displaySource, string action, int defaultMode)
    {
        var parent = Path.GetDirectoryName(Path.GetFullPath(displaySource))!;
        var subfolder = action == "Compress" ? _text.Output_Compression : _text.Output_Decompression;
        var custom = _preferences.CustomOutputFolder ?? "";
        var mode = new ComboBox
        {
            ItemsSource = new[] { _text.Output_NextToSource, _text.Output_ActionSubdirectory, _text.Output_Custom },
            SelectedIndex = Math.Clamp(defaultMode, 0, 2),
            HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch
        };
        var path = new TextBox();
        var browse = new Button { Content = _text.Output_Browse };
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8, Children = { path, browse } };
        Grid.SetColumn(browse, 1);
        var start = new Button { Content = "OK", IsDefault = true };
        var cancel = new Button { Content = _text.Nsz_Cancel, IsCancel = true };
        var error = new TextBlock { TextWrapping = global::Avalonia.Media.TextWrapping.Wrap };
        var panel = new StackPanel { Margin = new global::Avalonia.Thickness(16), Spacing = 10,
            Children = { mode, row, error, PackageDialogButtons(start, cancel) } };
        var dialog = PackageDialog(_text.Output_Title, panel);

        void UpdatePath()
        {
            path.IsReadOnly = mode.SelectedIndex != 2;
            path.Text = mode.SelectedIndex switch { 0 => parent, 1 => Path.Combine(parent, subfolder), _ => custom };
        }
        mode.SelectionChanged += (_, _) => UpdatePath();
        path.TextChanged += (_, _) => { if (mode.SelectedIndex == 2) custom = path.Text ?? ""; };
        browse.Click += async (_, _) =>
        {
            var folders = await dialog.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = _text.Output_Title });
            if (folders.Count == 0 || folders[0].TryGetLocalPath() is not { } folder) return;
            custom = folder;
            mode.SelectedIndex = 2;
            UpdatePath();
        };
        start.Click += (_, _) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path.Text)) { error.Text = _text.Nsz_SelectDestination; return; }
                dialog.Close(Path.GetFullPath(path.Text.Trim()));
            }
            catch (Exception ex) when (ex is IOException or ArgumentException or NotSupportedException) { error.Text = ex.Message; }
        };
        cancel.Click += (_, _) => dialog.Close(null);
        UpdatePath();
        var destination = await dialog.ShowDialog<string?>(this);
        if (destination == null || _closed) return null;
        var extension = Path.GetExtension(source).ToLowerInvariant() switch
        {
            ".nsp" => ".nsz", ".nsz" => ".nsp", ".xci" => ".xcz", ".xcz" => ".xci",
            _ => throw new InvalidOperationException("Unsupported package type.")
        };
        var output = Path.Combine(destination, Path.GetFileNameWithoutExtension(source) + extension);
        if (!File.Exists(output) && !Directory.Exists(output)) return new(output, false);

        var replace = new Button { Content = _text.Nsz_Replace, IsVisible = !Directory.Exists(output) };
        var number = new Button { Content = _text.Nsz_Number, IsDefault = true };
        var stop = new Button { Content = _text.Nsz_Cancel, IsCancel = true };
        var conflict = PackageDialog(_text.Output_Title, new StackPanel
        {
            Margin = new global::Avalonia.Thickness(16), Spacing = 12,
            Children =
            {
                new TextBlock { Text = _text.Nsz_OutputExists + Environment.NewLine + output,
                    TextWrapping = global::Avalonia.Media.TextWrapping.Wrap },
                PackageDialogButtons(replace, number, stop)
            }
        });
        replace.Click += (_, _) => conflict.Close(new SinglePackageOutput(output, true));
        number.Click += (_, _) =>
        {
            var index = 1;
            string next;
            do { next = Path.Combine(destination, Path.GetFileNameWithoutExtension(output) + $" ({index++})" + extension); }
            while (File.Exists(next) || Directory.Exists(next));
            conflict.Close(new SinglePackageOutput(next, false));
        };
        stop.Click += (_, _) => conflict.Close(null);
        return await conflict.ShowDialog<SinglePackageOutput?>(this);
    }

    private Window PackageDialog(string title, Control content)
    {
        var dialog = new Window { Title = title, Content = content, Width = 620,
            SizeToContent = SizeToContent.Height, CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        WpfTheme.Apply(dialog);
        return dialog;
    }

    private static StackPanel PackageDialogButtons(params Button[] buttons)
    {
        var panel = new StackPanel { Orientation = global::Avalonia.Layout.Orientation.Horizontal, Spacing = 8,
            HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Right };
        foreach (var button in buttons) panel.Children.Add(button);
        return panel;
    }
}
