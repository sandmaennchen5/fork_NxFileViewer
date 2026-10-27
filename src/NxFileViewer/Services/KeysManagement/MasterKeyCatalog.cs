using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Emignatik.NxFileViewer.Services.KeysManagement;

public sealed class MasterKeyCatalog
{
    private static readonly Lazy<MasterKeyCatalog> Catalog = new(() => {
        using var stream = typeof(MasterKeyCatalog).Assembly.GetManifestResourceStream("Viewer.MasterKeys.json")
            ?? throw new InvalidDataException("Master key catalog resource missing.");
        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    });
    public static MasterKeyCatalog Current => Catalog.Value;
    public IReadOnlyDictionary<string, uint> Checksums { get; }
    public IReadOnlyDictionary<int, string> Firmware { get; }

    private MasterKeyCatalog(Dictionary<string, uint> checksums, Dictionary<int, string> firmware)
    {
        Checksums = new ReadOnlyDictionary<string, uint>(checksums);
        Firmware = new ReadOnlyDictionary<int, string>(firmware);
    }
    public static MasterKeyCatalog Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("SchemaVersion", out var schema) || schema.ValueKind != JsonValueKind.Number || !schema.TryGetInt32(out var version) || version != 1 ||
            !root.TryGetProperty("MasterKeys", out var entries) || entries.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Unsupported master key catalog schema.");
        var checksums = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
        var firmware = new Dictionary<int, string>();
        foreach (var entry in entries.EnumerateObject())
        {
            if (!Regex.IsMatch(entry.Name, "^[0-9a-f]{2}$", RegexOptions.CultureInvariant) ||
                entry.Value.ValueKind != JsonValueKind.Object ||
                !entry.Value.TryGetProperty("Firmware", out var fw) || fw.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(fw.GetString()) ||
                !entry.Value.TryGetProperty("Crc32", out var crc) || crc.ValueKind != JsonValueKind.Number || !crc.TryGetUInt32(out var checksum))
                throw new InvalidDataException("Invalid master key catalog entry: " + entry.Name);
            var revision = Convert.ToInt32(entry.Name, 16);
            if (!firmware.TryAdd(revision, fw.GetString()!) || !checksums.TryAdd("master_key_" + entry.Name, checksum))
                throw new InvalidDataException("Duplicate master key revision: " + entry.Name);
        }
        if (firmware.Count == 0) throw new InvalidDataException("Master key catalog must not be empty.");
        return new(checksums, firmware);
    }
}