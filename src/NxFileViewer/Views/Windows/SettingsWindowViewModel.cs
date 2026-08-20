using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Windows.Input;
using Emignatik.NxFileViewer.Localization;
using Emignatik.NxFileViewer.Localization.Keys;
using Emignatik.NxFileViewer.Services.BackgroundTask;
using Emignatik.NxFileViewer.Services.BackgroundTask.RunnableImpl;
using Emignatik.NxFileViewer.Services.FileLocationOpening;
using Emignatik.NxFileViewer.Services.KeysManagement;
using Emignatik.NxFileViewer.Styling.Theme;
using Emignatik.NxFileViewer.Settings;
using Emignatik.NxFileViewer.Utils.MVVM;
using Emignatik.NxFileViewer.Utils.MVVM.Commands;
using Emignatik.NxFileViewer.Utils.MVVM.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace Emignatik.NxFileViewer.Views.Windows;

public class SettingsWindowViewModel : WindowViewModelBase
{
    private readonly IAppSettingsManager _appSettingsManager;
    private readonly IMainBackgroundTaskRunnerService _backgroundTaskRunnerService;
    private readonly IServiceProvider _serviceProvider;
    private readonly IKeySetProviderService _keySetProviderService;
    private readonly IFileLocationOpenerService _fileLocationOpenerService;

    private IAppSettings _editedSettings;
    private ILocalization<ILocalizationKeys>? _selectedLanguage;


