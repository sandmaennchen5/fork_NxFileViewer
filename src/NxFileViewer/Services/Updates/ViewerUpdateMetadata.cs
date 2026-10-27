using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;

namespace Emignatik.NxFileViewer.Services.Updates;

public sealed record ViewerMigrationAction(string Type, string From, string To, string IfTargetExists = "KeepExisting");
public sealed record ViewerMigration(string Id, string WhenInstalledVersionBelow, string ApplyFromTargetVersion, ViewerMigrationAction[] Actions);
public sealed record ViewerReleasePackage(string Architecture, bool SelfContained, string AssetName)
{
    public string? Sha256 { get; init; }
    public string? Url { get; init; }
}
public sealed record ViewerReleaseManifest(int SchemaVersion, string ApplicationId, string Version, ViewerReleasePackage[] Packages)
{
    public ViewerReleasePackage[] ExplorerPackages { get; init; } = Array.Empty<ViewerReleasePackage>();
    public bool Draft { get; init; }
    public bool Prerelease { get; init; }
}
public sealed record ViewerUpdateSourceOptions(bool Enabled = true, int TimeoutSeconds = 30);
public sealed record ViewerUpdateState(int SchemaVersion, string ApplicationId, string[] UpdateSources, string[] CompletedMigrations)
{
    public Dictionary<string, ViewerUpdateSourceOptions> UpdateSourceOptions { get; init; } = new();
}

public static class ViewerUpdateMetadata
{
    public const string ReleaseManifestName = "nxfileviewer-update.json";
    public const string StateName = "NxFileViewerFork.update-state.json";
    public const int MaximumManifestSize = 65536;

    public static string Repository(string value)
    {
        if (value == null || value.Length > 201 || !Regex.IsMatch(value, @"^[A-Za-z0-9_-][A-Za-z0-9_.-]*/[A-Za-z0-9_.-]+$") ||
            value.Split('/').Any(p => p is "." or ".." || p.EndsWith('.')))
            throw new InvalidDataException("Invalid GitHub update repository.");
        return value;
    }

    public static string Asset(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 200 || value != ViewerPackageLayout.Relative(value) ||
            value.Contains('/') || value.Contains('\\') || !value.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Invalid update ZIP name.");
        return value;
    }

