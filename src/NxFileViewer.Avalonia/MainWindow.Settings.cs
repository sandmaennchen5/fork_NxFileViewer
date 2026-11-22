using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Styling;

namespace NxFileViewer.Avalonia;

public partial class MainWindow
{
    private ViewerPreferences _preferences = new();
    private Emignatik.NxFileViewer.Settings.AppSettings _editedSharedSettings = new();
    private void CancelSettings(object? sender, RoutedEventArgs e) => InitializePreferences();
    private void InitializePreferences()
    {
        var preferences = new ViewerPreferences();
        try { preferences = ViewerPreferences.Load(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            SettingsStatus.Text = ex.Message;
        }
        LoadPreferenceControls(AdoptExistingPlugins(preferences));
    }
    private void ResetSettings(object? sender, RoutedEventArgs e) => LoadPreferenceControls(ViewerPreferences.FromShared(new Emignatik.NxFileViewer.Settings.AppSettings()));
    private void LoadPreferenceControls(ViewerPreferences preferences)
    {
        _editedSharedSettings = JsonSerializer.Deserialize<Emignatik.NxFileViewer.Settings.AppSettings>(JsonSerializer.Serialize(preferences.SharedSettings))!;
        SelectChoice(LanguageChoice, preferences.Language);
        SelectChoice(ThemeChoice, preferences.Theme);
        InitializeTitleDbChoice(preferences);
        _keysPath = ResolveKeyFile(preferences.ProdKeysPath, "prod.keys");
        if (string.IsNullOrWhiteSpace(_keysPath) && File.Exists(Path.Combine(AppContext.BaseDirectory, "prod.keys")))
            _keysPath = Path.Combine(AppContext.BaseDirectory, "prod.keys");
        NszGuiPath.Text = preferences.NszGuiPath;
        NandGuiPath.Text = preferences.NandGuiPath;
        RustDesktopPath.Text = preferences.RustDesktopPath;
        KronosGuiPath.Text = preferences.KronosGuiPath;
        RustBackendPath.Text = preferences.RustBackendPath;
        SettingsKeysPath.Text = _keysPath;
        TitleKeysPath.Text = ResolveKeyFile(preferences.TitleKeysPath, "title.keys");
        ApplyPreferences(preferences);
    }

    private static void SelectChoice(ComboBox choice, string value) =>
        choice.SelectedItem = choice.Items.OfType<ComboBoxItem>().FirstOrDefault(item => Equals(item.Tag, value))
            ?? choice.Items[0];

    private void ApplyPreferences(ViewerPreferences preferences)
    {
        _preferences = preferences;
        PackageFile.AllowBlockless = preferences.SharedSettings.OpenBlocklessCompressionNCZ; PackageFile.IgnoreMissingDeltaFragments = preferences.SharedSettings.IgnoreMissingDeltaFragments;
        FileMtpButton.IsVisible = BatchMtpButton.IsVisible = OperatingSystem.IsWindows() && preferences.SharedSettings.EnableMtp;
        ExportVisibleSetting.IsChecked = preferences.ExportVisibleColumnsOnly;
        SelectChoice(LogLevelSetting, preferences.LogMinimumLevel);
        LogRetentionSetting.Value = preferences.LogFileRetention;
        AutoUpdateSetting.IsChecked = preferences.CheckUpdatesOnStart;
        PreReleaseSetting.IsChecked = preferences.IncludePreReleases;
        InjectTicketsSetting.IsChecked = PackageFile.InjectTicketKeys = preferences.InjectTicketKeys;
        NszActiveSetting.IsChecked = preferences.NszActive;
        PackageOperationsActiveSetting.IsChecked = PackagePluginSettingsTab.IsVisible = preferences.PackageOperationsActive;
        PackageFile.TitleKeysFilePath = TitleKeysPath.Text;
        CompressionEngineSetting.SelectedIndex = preferences.CompressionEngine;
        NszCliPath.Text = preferences.NszCliPath;
        CompressionLevelSetting.Text = preferences.CompressionLevel.ToString(System.Globalization.CultureInfo.InvariantCulture);
        CompressionModeSetting.SelectedIndex = preferences.CompressionMode;
        if (CompressionBlockSetting.Items.Count == 0)
            for (var exponent = 14; exponent <= 32; exponent++) { var bytes = 1L << exponent; CompressionBlockSetting.Items.Add(new CompressionBlockChoice(exponent, exponent < 20 ? $"{bytes / 1024} KiB" : exponent < 30 ? $"{bytes / 1048576} MiB" : $"{bytes / 1073741824} GiB")); }
        CompressionBlockSetting.SelectedItem = CompressionBlockSetting.Items.OfType<CompressionBlockChoice>().FirstOrDefault(choice => choice.Exponent == preferences.CompressionBlockSize);
        CompressionEngineChanged(null, null!);
        HistoryEnabledSetting.IsChecked = preferences.BatchHistoryEnabled;
        HistoryLimitSetting.Value = preferences.BatchHistoryLimit;
        AutoVerifyBatchDropSetting.IsChecked = preferences.AutoVerifyBatchDrop;
        NszDirectSetting.IsChecked = preferences.NszDirectStart;
        NandDirectSetting.IsChecked = preferences.NandDirectStart;
        RustDirectSetting.IsChecked = preferences.RustDirectStart;
        KronosDirectSetting.IsChecked = preferences.KronosDirectStart;
        _text = GetText(preferences.Language);
        Emignatik.NxFileViewer.Localization.LocalizationManager.Instance.Keys = _text;
        DataContext = _text;
        ApplyTitleDbPreferences(preferences);
        LoadRenamePresets();
        LoadNamingSettings(preferences);
        LoadPluginGeneralSettings(preferences);
        BuildProgramSettings();
        VerifyOnOpenSetting.IsChecked = _verifyOnOpen = preferences.VerifyOnOpen;
        NandActiveSetting.IsChecked = NandTab.IsVisible = preferences.NandActive;
        FirmwareActiveSetting.IsChecked = FirmwareTab.IsVisible = preferences.FirmwareActive;
        RequestedThemeVariant = preferences.Theme switch
        {
            "Light" => ThemeVariant.Light,
            "Dark" => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };
        WpfTheme.Apply(this);
        RefreshHome();
        ValidateKeys(null, new RoutedEventArgs());
    }

    private sealed record CompressionBlockChoice(int Exponent, string Label) { public override string ToString() => Label; }
    private int ReadCompressionLevel() => int.TryParse(CompressionLevelSetting.Text, out var value) && value is >= 1 and <= 22 ? value : throw new ArgumentException(_text.Nsz_Level);
    private void CompressionEngineChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (CompressionModeSetting == null || CompressionBlockSetting == null) return;
        CompressionModeSetting.IsEnabled = CompressionEngineSetting.SelectedIndex != 0;
        CompressionBlockSetting.IsEnabled = CompressionModeSetting.IsEnabled && CompressionModeSetting.SelectedIndex == 2;
    }
    private void SavePreferences(object? sender, RoutedEventArgs e)
    {
        var preferences = new ViewerPreferences(
            (LanguageChoice.SelectedItem as ComboBoxItem)?.Tag as string ?? "",
            (ThemeChoice.SelectedItem as ComboBoxItem)?.Tag as string ?? "",
            _keysPath, NszGuiPath.Text, NandGuiPath.Text, RustDesktopPath.Text, KronosGuiPath.Text, RustBackendPath.Text) { SharedSettings = _editedSharedSettings, PluginLogLevels = _pluginLogEditors.ToDictionary(pair => pair.Key, pair => (pair.Value.SelectedItem as ComboBoxItem)?.Tag as string ?? ""), OutputModes = _outputEditors.ToDictionary(pair => pair.Key, pair => pair.Value.SelectedIndex), CustomOutputFolder = _customOutputEditor.Text, InvalidFileNameCharsReplacement = _invalidReplacementEditor.Text ?? "", ReplaceWhiteSpaceChars = _replaceWhiteSpaceEditor.IsChecked == true, WhiteSpaceCharsReplacement = _whiteSpaceReplacementEditor.Text ?? "", InjectTicketKeys = InjectTicketsSetting.IsChecked == true, NszActive = NszActiveSetting.IsChecked == true, PackageOperationsActive = PackageOperationsActiveSetting.IsChecked == true, ExportVisibleColumnsOnly = ExportVisibleSetting.IsChecked == true, LogMinimumLevel = (LogLevelSetting.SelectedItem as ComboBoxItem)?.Tag as string ?? "INFO", LogFileRetention = (int)(LogRetentionSetting.Value ?? 14), CheckUpdatesOnStart = AutoUpdateSetting.IsChecked == true, IncludePreReleases = PreReleaseSetting.IsChecked == true, CompressionEngine = CompressionEngineSetting.SelectedIndex, NszCliPath = NszCliPath.Text, CompressionLevel = int.TryParse(CompressionLevelSetting.Text, out var compressionLevel) ? compressionLevel : 0, CompressionMode = CompressionModeSetting.SelectedIndex, CompressionBlockSize = (CompressionBlockSetting.SelectedItem as CompressionBlockChoice)?.Exponent ?? 20, TitleKeysPath = TitleKeysPath.Text, TitleDbRepository = (TitleDbRepositoryChoice.SelectedItem as ComboBoxItem)?.Tag as string, NamingPatterns = _patternEditors.ToDictionary(pair => pair.Key, pair => pair.Value.Text ?? ""), VerifyOnOpen = VerifyOnOpenSetting.IsChecked == true, NandActive = NandActiveSetting.IsChecked == true, FirmwareActive = FirmwareActiveSetting.IsChecked == true, BatchHistoryEnabled = HistoryEnabledSetting.IsChecked == true, BatchHistoryLimit = (int)(HistoryLimitSetting.Value ?? 5), AutoVerifyBatchDrop = AutoVerifyBatchDropSetting.IsChecked == true, NszDirectStart = NszDirectSetting.IsChecked == true, NandDirectStart = NandDirectSetting.IsChecked == true, RustDirectStart = RustDirectSetting.IsChecked == true, KronosDirectStart = KronosDirectSetting.IsChecked == true };
        try
        {
            if (preferences.CompressionLevel is < 1 or > 22) throw new ArgumentException(_text.Nsz_Level);
            if (preferences.InvalidFileNameCharsReplacement.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || preferences.WhiteSpaceCharsReplacement.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new ArgumentException(_text.RenamingTool_BadInvalidFileNameCharReplacement);
            foreach (var entry in preferences.NamingPatterns ?? new())
            {
                var type = entry.Key switch { "Application" => Emignatik.NxFileViewer.Services.FileRenaming.Exceptions.PatternType.Application, "Patch" => Emignatik.NxFileViewer.Services.FileRenaming.Exceptions.PatternType.Patch, "Addon" => Emignatik.NxFileViewer.Services.FileRenaming.Exceptions.PatternType.Addon, "Combined" => Emignatik.NxFileViewer.Services.FileRenaming.Exceptions.PatternType.Super, _ => Emignatik.NxFileViewer.Services.FileRenaming.Exceptions.PatternType.Multi };
                try { Emignatik.NxFileViewer.Services.FileRenaming.NamingPresets.ValidatePattern(entry.Value, type); }
                catch (Exception ex) { throw new ArgumentException(ex.Message, ex); }
            }
            preferences.Save();
            ApplyPreferences(preferences);
            SettingsStatus.Text = "";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            SettingsStatus.Text = ex.Message;
        }
    }
}
