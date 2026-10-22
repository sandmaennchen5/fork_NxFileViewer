using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;

namespace Emignatik.NxFileViewer.Services.Updates;

public sealed record ViewerPackageManifest(int SchemaVersion, string ApplicationId, string Executable)
{
    public string? Version { get; init; }
    public string[] UpdateSources { get; init; } = Array.Empty<string>();
    public Dictionary<string, ViewerUpdateSourceOptions> UpdateSourceOptions { get; init; } = new();
    public ViewerMigration[] Migrations { get; init; } = Array.Empty<ViewerMigration>();
}
public sealed record ViewerPackageFile(string Path, string Sha256, bool Remove = false)
{
    public bool ExpectedAbsent { get; init; }
    public bool ExpectedPresent { get; init; }
}
public sealed record ViewerUpdateJournalEntry(string Path, bool Existed, string? BeforeHash, string? AfterHash);
public sealed record ViewerUpdateJournal(int SchemaVersion, string ApplicationId, string PreviousExecutable,
    string Executable, ViewerUpdateJournalEntry[] Entries, string[] CreatedDirectories, string[] RemovedDirectories);

public static class ViewerPackageLayout
{
    public const string ApplicationId = "NxFileViewerFork";
    public const string ManifestName = "nxfileviewer-package.json";
    public const long MaximumSize = 512L * 1024 * 1024;
    public static string Relative(string path, bool directory = false)
    {
        var value = path.Replace('\\', '/');
        if (directory) value = value.TrimEnd('/');
        var parts = value.Split('/');
        if (value.Length == 0 || value.Length > 1024 || parts.Length > 32 || parts.Any(p =>
            p.Length == 0 || p is "." or ".." || p.EndsWith('.') || p.EndsWith(' ') ||
            p.Any(c => c < 32 || "<>:\"|?*".Contains(c)) || Regex.IsMatch(p.Split('.')[0], @"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])$", RegexOptions.IgnoreCase)) ||
            parts[0].Equals("Updates", StringComparison.OrdinalIgnoreCase) || parts[0].Equals(".git", StringComparison.OrdinalIgnoreCase) ||
            parts[0].Equals(".codex", StringComparison.OrdinalIgnoreCase) ||
            value.EndsWith(".keys", StringComparison.OrdinalIgnoreCase) || value.EndsWith(".settings.json", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Unsafe or user-data path in update package: " + path);
        return value;
    }
    public static string ExecutableName(string name)
    {
        var path = Relative(name);
        if (path.Contains('/') || !path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || path.Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("NxFileViewerFork.Open.exe", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Invalid main executable name.");
        return path;
    }
    public static string Inside(string root, string relative)
    {
        root = Path.GetFullPath(root);
        var full = Path.GetFullPath(Path.Combine(root, Relative(relative).Replace('/', Path.DirectorySeparatorChar)));
        if (!full.StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Package path escapes its root.");
        return full;
    }
    public static void NoLinks(string path)
    {
        for (var item = Path.GetFullPath(path); !string.IsNullOrEmpty(item); item = Path.GetDirectoryName(item))
            if ((File.Exists(item) || Directory.Exists(item)) && (File.GetAttributes(item) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Update paths must not contain symbolic links or junctions.");
    }
    public static string InstalledExecutable(string root)
    {
        var manifestPath = Path.Combine(root, ManifestName);
        var name = "NxFileViewer.exe";
        if (File.Exists(manifestPath)) name = ReadManifest(File.ReadAllBytes(manifestPath)).Executable;
        return Path.Combine(root, ExecutableName(name));
    }
    public static ViewerPackageManifest ReadManifest(byte[] bytes)
    {
        if (bytes.Length > 65536) throw new InvalidDataException("Package manifest is too large.");
        var manifest = JsonSerializer.Deserialize<ViewerPackageManifest>(bytes) ?? throw new InvalidDataException("Package manifest missing.");
        if (manifest.SchemaVersion != 1 || manifest.ApplicationId != ApplicationId || string.IsNullOrEmpty(manifest.Executable)) throw new InvalidDataException("Not a compatible NxFileViewer Fork package.");
        _ = ExecutableName(manifest.Executable);
        ViewerUpdateMetadata.Validate(manifest);
        return manifest;
    }
    public static PreparedViewerUpdate Extract(string archive, string stage, string assetName, CancellationToken token)
    {
        var payload = Path.Combine(stage, "payload");
        if (Directory.Exists(payload)) throw new IOException("Update payload already exists.");
        NoLinks(stage);
        using var zip = ZipFile.OpenRead(archive);
        if (zip.Entries.Count == 0 || zip.Entries.Count > 10000) throw new InvalidDataException("Invalid package entry count.");
        var legacyPrefix = Path.GetFileNameWithoutExtension(assetName) + "/";
        var names = zip.Entries.Select(e => e.FullName.Replace('\\', '/')).ToArray();
        var stripLegacy = !names.Contains(ManifestName, StringComparer.OrdinalIgnoreCase) &&
            names.All(n => n == legacyPrefix.TrimEnd('/') || n.StartsWith(legacyPrefix, StringComparison.Ordinal));
        var entries = new List<(ZipArchiveEntry Entry, string Name, bool Directory)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long total = 0;
        foreach (var entry in zip.Entries)
        {
            token.ThrowIfCancellationRequested();
            var name = entry.FullName.Replace('\\', '/');
            if (stripLegacy && name.TrimEnd('/') == legacyPrefix.TrimEnd('/')) continue;
            if (stripLegacy) name = name[legacyPrefix.Length..];
            var isDirectory = name.EndsWith('/');
            name = Relative(name, isDirectory);
            if (name.Equals(ViewerUpdateMetadata.StateName, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Local update state cannot be supplied by a package.");
            if (((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000 || !seen.Add(name) ||
                entry.Length < 0 || (total += entry.Length) > MaximumSize || (isDirectory && entry.Length != 0))
                throw new InvalidDataException("Duplicate, linked or oversized package entry.");
            entries.Add((entry, name, isDirectory));
        }
        foreach (var entry in entries)
            if (!entry.Directory && entries.Any(other => other.Name.StartsWith(entry.Name + "/", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("Conflicting file and directory paths in package.");
        var manifestEntries = entries.Where(e => e.Name.Equals(ManifestName, StringComparison.OrdinalIgnoreCase)).ToArray();
        string main;
        if (manifestEntries.Length == 1 && !manifestEntries[0].Directory)
        {
            using var stream = manifestEntries[0].Entry.Open(); using var bytes = new MemoryStream();
            Copy(stream, bytes, manifestEntries[0].Entry.Length, token, 65536);
            main = ReadManifest(bytes.ToArray()).Executable;
        }
        else
        {
            // Compatibility with existing single-EXE releases; new multi-file releases carry an explicit identity.
            if (entries.Count(e => !e.Directory) != 1 || entries.Single(e => !e.Directory).Name != "NxFileViewer.exe")
                throw new InvalidDataException("Multi-file packages require " + ManifestName + ".");
            main = "NxFileViewer.exe";
        }
        if (!entries.Any(e => !e.Directory && e.Name.Equals(main, StringComparison.OrdinalIgnoreCase))) throw new InvalidDataException("Package main executable missing.");
        Directory.CreateDirectory(payload);
        var files = new List<ViewerPackageFile>();
        var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            var destination = Inside(payload, entry.Name);
            if (entry.Directory) { Directory.CreateDirectory(destination); directories.Add(entry.Name); continue; }
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            using (var input = entry.Entry.Open()) using (var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write))
                Copy(input, output, entry.Entry.Length, token, MaximumSize);
            using var file = File.OpenRead(destination);
            files.Add(new(entry.Name, Convert.ToHexString(SHA256.HashData(file))));
        }
        return new PreparedViewerUpdate(stage, Inside(payload, main)) { EntryPoint = main, Files = files.ToArray(), Directories = directories.ToArray() };
    }
    private static void Copy(Stream input, Stream output, long expected, CancellationToken token, long limit)
    {
        if (expected > limit) throw new InvalidDataException("Package entry exceeds size limit.");
        var buffer = new byte[81920]; long total = 0; int read;
        while ((read = input.Read(buffer)) > 0)
        {
            token.ThrowIfCancellationRequested();
            if ((total += read) > expected || total > limit) throw new InvalidDataException("Package entry exceeds declared size.");
            output.Write(buffer, 0, read);
        }
        if (total != expected) throw new InvalidDataException("Incomplete package entry.");
    }
    public static void DeleteOwnedStage(string stage)
    {
        if (!Guid.TryParseExact(Path.GetFileName(stage), "N", out _) || Path.GetFileName(Path.GetDirectoryName(stage)) != "Updates")
            throw new IOException("Not an owned update stage.");
        NoLinks(stage);
        if (Directory.Exists(stage)) Directory.Delete(stage, true);
    }
}
