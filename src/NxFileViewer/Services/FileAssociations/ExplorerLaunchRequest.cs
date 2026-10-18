using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Emignatik.NxFileViewer.Services.FileAssociations;

public enum ExplorerLaunchMode { Automatic, FileOpen, FileVerify, BatchOpen, BatchVerify }

public sealed record ExplorerLaunchRequest(ExplorerLaunchMode Mode, string[] Paths)
{
    public const int MaximumBytes = 8 * 1024 * 1024;
    public static string RequestDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NxFileViewerFork", "ExplorerRequests");

    public static ExplorerLaunchRequest Parse(IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0) return new(ExplorerLaunchMode.Automatic, Array.Empty<string>());
        if (arguments[0] == "--explorer-request")
        {
            if (arguments.Count != 2) throw new ArgumentException("Expected one Explorer selection file.");
            return ReadSelection(arguments[1]);
        }
        var mode = arguments[0] switch {
            "--file-open" => ExplorerLaunchMode.FileOpen,
            "--file-verify" => ExplorerLaunchMode.FileVerify,
            "--batch-open" => ExplorerLaunchMode.BatchOpen,
            "--batch-verify" => ExplorerLaunchMode.BatchVerify,
            _ => ExplorerLaunchMode.Automatic
        };
        var skip = mode == ExplorerLaunchMode.Automatic ? 0 : 1;
        if (skip < arguments.Count && arguments[skip] == "--") skip++;
        return Create(mode, arguments.Skip(skip));
    }

    public static ExplorerLaunchRequest Create(ExplorerLaunchMode mode, IEnumerable<string> paths)
    {
        if (!Enum.IsDefined(mode)) throw new ArgumentException("Unknown Explorer action.");
        var normalized = paths.Select(p => {
            if (string.IsNullOrWhiteSpace(p) || p.IndexOf('\0') >= 0) throw new ArgumentException("Invalid file path.");
            return Path.GetFullPath(p);
        }).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (mode != ExplorerLaunchMode.Automatic && normalized.Length == 0)
            throw new ArgumentException("Select at least one file or folder.");
        if (mode is ExplorerLaunchMode.FileOpen or ExplorerLaunchMode.FileVerify &&
            (normalized.Length != 1 || Directory.Exists(normalized[0])))
            throw new ArgumentException("The File action requires exactly one file.");
        return new(mode, normalized);
    }

    private static ExplorerLaunchRequest ReadSelection(string selectionFile)
    {
        var fullPath = Path.GetFullPath(selectionFile);
        if (!string.Equals(Path.GetDirectoryName(fullPath), RequestDirectory, StringComparison.OrdinalIgnoreCase) ||
            !Guid.TryParseExact(Path.GetFileNameWithoutExtension(fullPath), "N", out _) ||
            Path.GetExtension(fullPath) != ".selection")
            throw new ArgumentException("Invalid Explorer selection file location.");
        // Only delete our own selection files, never an arbitrary command-line path.
        try
        {
            using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.None);
            if (stream.Length > MaximumBytes || stream.Length % 2 != 0) throw new InvalidDataException("Invalid Explorer selection size.");
            using var reader = new StreamReader(stream, Encoding.Unicode, false);
            var fields = reader.ReadToEnd().Split('\0');
            if (fields.Length < 4 || fields[0] != "NXFV1" || fields[^1] != "")
                throw new InvalidDataException("Invalid Explorer selection format.");
            var mode = fields[1] switch {
                "file-open" => ExplorerLaunchMode.FileOpen,
                "file-verify" => ExplorerLaunchMode.FileVerify,
                "batch-open" => ExplorerLaunchMode.BatchOpen,
                "batch-verify" => ExplorerLaunchMode.BatchVerify,
                _ => throw new InvalidDataException("Unknown Explorer action.")
            };
            return Create(mode, fields.Skip(2).Take(fields.Length - 3));
        }
        finally { File.Delete(fullPath); }
    }
}
