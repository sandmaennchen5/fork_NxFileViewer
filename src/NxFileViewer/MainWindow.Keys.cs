using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Emignatik.NxFileViewer.Services.KeysManagement;
using Emignatik.NxFileViewer.Services.OnlineServices;
namespace NxFileViewer.Avalonia;
public partial class MainWindow
{
    private CancellationTokenSource? _keyDownloadCancellation;

    private void RefreshActiveKeyPaths()
    {
        _keysPath = ResolveKeyFile(_preferences.ProdKeysPath, "prod.keys");
        PackageFile.TitleKeysFilePath = ResolveKeyFile(_preferences.TitleKeysPath, "title.keys");
        RefreshStatusKeyWarning();
    }

    private void RefreshStatusKeyWarning()
    {
        var needsProdKeys = !File.Exists(_keysPath) || _overviewPackage?.NeedsProdKeys == true;
        var warning = OverviewMissingKeys.Content as MissingKeysWarning;
        StatusKeyWarning.IsVisible = needsProdKeys || warning != null;
        if (StatusKeyWarning.ContextMenu?.Items[0] is MenuItem copy) copy.IsVisible = warning != null;
        ToolTip.SetTip(StatusKeyWarning, string.Join(Environment.NewLine,
            new[] { needsProdKeys ? _text.WarnNoProdKeysFileFound : null, warning?.Entries }
                .Where(text => !string.IsNullOrWhiteSpace(text))));
    }

    private void OpenKeySettings(object? sender, TappedEventArgs e)
    {
        if (_workspaceJobs.Count > 0) return;
        Workspace.SelectedItem = SettingsTab;
        SettingsPages.SelectedItem = KeySettingsTab;
        e.Handled = true;
    }

    private async void CopyStatusKeyWarning(object? sender, RoutedEventArgs e)
    {
        if (Clipboard != null && OverviewMissingKeys.Content is MissingKeysWarning warning)
            await Clipboard.SetTextAsync(warning.KeyNames);
    }

