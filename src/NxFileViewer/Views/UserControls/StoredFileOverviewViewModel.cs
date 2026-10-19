using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Emignatik.NxFileViewer.Localization;
using Emignatik.NxFileViewer.Models.Overview;
using Emignatik.NxFileViewer.Services.Integrity;
using Emignatik.NxFileViewer.Services.Security;
using Emignatik.NxFileViewer.Styling.Theme;
using Emignatik.NxFileViewer.Utils;
using Emignatik.NxFileViewer.Utils.MVVM;
using Emignatik.NxFileViewer.Utils.MVVM.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace Emignatik.NxFileViewer.Views.UserControls;

// Adapts persisted metadata to the same view as a live file, without opening the source.
public sealed class StoredFileOverviewViewModel : ViewModelBase
{
    private readonly BatchIntegrityResult _result;
    private readonly IBrushesProvider _brushes;
    private bool _dismissed;
    private StoredCnmtContainerViewModel? _selected;
    public StoredFileOverviewViewModel(BatchIntegrityResult result, IServiceProvider services, Func<BatchIconReference, byte[]?>? readIcon = null)
    {
        _result = result;
        _brushes = services.GetRequiredService<IBrushesProvider>();
        var packages = result.Packages.Length > 0 ? result.Packages : new[]
        {
            new BatchPackageSummary(result.Title, result.TitleId, result.ContentType, result.Version)
            {
                Titles = result.Titles, BaseTitleId = result.BaseTitleId, SystemVersion = result.SystemVersion,
                MasterKey = result.MasterKey, MinimumApplicationVersion = result.MinimumApplicationVersion,
                Distribution = result.Distribution, BuildId = result.BuildId, IsDemo = result.IsDemo,
                Permission = result.Permission, FileSystemPermissions = result.FileSystemPermissions,
                AcidSignatureValidity = result.AcidSignatureValidity, Publisher = result.Publisher,
                DisplayVersion = result.DisplayVersion, Languages = result.Languages,
                SecurityDetails = result.SecurityDetails, PatchLevel = result.PatchLevel
            }
        };
        CnmtContainers = packages.Select(package => new StoredCnmtContainerViewModel(package, services, readIcon)).ToArray();
        SelectedCnmtContainer = CnmtContainers.FirstOrDefault();
        DismissMissingKeysWarningCommand = new RelayCommand(() => { _dismissed = true; NotifyPropertyChanged(nameof(IsMissingKeysWarningVisible)); });
        CopyMissingKeysCommand = new RelayCommand(() => Clipboard.SetText(MissingKeys), () => !string.IsNullOrWhiteSpace(MissingKeys));
        ThemeObserver.Observe(services.GetService<IThemeService>(), this, static view => view.NotifyPropertyChanged(nameof(NcasIntegrityValidityColor)));
    }
    public string FileType => _result.TypeDisplay;
    public string PackageStructure => _result.StructureDisplay;
    public string CompressionType => _result.Compression;
    public string NcaSignature => _result.NcaSignature;
    public NcasIntegrity NcasIntegrity => _result.Integrity;
    public string FileSize => _result.FileSizeDisplay;
    public string CompressionRatio => _result.CompressionRatio is { } ratio
        ? _result.EstimatedUncompressedSize is { } size
            ? $"{ratio:P1} ({size.ToFileSize()} {LocalizationManager.Instance.Current.Keys.FileInfo_Uncompressed})"
            : ratio.ToString("P1") : "—";
    public string SystemUpdateVersion => _result.SystemUpdateVersion;
    public Visibility SystemUpdateVisibility => string.IsNullOrWhiteSpace(SystemUpdateVersion) ? Visibility.Collapsed : Visibility.Visible;
    public Visibility VerifyControlsVisibility => Visibility.Collapsed;
    public Visibility HistoryNoticeVisibility => LogoVisibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
    public Visibility LogoVisibility => HomebrewTitle?.Icon != null ? Visibility.Visible : Visibility.Collapsed;
    public bool IsMissingKeysWarningVisible => !_dismissed && (_result.HasMissingKeys || !string.IsNullOrWhiteSpace(MissingKeys));
    public string MissingKeysHeading => Services.KeysManagement.MissingKeyDiagnostics.Heading(_result.MissingKeyDetails, LocalizationManager.Instance.Current.Keys);
    public string MissingKeysExplanation => Services.KeysManagement.MissingKeyDiagnostics.Explain(_result.MissingKeyDetails, LocalizationManager.Instance.Current.Keys);
    public string MissingKeys => _result.MissingKeyDetails.Length > 0
        ? Services.KeysManagement.MissingKeyDiagnostics.Describe(_result.MissingKeyDetails, LocalizationManager.Instance.Current.Keys) : _result.MissingKeys;
    public ICommand DismissMissingKeysWarningCommand { get; }
    public ICommand CopyMissingKeysCommand { get; }
    public ICommand? VerifyNcasIntegrityCommand => null;
    public bool IsHomebrew => _result.FileType.StartsWith("NRO", StringComparison.OrdinalIgnoreCase);
    public bool HasPackageDetails => !IsHomebrew;
    public bool IsSuperPackage => CnmtContainers.Count > 1;
    public IReadOnlyList<StoredCnmtContainerViewModel> CnmtContainers { get; }
    public StoredCnmtContainerViewModel? SelectedCnmtContainer
    {
        get => _selected;
        set { _selected?.SelectedTitle?.ReleaseImage(); _selected = value; NotifyPropertyChanged(); }
    }
    public string MultiContentPackageToolTip => LocalizationManager.Instance.Current.Keys.MultiContentPackageToolTip.SafeFormat(FileType);
    public bool HasInstalledTitlesWithoutMeta => _result.InstalledTitlesWithoutMeta.Length > 0;
    public string InstalledTitlesWithoutMeta => string.Join(Environment.NewLine, _result.InstalledTitlesWithoutMeta.Select(t => $"{t.TitleId}  {t.Title}  {t.Publisher}  {t.DisplayVersion}"));
    public StoredTitleInfoViewModel? HomebrewTitle => CnmtContainers.FirstOrDefault()?.SelectedTitle;
    public string HomebrewVersion => _result.DisplayVersion;
    public string HomebrewLanguages => _result.Languages;
    public string HomebrewBuildId => _result.BuildId;
    public ICommand? SaveHomebrewImageCommand => CnmtContainers.FirstOrDefault()?.SaveSelectedImageCommand;
    public ICommand? CopyHomebrewImageCommand => CnmtContainers.FirstOrDefault()?.CopySelectedImageCommand;
    public Brush NcasIntegrityValidityColor => NcasIntegrity switch
    {
        NcasIntegrity.Original => _brushes.FontBrushSuccess,
        NcasIntegrity.Incomplete or NcasIntegrity.Modified => _brushes.FontBrushWarning,
        NcasIntegrity.Error or NcasIntegrity.Corrupted => _brushes.FontBrushError,
        _ => _brushes.FontBrushDefault
    };
}

