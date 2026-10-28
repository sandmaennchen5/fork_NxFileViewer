using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Emignatik.NxFileViewer.Services.KeysManagement;

public static partial class KeyFileValidator
{
    // The build-time catalog contains fingerprints, never actual master keys.
    private static IReadOnlyDictionary<string, uint> MasterKeyChecksums => MasterKeyCatalog.Current.Checksums;
    public static KeyFileValidationResult ValidateProdKeys(string? filePath)
    {
        if (!IsExistingFile(filePath))
            return MissingFileResult();

        var entries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var invalidLines = new List<int>();
        ParseKeyValueFile(filePath!, entries, invalidLines);

        var missing = MasterKeyChecksums.Keys.Where(key => !entries.ContainsKey(key)).ToArray();
        var invalid = MasterKeyChecksums
            .Where(expected => entries.TryGetValue(expected.Key, out var value) && !MatchesChecksum(value, expected.Value))
            .Select(expected => expected.Key)
            .ToArray();
        var highestValidRevision = MasterKeyChecksums
            .Where(expected => entries.TryGetValue(expected.Key, out var value) && MatchesChecksum(value, expected.Value))
            .Select(expected => Convert.ToInt32(expected.Key[^2..], 16))
            .DefaultIfEmpty(-1)
            .Max();
        var unsupported = entries.Keys
            .Where(key => MasterKeyNameRegex().IsMatch(key) &&
                          (!MasterKeyChecksums.ContainsKey(key) ||
                           MasterKeyFirmwareMap.GetSupportedFirmware(Convert.ToInt32(key[^2..], 16)) == null))
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new KeyFileValidationResult(true, entries.Count, missing, invalid, invalidLines,
            highestValidRevision < 0 ? null : highestValidRevision, unsupported);
    }

    public static KeyFileValidationResult ValidateTitleKeys(string? filePath)
    {
        if (!IsExistingFile(filePath))
            return MissingFileResult();

        var validEntries = 0;
        var invalidLines = new List<int>();
        var lineNumber = 0;
        foreach (var rawLine in File.ReadLines(filePath!))
        {
            lineNumber++;
            var line = StripComment(rawLine).Trim();
            if (line.Length == 0)
                continue;

            var match = KeyValueRegex().Match(line);
            if (!match.Success || match.Groups[1].Value.Length != 32 || match.Groups[2].Value.Length != 32)
                invalidLines.Add(lineNumber);
            else
                validEntries++;
        }

        return new KeyFileValidationResult(true, validEntries, [], [], invalidLines);
    }

    private static void ParseKeyValueFile(string filePath, IDictionary<string, string> entries, ICollection<int> invalidLines)
    {
        var lineNumber = 0;
        foreach (var rawLine in File.ReadLines(filePath))
        {
            lineNumber++;
            var line = StripComment(rawLine).Trim();
            if (line.Length == 0)
                continue;

            var match = KeyValueRegex().Match(line);
            if (!match.Success || match.Groups[2].Value.Length % 2 != 0)
            {
                invalidLines.Add(lineNumber);
                continue;
            }

            entries[match.Groups[1].Value] = match.Groups[2].Value;
        }
    }

    private static string StripComment(string line)
    {
        var commentIndex = line.IndexOf('#');
        return commentIndex < 0 ? line : line[..commentIndex];
    }

    private static bool MatchesChecksum(string hexValue, uint expectedChecksum)
    {
        if (hexValue.Length != 32)
            return false;

        try
        {
            return ComputeCrc32(Convert.FromHexString(hexValue)) == expectedChecksum;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static uint ComputeCrc32(ReadOnlySpan<byte> bytes)
    {
        var crc = uint.MaxValue;
        foreach (var value in bytes)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
                crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0 : 0xedb88320u);
        }
        return ~crc;
    }

    private static bool IsExistingFile(string? filePath)
    {
        try
        {
            return !string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath);
        }
        catch
        {
            return false;
        }
    }

    private static KeyFileValidationResult MissingFileResult() => new(false, 0, [], [], []);

    [GeneratedRegex(@"^\s*([a-zA-Z0-9_]+)\s*=\s*([a-fA-F0-9]+)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex KeyValueRegex();

    [GeneratedRegex(@"^master_key_[0-9a-f]{2}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MasterKeyNameRegex();
}
