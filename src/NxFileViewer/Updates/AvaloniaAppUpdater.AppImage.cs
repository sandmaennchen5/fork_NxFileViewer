using System;
using System.IO;
using System.Runtime.InteropServices;

namespace NxFileViewer.Avalonia.Updates;

internal static partial class AvaloniaAppUpdater
{
    internal static bool IsAppImageName(string name) => name.Length > ".AppImage".Length &&
        name == Path.GetFileName(name) && !name.Contains('\\') && !name.Contains(':') &&
        name.EndsWith(".AppImage", StringComparison.OrdinalIgnoreCase);

    internal static string? GetRunningAppImage()
    {
        if (!OperatingSystem.IsLinux()) return null;
        var path = Environment.GetEnvironmentVariable("APPIMAGE");
        return path != null && Path.IsPathFullyQualified(path) && IsAppImageName(Path.GetFileName(path))
            ? Path.GetFullPath(path) : null;
    }

    private static string RuntimeAppImageRid() =>
        "linux-" + RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();

    internal static void ValidateAppImage(string path, string rid)
    {
        ValidateNativeExecutable(path, rid);
        using var input = File.OpenRead(path);
        input.Position = 8;
        if (input.ReadByte() != 'A' || input.ReadByte() != 'I' || input.ReadByte() != 2)
            throw new InvalidDataException("The update is not a Type 2 AppImage.");
    }

    private static void ReplaceAppImage(string source, string destination)
    {
        var staged = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.Copy(source, staged);
            MakeExecutable(staged);
            File.Move(staged, destination, overwrite: true);
        }
        finally { File.Delete(staged); }
    }

    internal static bool IsNativePackageReadyPath(string fullPath, string package)
    {
        var stage = Path.GetDirectoryName(fullPath);
        var updates = Path.Combine(Path.GetDirectoryName(package)!, "Updates");
        if (stage == null || !IsStageName(stage) || Path.GetDirectoryName(stage) != updates ||
            Path.GetFileName(fullPath) != "ready") return false;
        CheckDirectory(stage);
        if (File.Exists(fullPath)) CheckFile(fullPath);
        return true;
    }
}
