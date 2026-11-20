using System;
using System.IO;
using System.Text.Json;

namespace NxFileViewer.Avalonia;

internal sealed record ViewerPreferences(string Language = "", string Theme = "", string? ProdKeysPath = null)
{
    private static string FilePath => Path.Combine(AppContext.BaseDirectory, "NxFileViewerFork.Avalonia.settings.json");

    internal static ViewerPreferences Load() =>
        File.Exists(FilePath)
            ? JsonSerializer.Deserialize<ViewerPreferences>(File.ReadAllText(FilePath)) ?? new ViewerPreferences()
            : new ViewerPreferences();

    internal void Save()
    {
        var temporary = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, FilePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