public sealed class StoredCnmtContainerViewModel : ViewModelBase
{
    private readonly BatchPackageSummary _package;
    private readonly IBrushesProvider _brushes;
    private StoredTitleInfoViewModel? _selected;
    public StoredCnmtContainerViewModel(BatchPackageSummary package, IServiceProvider services, Func<BatchIconReference, byte[]?>? readIcon = null)
    {
        _package = package;
        _brushes = services.GetRequiredService<IBrushesProvider>();
        var titles = package.Titles.Length > 0 ? package.Titles : package.Languages.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(language => new BatchTitleSummary(language, package.Title, package.Publisher)).ToArray();
        if (titles.Length == 0) titles = new[] { new BatchTitleSummary("", package.Title, package.Publisher) };
        Titles = titles.Select(title => new StoredTitleInfoViewModel(title, readIcon)).ToArray();
        SaveSelectedImageCommand = new RelayCommand(() =>
        {
            if (SelectedTitle?.LoadTitle() is not { } title) return;
            var command = services.GetRequiredService<Emignatik.NxFileViewer.Commands.ISaveTitleImageCommand>();
            command.Title = title;
            command.Execute(null);
            command.Title = null;
        }, () => SelectedTitle?.HasIcon == true);
        CopySelectedImageCommand = new RelayCommand(() =>
        {
            var command = services.GetRequiredService<Emignatik.NxFileViewer.Commands.ICopyImageCommand>();
            command.Image = SelectedTitle?.Icon;
            command.Execute(null);
            command.Image = null;
        }, () => SelectedTitle?.HasIcon == true);
        SelectedTitle = Titles.FirstOrDefault();
        ThemeObserver.Observe(services.GetService<IThemeService>(), this, static view => view.NotifyPropertyChanged(nameof(SecurityColor)));
    }
    public string DisplayName => _package.DisplayName;
    public string Type => _package.ContentType;
    public string TitleId => _package.TitleId;
    public string TitleVersion => _package.Version;
    public string PatchLevel => _package.PatchLevel;
    public string MinimumSystemVersion => _package.SystemVersion;
    public string BaseTitleId => _package.BaseTitleId;
    public string MasterKey => _package.MasterKey;
    public string MinimumApplicationVersion => _package.MinimumApplicationVersion;
    public string Distribution => _package.Distribution;
    public string BuildID => _package.BuildId;
    public bool? IsDemo => _package.IsDemo;
    public Visibility SecurityVisibility => Visibility.Visible;
    public string SecurityDetails => _package.SecurityDetails;
    public string SecurityLevel => _package.PermissionDisplay;
    public string FileSystemPermissions => _package.FileSystemPermissions;
    public string AcidSignatureValidity => _package.AcidSignatureValidity;
    public Brush SecurityColor => _package.Permission switch
    {
        ProgramSecurityLevel.Safe => _brushes.FontBrushSuccess,
        ProgramSecurityLevel.Unsafe => _brushes.FontBrushWarning,
        ProgramSecurityLevel.Dangerous => _brushes.FontBrushError,
        _ => _brushes.FontBrushDefault
    };
    public Visibility PresentationGroupBoxVisibility => Visibility.Visible;
    public string DisplayVersion => _package.DisplayVersion;
    public IReadOnlyList<StoredTitleInfoViewModel> Titles { get; }
    public StoredTitleInfoViewModel? SelectedTitle
    {
        get => _selected;
        set
        {
            _selected?.ReleaseImage();
            _selected = value;
            NotifyPropertyChanged();
            NotifyPropertyChanged(nameof(LogoVisibility));
            NotifyPropertyChanged(nameof(HistoryNoticeVisibility));
            SaveSelectedImageCommand?.TriggerCanExecuteChanged();
            CopySelectedImageCommand?.TriggerCanExecuteChanged();
        }
    }
    public Visibility LogoVisibility => SelectedTitle?.Icon != null ? Visibility.Visible : Visibility.Collapsed;
    public Visibility HistoryNoticeVisibility => LogoVisibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
    public RelayCommand SaveSelectedImageCommand { get; }
    public RelayCommand CopySelectedImageCommand { get; }
}

public sealed class StoredTitleInfoViewModel(BatchTitleSummary title, Func<BatchIconReference, byte[]?>? readIcon = null)
{
    private BitmapSource? _icon;
    private bool _loaded;
    public string AppName => title.Title;
    public string Publisher => title.Publisher;
    public NacpLanguage Language => Enum.TryParse<NacpLanguage>(title.Language, out var value) ? value : (NacpLanguage)uint.MaxValue;
    public bool HasIcon => title.Icon != null;
    public TitleInfo LoadTitle() => new(new NacpTitleEntry(title.Title, title.Publisher), Language)
    {
        Icon = title.Icon == null ? null : readIcon?.Invoke(title.Icon)
    };
    public void ReleaseImage() { _icon = null; _loaded = false; }
    public BitmapSource? Icon
    {
        get
        {
            if (_loaded) return _icon;
            _loaded = true;
            try
            {
                var bytes = LoadTitle().Icon;
                if (bytes == null) return null;
                using var stream = new MemoryStream(bytes);
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = stream;
                image.EndInit();
                image.Freeze();
                return _icon = image;
            }
            catch (Exception ex) when (ex is IOException or NotSupportedException or ArgumentException or FormatException)
            { return null; }
        }
    }
}