using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace NxFileViewer.Avalonia.Updates;

internal static partial class AvaloniaAppUpdater
{
    private const string BackupManifest = "backup-pending.json";
    private const string CompletedBackup = "backup-complete.json";
    private static readonly string[] UpdateExecutables =
        ["NxFileViewer", "NxFileViewer.exe", "NxFileViewerFork.Avalonia", "NxFileViewerFork.Avalonia.exe", "NxFileViewerFork", "NxFileViewerFork.exe"];
    private static string[] UpdateNames(string executable) => [executable, ManifestName, LegacyManifestName, "LICENSE.txt", "start.sh", "install-shortcut.sh", "nxfileviewer-package.json"];
    private static string NewStagePath(string root) => Path.Combine(root, "update-" + Guid.NewGuid().ToString("N"));
    private static bool IsStageName(string path) => Regex.IsMatch(Path.GetFileName(path), @"\A(?:update|avalonia)-[a-f0-9]{32}\z");
    private static FileStream OpenUpdateLock(string updates) =>
        new(Path.Combine(updates, "update.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

    internal static string[] CompletedStages(string install)
    {
        var updates = Path.Combine(install, "Updates");
        if (!Directory.Exists(updates)) return [];
        CheckDirectory(updates);
        return Directory.EnumerateDirectories(updates)
            .Where(path => IsStageName(path) &&
                (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0 &&
                (File.Exists(Path.Combine(path, CompletedBackup)) || File.Exists(Path.Combine(path, "ready"))))
            .OrderByDescending(Directory.GetLastWriteTimeUtc).ToArray();
    }

    internal static (string Stage, UpdateBackup Backup, string? LegacyExecutable)? FindRollback(string currentExecutable)
    {
        var install = Path.GetDirectoryName(currentExecutable)!;
        if (!File.Exists(currentExecutable)) return null;
        foreach (var stage in CompletedStages(install))
        {
            try
            {
                var receipt = Path.Combine(stage, CompletedBackup);
                var (backup, legacyExecutable) = ReadBackup(stage, receipt, install);
                if (backup == null || !UpdateExecutables.Contains(backup.Executable) ||
                    backup.InstalledExecutable != Path.GetFileName(currentExecutable) ||
                    !ValidFiles(backup.InstalledFiles, backup.InstalledExecutable, install) ||
                    !ValidFiles(backup.Files, backup.Executable, Path.Combine(stage, "backup"), legacyExecutable))
                    continue;
                return (stage, backup, legacyExecutable);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException) { }
        }
        return null;
    }

    private static (UpdateBackup? Backup, string? LegacyExecutable) ReadBackup(string stage, string receipt, string install)
    {
        if (File.Exists(receipt))
        {
            CheckFile(receipt);
            return (JsonSerializer.Deserialize<UpdateBackup>(File.ReadAllText(receipt)), null);
        }
        // Older successful updates have a job and a backup, but no receipt.
        if (File.Exists(Path.Combine(stage, BackupManifest)) || File.Exists(Path.Combine(stage, "update-error.txt"))) return default;
        var jobPath = Path.Combine(stage, "job.json");
        CheckFile(jobPath);
        var job = JsonSerializer.Deserialize<UpdateJob>(File.ReadAllText(jobPath));
        if (job?.PreviousSha256 == null || !UpdateExecutables.Contains(job.Executable) ||
            !UpdateExecutables.Contains(job.PreviousExecutable)) return default;
        var root = Path.Combine(stage, "backup");
        CheckDirectory(root);
        var saved = Directory.GetFiles(root).Select(path =>
        {
            CheckFile(path);
            return new UpdateFile(Path.GetFileName(path), Hash(path));
        }).ToArray();
        string? legacyExecutable = null;
        if (job.Executable != job.PreviousExecutable && saved.All(file => file.Name != job.PreviousExecutable))
        {
            // Versions before the receipt kept the renamed executable in the installation directory.
            var path = Path.Combine(install, job.PreviousExecutable);
            CheckFile(path);
            if (Hash(path) != job.PreviousSha256) return default;
            legacyExecutable = path;
            saved = [.. saved, new(job.PreviousExecutable, job.PreviousSha256)];
        }
        return saved.Any(file => file.Name == job.PreviousExecutable && file.Sha256 == job.PreviousSha256)
            ? (new(job.PreviousExecutable, job.Executable, saved, job.Files), legacyExecutable) : default;
    }

    private static bool ValidFiles(UpdateFile[] files, string executable, string root, string? legacyExecutable = null)
    {
        CheckDirectory(root);
        return files is { Length: > 0 and <= 5 } && files.Any(file => file.Name == executable) &&
            files.Select(file => file.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() == files.Length &&
            files.All(file =>
            {
                if (Path.GetFileName(file.Name) != file.Name || !UpdateNames(executable).Contains(file.Name)) return false;
                var path = file.Name == executable && legacyExecutable != null
                    ? legacyExecutable : Path.Combine(root, file.Name);
                CheckFile(path);
                return Hash(path) == file.Sha256;
            });
    }

    internal static string PrepareRollback(string currentExecutable)
    {
        var install = Path.GetDirectoryName(currentExecutable)!;
        var (previous, backup, legacyExecutable) = FindRollback(currentExecutable) ?? throw new IOException("No matching update backup is available.");
        var updates = Path.Combine(install, "Updates");
        using var updateLock = OpenUpdateLock(updates);
        var stage = NewStagePath(updates);
        var payload = Path.Combine(stage, "payload");
        Directory.CreateDirectory(payload);
        try
        {
            foreach (var file in backup.Files)
                File.Copy(file.Name == backup.Executable && legacyExecutable != null
                    ? legacyExecutable : Path.Combine(previous, "backup", file.Name), Path.Combine(payload, file.Name));
            if (!ValidFiles(backup.Files, backup.Executable, payload)) throw new InvalidDataException("Update backup changed.");
            var remove = backup.InstalledFiles.Where(file => !backup.Files.Any(saved => saved.Name == file.Name)).ToArray();
            return CreateJob(stage, currentExecutable, backup.Executable, remove, backup.InstalledFiles);
        }
        catch { Directory.Delete(stage, true); throw; }
    }

    internal static void CleanCompletedStages(string install, IReadOnlyCollection<string> selected)
    {
        var updates = Path.Combine(install, "Updates");
        if (!Directory.Exists(updates)) return;
        CheckDirectory(updates);
        using var updateLock = OpenUpdateLock(updates);
        foreach (var stage in CompletedStages(install).Where(selected.Contains))
            Directory.Delete(stage, recursive: true);
    }
}
