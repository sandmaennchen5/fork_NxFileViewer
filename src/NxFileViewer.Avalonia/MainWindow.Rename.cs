using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using LibHac.Ncm;

namespace NxFileViewer.Avalonia;

public partial class MainWindow
{
    private string? _renameSource;
    private int _renameVersion;
    private sealed record RenamePresetChoiceRow(string Name, string Pattern)
    {
        public override string ToString() => Name;
    }

    private void LoadRenamePresets()
    {
        using var resource = typeof(MainWindow).Assembly.GetManifestResourceStream("Viewer.NamingPresets.json")
            ?? throw new InvalidDataException("Naming presets missing.");
        using var catalog = JsonDocument.Parse(resource);
        var language = _text.CultureName.Split('-')[0];
        var choices = new List<RenamePresetChoiceRow>();
        foreach (var section in new[] { "Application", "Patch", "Addon", "Combined", "MultiPackage" })
        {
            var label = section switch
            {
                "Application" => _text.RenamingTool_ApplicationPattern,
                "Patch" => _text.RenamingTool_PatchPattern,
                "Addon" => _text.RenamingTool_AddonPattern,
                "Combined" => _text.RenamingTool_SuperPattern,
                _ => _text.RenamingTool_MultiPattern
            };
            foreach (var preset in catalog.RootElement.GetProperty(section).GetProperty("Presets").EnumerateArray())
            {
                var pattern = preset.GetProperty("Pattern").GetString()!;
                if (pattern.Contains('/') || pattern.Contains('\\') || pattern.Contains("{W"))
                    continue;
                var names = preset.GetProperty("Names");
                var name = names.TryGetProperty(language, out var localized) ? localized.GetString() : names.GetProperty("en").GetString();
                choices.Add(new RenamePresetChoiceRow(label + " · " + name, pattern));
            }
        }
        RenamePresetChoice.ItemsSource = choices;
        RenamePresetChoice.SelectedIndex = choices.Count > 0 ? 0 : -1;
    }

