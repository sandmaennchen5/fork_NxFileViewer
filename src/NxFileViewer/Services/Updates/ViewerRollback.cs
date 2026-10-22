using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;

namespace Emignatik.NxFileViewer.Services.Updates;

public static class ViewerRollback
{
    internal static ViewerUpdateJournal ReadJournal(string stage, string applicationDirectory)
    {
        applicationDirectory = Path.GetFullPath(applicationDirectory);
        if (!Guid.TryParseExact(Path.GetFileName(stage), "N", out _) ||
            !string.Equals(Path.GetDirectoryName(Path.GetFullPath(stage)), Path.Combine(applicationDirectory, "Updates"), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Invalid update stage.");
        ViewerPackageLayout.NoLinks(stage);
        using var plan = JsonDocument.Parse(File.ReadAllText(Path.Combine(stage, "plan.json")));
        var root = plan.RootElement;
        if (root.GetProperty("SchemaVersion").GetInt32() != 2 || root.GetProperty("ApplicationId").GetString() != ViewerPackageLayout.ApplicationId ||
            !string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(root.GetProperty("TargetRoot").GetString()!)), Path.TrimEndingDirectorySeparator(applicationDirectory), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Update snapshot identity mismatch.");
        var target = Path.GetFullPath(root.GetProperty("Target").GetString()!);
        var previous = Path.GetFullPath(root.GetProperty("PreviousTarget").GetString()!);
        foreach (var executable in new[] { target, previous })
            if (!string.Equals(Path.GetDirectoryName(executable), Path.TrimEndingDirectorySeparator(applicationDirectory), StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Snapshot executable escapes application directory.");
        var journal = JsonSerializer.Deserialize<ViewerUpdateJournal>(File.ReadAllText(Path.Combine(stage, "journal.json"))) ?? throw new InvalidDataException("Update snapshot missing.");
        if (journal.SchemaVersion != 1 || journal.ApplicationId != ViewerPackageLayout.ApplicationId ||
            ViewerPackageLayout.ExecutableName(journal.Executable) != Path.GetFileName(target) ||
            ViewerPackageLayout.ExecutableName(journal.PreviousExecutable) != Path.GetFileName(previous) ||
            journal.Entries == null || journal.CreatedDirectories == null || journal.RemovedDirectories == null ||
            journal.Entries.Select(e => e.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count() != journal.Entries.Length)
            throw new InvalidDataException("Invalid package recovery journal.");
        var allowed = root.GetProperty("Files").EnumerateArray().Select(e => ViewerPackageLayout.Relative(e.GetProperty("Path").GetString()!))
            .Append(journal.PreviousExecutable).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in journal.Entries)
        {
            if (!allowed.Contains(ViewerPackageLayout.Relative(entry.Path)) ||
                (entry.Existed && (entry.BeforeHash == null || entry.BeforeHash.Length != 64 || !entry.BeforeHash.All(Uri.IsHexDigit))) ||
                (entry.AfterHash != null && (entry.AfterHash.Length != 64 || !entry.AfterHash.All(Uri.IsHexDigit)))) throw new InvalidDataException("Invalid snapshot file entry.");
            if (entry.Existed)
            {
                var backup = ViewerPackageLayout.Inside(Path.Combine(stage, "backup"), entry.Path);
                ViewerPackageLayout.NoLinks(backup);
                if (!File.Exists(backup)) throw new InvalidDataException("Recovery backup is missing.");
            }
        }
        foreach (var path in journal.CreatedDirectories.Concat(journal.RemovedDirectories)) _ = ViewerPackageLayout.Relative(path);
        if (!journal.Entries.Any(e => e.Existed && e.Path.Equals(journal.PreviousExecutable, StringComparison.OrdinalIgnoreCase))) throw new InvalidDataException("Main executable backup missing.");
        return journal;
    }
    private static IEnumerable<(string Stage, string Main, ViewerUpdateJournal? Journal)> Backups(string applicationDirectory)
    {
        var updates = Path.Combine(applicationDirectory, "Updates");
        if (!Directory.Exists(updates)) yield break;
        var candidates = new List<(string Stage, string Main, ViewerUpdateJournal? Journal)>();
        foreach (var stage in Directory.EnumerateDirectories(updates))
        {
            if (!File.Exists(Path.Combine(stage, "completed")) || File.Exists(Path.Combine(stage, "error.txt"))) continue;
            try
            {
                if (File.Exists(Path.Combine(stage, "journal.json")))
                {
                    var journal = ReadJournal(stage, applicationDirectory);
                    candidates.Add((stage, ViewerPackageLayout.Inside(Path.Combine(stage, "backup"), journal.PreviousExecutable), journal));
                }
            }
            catch (Exception ex) when (ex is IOException or JsonException or ArgumentException or KeyNotFoundException or InvalidOperationException or UnauthorizedAccessException) { }
        }
        foreach (var stage in OldVersionCleanup.ViewerUpdateDirectories(applicationDirectory))
            if (!File.Exists(Path.Combine(stage, "journal.json")) && File.Exists(Path.Combine(stage, "previous.exe"))) candidates.Add((stage, Path.Combine(stage, "previous.exe"), null));
        foreach (var entry in candidates.OrderByDescending(e => File.GetLastWriteTimeUtc(Path.Combine(e.Stage, "completed")))) yield return entry;
    }
    public static string? FindBackup(string applicationDirectory) => Backups(applicationDirectory).Select(e => e.Main).FirstOrDefault();
    public static PreparedViewerUpdate Prepare(string applicationDirectory)
    {
        var backup = Backups(applicationDirectory).FirstOrDefault();
        if (backup.Stage == null) throw new IOException("No previous application version is available.");
        var stage = Path.Combine(applicationDirectory, "Updates", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        var payload = Path.Combine(stage, "payload"); Directory.CreateDirectory(payload);
        try
        {
            if (backup.Journal == null)
            {
                var main = "NxFileViewer.exe";
                var executable = Path.Combine(payload, main); File.Copy(backup.Main, executable);
                var files = new[] { new ViewerPackageFile(main, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(executable)))) };
                return new(stage, executable) { EntryPoint = main, Files = files };
            }
            var journal = backup.Journal;
            var restore = new List<ViewerPackageFile>();
            foreach (var entry in journal.Entries)
            {
                if (entry.Existed)
                {
                    var source = ViewerPackageLayout.Inside(Path.Combine(backup.Stage, "backup"), entry.Path);
                    using var input = File.OpenRead(source);
                    var digest = Convert.ToHexString(SHA256.HashData(input));
                    if (!digest.Equals(entry.BeforeHash, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Recovery backup hash mismatch.");
                    var destination = ViewerPackageLayout.Inside(payload, entry.Path); Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    File.Copy(source, destination); restore.Add(new(entry.Path, digest));
                }
                else if (entry.AfterHash != null) restore.Add(new(entry.Path, entry.AfterHash, true));
            }
            return new(stage, ViewerPackageLayout.Inside(payload, journal.PreviousExecutable)) {
                EntryPoint = journal.PreviousExecutable, Files = restore.ToArray(), Directories = journal.RemovedDirectories,
                RemoveDirectories = journal.CreatedDirectories
            };
        }
        catch { ViewerPackageLayout.DeleteOwnedStage(stage); throw; }
    }
}