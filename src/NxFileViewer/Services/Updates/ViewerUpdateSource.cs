using System;
using System.IO;
using System.Linq;

namespace Emignatik.NxFileViewer.Services.Updates;

public enum ViewerUpdateSourceKind { GitHub, GitLab, Forgejo, Manifest }

public sealed record ViewerUpdateSource(ViewerUpdateSourceKind Kind, string Value, Uri Endpoint, string? Repository = null)
{
    public string Key => Kind == ViewerUpdateSourceKind.GitHub ? "github:" + Repository!.ToLowerInvariant() : Kind + ":" + Endpoint.AbsoluteUri;
    public string DisplayName => Kind == ViewerUpdateSourceKind.GitHub ? Repository! : Kind + ":" + Endpoint.GetLeftPart(UriPartial.Path);

    public static Uri Https(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 4096 || value.Any(char.IsControl) ||
            !Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.UserInfo.Length != 0 || uri.Fragment.Length != 0)
            throw new InvalidDataException("Update addresses must be absolute HTTPS URLs without credentials or fragments.");
        return uri;
    }

    public static ViewerUpdateSource Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 2048 || value.Any(char.IsWhiteSpace)) throw new InvalidDataException("Invalid update source.");
        string payload = value;
        ViewerUpdateSourceKind kind;
        if (value.StartsWith("github:", StringComparison.OrdinalIgnoreCase)) { kind = ViewerUpdateSourceKind.GitHub; payload = value[7..]; }
        else if (value.StartsWith("gitlab:", StringComparison.OrdinalIgnoreCase)) { kind = ViewerUpdateSourceKind.GitLab; payload = value[7..]; }
        else if (value.StartsWith("forgejo:", StringComparison.OrdinalIgnoreCase)) { kind = ViewerUpdateSourceKind.Forgejo; payload = value[8..]; }
        else if (value.StartsWith("manifest:", StringComparison.OrdinalIgnoreCase)) { kind = ViewerUpdateSourceKind.Manifest; payload = value[9..]; }
        else if (value.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) kind = ViewerUpdateSourceKind.Manifest;
        else kind = ViewerUpdateSourceKind.GitHub;
        if (kind == ViewerUpdateSourceKind.GitHub)
        {
            var repository = ViewerUpdateMetadata.Repository(payload);
            return new(kind, value, new Uri("https://api.github.com/repos/" + repository), repository);
        }
        var uri = Https(payload);
        if (kind == ViewerUpdateSourceKind.Manifest) return new(kind, value, uri);
        if (uri.Query.Length != 0) throw new InvalidDataException("Repository source URLs cannot contain query parameters.");
        var repositoryPath = uri.AbsolutePath.TrimEnd('/');
        if (kind == ViewerUpdateSourceKind.GitLab)
        {
            const string marker = "/api/v4/projects/";
            var at = repositoryPath.IndexOf(marker, StringComparison.Ordinal);
            if (at >= 0)
            {
                var id = repositoryPath[(at + marker.Length)..];
                if (id.Length == 0 || id.Contains('/')) throw new InvalidDataException("Expected a GitLab project API endpoint.");
                return new(kind, value, new Uri(uri.GetLeftPart(UriPartial.Authority) + repositoryPath));
            }
            var project = Uri.UnescapeDataString(repositoryPath.TrimStart('/'));
            if (project.EndsWith(".git", StringComparison.OrdinalIgnoreCase)) project = project[..^4];
            var parts = project.Split('/');
            if (parts.Length < 2 || parts.Any(p => string.IsNullOrEmpty(p) || p is "." or ".." || p.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('_' or '-' or '.'))))
                throw new InvalidDataException("Invalid GitLab project path.");
            return new(kind, value, new Uri(uri.GetLeftPart(UriPartial.Authority) + marker + Uri.EscapeDataString(project)));
        }
        const string forgejoMarker = "/api/v1/repos/";
        var markerAt = repositoryPath.IndexOf(forgejoMarker, StringComparison.Ordinal);
        string prefix;
        string forgejoRepository;
        if (markerAt >= 0) { prefix = repositoryPath[..markerAt]; forgejoRepository = Uri.UnescapeDataString(repositoryPath[(markerAt + forgejoMarker.Length)..]); }
        else
        {
            var segments = repositoryPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length < 2) throw new InvalidDataException("Invalid Forgejo repository path.");
            forgejoRepository = Uri.UnescapeDataString(string.Join('/', segments[^2..]));
            prefix = segments.Length > 2 ? "/" + string.Join('/', segments[..^2]) : "";
        }
        if (forgejoRepository.EndsWith(".git", StringComparison.OrdinalIgnoreCase)) forgejoRepository = forgejoRepository[..^4];
        _ = ViewerUpdateMetadata.Repository(forgejoRepository);
        return new(kind, value, new Uri(uri.GetLeftPart(UriPartial.Authority) + prefix + forgejoMarker + forgejoRepository));
    }

    public void ValidateDownload(Uri url)
    {
        _ = Https(url.AbsoluteUri);
        if (Kind == ViewerUpdateSourceKind.GitHub)
        {
            if (url.Host != "github.com" || !url.IsDefaultPort || !url.AbsolutePath.StartsWith("/" + Repository + "/releases/download/", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Unexpected GitHub update download URL.");
        }
        else if (Kind != ViewerUpdateSourceKind.Manifest && (url.Host != Endpoint.Host || url.Port != Endpoint.Port))
            throw new InvalidDataException("Release assets must use the configured forge origin; use an HTTPS manifest for external download hosts.");
    }
}
