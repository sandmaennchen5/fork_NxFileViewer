using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Emignatik.NxFileViewer.Services.Integrity;

namespace Emignatik.NxFileViewer.Services.Updates;

public static class InstalledDataStatus
{
    public static string? HighestFirmwareVersion(IEnumerable<string> manifests)
    {
        Version? highest = null;
        foreach (var manifest in manifests)
        {
            try
            {
                FirmwareComparison.ValidateManifests(new[] { manifest });
                using var json = JsonDocument.Parse(manifest);
                var root = json.RootElement;
                var label = root.TryGetProperty("tag", out var tag) ? tag.GetString() : root.GetProperty("name").GetString();
                var match = Regex.Match(label ?? "", @"\d+\.\d+\.\d+");
                if (match.Success && Version.TryParse(match.Value, out var version) && (highest == null || version > highest)) highest = version;
            }
            catch (Exception ex) when (ex is JsonException or InvalidDataException or InvalidOperationException or KeyNotFoundException or ArgumentException) { }
        }
        return highest?.ToString();
    }

    public static string? LocalFirmwareVersion(string applicationDirectory)
    {
        var directory = ApplicationDataPaths.FirmwareHashes(applicationDirectory);
        try
        {
            if (!Directory.Exists(directory)) return null;
            var manifests = new List<string>();
            foreach (var path in Directory.EnumerateFiles(directory, "*.json"))
            {
                try { manifests.Add(File.ReadAllText(path)); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            return HighestFirmwareVersion(manifests);
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    public static DateTime? TitleDbDate(string applicationDirectory, string region, string? repository = null)
    {
        if (!Regex.IsMatch(region, @"^[A-Z]{2}\.[a-z]{2}\z")) return null;
        var path = TitleDbSources.CatalogPath(ApplicationDataPaths.TitleDb(applicationDirectory), TitleDbSources.Resolve(repository), region);
        try { return File.Exists(path) ? File.GetLastWriteTimeUtc(path) : null; }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }
}
