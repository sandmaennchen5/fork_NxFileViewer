using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Emignatik.NxFileViewer.Settings;

/// <summary>Build-time defaults; saved user settings are deserialized over these initial values.</summary>
public static class DefaultSettings
{
    private static readonly JsonSerializerOptions Options = new() { Converters = { new JsonStringEnumConverter() } };
    private static readonly Lazy<JsonDocument> Document = new(() => {
        using var stream = typeof(DefaultSettings).Assembly.GetManifestResourceStream("Viewer.DefaultSettings.json")
            ?? throw new InvalidDataException("Default settings resource missing.");
        var document = JsonDocument.Parse(stream);
        if (document.RootElement.GetProperty("SchemaVersion").GetInt32() != 1)
            throw new InvalidDataException("Unsupported default settings schema.");
        return document;
    });

    public static T Get<T>(string section, string name)
    {
        var value = Document.Value.RootElement.GetProperty(section).GetProperty(name).Deserialize<T>(Options);
        if (value is null) throw new InvalidDataException($"Default setting {section}.{name} must not be null.");
        if (typeof(T).IsEnum && !Enum.IsDefined(typeof(T), value))
            throw new InvalidDataException($"Unknown default setting value for {section}.{name}.");
        return value;
    }
}