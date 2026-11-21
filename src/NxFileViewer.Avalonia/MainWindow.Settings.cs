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
    private void ResetSettings(object? sender, RoutedEventArgs e) => LoadPreferenceControls(new ViewerPreferences());
    private void LoadPreferenceControls(ViewerPreferences preferences)
    {
        SelectChoice(LanguageChoice, preferences.Language);
        SelectChoice(ThemeChoice, preferences.Theme);
        InitializeTitleDbChoice(preferences);
        _keysPath = preferences.ProdKeysPath;
        if (string.IsNullOrWhiteSpace(_keysPath) && File.Exists(Path.Combine(AppContext.BaseDirectory, "prod.keys")))
            _keysPath = Path.Combine(AppContext.BaseDirectory, "prod.keys");
        NszGuiPath.Text = preferences.NszGuiPath;
        NandGuiPath.Text = preferences.NandGuiPath;
        RustDesktopPath.Text = preferences.RustDesktopPath;
        KronosGuiPath.Text = preferences.KronosGuiPath;
        RustBackendPath.Text = preferences.RustBackendPath;
        SettingsKeysPath.Text = _keysPath;
        TitleKeysPath.Text = preferences.TitleKeysPath;
        ApplyPreferences(preferences);
    }

    private static void SelectChoice(ComboBox choice, string value) =>
        choice.SelectedItem = choice.Items.OfType<ComboBoxItem>().FirstOrDefault(item => Equals(item.Tag, value))
            ?? choice.Items[0];

    private void ApplyPreferences(ViewerPreferences preferences)
    {
        _preferences = preferences;
        ExportVisibleSetting.IsChecked = preferences.ExportVisibleColumnsOnly;
        SelectChoice(LogLevelSetting, preferences.LogMinimumLevel);
        LogRetentionSetting.Value = preferences.LogFileRetention;
        AutoUpdateSetting.IsChecked = preferences.CheckUpdatesOnStart;
        PreReleaseSetting.IsChecked = preferences.IncludePreReleases;
        InjectTicketsSetting.IsChecked = PackageFile.InjectTicketKeys = preferences.InjectTicketKeys;
        NszActiveSetting.IsChecked = preferences.NszActive;
        PackageOperationsActiveSetting.IsChecked = preferences.PackageOperationsActive;
        PackageFile.TitleKeysFilePath = preferences.TitleKeysPath;
        CompressionEngineSetting.SelectedIndex = preferences.CompressionEngine;
        NszCliPath.Text = preferences.NszCliPath;
        CompressionLevelSetting.Value = preferences.CompressionLevel;
        CompressionModeSetting.SelectedIndex = preferences.CompressionMode;
        CompressionBlockSetting.Value = preferences.CompressionBlockSize;
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
    }

    private void CompressionEngineChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (CompressionModeSetting == null || CompressionBlockSetting == null) return;
        CompressionModeSetting.IsEnabled = CompressionBlockSetting.IsEnabled = CompressionEngineSetting.SelectedIndex == 1;
    }
    private void SavePreferences(object? sender, RoutedEventArgs e)
    {
        var preferences = new ViewerPreferences(
            (LanguageChoice.SelectedItem as ComboBoxItem)?.Tag as string ?? "",
            (ThemeChoice.SelectedItem as ComboBoxItem)?.Tag as string ?? "",
            _keysPath, NszGuiPath.Text, NandGuiPath.Text, RustDesktopPath.Text, KronosGuiPath.Text, RustBackendPath.Text) { PluginLogLevels = _pluginLogEditors.ToDictionary(pair => pair.Key, pair => (pair.Value.SelectedItem as ComboBoxItem)?.Tag as string ?? ""), OutputModes = _outputEditors.ToDictionary(pair => pair.Key, pair => pair.Value.SelectedIndex), CustomOutputFolder = _customOutputEditor.Text, InvalidFileNameCharsReplacement = _invalidReplacementEditor.Text ?? "", ReplaceWhiteSpaceChars = _replaceWhiteSpaceEditor.IsChecked == true, WhiteSpaceCharsReplacement = _whiteSpaceReplacementEditor.Text ?? "", InjectTicketKeys = InjectTicketsSetting.IsChecked == true, NszActive = NszActiveSetting.IsChecked == true, PackageOperationsActive = PackageOperationsActiveSetting.IsChecked == true, ExportVisibleColumnsOnly = ExportVisibleSetting.IsChecked == true, LogMinimumLevel = (LogLevelSetting.SelectedItem as ComboBoxItem)?.Tag as string ?? "INFO", LogFileRetention = (int)(LogRetentionSetting.Value ?? 14), CheckUpdatesOnStart = AutoUpdateSetting.IsChecked == true, IncludePreReleases = PreReleaseSetting.IsChecked == true, CompressionEngine = CompressionEngineSetting.SelectedIndex, NszCliPath = NszCliPath.Text, CompressionLevel = (int)(CompressionLevelSetting.Value ?? 18), CompressionMode = CompressionModeSetting.SelectedIndex, CompressionBlockSize = (int)(CompressionBlockSetting.Value ?? 20), TitleKeysPath = TitleKeysPath.Text, TitleDbRepository = (TitleDbRepositoryChoice.SelectedItem as ComboBoxItem)?.Tag as string, NamingPatterns = _patternEditors.ToDictionary(pair => pair.Key, pair => pair.Value.Text ?? ""), VerifyOnOpen = VerifyOnOpenSetting.IsChecked == true, NandActive = NandActiveSetting.IsChecked == true, FirmwareActive = FirmwareActiveSetting.IsChecked == true, BatchHistoryEnabled = HistoryEnabledSetting.IsChecked == true, BatchHistoryLimit = (int)(HistoryLimitSetting.Value ?? 5), AutoVerifyBatchDrop = AutoVerifyBatchDropSetting.IsChecked == true, NszDirectStart = NszDirectSetting.IsChecked == true, NandDirectStart = NandDirectSetting.IsChecked == true, RustDirectStart = RustDirectSetting.IsChecked == true, KronosDirectStart = KronosDirectSetting.IsChecked == true };
        try
        {
            if (preferences.InvalidFileNameCharsReplacement.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || preferences.WhiteSpaceCharsReplacement.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new ArgumentException(_text.RenamingTool_BadInvalidFileNameCharReplacement);
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
