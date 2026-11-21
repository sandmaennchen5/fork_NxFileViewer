using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using System.IO;
namespace NxFileViewer.Avalonia;
public partial class MainWindow
{
    private sealed record DetailRow(string Name, string Value);
    private Bitmap? _batchIcon;
    private void BatchSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (BatchResults.SelectedItem is not BatchRow row) return;
        BatchDetailPath.Text = row.Path;
        BatchDetails.ItemsSource = new[] { new DetailRow("Typ", row.FileType), new DetailRow("Paketstruktur", row.Structure), new DetailRow("NCA-Signatur", row.Signature), new DetailRow("Integrität", row.Integrity), new DetailRow("Dateigröße", row.Size), new DetailRow("Titel", row.Title), new DetailRow("TitelID", row.TitleId), new DetailRow("Version", row.Version), new DetailRow("Details", row.Summary) };
        BatchDetailIcon.Source = null; _batchIcon?.Dispose(); _batchIcon = null;
        var icon = row.Package?.Titles.FirstOrDefault()?.NacpData?.Titles.FirstOrDefault(title => title?.Icon != null)?.Icon;
        if (icon != null) { using var stream = new MemoryStream(icon); _batchIcon = new Bitmap(stream); BatchDetailIcon.Source = _batchIcon; }
    }
    private void EntryPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(Entries).Properties.IsRightButtonPressed) return;
        if (e.Source is global::Avalonia.Visual visual)
        {
            for (var current = visual; current != null; current = global::Avalonia.VisualTree.VisualExtensions.GetVisualParent(current))
                if (current is TreeViewItem item) { Entries.SelectedItem = item.DataContext; break; }
        }
    }
}
