using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace NxFileViewer.Avalonia;

internal static class ViewerDataMigration
{
    private const string ReceiptName = ".migration-source.json";
    private static readonly string[] FileNames =
        [ViewerPreferences.FileName, "NxFileViewer.settings.json", "NxFileViewerFork.Avalonia.settings.json", "prod.keys", "title.keys"];
    private static readonly string[] DirectoryNames = ["History", "Logs", "Plugins", "Cache"];

    internal static bool HasData(string source) =>
        FileNames.Any(name => File.Exists(Path.Combine(source, name))) ||
        DirectoryNames.Any(name => Directory.Exists(Path.Combine(source, name)));

    internal static bool WasCopied(string source, string destination)
    {
        try
        {
            var receipt = Path.Combine(destination, ReceiptName);
            return File.Exists(receipt) && ReadSources(receipt).Contains(source, StringComparer.Ordinal);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException) { return false; }
    }

    private static string[] ReadSources(string receipt) =>
        File.Exists(receipt) ? JsonSerializer.Deserialize<string[]>(File.ReadAllText(receipt)) ?? [] : [];

    internal static (int Copied, int Conflicts) Copy(string source, string destination)
    {
        if (Directory.Exists(destination)) return CopyIntoExisting(source, destination);
        var stage = destination + ".migration-" + Guid.NewGuid().ToString("N");
        try
        {
            var result = CopyIntoExisting(source, stage);
            Directory.Move(stage, destination);
            return result;
        }
        finally
        {
            if (Directory.Exists(stage)) Directory.Delete(stage, recursive: true);
        }
    }

    private static (int Copied, int Conflicts) CopyIntoExisting(string source, string destination)
    {
        var copied = 0;
        var conflicts = 0;
        Directory.CreateDirectory(destination);
        EnsureLocal(destination);
        EnsureLocal(source);
        foreach (var name in FileNames)
        {
            var path = Path.Combine(source, name);
            if (File.Exists(path)) CopyFile(path, Path.Combine(destination, name));
        }
        foreach (var name in DirectoryNames)
        {
            var path = Path.Combine(source, name);
            if (Directory.Exists(path)) CopyDirectory(path, Path.Combine(destination, name));
        }
        var receipt = Path.Combine(destination, ReceiptName);
        if (File.Exists(receipt)) EnsureLocal(receipt);
        var sources = ReadSources(receipt).Append(source).Distinct(StringComparer.Ordinal).ToArray();
        var receiptTemporary = receipt + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(receiptTemporary, JsonSerializer.Serialize(sources));
            File.Move(receiptTemporary, receipt, overwrite: true);
        }
        finally { File.Delete(receiptTemporary); }
        return (copied, conflicts);

        void CopyDirectory(string input, string output)
        {
            EnsureLocal(input);
            if (File.Exists(output)) { conflicts++; return; }
            Directory.CreateDirectory(output);
            EnsureLocal(output);
            foreach (var file in Directory.EnumerateFiles(input))
                CopyFile(file, Path.Combine(output, Path.GetFileName(file)));
            foreach (var directory in Directory.EnumerateDirectories(input))
                CopyDirectory(directory, Path.Combine(output, Path.GetFileName(directory)));
        }

        void CopyFile(string input, string output)
        {
            EnsureLocal(input);
            if (File.Exists(output) || Directory.Exists(output)) { conflicts++; return; }
            var temporary = output + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.Copy(input, temporary);
                File.Move(temporary, output, overwrite: false);
                copied++;
            }
            finally { File.Delete(temporary); }
        }
    }

    private static void EnsureLocal(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Data migration does not follow links: " + path);
    }
}
