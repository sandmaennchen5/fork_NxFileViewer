using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace Emignatik.NxFileViewer.Services.OnlineServices;
internal sealed partial class TitleDbCatalog
{
    public static IReadOnlyDictionary<string, OnlineTitleInfo> ReadLocal(string region, string? repository = null)
    {
        if (!Regex.IsMatch(region, "^[A-Z]{2}\\.[a-z]{2}$")) return new Dictionary<string, OnlineTitleInfo>();
        var path = TitleDbSources.CatalogPath(ApplicationDataPaths.TitleDb(AppContext.BaseDirectory), TitleDbSources.Resolve(repository), region);
        try { return File.Exists(path) ? Parse(File.ReadAllText(path)) : new Dictionary<string, OnlineTitleInfo>(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        { return new Dictionary<string, OnlineTitleInfo>(); }
    }

    private static Dictionary<string, OnlineTitleInfo> Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            throw new JsonException("TitleDB must contain an object indexed by NSU ID.");
        var titles = new Dictionary<string, OnlineTitleInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in document.RootElement.EnumerateObject())
        {
            var value = entry.Value;
            if (value.ValueKind != JsonValueKind.Object)
                continue;
            var id = GetString(value, "id");
            var name = GetString(value, "name");
            if (!Regex.IsMatch(id, "^[a-fA-F0-9]{16}$") || string.IsNullOrWhiteSpace(name))
                continue;
            titles.TryAdd(id, new OnlineTitleInfo
            {
                Id = id, Name = name, Publisher = GetString(value, "publisher"),
                Description = GetString(value, "description"), IconUrl = GetString(value, "iconUrl")
            });
        }
        if (titles.Count == 0)
            throw new JsonException("TitleDB contains no usable title entries.");
        return titles;
    }

    private static string GetString(JsonElement value, string property) =>
        value.TryGetProperty(property, out var field) && field.ValueKind == JsonValueKind.String ? field.GetString()! : "";
}
