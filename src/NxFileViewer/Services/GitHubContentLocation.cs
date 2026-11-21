using System;
using System.Linq;

namespace Emignatik.NxFileViewer.Services;

public sealed record GitHubContentLocation(string Repository, string Ref, string Path)
{
    public string[] Sources { get; init; } = Array.Empty<string>();
    [System.Text.Json.Serialization.JsonIgnore]
    public string[] SourceSpecs => Sources.Length > 0 ? Sources : new[] { "github:" + Repository };
    private string ContentPath(string file) => string.Join("/", (Path.Length > 0 ? Path + "/" + file : file)
        .TrimEnd('/').Split('/').Select(Uri.EscapeDataString));
    public string ApiUrl(string file = "") => "https://api.github.com/repos/" + Repository + "/contents/" + ContentPath(file) +
        (Ref == "HEAD" ? "" : "?ref=" + Uri.EscapeDataString(Ref));
    public string RawUrl(string file) => "https://raw.githubusercontent.com/" + Repository + "/" + Uri.EscapeDataString(Ref) + "/" + ContentPath(file);
}
