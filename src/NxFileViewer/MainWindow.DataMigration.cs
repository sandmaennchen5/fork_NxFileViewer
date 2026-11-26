using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Interactivity;

namespace NxFileViewer.Avalonia;

public partial class MainWindow
{
    private bool _dataMigrationCompleted;

    private void RefreshDataMigration()
    {
        var source = _dataMigrationCompleted ? null : ViewerDataDirectory.MigrationSource();
        MigrateDataButton.Content = MigrationText("Programmdaten umziehen…", "Migrate application data…");
        MigrateDataButton.IsVisible = source != null;
    }

    private async void MigrateData(object? sender, RoutedEventArgs e)
    {
        if (_workspaceJobs.Count != 0 || _dataMigrationCompleted || ViewerDataDirectory.MigrationSource() is not { } source)
            return;
        var destination = ViewerDataDirectory.NewRoot;
        using var job = LockWorkspace(SettingsTab);
        if (!await ConfirmPluginCleanupAsync(
            MigrationText("Programmdaten umziehen", "Migrate application data"),
            MigrationText("Daten in den neuen Ordner kopieren? Vorhandene Dateien bleiben erhalten.",
                "Copy data to the new folder? Existing files will be kept.") +
            Environment.NewLine + source + Environment.NewLine + destination)) return;
        try
        {
            var result = await Task.Run(() => ViewerDataMigration.Copy(source, destination));
            if (_closed) return;
            _dataMigrationCompleted = true;
            RefreshDataMigration();
            UpdateStatus.Text = MigrationText(
                $"Kopiert: {result.Copied}. Bereits vorhanden: {result.Conflicts}. Bitte NxFileViewer neu starten.",
                $"Copied: {result.Copied}. Already present: {result.Conflicts}. Restart NxFileViewer.");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            if (!_closed) UpdateStatus.Text = error.Message;
        }
    }

    private string MigrationText(string german, string english) =>
        _text.CultureName.StartsWith("de", StringComparison.OrdinalIgnoreCase) ? german : english;
}
