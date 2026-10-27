using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Emignatik.NxFileViewer.Services.FileRenaming.Exceptions;
using Emignatik.NxFileViewer.Services.FileRenaming.Models.PatternParts;

namespace Emignatik.NxFileViewer.Services.FileRenaming;

public sealed record NamingPreset(string Id, Dictionary<string, string> Names, string Pattern)
{
    public string Name(string culture) => Names.GetValueOrDefault(culture.Split('-')[0].ToLowerInvariant()) ?? Names["en"];
}
public sealed record NamingPresetSection(string DefaultPreset, NamingPreset[] Presets)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public NamingPreset Default => Presets.Single(p => p.Id == DefaultPreset);
}
public sealed record NamingDefaults(string InvalidFileNameCharsReplacement, bool ReplaceWhiteSpaceChars, string WhiteSpaceCharsReplacement);
public sealed record NamingPresetCatalog(int SchemaVersion, NamingPresetSection Application, NamingPresetSection Patch,
    NamingPresetSection Addon, NamingPresetSection Combined, NamingPresetSection MultiPackage, NamingDefaults Defaults)
{
    public NamingPresetSection Section(PatternType type) => type switch {
        PatternType.Application => Application, PatternType.Patch => Patch, PatternType.Addon => Addon,
        PatternType.Super => Combined, PatternType.Multi => MultiPackage,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };
}
public static class NamingPresets
{
    public const string DefaultMultiPattern = "MultiPaket ({GameCount}G+{UpdateCount}U+{DlcCount}D).{Ext:L}";
    private static readonly Lazy<NamingPresetCatalog> Embedded = new(() => {
        using var stream = typeof(NamingPresets).Assembly.GetManifestResourceStream("Viewer.NamingPresets.json")
            ?? throw new InvalidDataException("Naming preset resource missing.");
        using var bytes = new MemoryStream(); stream.CopyTo(bytes);
        return Read(bytes.ToArray());
    });
    public static NamingPresetCatalog Current => Embedded.Value;
    public static NamingPresetCatalog Read(byte[] bytes)
    {
        if (bytes.Length > 131072) throw new InvalidDataException("Naming preset file exceeds 128 KiB.");
        var catalog = JsonSerializer.Deserialize<NamingPresetCatalog>(bytes) ?? throw new InvalidDataException("Naming presets missing.");
        if (catalog.SchemaVersion != 2) throw new InvalidDataException("Unsupported naming preset schema; expected 2.");
        if (catalog.Defaults == null ||
            catalog.Defaults.InvalidFileNameCharsReplacement == null || catalog.Defaults.WhiteSpaceCharsReplacement == null ||
            catalog.Defaults.InvalidFileNameCharsReplacement.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            catalog.Defaults.WhiteSpaceCharsReplacement.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new InvalidDataException("Naming Defaults require valid replacement strings without invalid Windows filename characters.");
        foreach (var type in Enum.GetValues<PatternType>())
        {
            var section = catalog.Section(type);
            if (section?.Presets == null || section.Presets.Length is < 1 or > 50)
                throw new InvalidDataException($"Invalid naming preset section: {type}.");
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var preset in section.Presets)
            {
                if (preset == null || preset.Id == null || !Regex.IsMatch(preset.Id, @"^[A-Za-z0-9_-]{1,64}$") ||
                    preset.Id.Equals("manual", StringComparison.OrdinalIgnoreCase) || !ids.Add(preset.Id) ||
                    preset.Names == null || !preset.Names.TryGetValue("en", out var name) || string.IsNullOrWhiteSpace(name) ||
                    preset.Names.Any(pair => string.IsNullOrWhiteSpace(pair.Value) || pair.Value.Length > 120))
                    throw new InvalidDataException($"Invalid naming preset identity or label in {type}.");
                try { ValidatePattern(preset.Pattern, type); }
                catch (Exception ex) { throw new InvalidDataException($"Invalid {type} pattern in preset {preset.Id}: {ex.Message}", ex); }
            }
            if (!section.Presets.Any(p => p.Id == section.DefaultPreset))
                throw new InvalidDataException($"Default naming preset does not exist in {type}.");
        }
        return catalog;
    }
    public static void ValidatePattern(string pattern, PatternType type)
    {
        if (pattern == null || pattern.Length > 2048) throw new ArgumentException("Naming patterns may contain at most 2048 characters.");
        var parser = new NamingPatternsParser();
        _ = type switch {
            PatternType.Application => parser.ParseApplicationPattern(pattern),
            PatternType.Patch => parser.ParsePatchPattern(pattern),
            PatternType.Addon => parser.ParseAddonPattern(pattern),
            PatternType.Super => parser.ParseSuperPattern(pattern),
            PatternType.Multi => parser.ParseMultiPattern(pattern),
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
    }
    public static string AvailableVariables(PatternType type) => string.Join(", ",PatternKeywordHelper.GetCorrespondingSerializedValues(type switch {
        PatternType.Application => PatternKeywordHelper.GetAllowedApplicationKeywords(),
        PatternType.Patch => PatternKeywordHelper.GetAllowedPatchKeywords(),
        PatternType.Addon => PatternKeywordHelper.GetAllowedAddonKeywords(),
        PatternType.Super => PatternKeywordHelper.GetAllowedApplicationKeywords(),
        PatternType.Multi => PatternKeywordHelper.GetAllowedMultiKeywords(),
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    }).Select(keyword => "{"+keyword+"}"));
}
