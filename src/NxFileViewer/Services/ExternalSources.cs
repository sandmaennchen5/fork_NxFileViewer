using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Emignatik.NxFileViewer.Services.Updates;

namespace Emignatik.NxFileViewer.Services;

public sealed record GitHubContentLocation(string Repository, string Ref, string Path)
{
    public string[] Sources { get; init; } = Array.Empty<string>();
    [System.Text.Json.Serialization.JsonIgnore]
    public string[] SourceSpecs => Sources.Length > 0 ? Sources : new[] { "github:" + Repository };
    private string ContentPath(string file) => string.Join("/", (Path.Length > 0 ? Path + "/" + file : file)
        .TrimEnd('/').Split('/').Select(Uri.EscapeDataString));
    public string ApiUrl(string file = "") => "https://api.github.com/repos/" + Repository + "/contents/" + ContentPath(file) + "?ref=" + Uri.EscapeDataString(Ref);
    public string RawUrl(string file) => "https://raw.githubusercontent.com/" + Repository + "/" + Uri.EscapeDataString(Ref) + "/" + ContentPath(file);
}

public sealed record ExternalSourceConfiguration(int SchemaVersion, GitHubContentLocation FirmwareHashes, GitHubContentLocation TitleDb,
    string NszRepository, string NandRepository, string ExplorerRepository,
    string TitlePageUrl, string TitleInfoApiUrl, string NLibApiUrl, string NxContentPageUrl)
{
    public string[] ExplorerSources { get; init; } = Array.Empty<string>();
    [System.Text.Json.Serialization.JsonIgnore]
    public string[] ExplorerSourceSpecs => ExplorerSources.Length > 0 ? ExplorerSources : new[] { "github:" + ExplorerRepository };
}

/// <summary>Build-time defaults embedded from update/external-sources.json, never loaded from the network.</summary>
public static class ExternalSources
{
    private static readonly Lazy<ExternalSourceConfiguration> Embedded = new(() => {
        using var stream = typeof(ExternalSources).Assembly.GetManifestResourceStream("Viewer.ExternalSources.json")
            ?? throw new InvalidDataException("Embedded external source configuration missing.");
        using var bytes = new MemoryStream();
        stream.CopyTo(bytes);
        return Read(bytes.ToArray());
    });
    public static ExternalSourceConfiguration Current => Embedded.Value;
    public static string ReleaseApi(string repository) => "https://api.github.com/repos/" + ViewerUpdateMetadata.Repository(repository) + "/releases/latest";
    public static string ReleaseDownloadPrefix(string repository) => "/" + ViewerUpdateMetadata.Repository(repository) + "/releases/download/";

    public static void ValidateRepositories(string[] sources)
    {
        if (sources == null || sources.Length is < 1 or > 8) throw new InvalidDataException("Expected 1 to 8 repository sources.");
        foreach (var spec in sources)
            if (ViewerUpdateSource.Parse(spec).Kind == ViewerUpdateSourceKind.Manifest) throw new InvalidDataException("Expected GitHub, GitLab or Forgejo repository.");
    }
    public static ExternalSourceConfiguration Read(byte[] bytes)
    {
        if (bytes.Length > 65536) throw new InvalidDataException("External source configuration too large.");
        var config = JsonSerializer.Deserialize<ExternalSourceConfiguration>(bytes) ?? throw new InvalidDataException("External sources missing.");
        if (config.SchemaVersion != 1) throw new InvalidDataException("Unsupported external source schema.");
        foreach (var location in new[] { config.FirmwareHashes, config.TitleDb })
        {
            if (location == null || string.IsNullOrWhiteSpace(location.Ref) || location.Ref.Length > 256 ||
                location.Ref.Any(char.IsControl) || location.Ref.Contains('\\') || location.Path == null || location.Path.Length > 1024 ||
                location.Path.Length > 0 && location.Path.Split('/').Any(part => string.IsNullOrWhiteSpace(part) || part is "." or ".." || part.Any(c => char.IsControl(c) || c is '\\' or '?' or '#')))
                throw new InvalidDataException("Invalid GitHub content location.");
            if (location.Sources == null) throw new InvalidDataException("Firmware sources cannot be null.");
            _ = ViewerUpdateMetadata.Repository(location.Repository);
            ValidateRepositories(location.SourceSpecs);
        }
        foreach (var repository in new[] { config.NszRepository, config.NandRepository, config.ExplorerRepository }) _ = ViewerUpdateMetadata.Repository(repository);
        if (config.ExplorerSources == null) throw new InvalidDataException("Explorer sources cannot be null.");
        ValidateRepositories(config.ExplorerSourceSpecs);
        if (config.TitleDb.Sources.Length != 0) throw new InvalidDataException("TitleDB supports its GitHub repository setting only.");
        foreach (var template in new[] { config.TitlePageUrl, config.TitleInfoApiUrl, config.NLibApiUrl, config.NxContentPageUrl })
        {
            if (template == null || !template.Contains("{TitleId}", StringComparison.Ordinal)) throw new InvalidDataException("URL template requires {TitleId}.");
            var url = template.Replace("{TitleId}", "0100000000000000").Replace("{Language}", "en");
            if (url.Contains('{') || url.Contains('}')) throw new InvalidDataException("Unknown URL placeholder.");
            _ = ViewerUpdateSource.Https(url);
        }
        return config;
    }
}