    public SettingsWindowViewModel(IAppSettingsManager appSettingsManager, IMainBackgroundTaskRunnerService backgroundTaskRunnerService, IServiceProvider serviceProvider,
        IKeySetProviderService keySetProviderService, IFileLocationOpenerService fileLocationOpenerService)
    {
        _appSettingsManager = appSettingsManager ?? throw new ArgumentNullException(nameof(appSettingsManager));
        _backgroundTaskRunnerService = backgroundTaskRunnerService ?? throw new ArgumentNullException(nameof(backgroundTaskRunnerService));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _keySetProviderService = keySetProviderService ?? throw new ArgumentNullException(nameof(keySetProviderService));
        _fileLocationOpenerService = fileLocationOpenerService ?? throw new ArgumentNullException(nameof(fileLocationOpenerService));

        BrowseProdKeysCommand = new RelayCommand(BrowseProdKeys);
        BrowseTitleKeysCommand = new RelayCommand(BrowseTitleKeys);
        ApplySettingsCommand = new RelayCommand(ApplySettings);
        CancelSettingsCommand = new RelayCommand(CancelSettings);
        ResetSettingsCommand = new RelayCommand(ResetSettings);
        DownloadProdKeysCommand = new RelayCommand(DownloadProdKeys, CanDownloadProdKeys);
        DownloadTitleKeysCommand = new RelayCommand(DownloadTitleKeys, CanDownloadTitleKeys);
        EditProdKeysCommand = new RelayCommand(OpenProdKeysLocation, CanOpenProdKeysLocation);
        EditTitleKeysCommand = new RelayCommand(OpenTitleKeysLocation, CanOpenTitleKeysLocation);

        InitializeFromSettings(appSettingsManager.Clone());

        _backgroundTaskRunnerService.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(IMainBackgroundTaskRunnerService.IsRunning))
            {
                DownloadProdKeysCommand.TriggerCanExecuteChanged();
                DownloadTitleKeysCommand.TriggerCanExecuteChanged();
            }
        };

        _keySetProviderService.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(IKeySetProviderService.ActualProdKeysFilePath))
                NotifyPropertyChanged(nameof(ActualProdKeysFilePath));
            else if (args.PropertyName == nameof(IKeySetProviderService.ActualTitleKeysFilePath))
                NotifyPropertyChanged(nameof(ActualTitleKeysFilePath));
            else if (args.PropertyName == nameof(IKeySetProviderService.ProdKeysValidation))
            {
                NotifyPropertyChanged(nameof(ProdKeysValidationSummary));
                NotifyPropertyChanged(nameof(AreProdKeysValid));
                NotifyPropertyChanged(nameof(HasProdKeysWarnings));
            }
            else if (args.PropertyName == nameof(IKeySetProviderService.TitleKeysValidation))
            {
                NotifyPropertyChanged(nameof(TitleKeysValidationSummary));
                NotifyPropertyChanged(nameof(AreTitleKeysValid));
            }
        };
    }

    public IAppSettings EditedSettings
    {
        get => _editedSettings;
        [MemberNotNull(nameof(_editedSettings))]
        private set
        {
            _editedSettings = value;
            NotifyPropertyChanged();
        }
    }

    public ICommand BrowseProdKeysCommand { get; }

    public ICommand BrowseTitleKeysCommand { get; }

    public ICommand ApplySettingsCommand { get; }

    public ICommand CancelSettingsCommand { get; }

    public ICommand ResetSettingsCommand { get; }

    public RelayCommand DownloadProdKeysCommand { get; }

    public RelayCommand DownloadTitleKeysCommand { get; }

    public RelayCommand EditProdKeysCommand { get; }

    public RelayCommand EditTitleKeysCommand { get; }


    public IEnumerable<LogLevel> LogLevels => Enum.GetValues<LogLevel>();

    public IEnumerable<ThemeOption> ThemeOptions { get; } = new[]
    {
        new ThemeOption(AppTheme.System, "Auto"),
        new ThemeOption(AppTheme.Light, "Light"),
        new ThemeOption(AppTheme.Dark, "Dark"),
    };

    public string ActualProdKeysFilePath => _keySetProviderService.ActualProdKeysFilePath ?? LocalizationManager.Instance.Current.Keys.NoneKeysFile;

    public string ActualTitleKeysFilePath => _keySetProviderService.ActualTitleKeysFilePath ?? LocalizationManager.Instance.Current.Keys.NoneKeysFile;

    public bool AreProdKeysValid => _keySetProviderService.ProdKeysValidation.IsValid;

    public bool HasProdKeysWarnings => _keySetProviderService.ProdKeysValidation.HasWarnings;

    public bool AreTitleKeysValid => _keySetProviderService.TitleKeysValidation.IsValid;

    public string ProdKeysValidationSummary => BuildValidationSummary(_keySetProviderService.ProdKeysValidation);

    public string TitleKeysValidationSummary => BuildValidationSummary(_keySetProviderService.TitleKeysValidation);

    public IEnumerable<ILocalization<ILocalizationKeys>> AvailableLanguages => LocalizationManager.Instance.AvailableLocalizations;

    public ILocalization<ILocalizationKeys>? SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            _selectedLanguage = value;
            if (value != null)
                EditedSettings.AppLanguage = value.CultureName;
            NotifyPropertyChanged();
        }
    }

    [MemberNotNull(nameof(_editedSettings))]
    private void InitializeFromSettings(IAppSettings appSettings)
    {
        EditedSettings = appSettings;
        this.SelectedLanguage = LocalizationManager.Instance.AvailableLocalizations.FindByCultureName(appSettings.AppLanguage);
    }

    private void BrowseProdKeys()
    {
        var clonedSettings = EditedSettings;
        if (BrowseKeysFilePath(clonedSettings.ProdKeysFilePath, LocalizationManager.Instance.Current.Keys.BrowseKeysFile_ProdTitle, out var selectedFilePath))
        {
            clonedSettings.ProdKeysFilePath = selectedFilePath;
        }
    }

    private void BrowseTitleKeys()
    {
        var clonedSettings = EditedSettings;
        if (BrowseKeysFilePath(clonedSettings.TitleKeysFilePath, LocalizationManager.Instance.Current.Keys.BrowseKeysFile_TitleTitle, out var selectedFilePath))
        {
            clonedSettings.TitleKeysFilePath = selectedFilePath;
        }
    }

    private async void DownloadProdKeys()
    {
        var clonedSettings = EditedSettings;
        var downloadFileRunnable = _serviceProvider.GetRequiredService<IDownloadFileRunnable>();
        downloadFileRunnable.Setup(clonedSettings.ProdKeysDownloadUrl, _keySetProviderService.AppDirProdKeysFilePath);
        await _backgroundTaskRunnerService.RunAsync(downloadFileRunnable);
        _keySetProviderService.Reset();
    }

    private async void DownloadTitleKeys()
    {
        var clonedSettings = EditedSettings;
        var downloadFileRunnable = _serviceProvider.GetRequiredService<IDownloadFileRunnable>();
        downloadFileRunnable.Setup(clonedSettings.TitleKeysDownloadUrl, _keySetProviderService.AppDirTitleKeysFilePath);
        await _backgroundTaskRunnerService.RunAsync(downloadFileRunnable);
        _keySetProviderService.Reset();
    }

    private bool CanDownloadTitleKeys()
    {
        return !_backgroundTaskRunnerService.IsRunning;
    }

    private bool CanDownloadProdKeys()
    {
        return !_backgroundTaskRunnerService.IsRunning;
    }


    private bool CanOpenProdKeysLocation()
    {
        return SafeCheckFileExists(ActualProdKeysFilePath);
    }

    private void OpenProdKeysLocation()
    {
        _fileLocationOpenerService.OpenFileLocationSafe(ActualProdKeysFilePath);
    }

    private bool CanOpenTitleKeysLocation()
    {
        return SafeCheckFileExists(ActualTitleKeysFilePath);
    }

    private void OpenTitleKeysLocation()
    {
        _fileLocationOpenerService.OpenFileLocationSafe(ActualTitleKeysFilePath);
    }


    private static bool SafeCheckFileExists(string? filePath)
    {
        try
        {
            if (string.IsNullOrEmpty(filePath))
                return false;
            return File.Exists(filePath);
        }
        catch
        {
            return false;
        }
    }

    private static string BuildValidationSummary(KeyFileValidationResult result)
    {
        var keys = LocalizationManager.Instance.Current.Keys;
        if (!result.FileExists)
            return keys.KeysValidation_MissingFile;

        var messages = new List<string>();
        if (result.UnsupportedMasterKeys is { Count: > 0 })
            messages.Add(keys.KeysValidation_UnsupportedMasterKeys.SafeFormat(string.Join(", ", result.UnsupportedMasterKeys)));
        if (result.HighestValidMasterKeyRevision is { } revision &&
            MasterKeyFirmwareMap.GetSupportedFirmware(revision) is { } firmware)
        {
            messages.Add(keys.KeysValidation_FirmwareEstimate.SafeFormat(
                $"master_key_{revision:x2}", firmware));
        }
        if (result.MissingKeys.Count > 0)
            messages.Add(keys.KeysValidation_MissingMasterKeys.SafeFormat(string.Join(", ", result.MissingKeys)));
        if (result.InvalidKeys.Count > 0)
            messages.Add(keys.KeysValidation_InvalidMasterKeys.SafeFormat(string.Join(", ", result.InvalidKeys)));
        if (result.InvalidLineNumbers.Count > 0)
            messages.Add(keys.KeysValidation_InvalidLines.SafeFormat(string.Join(", ", result.InvalidLineNumbers)));
        if (result.ValidEntryCount == 0 && messages.Count == 0)
            messages.Add(keys.KeysValidation_EmptyFile);
        if (messages.Count == 0)
            messages.Add(keys.KeysValidation_ValidEntries.SafeFormat(result.ValidEntryCount));

        return string.Join(Environment.NewLine, messages);
    }

    private static bool BrowseKeysFilePath(string initialFilePath, string title, [NotNullWhen(true)] out string? selectedFilePath)
    {
        selectedFilePath = null;

        var openFileDialog = new OpenFileDialog
        {
            Title = title,
            FileName = initialFilePath,
            Filter = LocalizationManager.Instance.Current.Keys.BrowseKeysFile_Filter,
        };

        var result = openFileDialog.ShowDialog();
        if (result != null && result.Value)
        {
            selectedFilePath = openFileDialog.FileName;
            return true;
        }
        else
        {
            return false;
        }
    }

    private void ApplySettings()
    {
        _appSettingsManager.Load(EditedSettings);
        this.Window?.Close();
    }

    private void CancelSettings()
    {
        this.Window?.Close();
    }

    private void ResetSettings()
    {
        InitializeFromSettings(_appSettingsManager.GetDefault());
    }
}

public sealed record ThemeOption(AppTheme Value, string DisplayName);