    private void RenamePresetSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (RenamePresetChoice.SelectedItem is not RenamePresetChoiceRow preset) return;
        ++_renameVersion;
        RenamePattern.Text = preset.Pattern;
    }

    private async void SuggestRename(object? sender, RoutedEventArgs e)
    {
        if (_renameSource is not { } source) return;
        var version = ++_renameVersion;
        var keysPath = _keysPath;
        var pattern = RenamePattern.Text;
        try
        {
            var suggestion = await Task.Run(() => CreateOfflineName(source, keysPath, pattern));
            if (_closed || version != _renameVersion || source != _renameSource) return;
            RenameTargetName.Text = suggestion;
            RenameStatus.Text = _text.RenamingTool_StatusSimulation;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or
            ArgumentException or LibHac.Common.LibHacException or LibHac.NSZ.NczFormatException or ZstdSharp.ZstdException or OverflowException)
        {
            if (!_closed && version == _renameVersion)
                RenameStatus.Text = ex.Message;
        }
    }

    private string CreateOfflineName(string source, string? keysPath, string? customPattern)
    {
        var package = PackageFile.Read(source, keysPath, null, default, includeDetails: false);
        if (package.Titles.Count == 0)
            throw new InvalidDataException(package.MetadataError ?? "No title metadata available.");

        var content = package.Titles.FirstOrDefault(title => title.Type == ContentMetaType.Application)
            ?? package.Titles[0];
        if (content.DetailsError != null) throw new InvalidDataException(content.DetailsError);
        var section = content.Type switch
        {
            ContentMetaType.Application => "Application",
            ContentMetaType.Patch => "Patch",
            ContentMetaType.AddOnContent => "Addon",
            _ => throw new InvalidDataException("Unsupported content type.")
        };
        if (package.Titles.Count > 1)
            section = package.Titles.Select(title => title.ApplicationTitleId).Distinct().Count() > 1
                ? "MultiPackage" : "Combined";

        using var resource = typeof(MainWindow).Assembly.GetManifestResourceStream("Viewer.NamingPresets.json")
            ?? throw new InvalidDataException("Naming presets missing.");
        using var catalog = JsonDocument.Parse(resource);
        var presets = catalog.RootElement.GetProperty(section).GetProperty("Presets").EnumerateArray().ToArray();
        var preset = presets.FirstOrDefault(item => item.GetProperty("Id").GetString() == "offline");
        if (preset.ValueKind == JsonValueKind.Undefined)
            preset = presets.First(item => item.GetProperty("Id").GetString() == "default");
        var pattern = string.IsNullOrWhiteSpace(customPattern) ? (_namingPatterns.TryGetValue(section, out var configured) && !string.IsNullOrWhiteSpace(configured) ? configured : preset.GetProperty("Pattern").GetString()!) : customPattern;
        if (pattern.IndexOfAny(new[] { '/', '\\' }) >= 0)
            throw new ArgumentException("Folder patterns are not supported in the single-file rename view.");
        var titleName = content.NacpData?.Titles.FirstOrDefault(title => !string.IsNullOrWhiteSpace(title?.Name))?.Name ?? Path.GetFileNameWithoutExtension(source).Split('[')[0].Trim();
        var titles = LocalTitleDb();
        titles.TryGetValue(content.TitleId, out var localTitle);
        titles.TryGetValue(content.ApplicationTitleId, out var localBase);
        var values = new System.Collections.Generic.Dictionary<string, string>
        {
            ["Title"] = titleName,
            ["WTitle"] = localTitle?.Name ?? titleName,
            ["WAppTitle"] = localBase?.Name ?? titleName,
            ["TitleId"] = content.TitleId,
            ["AppId"] = content.ApplicationTitleId,
            ["PatchId"] = content.PatchTitleId,
            ["PatchNum"] = content.PatchNumber.ToString(),
            ["VerDsp"] = content.NacpData?.DisplayVersion ?? "NO_VERSION",
            ["VerNum"] = content.Version.Version.ToString(),
            ["Ext"] = package.Type,
            ["GameCount"] = package.Titles.Count(title => title.Type == ContentMetaType.Application).ToString(),
            ["UpdateCount"] = package.Titles.Count(title => title.Type == ContentMetaType.Patch).ToString(),
            ["DlcCount"] = package.Titles.Count(title => title.Type == ContentMetaType.AddOnContent).ToString(),
            ["PackageVerNum"] = package.Titles.Where(title => title.Type is ContentMetaType.Application or ContentMetaType.Patch)
                .Select(title => title.Version.Version).DefaultIfEmpty(content.Version.Version).Max().ToString()
        };
        var replacement = _preferences.InvalidFileNameCharsReplacement;
        var literal = Regex.Replace(pattern, @"\{(\w+)(?::([LU]))?\}", "");
        if (literal.Contains('{') || literal.Contains('}'))
            throw new ArgumentException("Invalid naming pattern.");
        var result = Regex.Replace(pattern, @"\{(\w+)(?::([LU]))?\}", match =>
        {
            if (!values.TryGetValue(match.Groups[1].Value, out var value))
                throw new ArgumentException("Unsupported naming keyword: " + match.Groups[1].Value);
            value = match.Groups[2].Value switch { "L" => value.ToLowerInvariant(), "U" => value.ToUpperInvariant(), _ => value };
            foreach (var invalid in Path.GetInvalidFileNameChars())
                value = value.Replace(invalid.ToString(), replacement);
            return value;
        });
        return _preferences.ReplaceWhiteSpaceChars ? Regex.Replace(result, @"\s", _ => _preferences.WhiteSpaceCharsReplacement) : result;
    }

    private async void ChooseRenameFile(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = _text.MenuItem_Open });
        if (files.FirstOrDefault()?.TryGetLocalPath() is not { } path) return;

        _renameSource = Path.GetFullPath(path);
        ++_renameVersion;
        RenameSourceText.Text = _renameSource;
        RenameTargetName.Text = Path.GetFileName(_renameSource);
        RenameStatus.Text = "";
        UpdateRenamePreview();
    }

    private void RenameNameChanged(object? sender, TextChangedEventArgs e)
    {
        ++_renameVersion;
        if (RenameApply != null) UpdateRenamePreview();
    }

    private string GetRenameDestination()
    {
        return GetRenameDestination(_renameSource, RenameTargetName.Text ?? "");
    }

    private string GetRenameDestination(string? source, string name)
    {
        if (source == null || string.IsNullOrWhiteSpace(name) || name != Path.GetFileName(name) ||
            name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.EndsWith('.') || name.EndsWith(' ') ||
            name is "." or "..")
            throw new ArgumentException(_text.RenamingTool_NewName);

        var stem = name.Split('.')[0];
        if (new[] { "CON", "PRN", "AUX", "NUL" }.Contains(stem, StringComparer.OrdinalIgnoreCase) ||
            (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase) ||
                stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) && stem[3] is >= '1' and <= '9'))
            throw new ArgumentException(_text.RenamingTool_NewName);

        return Path.Combine(Path.GetDirectoryName(source)!, name);
    }

    private void UpdateRenamePreview()
    {
        RenameApply.IsEnabled = false;
        try
        {
            var destination = GetRenameDestination();
            RenamePreview.Text = destination;
            RenameApply.IsEnabled = !string.Equals(destination, _renameSource, StringComparison.OrdinalIgnoreCase) &&
                !File.Exists(destination) && !Directory.Exists(destination);
        }
        catch (ArgumentException)
        {
            RenamePreview.Text = "";
        }
    }

    private async void ApplyRename(object? sender, RoutedEventArgs e)
    {
        if (_renameSource is not { } source) return;
        try
        {
            var destination = GetRenameDestination();
            File.Move(source, destination, overwrite: false);
            ++_renameVersion;
            _renameSource = destination;
            RenameSourceText.Text = destination;
            RenameStatus.Text = _text.RenamingTool_StatusRenamed;
            UpdateRenamePreview();
            if (string.Equals(_openedPath, source, StringComparison.OrdinalIgnoreCase))
                await OpenFileAsync(destination);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            RenameStatus.Text = ex.Message;
            UpdateRenamePreview();
        }
    }
}