    private async void CopyKeysToProfile(object? sender, RoutedEventArgs e)
    {
        if (_keyDownloadCancellation != null || sender is not Button button || !button.IsEnabled) return;
        using var workspaceJob = LockWorkspace(SettingsTab);
        button.IsEnabled = false;
        KeyDownloadStatus.IsVisible = true;
        KeyDownloadStatus.Classes.Remove("key-invalid");
        try
        {
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            var candidates = new[]
            {
                (Path: ResolveKeyFile(SettingsKeysPath.Text, "prod.keys"), Name: "prod.keys"),
                (Path: ResolveKeyFile(TitleKeysPath.Text, "title.keys"), Name: "title.keys")
            };
            var files = candidates.Where(file => File.Exists(file.Path))
                .Select(file => (Source: Path.GetFullPath(file.Path!), Target: Path.Combine(SharedKeyFiles.DirectoryPath, file.Name)))
                .Where(file => !string.Equals(file.Source, file.Target, comparison))
                .Select(file => (file.Source, file.Target, Overwrite: File.Exists(file.Target))).ToArray();
            if (files.Length == 0)
            {
                KeyDownloadStatus.Text = candidates.Any(file => File.Exists(file.Path))
                    ? SharedKeyFiles.DirectoryPath : _text.KeysValidation_MissingFile;
                return;
            }
            var existing = files.Where(file => file.Overwrite).Select(file => file.Target).ToArray();
            if (existing.Length > 0 && !await ConfirmPluginCleanupAsync(_text.SettingsView_GroupBoxKeys,
                _text.Keys_ReplaceShared + Environment.NewLine + string.Join(Environment.NewLine, existing), _text.SettingsView_Button_Apply))
                return;
            foreach (var file in files)
                SharedKeyFiles.Copy(file.Source, file.Target, file.Overwrite);
            RefreshActiveKeyPaths();
            ValidateKeys(null, new RoutedEventArgs());
            RefreshHome();
            KeyDownloadStatus.Text = _text.Keys_SharedCopied + Environment.NewLine + SharedKeyFiles.DirectoryPath;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        {
            KeyDownloadStatus.Classes.Add("key-invalid");
            KeyDownloadStatus.Text = error.Message;
        }
        finally { button.IsEnabled = true; }
    }

    private void CancelKeyDownload(object? sender, RoutedEventArgs e) => _keyDownloadCancellation?.Cancel();

    private async void DownloadKeyFiles(object? sender, RoutedEventArgs e)
    {
        if (_keyDownloadCancellation != null) return;
        using var workspaceJob = LockWorkspace(SettingsTab);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(4));
        _keyDownloadCancellation = cancellation;
        DownloadKeysButton.IsEnabled = false;
        CancelKeysDownloadButton.IsVisible = KeyDownloadStatus.IsVisible = true;
        KeyDownloadStatus.Classes.Remove("key-invalid");
        try
        {
            var host = KeysDownloadHost.Text ?? "";
            var prod = KeyDownloads.Destination(SettingsKeysPath.Text, Path.Combine(ViewerDataDirectory.Root, "prod.keys"));
            var title = KeyDownloads.Destination(TitleKeysPath.Text, Path.Combine(ViewerDataDirectory.Root, "title.keys"));
            if (string.Equals(prod, title, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                throw new ArgumentException(KeyValidationMessage(
                    "prod.keys und title.keys benötigen unterschiedliche Zielpfade.",
                    "prod.keys and title.keys require different destination paths."));
            var prodUrl = KeyDownloads.ResolveUrl(ProdKeysDownloadUrl.Text?.Trim() ?? "", host);
            var titleUrl = KeyDownloads.ResolveUrl(TitleKeysDownloadUrl.Text?.Trim() ?? "", host);
            var downloader = new HttpDownloader();
            await Download(prodUrl, prod, KeyFileValidator.ValidateProdKeys);
            await Download(titleUrl, title, KeyFileValidator.ValidateTitleKeys);
            if (!_closed) KeyDownloadStatus.Text = _text.Status_Ready;

            Task Download(string url, string path, Func<string?, KeyFileValidationResult> validate)
            {
                KeyDownloadStatus.Text = string.Format(_text.Status_DownloadingFile, Path.GetFileName(path));
                return KeyDownloads.DownloadValidatedAsync(downloader, url, path, cancellation.Token, (incoming, existing) =>
                    global::Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
                    {
                        cancellation.Token.ThrowIfCancellationRequested();
                        var validation = validate(incoming);
                        if (!validation.IsValid)
                            throw new InvalidDataException(Path.GetFileName(existing) + ": " + DescribeKeyValidation(validation));
                        if (!File.Exists(existing)) return true;
                        var details = string.Join(Environment.NewLine, _text.Keys_ReplaceDownloaded, existing,
                            _text.Keys_ExistingValidation, DescribeKeyValidation(validate(existing)),
                            _text.Keys_IncomingValidation, DescribeKeyValidation(validation));
                        return await ConfirmPluginCleanupAsync(_text.SettingsView_GroupBoxKeys, details, _text.SettingsView_Button_Apply);
                    }));
            }
        }
        catch (OperationCanceledException)
        {
            if (!_closed) KeyDownloadStatus.Text = _text.Update_Cancelled;
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or
            System.Net.Http.HttpRequestException or System.Net.WebException or NotSupportedException)
        {
            if (!_closed)
            {
                KeyDownloadStatus.Classes.Add("key-invalid");
                KeyDownloadStatus.Text = error.Message;
            }
        }
        finally
        {
            _keyDownloadCancellation = null;
            if (!_closed)
            {
                DownloadKeysButton.IsEnabled = true;
                CancelKeysDownloadButton.IsVisible = false;
                RefreshActiveKeyPaths();
                ValidateKeys(null, new RoutedEventArgs());
                RefreshHome();
            }
        }
    }

    internal static string? ResolveKeyFile(string? custom, string name)
    {
        if (!string.IsNullOrWhiteSpace(custom) && File.Exists(custom)) return Path.GetFullPath(custom);
        var local = Path.Combine(ViewerDataDirectory.Root, name);
        if (File.Exists(local)) return local;
        var profile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".switch", name);
        return File.Exists(profile) ? profile : custom;
    }
    private async void ChooseTitleKeys(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "title.keys", FileTypeFilter = new[] { FilePickerFileTypes.All } });
        if (files.FirstOrDefault()?.TryGetLocalPath() is { } path) { TitleKeysPath.Text = path; ValidateKeys(sender, e); }
    }
    private void ValidateKeys(object? sender, RoutedEventArgs e)
    {
        ShowKeyLocations(ProdKeyLocations, KeyValidationText, SettingsKeysPath.Text, "prod.keys", KeyFileValidator.ValidateProdKeys);
        ShowKeyLocations(TitleKeyLocations, TitleKeyValidationText, TitleKeysPath.Text, "title.keys", KeyFileValidator.ValidateTitleKeys);
    }

    private void ShowKeyLocations(StackPanel locations, TextBlock customStatus, string? custom, string name,
        Func<string?, KeyFileValidationResult> validate)
    {
        var program = Path.Combine(ViewerDataDirectory.Root, name);
        var profile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".switch", name);
        var active = ResolveKeyFile(custom, name);
        bool IsActive(string path) => File.Exists(path) && string.Equals(Path.GetFullPath(path), active,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        locations.Children.Clear();
        AddLocation(_text.Keys_ProgramFolder, program);
        AddLocation(_text.Keys_SharedFolder, profile);
        if (string.IsNullOrWhiteSpace(custom)) customStatus.Text = "";
        else SetValidation(customStatus, custom, IsActive(custom) && Path.GetFullPath(custom) != program && Path.GetFullPath(custom) != profile);
        customStatus.IsVisible = !string.IsNullOrEmpty(customStatus.Text);

        void AddLocation(string label, string path)
        {
            var heading = new StackPanel { Orientation = global::Avalonia.Layout.Orientation.Horizontal, Spacing = 8 };
            heading.Children.Add(new TextBlock { Text = label, FontWeight = global::Avalonia.Media.FontWeight.Bold });
            if (IsActive(path))
            {
                var inUse = new TextBlock { Text = _text.Keys_InUse };
                inUse.Classes.Add("key-valid");
                heading.Children.Add(inUse);
            }
            var value = new TextBox { Text = path, IsReadOnly = true };
            value.Classes.Add("overview-value");
            var reveal = new Button { Content = _text.ContextMenu_OpenFileLocation,
                IsEnabled = File.Exists(path) || Directory.Exists(Path.GetDirectoryName(path)) };
            ToolTip.SetTip(reveal, _text.SettingsView_ToolTip_OpenKeysLocation);
            reveal.Click += async (_, _) => await RevealLocalSourceAsync(path);
            var location = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(reveal, Dock.Right);
            location.Children.Add(reveal);
            location.Children.Add(value);
            var state = new TextBlock { TextWrapping = global::Avalonia.Media.TextWrapping.Wrap };
            SetValidation(state, path);
            var panel = new StackPanel { Spacing = 2, Children = { heading, location, state } };
            var border = new Border { Child = panel };
            border.Classes.Add("overview-group");
            locations.Children.Add(border);
        }

        void SetValidation(TextBlock text, string path, bool showActive = false)
        {
            try
            {
                var result = validate(path);
                text.Text = (showActive ? _text.Keys_InUse + " · " : "") + DescribeKeyValidation(result);
                var warnings = result.HasWarnings || result.ConsoleKeys?.Values.Any(status => status != ConsoleKeyStatus.Present) == true;
                text.Classes.Set("key-valid", result.IsValid && !warnings);
                text.Classes.Set("key-warning", !result.FileExists || (result.IsValid && warnings));
                text.Classes.Set("key-invalid", result.FileExists && !result.IsValid);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                text.Text = ex.Message;
                text.Classes.Set("key-valid", false);
                text.Classes.Set("key-warning", false);
                text.Classes.Set("key-invalid", true);
            }
        }
    }
    private async void OpenCustomKeyLocation(object? sender, RoutedEventArgs e)
    {
        var path = (sender as Button)?.Tag as string == "prod.keys" ? SettingsKeysPath.Text : TitleKeysPath.Text;
        if (string.IsNullOrWhiteSpace(path)) return;
        try { await RevealLocalSourceAsync(Path.GetFullPath(path)); }
        catch (Exception error) when (error is IOException or ArgumentException or UnauthorizedAccessException)
        { SettingsStatus.Text = error.Message; }
    }

    internal string DescribeKeyValidation(KeyFileValidationResult result)
    {
        if (!result.FileExists) return _text.KeysValidation_MissingFile;
        var lines = new System.Collections.Generic.List<string>();
        if (result.IsValid) lines.Add(string.Format(_text.KeysValidation_ValidEntries, result.ValidEntryCount));
        if (result.MissingKeys.Count > 0) lines.Add(string.Format(_text.KeysValidation_MissingMasterKeys, string.Join(", ", result.MissingKeys)));
        if (result.InvalidKeys.Count > 0) lines.Add(string.Format(_text.KeysValidation_InvalidMasterKeys, string.Join(", ", result.InvalidKeys)));
        if (result.InvalidLineNumbers.Count > 0) lines.Add(string.Format(_text.KeysValidation_InvalidLines, string.Join(", ", result.InvalidLineNumbers)));
        if (result.HighestValidMasterKeyRevision is { } revision) lines.Add(string.Format(_text.KeysValidation_FirmwareEstimate, $"master_key_{revision:x2}", MasterKeyFirmwareMap.GetSupportedFirmware(revision)));
        if (result.UnsupportedMasterKeys is { Count: > 0 } unsupported)
            lines.Add(string.Format(_text.KeysValidation_UnsupportedMasterKeys, string.Join(", ", unsupported)));
        if (result.ConsoleKeys is { } consoleKeys)
        {
            foreach (var group in consoleKeys.GroupBy(key => key.Value))
            {
                var label = group.Key switch
                {
                    ConsoleKeyStatus.Present => _text.KeysValidation_ConsolePresent,
                    ConsoleKeyStatus.InvalidFormat => _text.KeysValidation_ConsoleInvalid,
                    _ => _text.KeysValidation_ConsoleMissing
                };
                lines.Add(string.Format(label, string.Join(", ", group.Select(key => key.Key))));
            }
        }
        return lines.Count == 0
            ? KeyValidationMessage("Keine gültigen Schlüssel gefunden.", "No valid keys found.")
            : string.Join(Environment.NewLine, lines);
    }

    private static string KeyValidationMessage(string german, string english) =>
        System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "de" ? german : english;
}
