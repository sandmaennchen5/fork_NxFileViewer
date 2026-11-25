using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
namespace NxFileViewer.LogViewer;

public enum EntryLevel { Unknown = -1, Trace, Debug, Information, Warning, Error, Critical }

public sealed record LogEntry(string Timestamp, EntryLevel Level, string Message, string RawText,
    string? FilePath, int SectionId, bool IsFileMarker)
{
    public string LevelLabel => Level switch
    {
        EntryLevel.Information => "INFO", EntryLevel.Warning => "WARN",
        EntryLevel.Unknown => "TEXT", _ => Level.ToString().ToUpperInvariant()
    };
    public bool IsMemory => Message.Contains("workingSetBytes=", StringComparison.OrdinalIgnoreCase)
        || Message.Contains("managedBytes=", StringComparison.OrdinalIgnoreCase)
        || Message.Contains("manual-before-gc", StringComparison.OrdinalIgnoreCase)
        || Message.Contains("manual-after-gc", StringComparison.OrdinalIgnoreCase)
        || (Message.Contains("RAM ", StringComparison.OrdinalIgnoreCase) && Message.Contains("MiB", StringComparison.OrdinalIgnoreCase));
}
public sealed record LogDisplayEntry(LogEntry Entry, bool ShowHeader)
{
    public string Header => Entry.FilePath ?? UiText.General;
}

/// <summary>Reads existing logs only. Multiline exceptions stay attached to their original event.</summary>
public static class LogDocument
{
    private static readonly Regex Header = new(@"^\[(?<time>\d{4}-\d{2}-\d{2} [^\]]+)\]\s+\[(?<level>[A-Za-z]+)\]\s?(?<message>.*)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex CompactHeader = new(@"^(?<time>(?:[01]\d|2[0-3]):[0-5]\d:[0-5]\d)  \[(?<level>[A-Za-z]+)\]  (?<message>.*)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex LegacyFilePath = new(@"^(?:[A-Za-z]:[\\/]|\\\\|/|MTP: ).+\.[A-Za-z0-9]{2,5}(?: :: .+)?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    public static IReadOnlyList<LogEntry> ReadFile(string path, CancellationToken token = default)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return Read(reader, token);
    }
    public static IReadOnlyList<LogEntry> Read(TextReader reader, CancellationToken token = default)
    {
        var entries = new List<LogEntry>();
        var message = new StringBuilder();
        var raw = new StringBuilder();
        string time = "";
        var level = EntryLevel.Unknown;
        string? filePath = null;
        var section = 0;
        var hasEntry = false;
        var marker = false;
        void Finish()
        {
            if (hasEntry) entries.Add(new(time, level, message.ToString(), raw.ToString(), filePath, section, marker));
        }
        while (reader.ReadLine() is { } line)
        {
            token.ThrowIfCancellationRequested();
            var text = line.TrimStart('\uFEFF');
            var match = CompactHeader.Match(text);
            var compact = match.Success;
            if (!compact) match = Header.Match(text);
            var isHeader = compact || (match.Success && DateTimeOffset.TryParse(match.Groups["time"].Value,
                CultureInfo.InvariantCulture, DateTimeStyles.None, out _));
            if (isHeader)
            {
                Finish();
                time = match.Groups["time"].Value;
                level = ParseLevel(match.Groups["level"].Value);
                var firstMessage = match.Groups["message"].Value;
                marker = TryGetFileMarker(firstMessage, out var openedPath, allowEmpty: compact);
                if (compact && !marker && LegacyFilePath.IsMatch(firstMessage))
                { marker = true; openedPath = firstMessage; }
                if (compact && !marker && filePath != null && firstMessage.Length == 0) marker = true;
                if (marker) { filePath = openedPath; section++; }
                else if (firstMessage.Contains("NxFileViewer Fork session started.", StringComparison.Ordinal))
                { filePath = null; section++; }
                message.Clear().Append(firstMessage);
                raw.Clear().Append(line);
                hasEntry = true;
            }
            else if (hasEntry)
            {
                message.AppendLine().Append(line);
                raw.AppendLine().Append(line);
            }
            else
            {
                message.Append(line);
                raw.Append(line);
                hasEntry = true;
            }
        }
        token.ThrowIfCancellationRequested();
        Finish();
        return entries;
    }
    public static IReadOnlyList<LogDisplayEntry> Filter(IReadOnlyList<LogEntry> entries, string? search,
        EntryLevel minimum = EntryLevel.Unknown, bool memoryOnly = false, CancellationToken token = default)
    {
        var result = new List<LogDisplayEntry>();
        int? previousSection = null;
        var term = search?.Trim() ?? "";
        foreach (var entry in entries)
        {
            token.ThrowIfCancellationRequested();
            if (entry.Level < minimum || (memoryOnly && !entry.IsMemory)) continue;
            if (term.Length > 0 && !entry.RawText.Contains(term, StringComparison.OrdinalIgnoreCase)
                && !(entry.FilePath?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)) continue;
            result.Add(new(entry, previousSection != entry.SectionId));
            previousSection = entry.SectionId;
        }
        return result;
    }
    private static EntryLevel ParseLevel(string text) => text.ToUpperInvariant() switch
    {
        "TRACE" => EntryLevel.Trace, "DEBUG" => EntryLevel.Debug,
        "INFORMATION" or "INFO" => EntryLevel.Information, "WARNING" or "WARN" => EntryLevel.Warning,
        "ERROR" => EntryLevel.Error, "CRITICAL" or "FATAL" => EntryLevel.Critical, _ => EntryLevel.Unknown
    };
    private static bool TryGetFileMarker(string message, out string? path, bool allowEmpty)
    {
        path = null;
        var start = message.IndexOf("=====>", StringComparison.Ordinal);
        var end = message.LastIndexOf("<=====", StringComparison.Ordinal);
        if (start < 0 || end <= start || !string.IsNullOrWhiteSpace(message[(end + 6)..])) return false;
        var value = message[(start + 6)..end].Trim();
        path = value.Length > 0 ? value : null;
        return path != null || allowEmpty;
    }
}
