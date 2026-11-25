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
        NandBisKeysPath.Text = preferences.SharedSettings.NandBisKeysPath;
        RustDesktopPath.Text = preferences.RustDesktopPath;
        KronosGuiPath.Text = preferences.KronosGuiPath;
        RustBackendPath.Text = preferences.RustBackendPath;
        SettingsKeysPath.Text = preferences.ProdKeysPath;
        TitleKeysPath.Text = preferences.TitleKeysPath;
        Emignatik.NxFileViewer.Services.KeysManagement.KeyDownloads.MigrateLegacyHost(_editedSharedSettings);
        KeysDownloadHost.Text = _editedSharedSettings.KeysDownloadHost;
        ProdKeysDownloadUrl.Text = _editedSharedSettings.ProdKeysDownloadUrl;
        TitleKeysDownloadUrl.Text = _editedSharedSettings.TitleKeysDownloadUrl;
        ApplyPreferences(preferences);
        RefreshManagedPluginRows();
    }

    private static void SelectChoice(ComboBox choice, string value) =>
        choice.SelectedItem = choice.Items.OfType<ComboBoxItem>().FirstOrDefault(item => Equals(item.Tag, value))
            ?? choice.Items[0];

    private void ApplyPreferences(ViewerPreferences preferences)
    {
        var retentionChanged = _preferences.LogFileRetention != preferences.LogFileRetention;
        _preferences = preferences;
        _memoryTrace?.Refresh();
        if (retentionChanged) ApplyLogRetention();
        PackageFile.AllowBlockless = preferences.SharedSettings.OpenBlocklessCompressionNCZ; PackageFile.IgnoreMissingDeltaFragments = preferences.SharedSettings.IgnoreMissingDeltaFragments;
        FileMtpButton.IsVisible = BatchMtpButton.IsVisible = OperatingSystem.IsWindows() && preferences.SharedSettings.EnableMtp;
        MtpActiveSetting.IsVisible = OperatingSystem.IsWindows();
        MtpActiveSetting.IsChecked = preferences.SharedSettings.EnableMtp;
        NupActiveSetting.IsChecked = preferences.SharedSettings.EnableNup;
        NandPluginActiveSetting.IsChecked = preferences.SharedSettings.EnableNandPlugin;
        NandPluginSettingsTab.IsVisible = preferences.NandActive && preferences.SharedSettings.EnableNandPlugin;
        ExportVisibleSetting.IsChecked = preferences.ExportVisibleColumnsOnly;
        SelectChoice(LogLevelSetting, preferences.LogMinimumLevel);
        LogRetentionSetting.Value = preferences.LogFileRetention;
        AutoUpdateSetting.IsChecked = preferences.CheckUpdatesOnStart;
        PreReleaseSetting.IsChecked = preferences.IncludePreReleases;
        InjectTicketsSetting.IsChecked = PackageFile.InjectTicketKeys = preferences.InjectTicketKeys;
        ReloadKeysBeforeOpenSetting.IsChecked = preferences.SharedSettings.AlwaysReloadKeysBeforeOpen;
        SaveTicketKeysSetting.IsChecked = PackageFile.SaveTicketKeys = preferences.SharedSettings.SaveTicketKeys;
        NszActiveSetting.IsChecked = preferences.NszActive;
        RefreshFilePackageActions();
        PackageOperationsActiveSetting.IsChecked = PackagePluginSettingsTab.IsVisible = preferences.PackageOperationsActive;
        PackageEngineSetting.SelectedIndex = (int)preferences.SharedSettings.PackageEngine;
        _keysPath = ResolveKeyFile(preferences.ProdKeysPath, "prod.keys");
        PackageFile.TitleKeysFilePath = ResolveKeyFile(preferences.TitleKeysPath, "title.keys");
        PackageFile.CustomTitleKeysPath = preferences.TitleKeysPath;
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
        if (_batchMemoryDiagnostics != null) RefreshBatchDiagnostics("diagnostics-setting-changed");
        Emignatik.NxFileViewer.Localization.LocalizationManager.Instance.Keys = _text;
        DataContext = _text;
        if (OperatingSystem.IsWindows())
        {
            try { Emignatik.NxFileViewer.Services.FileAssociations.ExplorerIntegration.UpdateLanguage(_text.CultureName); }
            catch (Exception error) { LogSession("WARN", error.Message); }
        }
        RenameCompactPaths.Content = _text.CultureName.StartsWith("de", StringComparison.OrdinalIgnoreCase)
            ? "Pfade abkürzen (QUELL:: / ZIEL::)" : "Shorten paths (QUELL:: / ZIEL::)";
        RenameBatchPreviewButton.Content = _text.CultureName.Split('-')[0] switch
        {
            "de" => "Prüfen", "fr" => "Vérifier", "es" => "Comprobar", _ => "Check"
        };
        ApplyTitleDbPreferences(preferences);
        RefreshUpdateDataSummaries();
        LoadRenamePresets();
        LoadNamingSettings(preferences);
        LoadPluginGeneralSettings(preferences);
        BuildProgramSettings();
        VerifyOnOpenSetting.IsChecked = _verifyOnOpen = preferences.VerifyOnOpen;
        NandActiveSetting.IsChecked = NandTab.IsVisible = preferences.NandActive;
        FirmwareActiveSetting.IsChecked = preferences.FirmwareActive;
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
        if (_keyDownloadCancellation != null) return;
        _editedSharedSettings.BatchGroupFiles = BatchGroupFiles.IsChecked == true;
        _editedSharedSettings.SdBatchSourceMode = _batchSourceMode;
        _editedSharedSettings.AlwaysReloadKeysBeforeOpen = ReloadKeysBeforeOpenSetting.IsChecked == true;
        _editedSharedSettings.SaveTicketKeys = SaveTicketKeysSetting.IsChecked == true;
        _editedSharedSettings.NandBisKeysPath = NandBisKeysPath.Text?.Trim() ?? "";
        _editedSharedSettings.PackageEngine = (Emignatik.NxFileViewer.Settings.PackageEngine)Math.Max(0, PackageEngineSetting.SelectedIndex);
        _editedSharedSettings.EnableNup = NupActiveSetting.IsChecked == true;
        _editedSharedSettings.EnableNandPlugin = NandPluginActiveSetting.IsChecked == true;
        if (OperatingSystem.IsWindows()) _editedSharedSettings.EnableMtp = MtpActiveSetting.IsChecked == true;
        _editedSharedSettings.KeysDownloadHost = KeysDownloadHost.Text?.Trim() ?? "";
        _editedSharedSettings.ProdKeysDownloadUrl = ProdKeysDownloadUrl.Text?.Trim() ?? "";
        _editedSharedSettings.TitleKeysDownloadUrl = TitleKeysDownloadUrl.Text?.Trim() ?? "";
        var preferences = new ViewerPreferences(
            (LanguageChoice.SelectedItem as ComboBoxItem)?.Tag as string ?? "",
            (ThemeChoice.SelectedItem as ComboBoxItem)?.Tag as string ?? "",
            SettingsKeysPath.Text, NszGuiPath.Text, NandGuiPath.Text, RustDesktopPath.Text, KronosGuiPath.Text, RustBackendPath.Text) { SharedSettings = _editedSharedSettings, PluginLogLevels = _pluginLogEditors.ToDictionary(pair => pair.Key, pair => (pair.Value.SelectedItem as ComboBoxItem)?.Tag as string ?? ""), OutputModes = _outputEditors.ToDictionary(pair => pair.Key, pair => pair.Value.SelectedIndex), CustomOutputFolder = _customOutputEditor.Text, InvalidFileNameCharsReplacement = _invalidReplacementEditor.Text ?? "", ReplaceWhiteSpaceChars = _replaceWhiteSpaceEditor.IsChecked == true, WhiteSpaceCharsReplacement = _whiteSpaceReplacementEditor.Text ?? "", InjectTicketKeys = InjectTicketsSetting.IsChecked == true, NszActive = NszActiveSetting.IsChecked == true, PackageOperationsActive = PackageOperationsActiveSetting.IsChecked == true, ExportVisibleColumnsOnly = ExportVisibleSetting.IsChecked == true, LogMinimumLevel = (LogLevelSetting.SelectedItem as ComboBoxItem)?.Tag as string ?? "INFO", LogFileRetention = (int)(LogRetentionSetting.Value ?? 14), CheckUpdatesOnStart = AutoUpdateSetting.IsChecked == true, IncludePreReleases = PreReleaseSetting.IsChecked == true, CompressionEngine = CompressionEngineSetting.SelectedIndex, NszCliPath = NszCliPath.Text, CompressionLevel = int.TryParse(CompressionLevelSetting.Text, out var compressionLevel) ? compressionLevel : 0, CompressionMode = CompressionModeSetting.SelectedIndex, CompressionBlockSize = (CompressionBlockSetting.SelectedItem as CompressionBlockChoice)?.Exponent ?? 20, TitleKeysPath = TitleKeysPath.Text, TitleDbRepository = (TitleDbRepositoryChoice.SelectedItem as ComboBoxItem)?.Tag as string, NamingPatterns = _patternEditors.ToDictionary(pair => pair.Key, pair => pair.Value.Text ?? ""), VerifyOnOpen = VerifyOnOpenSetting.IsChecked == true, NandActive = NandActiveSetting.IsChecked == true, FirmwareActive = FirmwareActiveSetting.IsChecked == true, BatchHistoryEnabled = HistoryEnabledSetting.IsChecked == true, BatchHistoryLimit = (int)(HistoryLimitSetting.Value ?? 5), AutoVerifyBatchDrop = AutoVerifyBatchDropSetting.IsChecked == true, NszDirectStart = NszDirectSetting.IsChecked == true, NandDirectStart = NandDirectSetting.IsChecked == true, RustDirectStart = RustDirectSetting.IsChecked == true, KronosDirectStart = KronosDirectSetting.IsChecked == true };
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
            preferences = preferences with
            {
                RenameInputs = CaptureRenameInputs(), WindowPlacement = _preferences.WindowPlacement,
                LastBatchPath = BatchInputPath.Text?.Trim(), LastOpenedPath = _lastOpenedPath, BatchLayout = CaptureBatchLayout()
            };
            preferences.Save();
            ApplyPreferences(preferences);
            InvalidateRenamePreview();
            RefreshManagedPluginRows();
            if (OperatingSystem.IsWindows())
                Emignatik.NxFileViewer.Services.FileAssociations.ExplorerRegistration.SynchronizeFeatures(
                    preferences.NszActive, preferences.PackageOperationsActive, preferences.SharedSettings.EnableNup);
            SettingsStatus.Text = "";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or System.Security.SecurityException)
        {
            SettingsStatus.Text = ex.Message;
        }
    }
}
