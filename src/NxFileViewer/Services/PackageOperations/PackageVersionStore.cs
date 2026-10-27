using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Security.Cryptography;
using Emignatik.NxFileViewer.Settings;
using Emignatik.NxFileViewer.Services.Updates;

namespace Emignatik.NxFileViewer.Services.PackageOperations;

public enum PackageComponent { Rust, Kronos, Desktop }
public sealed class PackageVersionStore(IAppSettings settings, string? rootDirectory = null)
{
    private sealed record State(string Current, string? Previous);
    public static string VersionLabel(PackageComponent component, string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "";
        try
        {
            var metadata = PluginVersionStorage.Metadata(component == PackageComponent.Kronos ? path : Path.GetDirectoryName(path)!);
            if (!string.IsNullOrWhiteSpace(metadata?.Version)) return metadata.Version;
            if (!string.IsNullOrWhiteSpace(metadata?.Commit))
                return metadata.Commit[..Math.Min(12, metadata.Commit.Length)];
        }
        catch (Exception ex) when (ex is IOException or JsonException or ArgumentException or UnauthorizedAccessException) { }
        return "";
    }

    public string Root => Path.GetFullPath(rootDirectory ?? Path.Combine(AppContext.BaseDirectory, "Plugins", "PackageEngines"));
    private static string Prefix(PackageComponent component) => component + "-";
    private string StatePath(PackageComponent component) => Path.Combine(Root, component.ToString(), "active.json");
    public string Selected(PackageComponent component) => component switch
    {
        PackageComponent.Rust => settings.RustPackageExecutablePath,
        PackageComponent.Kronos => settings.KronosPackageDirectory,
        _ => settings.RustDesktopExecutablePath
    };
    public void Select(PackageComponent component, string path)
    {
        switch (component)
        {
            case PackageComponent.Rust: settings.RustPackageExecutablePath = path; break;
            case PackageComponent.Kronos: settings.KronosPackageDirectory = path; break;
            default: settings.RustDesktopExecutablePath = path; break;
        }
    }
    private static string Executable(PackageComponent component, string path) => component == PackageComponent.Kronos ? Path.Combine(path, "runtime", "python.exe") : path;
    private static string DirectoryOf(PackageComponent component, string path) => component == PackageComponent.Kronos ? path : Path.GetDirectoryName(path)!;
    private string Resolve(string relative)
    {
        var path = Path.GetFullPath(relative, Root);
        if (!path.StartsWith(Root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Invalid package version state.");
        return path;
    }
    private PluginVersionStorage Storage(PackageComponent component) => new(Root, StatePath(component));
    private State? Read(PackageComponent component)
    {
        var storage = Storage(component);
        if (!File.Exists(StatePath(component)))
        {
            var legacy = Path.Combine(Root, component + "-active.json");
            if (!File.Exists(legacy)) return null;
            var old = JsonSerializer.Deserialize<State>(File.ReadAllText(legacy))
                ?? throw new InvalidDataException("Invalid legacy plugin pointer.");
            // Resolve before writing, including inactive previous paths.
            storage.Write(Resolve(old.Current), old.Previous == null ? null : Resolve(old.Previous));
        }
        var pointers = storage.Read()!;
        return new(Path.GetRelativePath(Root, pointers.Current),
            pointers.Previous == null ? null : Path.GetRelativePath(Root, pointers.Previous));
    }
    private void Write(PackageComponent component, State state) =>
        Storage(component).Write(Resolve(state.Current), state.Previous == null ? null : Resolve(state.Previous));
    private bool Valid(PackageComponent component, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path)) return false;
        if (component != PackageComponent.Kronos && !Path.GetFileName(path).Equals(component == PackageComponent.Rust ? "nscb_rust.exe" : "nscb-desktop.exe", StringComparison.OrdinalIgnoreCase)) return false;
        var directory = Path.GetFullPath(DirectoryOf(component, path));
        var name = Path.GetFileName(directory);
        if (!string.Equals(Path.GetDirectoryName(directory), Root, StringComparison.OrdinalIgnoreCase) ||
            !name.StartsWith(Prefix(component), StringComparison.Ordinal) ||
            !Guid.TryParseExact(name[Prefix(component).Length..], "N", out _) || !File.Exists(Executable(component, path))) return false;
        var metadata = Path.Combine(directory, "installation.json");
        if (!File.Exists(metadata) || new FileInfo(metadata).Length > 65536) return false;
        using var json = JsonDocument.Parse(File.ReadAllText(metadata));
        var repository = component switch {
            PackageComponent.Rust => ExternalSources.Current.RustPackageRepository,
            PackageComponent.Kronos => ExternalSources.Current.KronosPackageRepository,
            _ => ExternalSources.Current.RustDesktopRepository };
        return json.RootElement.TryGetProperty("Repository", out var r) && r.GetString() == repository &&
            (component != PackageComponent.Kronos || File.Exists(Path.Combine(path, "ztools", "squirrel.py")));
    }
    public void Synchronize()
    {
        foreach (var component in Enum.GetValues<PackageComponent>())
        {
            var path = Selected(component);
            var saved = Read(component);
            if (string.IsNullOrWhiteSpace(path) && saved != null && Valid(component, Resolve(saved.Current)))
            {
                path = Resolve(saved.Current);
                Select(component, path);
            }
            if (!Valid(component, path)) continue;
            var relative = Path.GetRelativePath(Root, path);
            var state = Read(component);
            if (string.Equals(state?.Current, relative, StringComparison.OrdinalIgnoreCase)) continue;
            var previous = state != null && Valid(component, Resolve(state.Current)) ? state.Current : null;
            Write(component, new(relative, previous));
        }
    }
    public bool CanRollback(PackageComponent component)
    {
        try {
            var state = Read(component);
            return state?.Previous != null && Valid(component, Selected(component)) &&
                string.Equals(Selected(component), Resolve(state.Current), StringComparison.OrdinalIgnoreCase) && Valid(component, Resolve(state.Previous));
        } catch (Exception ex) when (ex is IOException or JsonException or ArgumentException) { return false; }
    }
    public void Rollback(PackageComponent component)
    {
        if (!CanRollback(component)) throw new InvalidOperationException("No previous managed package engine version.");
        var previous = Resolve(Read(component)!.Previous!);
        if (component != PackageComponent.Kronos) Storage(component).VerifyExecutable(previous);
        Select(component, previous);
        Synchronize();
    }
    public System.Collections.Generic.IReadOnlyList<string> OldVersions(PackageComponent component) =>
        OldVersionCleanup.PackageEngineVersions(Root, Prefix(component), Executable(component, Selected(component)))
            .Where(path => Valid(component, component == PackageComponent.Kronos ? path :
                Path.Combine(path, component == PackageComponent.Rust ? "nscb_rust.exe" : "nscb-desktop.exe"))).ToArray();
    public void Remove(PackageComponent component, System.Collections.Generic.IReadOnlyList<string> paths)
    {
        foreach (var path in paths)
        {
            if (!OldVersions(component).Contains(path, StringComparer.OrdinalIgnoreCase)) throw new IOException("Version is no longer safe to remove.");
            OldVersionCleanup.DeletePackageEngineVersion(Root, Prefix(component), path, Executable(component, Selected(component)));
        }
        var state = Read(component);
        if (state?.Previous != null && !Valid(component, Resolve(state.Previous))) Write(component, state with { Previous = null });
    }
}