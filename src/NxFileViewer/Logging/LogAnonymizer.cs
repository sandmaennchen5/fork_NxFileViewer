using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Emignatik.NxFileViewer.Logging;

/// <summary>Creates a shareable copy. The original session log is never modified.</summary>
public sealed class LogAnonymizer
{
    private readonly Dictionary<string, string> _paths = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly string[] _privateNames;

    public LogAnonymizer() : this(new[] { Environment.UserName, Environment.MachineName, Environment.UserDomainName }) { }

    public LogAnonymizer(IEnumerable<string> privateNames)
    {
        _privateNames = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Where(privateNames,
            value => !string.IsNullOrWhiteSpace(value)));
    }

    public string Anonymize(string text)
    {
        text = SessionFileLog.RedactSecrets(text);
        text = Regex.Replace(text, @"file:/{2,3}[^\r\n\t""'<>|;]+", match => PathLabel(match.Value),
            RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"(?:[A-Za-z]:[\\/]|\\\\(?:\?\\)?(?:[A-Za-z]:[\\/])?)[^\r\n\t""'<>|;:]+",
            match => PathLabel(match.Value));
        text = Regex.Replace(text, @"(?<![\w:])//[^\r\n\t""'<>|;:]+", match => PathLabel(match.Value));
        text = Regex.Replace(text, @"(?<![\w:/])/(?!/)[^\r\n\t""'<>|;:]+", match => PathLabel(match.Value));
        // Quotes make filename boundaries explicit, including filenames containing spaces.
        text = Regex.Replace(text, """(?<quote>["'«])(?<file>(?!\[(?:PATH|FILE)_\d+\])[^"'«»\r\n\\/<>|]+\.[a-zA-Z][a-zA-Z0-9]{0,7})["'»]""",
            match => match.Groups["quote"].Value + FileLabel(match.Groups["file"].Value) +
                (match.Groups["quote"].Value == "«" ? "»" : match.Groups["quote"].Value));
        text = Regex.Replace(text,
            @"(?<![\w\]])[^\s\\/:""*?<>|()\[\]{};,=]+(?:[ \t]+[^\s\\/:""*?<>|()\[\]{};,=]+)*?\.(?:nsp|nsz|xci|xcz|nca|ncz|nro|nso|tik|cert|cnmt|pfs0|hfs0|bin|dat|txt|log|json|keys|ini|cfg|xml|csv|db|sqlite|zip|7z|rar|png|jpg|jpeg|webp|gif|dll|exe|cs|pdb)\b",
            match => FileLabel(match.Value), RegexOptions.IgnoreCase);
        foreach (var name in _privateNames)
            text = Regex.Replace(text,
                @"\[(?:PRIVATE|REDACTED|(?:PATH|FILE)_\d+)\]|(?<name>(?<![\p{L}\p{N}_])" +
                Regex.Escape(name) + @"(?![\p{L}\p{N}_]))",
                match => match.Groups["name"].Success ? "[PRIVATE]" : match.Value, RegexOptions.IgnoreCase);
        return text;
    }

    private string PathLabel(string value) => Label(_paths, value, "PATH");
    private string FileLabel(string value) => Label(_files, value, "FILE");

    private static string Label(Dictionary<string, string> labels, string value, string kind)
    {
        var trailing = value.Length - value.TrimEnd().Length;
        var name = value.TrimEnd();
        if (!labels.TryGetValue(name, out var label))
        {
            var extension = Regex.Match(name, @"\.[a-zA-Z0-9]{1,8}$").Value.ToLowerInvariant();
            label = $"[{kind}_{labels.Count + 1}]{extension}";
            labels.Add(name, label);
        }
        return label + value.Substring(value.Length - trailing);
    }
}
