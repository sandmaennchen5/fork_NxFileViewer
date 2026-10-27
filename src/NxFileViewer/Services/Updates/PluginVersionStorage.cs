using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Emignatik.NxFileViewer.Services.Updates;

public sealed record PluginInstallation(string Repository, string? Version = null, string? Commit = null,
    string? Sha256 = null, string? SourceAssetName = null, string? SourceDigest = null,
    string? ExecutableDigest = null, string? Python = null);
public sealed record PluginVersionPointers(string Current, string? Previous);

/// <summary>Shared portable pointer and immutable per-installation metadata format.</summary>
public sealed class PluginVersionStorage(string rootDirectory, string pointerPath)
{
    private sealed record Document(int SchemaVersion, string Current, string? Previous);
    private static readonly JsonSerializerOptions Json = new() {
        WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    private string Root => Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootDirectory));
    private string Pointer => Path.GetFullPath(pointerPath);
    private string Inside(string path)
    {
        var full = Path.GetFullPath(path);
        if (!full.StartsWith(Root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Plugin path leaves its managed directory.");
        return full;
    }
    public PluginVersionPointers? Read()
    {
        if (!File.Exists(Pointer)) return null;
        using var json = JsonDocument.Parse(File.ReadAllText(Pointer));
        if (!json.RootElement.TryGetProperty("SchemaVersion", out var schema) || schema.GetInt32() != 1)
            throw new InvalidDataException("Unsupported plugin pointer schema.");
        var current = json.RootElement.GetProperty("Current").GetString();
        if (string.IsNullOrWhiteSpace(current)) throw new InvalidDataException("Missing current plugin installation.");
        var previous = json.RootElement.TryGetProperty("Previous", out var p) ? p.GetString() : null;
        var directory = Path.GetDirectoryName(Pointer)!;
        return new(Inside(Path.GetFullPath(current, directory)),
            previous == null ? null : Inside(Path.GetFullPath(previous, directory)));
    }
    public void Write(string current, string? previous)
    {
        Inside(Pointer);
        var directory = Path.GetDirectoryName(Pointer)!;
        var document = new Document(1, Path.GetRelativePath(directory, Inside(current)),
            previous == null ? null : Path.GetRelativePath(directory, Inside(previous)));
        AtomicWrite(Pointer, document);
    }
    public static PluginInstallation? Metadata(string directory)
    {
        var path = Path.Combine(directory, "installation.json");
        if (!File.Exists(path)) return null;
        if (new FileInfo(path).Length > 65536) throw new InvalidDataException("Plugin metadata is too large.");
        return JsonSerializer.Deserialize<PluginInstallation>(File.ReadAllText(path));
    }
    public PluginInstallation EnsureMetadata(string executable, PluginInstallation installation)
    {
        var directory = Path.GetDirectoryName(Inside(executable))!;
        for (var item = directory; item != null && item.Length >= Root.Length; item = Path.GetDirectoryName(item))
            if (Directory.Exists(item) && (File.GetAttributes(item) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Linked plugin metadata directories are not supported.");
        var existing = Metadata(directory);
        if (existing != null) return existing;
        AtomicWrite(Path.Combine(directory, "installation.json"), installation);
        return installation;
    }
    public void VerifyExecutable(string executable)
    {
        var metadata = Metadata(Path.GetDirectoryName(Inside(executable))!);
        var hash = metadata?.ExecutableDigest ?? metadata?.Sha256;
        if (string.IsNullOrWhiteSpace(hash)) return;
        if (hash.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)) hash = hash[7..];
        using var input = File.OpenRead(executable);
        if (!string.Equals(hash, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(input)), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Plugin executable SHA-256 mismatch.");
    }
    public static void AtomicWrite<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temporary, JsonSerializer.Serialize(value, Json)); File.Move(temporary, path, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}