using System.IO;
using System;
using System.ComponentModel;
using System.Text.Json.Serialization;
using Emignatik.NxFileViewer.Models.Overview;

namespace Emignatik.NxFileViewer.Services.Integrity;

public sealed record BatchIntegrityResult(
    string FilePath,
    string FileType,
    string PackageType,
    string Structure,
    string Compression,
    NcasIntegrity Integrity,
    string? Error) : INotifyPropertyChanged
{
    // WPF indexes rows by hash. UI subscriptions and expansion must not change it.
    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(FilePath);
    [JsonIgnore] public string SourceGroupKey => BatchSourceGrouping.Key(FilePath);
    public BatchPackageSummary[] Packages { get; init; } = Array.Empty<BatchPackageSummary>();
    [JsonIgnore] public bool HasMultiplePackages => Packages.Length > 1;
    private bool _packagesExpanded;
    [JsonIgnore]
    public bool PackagesExpanded
    {
        get => _packagesExpanded;
        set
        {
            if (_packagesExpanded == value) return;
            _packagesExpanded = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PackagesExpanded)));
        }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    public bool IsNand { get; init; }
    public string? NandDetails { get; init; }
    public bool? NamingMatches { get; init; }
    public string? ProposedPath { get; init; }
    public string? NamingError { get; init; }
    public string? Diagnostic => Error ?? NamingError;
    public string NamingStatus => NamingError != null ? Localization.LocalizationManager.Instance.Current.Keys.RenamingTool_StatusError
        : NamingMatches == true ? Localization.LocalizationManager.Instance.Current.Keys.BatchNaming_Matches
        : NamingMatches == false ? Localization.LocalizationManager.Instance.Current.Keys.BatchNaming_Differs : "—";
    public string? SourceFingerprint { get; init; }
    [JsonIgnore] public string StructureDisplay => PackageAnalysis.PackageStructureAnalyzer.GetDisplayName(Structure, Localization.LocalizationManager.Instance.Current.Keys);
    [JsonIgnore] public string TypeDisplay => IsFirmware ? "FIRMWARE" : PackageType;
    public string ContentType { get; init; } = "";
    public Security.ProgramSecurityLevel? Permission { get; init; }
    [JsonIgnore] public string PermissionDisplay => BatchContentMetadata.PermissionDisplay(Permission);
    public string BaseTitleId { get; init; } = "";
    public string MinimumApplicationVersion { get; init; } = "";
    public string SystemUpdateVersion { get; init; } = "";
    public bool? IsDemo { get; init; }
    public string FileSystemPermissions { get; init; } = "";
    public string AcidSignatureValidity { get; init; } = "";
    public LibHac.Common.Validity? NcaSignatureValidity { get; init; }
    [JsonIgnore] public string NcaSignature => NcaSignatureValidity switch
    {
        LibHac.Common.Validity.Valid => Localization.LocalizationManager.Instance.Current.Keys.Signature_Passed,
        LibHac.Common.Validity.Invalid => Localization.LocalizationManager.Instance.Current.Keys.Signature_NotPassed,
        _ => Localization.LocalizationManager.Instance.Current.Keys.Signature_Unchecked
    };
    public MissingKey[] MissingKeyDetails { get; init; } = Array.Empty<MissingKey>();
    public string MissingKeys { get; init; } = "";
    [JsonIgnore] public string FileSizeDisplay => FileSize is { } size ? Utils.FileSizeHelper.ToFileSize(size) : "—";
    [JsonIgnore] public string SourceSizeDisplay => SourceSize is { } size ? Utils.FileSizeHelper.ToFileSize(size) : "—";
    [JsonIgnore] public string OutputSizeDisplay => OutputSize is { } size ? Utils.FileSizeHelper.ToFileSize(size) : "—";
    public BatchTitleSummary[] Titles { get; init; } = Array.Empty<BatchTitleSummary>();
    public string Title { get; init; } = "";
    public string TitleId { get; init; } = "";
    public string Publisher { get; init; } = "";
    public string Version { get; init; } = "";
    public long? VersionNumber => long.TryParse(Version, out var value) ? value : null;
    public string DisplayVersion { get; init; } = "";
    public string SystemVersion { get; init; } = "";
    public string MasterKey { get; init; } = "";
    public string BuildId { get; init; } = "";
    public string Distribution { get; init; } = "";
    public string Languages { get; init; } = "";
    public long? FileSize { get; init; }
    public double? CompressionRatio { get; init; }
    public string? ConversionStatus { get; init; }
    public bool ConversionFailed { get; init; }
    public string? ConvertedPath { get; init; }
    public long? SourceSize { get; init; }
    public long? OutputSize { get; init; }
    public bool HasMissingKeys { get; init; }
    public bool IsFirmware { get; init; }
    public string? FirmwareDetails { get; init; }
    public string FileName => Path.GetFileName(FilePath.TrimEnd('/'));
}

public sealed record BatchPackageSummary(string Title, string TitleId, string Type, string Version)
{
    [JsonIgnore] public string ContentType => Type switch { "Application" => "Base", "Patch" => "Update", "AddOnContent" => "DLC", _ => Type };
    [JsonIgnore] public string DisplayName => $"{ContentType} — {TitleId} — {Title}";
    public BatchTitleSummary[] Titles { get; init; } = Array.Empty<BatchTitleSummary>();
    [JsonIgnore] public long? VersionNumber => long.TryParse(Version, out var value) ? value : null;
    public string BaseTitleId { get; init; } = "";
    public string MinimumApplicationVersion { get; init; } = "";
    public bool? IsDemo { get; init; }
    public string FileSystemPermissions { get; init; } = "";
    public string AcidSignatureValidity { get; init; } = "";
    public string SystemVersion { get; init; } = "";
    public string MasterKey { get; init; } = "";
    public string BuildId { get; init; } = "";
    public string Distribution { get; init; } = "";
    public Security.ProgramSecurityLevel? Permission { get; init; }
    [JsonIgnore] public string PermissionDisplay => BatchContentMetadata.PermissionDisplay(Permission);
    public string Publisher { get; init; } = "";
    public string DisplayVersion { get; init; } = "";
    public string Languages { get; init; } = "";
}

public sealed record BatchTitleSummary(string Language, string Title, string Publisher);
