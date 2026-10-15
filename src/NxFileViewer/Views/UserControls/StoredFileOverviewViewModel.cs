using System;
using System.Collections.Generic;
using System.Linq;
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
    public StoredFileOverviewViewModel(BatchIntegrityResult result, IServiceProvider services)
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
                DisplayVersion = result.DisplayVersion, Languages = result.Languages
            }
        };
        CnmtContainers = packages.Select(package => new StoredCnmtContainerViewModel(package, services)).ToArray();
        SelectedCnmtContainer = CnmtContainers.FirstOrDefault();
        DismissMissingKeysWarningCommand = new RelayCommand(() => { _dismissed = true; NotifyPropertyChanged(nameof(IsMissingKeysWarningVisible)); });
        CopyMissingKeysCommand = new RelayCommand(() => Clipboard.SetText(MissingKeys), () => !string.IsNullOrWhiteSpace(MissingKeys));
        ThemeObserver.Observe(services.GetService<IThemeService>(), this, static view => view.NotifyPropertyChanged(nameof(NcasIntegrityValidityColor)));
    }
    public string FileType => _result.TypeDisplay;
    public string PackageStructure => _result.Structure;
    public string CompressionType => _result.Compression;
    public string NcaSignature => _result.NcaSignature;
    public NcasIntegrity NcasIntegrity => _result.Integrity;
    public string FileSize => _result.FileSizeDisplay;
    public string CompressionRatio => _result.CompressionRatio?.ToString("P1") ?? "—";
    public string SystemUpdateVersion => _result.SystemUpdateVersion;
    public Visibility SystemUpdateVisibility => string.IsNullOrWhiteSpace(SystemUpdateVersion) ? Visibility.Collapsed : Visibility.Visible;
    public Visibility VerifyControlsVisibility => Visibility.Collapsed;
    public Visibility HistoryNoticeVisibility => Visibility.Visible;
    public Visibility LogoVisibility => Visibility.Collapsed;
    public bool IsMissingKeysWarningVisible => !_dismissed && (_result.HasMissingKeys || !string.IsNullOrWhiteSpace(MissingKeys));
    public string MissingKeys => _result.MissingKeys;
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
        set { _selected = value; NotifyPropertyChanged(); }
    }
    public string MultiContentPackageToolTip => LocalizationManager.Instance.Current.Keys.MultiContentPackageToolTip.SafeFormat(FileType);
    public bool HasInstalledTitlesWithoutMeta => false;
    public string InstalledTitlesWithoutMeta => "";
    public StoredTitleInfoViewModel? HomebrewTitle => CnmtContainers.FirstOrDefault()?.SelectedTitle;
    public string HomebrewVersion => _result.DisplayVersion;
    public string HomebrewLanguages => _result.Languages;
    public string HomebrewBuildId => _result.BuildId;
    public ICommand? SaveHomebrewImageCommand => null;
    public ICommand? CopyHomebrewImageCommand => null;
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
    public StoredCnmtContainerViewModel(BatchPackageSummary package, IServiceProvider services)
    {
        _package = package;
        _brushes = services.GetRequiredService<IBrushesProvider>();
        var titles = package.Titles.Length > 0 ? package.Titles : package.Languages.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(language => new BatchTitleSummary(language, package.Title, package.Publisher)).ToArray();
        if (titles.Length == 0) titles = new[] { new BatchTitleSummary("", package.Title, package.Publisher) };
        Titles = titles.Select(title => new StoredTitleInfoViewModel(title)).ToArray();
        SelectedTitle = Titles.FirstOrDefault();
        ThemeObserver.Observe(services.GetService<IThemeService>(), this, static view => view.NotifyPropertyChanged(nameof(SecurityColor)));
    }
    public string DisplayName => _package.DisplayName;
    public string Type => _package.ContentType;
    public string TitleId => _package.TitleId;
    public string TitleVersion => _package.Version;
    public string PatchLevel => "";
    public string MinimumSystemVersion => _package.SystemVersion;
    public string BaseTitleId => _package.BaseTitleId;
    public string MasterKey => _package.MasterKey;
    public string MinimumApplicationVersion => _package.MinimumApplicationVersion;
    public string Distribution => _package.Distribution;
    public string BuildID => _package.BuildId;
    public bool? IsDemo => _package.IsDemo;
    public Visibility SecurityVisibility => Visibility.Visible;
    public string SecurityDetails => "";
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
        set { _selected = value; NotifyPropertyChanged(); }
    }
    public Visibility LogoVisibility => Visibility.Collapsed;
    public Visibility HistoryNoticeVisibility => Visibility.Visible;
    public ICommand? SaveSelectedImageCommand => null;
    public ICommand? CopySelectedImageCommand => null;
}

public sealed class StoredTitleInfoViewModel(BatchTitleSummary title)
{
    public string AppName => title.Title;
    public string Publisher => title.Publisher;
    public NacpLanguage Language => Enum.TryParse<NacpLanguage>(title.Language, out var value) ? value : (NacpLanguage)uint.MaxValue;
    public BitmapSource? Icon => null;
}