    public static ViewerUpdateState ReadState(string root)
    {
        var path = Path.Combine(root, StateName);
        if (!File.Exists(path))
        {
            // Older installers copy the package manifest but cannot create the new source state yet.
            var packagePath = Path.Combine(root, ViewerPackageLayout.ManifestName);
            if (File.Exists(packagePath))
                try
                {
                    ViewerPackageLayout.NoLinks(packagePath);
                    var package = ViewerPackageLayout.ReadManifest(File.ReadAllBytes(packagePath));
                    if (package.UpdateSources.Length > 0) return new(1, ViewerPackageLayout.ApplicationId, package.UpdateSources, Array.Empty<string>()) { UpdateSourceOptions = package.UpdateSourceOptions };
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException) { }
            return new(1, ViewerPackageLayout.ApplicationId, new[] { ViewerUpdateService.Repository }, Array.Empty<string>());
        }
        ViewerPackageLayout.NoLinks(path);
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length > MaximumManifestSize) throw new InvalidDataException("Update state is too large.");
        var state = JsonSerializer.Deserialize<ViewerUpdateState>(bytes) ?? throw new InvalidDataException("Update state missing.");
        if (state.SchemaVersion != 1 || state.ApplicationId != ViewerPackageLayout.ApplicationId || state.UpdateSources == null ||
            state.UpdateSources.Length == 0 || state.UpdateSources.Length > 16 || state.CompletedMigrations == null || state.CompletedMigrations.Length > 1024 ||
            state.CompletedMigrations.Any(id => string.IsNullOrWhiteSpace(id) || id.Length > 128))
            throw new InvalidDataException("Invalid local update state.");
        foreach (var source in state.UpdateSources) _ = ViewerUpdateSource.Parse(source);
        ValidateSourceOptions(state.UpdateSources, state.UpdateSourceOptions);
        return state;
    }

    public static void Validate(ViewerPackageManifest manifest)
    {
        if (manifest.Version != null && !ViewerVersion.TryParse(manifest.Version, out _)) throw new InvalidDataException("Invalid package version.");
        if (manifest.UpdateSources == null || manifest.UpdateSources.Length > 8 || manifest.Migrations == null || manifest.Migrations.Length > 128)
            throw new InvalidDataException("Invalid package update metadata.");
        foreach (var source in manifest.UpdateSources) _ = ViewerUpdateSource.Parse(source);
        ValidateSourceOptions(manifest.UpdateSources, manifest.UpdateSourceOptions);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var migration in manifest.Migrations)
        {
            if (migration == null || string.IsNullOrWhiteSpace(migration.Id) || migration.Id.Length > 128 || !ids.Add(migration.Id) ||
                !ViewerVersion.TryParse(migration.WhenInstalledVersionBelow, out _) || !ViewerVersion.TryParse(migration.ApplyFromTargetVersion, out _) ||
                manifest.Version == null || migration.Actions == null || migration.Actions.Length > 128)
                throw new InvalidDataException("Invalid migration identity or version conditions.");
            foreach (var action in migration.Actions)
            {
                if (action == null || action.Type is not ("MoveDirectory" or "MoveFile") || action.IfTargetExists != "KeepExisting")
                    throw new InvalidDataException("Unsupported migration action.");
                var from = ViewerPackageLayout.Relative(action.From);
                var to = ViewerPackageLayout.Relative(action.To);
                if (Reserved(from) || Reserved(to) || from.Equals(to, StringComparison.OrdinalIgnoreCase) ||
                    from.StartsWith(to + "/", StringComparison.OrdinalIgnoreCase) || to.StartsWith(from + "/", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Overlapping or reserved migration path.");
            }
        }
    }

    public static void ValidateSourceOptions(string[] sources, Dictionary<string, ViewerUpdateSourceOptions> options)
    {
        if (options == null || options.Count > 16) throw new InvalidDataException("Invalid update source options.");
        var known = sources.Select(source => ViewerUpdateSource.Parse(source).Key).ToHashSet(StringComparer.Ordinal);
        var configured = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (source, value) in options)
        {
            var key = ViewerUpdateSource.Parse(source).Key;
            if (!known.Contains(key) || !configured.Add(key) || value == null || value.TimeoutSeconds is < 1 or > 300)
                throw new InvalidDataException("Source options require a listed source and a timeout between 1 and 300 seconds.");
        }
    }

    public static ViewerUpdateSourceOptions SourceOptions(ViewerUpdateState state, ViewerUpdateSource source) =>
        state.UpdateSourceOptions.FirstOrDefault(pair => ViewerUpdateSource.Parse(pair.Key).Key == source.Key).Value ?? new();

    private static bool Reserved(string path) => path.Equals(StateName, StringComparison.OrdinalIgnoreCase) ||
        path.Equals(ViewerPackageLayout.ManifestName, StringComparison.OrdinalIgnoreCase);

    public static ViewerReleaseManifest ReadRelease(byte[] bytes, string tag)
    {
        if (bytes.Length > MaximumManifestSize) throw new InvalidDataException("Release manifest is too large.");
        var manifest = JsonSerializer.Deserialize<ViewerReleaseManifest>(bytes) ?? throw new InvalidDataException("Release manifest missing.");
        if (manifest.SchemaVersion != 1 || manifest.ApplicationId != ViewerPackageLayout.ApplicationId ||
            !ViewerVersion.TryParse(manifest.Version, out var version) || !ViewerVersion.TryParse(tag, out var expected) || version.CompareTo(expected) != 0 ||
            manifest.Packages == null || manifest.Packages.Length > 4)
            throw new InvalidDataException("Release manifest identity or version mismatch.");
        var variants = new HashSet<string>(StringComparer.Ordinal);
        foreach (var package in manifest.Packages)
        {
            if (package == null || package.Architecture is not ("x64" or "x86") || !variants.Add(package.Architecture + ":" + package.SelfContained))
                throw new InvalidDataException("Invalid or duplicate release distribution.");
            _ = Asset(package.AssetName);
            if (package.Sha256 != null && (package.Sha256.Length != 64 || !package.Sha256.All(Uri.IsHexDigit))) throw new InvalidDataException("Invalid package SHA-256.");
            if (package.Url != null && (package.Url.Length > 4096 || package.Url.Any(char.IsControl) || package.Url.Contains('\\') ||
                package.Url.StartsWith("//", StringComparison.Ordinal) || !Uri.TryCreate(package.Url, UriKind.RelativeOrAbsolute, out var address)))
                throw new InvalidDataException("Invalid package download address.");
            if (package.Url != null && Uri.TryCreate(package.Url, UriKind.Absolute, out var absolute)) _ = ViewerUpdateSource.Https(absolute.AbsoluteUri);
        }
        if (manifest.ExplorerPackages == null || manifest.ExplorerPackages.Length > 2) throw new InvalidDataException("Invalid Explorer package list.");
        var explorerVariants = new HashSet<string>(StringComparer.Ordinal);
        foreach (var package in manifest.ExplorerPackages)
        {
            if (package == null || package.Architecture is not ("x64" or "arm64") || !explorerVariants.Add(package.Architecture) || package.SelfContained || package.Url != null)
                throw new InvalidDataException("Invalid Explorer distribution.");
            _ = Asset(package.AssetName);
            _ = ViewerUpdateService.ExpectedHash(package.Sha256 == null ? null : "sha256:" + package.Sha256);
        }
        return manifest;
    }

    // Only stages files. The installer commits these changes and its journal can undo all of them.
    public static PreparedViewerUpdate Prepare(PreparedViewerUpdate prepared, string root, string installedVersion, string targetVersion, CancellationToken token)
    {
        var payload = Path.Combine(prepared.Directory, "payload");
        var manifestPath = Path.Combine(payload, ViewerPackageLayout.ManifestName);
        if (!File.Exists(manifestPath)) return prepared;
        var manifest = ViewerPackageLayout.ReadManifest(File.ReadAllBytes(manifestPath));
        if (!ViewerVersion.TryParse(installedVersion, out var installed) || !ViewerVersion.TryParse(targetVersion, out var target))
            throw new InvalidDataException("Installed or target update version unavailable.");
        if (manifest.Version != null && (!ViewerVersion.TryParse(manifest.Version, out var declared) || declared.CompareTo(target) != 0))
            throw new InvalidDataException("Package manifest version does not match release.");
        var isUpgrade = target.CompareTo(installed) > 0;
        var state = ReadState(root);
        var completed = state.CompletedMigrations.ToHashSet(StringComparer.Ordinal);
        var files = prepared.Files.ToList();
        var paths = files.Select(f => f.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var directories = prepared.Directories.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var removeDirectories = prepared.RemoveDirectories.ToHashSet(StringComparer.OrdinalIgnoreCase);
        long copiedSize = 0;
        bool changed = manifest.UpdateSources.Length > 0;
        var migrationTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void StageFile(string from, string to)
        {
            token.ThrowIfCancellationRequested();
            var previous = files.FirstOrDefault(f => f.Path.Equals(from, StringComparison.OrdinalIgnoreCase));
            if (previous?.Remove == true) return;
            bool stagedSource = previous != null && migrationTargets.Contains(from);
            if (previous != null && !stagedSource) throw new InvalidDataException("Migration source conflicts with a packaged file.");
            var source = ViewerPackageLayout.Inside(stagedSource ? payload : root, from);
            var destination = ViewerPackageLayout.Inside(root, to);
            ViewerPackageLayout.NoLinks(source); ViewerPackageLayout.NoLinks(destination);
            if (!File.Exists(source)) return;
            if (migrationTargets.Contains(to)) return;
            if (paths.Contains(to)) throw new InvalidDataException("Migration target conflicts with a package operation.");
            if (File.Exists(destination)) return; // Keep both the target and the conflicting source.
            if (Directory.Exists(destination)) throw new IOException("Directory blocks a migration target.");
            if (Reserved(from) || Reserved(to)) throw new InvalidDataException("Reserved migration path.");
            var staged = ViewerPackageLayout.Inside(payload, to);
            Directory.CreateDirectory(Path.GetDirectoryName(staged)!);
            string hash;
            if (stagedSource)
            {
                hash = previous!.Sha256;
                File.Move(source, staged);
                files.Remove(previous); paths.Remove(from); migrationTargets.Remove(from);
            }
            else
            {
                copiedSize += new FileInfo(source).Length;
                if (copiedSize > ViewerPackageLayout.MaximumSize || files.Count > 9997 || !paths.Add(from)) throw new InvalidDataException("Migration exceeds limits or conflicts with a package operation.");
                File.Copy(source, staged, false);
                using var input = File.OpenRead(staged);
                hash = Convert.ToHexString(SHA256.HashData(input));
                files.Add(new(from, hash, true) { ExpectedPresent = true });
            }
            paths.Add(to); migrationTargets.Add(to);
            files.Add(new(to, hash) { ExpectedAbsent = true });
        }
        void StageDirectory(string from, string to)
        {
            token.ThrowIfCancellationRequested();
            var source = ViewerPackageLayout.Inside(root, from);
            var destination = ViewerPackageLayout.Inside(root, to);
            ViewerPackageLayout.NoLinks(source); ViewerPackageLayout.NoLinks(destination);
            if (File.Exists(source) || File.Exists(destination)) throw new IOException("File blocks a directory migration.");
            var sourceFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var sourceDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            void Collect(string relative)
            {
                token.ThrowIfCancellationRequested();
                var directory = ViewerPackageLayout.Inside(root, relative);
                ViewerPackageLayout.NoLinks(directory);
                sourceDirectories.Add(relative);
                foreach (var file in Directory.EnumerateFiles(directory)) sourceFiles.Add(relative + "/" + Path.GetFileName(file));
                foreach (var child in Directory.EnumerateDirectories(directory)) Collect(relative + "/" + Path.GetFileName(child));
            }
            if (Directory.Exists(source)) Collect(from);
            foreach (var file in migrationTargets.Where(p => p.StartsWith(from + "/", StringComparison.OrdinalIgnoreCase)).ToArray()) sourceFiles.Add(file);
            foreach (var directory in directories.Where(p => p.Equals(from, StringComparison.OrdinalIgnoreCase) || p.StartsWith(from + "/", StringComparison.OrdinalIgnoreCase)).ToArray()) sourceDirectories.Add(directory);
            if (sourceFiles.Count == 0 && sourceDirectories.Count == 0) return;
            sourceDirectories.Add(from);
            foreach (var directory in sourceDirectories)
            {
                var targetDirectory = to + directory[from.Length..];
                var targetPath = ViewerPackageLayout.Inside(root, targetDirectory);
                ViewerPackageLayout.NoLinks(targetPath);
                if (File.Exists(targetPath)) throw new IOException("File blocks a directory migration.");
                directories.Add(targetDirectory); removeDirectories.Add(directory);
            }
            foreach (var file in sourceFiles.OrderBy(p => p, StringComparer.OrdinalIgnoreCase)) StageFile(file, to + file[from.Length..]);
        }
        foreach (var migration in manifest.Migrations)
        {
            ViewerVersion.TryParse(migration.WhenInstalledVersionBelow, out var below);
            ViewerVersion.TryParse(migration.ApplyFromTargetVersion, out var firstTarget);
            if (!isUpgrade || completed.Contains(migration.Id) || installed.CompareTo(below) >= 0 || target.CompareTo(firstTarget) < 0) continue;
            foreach (var action in migration.Actions)
            {
                if (action.Type == "MoveDirectory") StageDirectory(action.From, action.To);
                else
                {
                    if (Directory.Exists(ViewerPackageLayout.Inside(root, action.From))) throw new IOException("Directory blocks a file migration.");
                    StageFile(action.From, action.To);
                }
            }
            completed.Add(migration.Id); changed = true;
        }
        if (changed)
        {
            if (!paths.Add(StateName)) throw new InvalidDataException("Update state must not be supplied by an archive.");
            var sources = (manifest.UpdateSources.Length > 0 ? manifest.UpdateSources : state.UpdateSources)
                .DistinctBy(source => ViewerUpdateSource.Parse(source).Key, StringComparer.Ordinal).ToArray();
            var nextState = new ViewerUpdateState(1, ViewerPackageLayout.ApplicationId, sources, completed.OrderBy(id => id, StringComparer.Ordinal).ToArray())
            {
                UpdateSourceOptions = manifest.UpdateSources.Length > 0 ? manifest.UpdateSourceOptions : state.UpdateSourceOptions
            };
            if (nextState.CompletedMigrations.Length > 1024) throw new InvalidDataException("Too many completed migrations.");
            var bytes = JsonSerializer.SerializeToUtf8Bytes(nextState);
            if (bytes.Length > MaximumManifestSize) throw new InvalidDataException("Update state exceeds size limit.");
            File.WriteAllBytes(Path.Combine(payload, StateName), bytes);
            files.Add(new(StateName, Convert.ToHexString(SHA256.HashData(bytes))));
        }
        return prepared with { Files = files.ToArray(), Directories = directories.ToArray(), RemoveDirectories = removeDirectories.ToArray() };
    }
}
