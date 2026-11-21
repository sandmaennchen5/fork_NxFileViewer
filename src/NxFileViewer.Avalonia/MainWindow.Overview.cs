using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Emignatik.NxFileViewer.FileLoading.QuickFileInfoLoading;
using Emignatik.NxFileViewer.Models.Overview;
using Emignatik.NxFileViewer.Services.PackageAnalysis;
using Emignatik.NxFileViewer.Utils;

namespace NxFileViewer.Avalonia;

public partial class MainWindow
{
    private sealed record OverviewLanguage(string Name, Title Title)
    {
        public override string ToString() => Name;
    }

    private Bitmap? _overviewBitmap;

    private void UpdateOverview(PackageFile package)
    {
        OverviewType.Text = package.Type;
        var structure = package.Type is "NSP" or "NSZ"
            ? PackageStructureAnalyzer.AnalyzeNspNames(package.Entries.Select(entry => entry.Name)) : PackageStructure.Unknown;
        OverviewStructure.Text = PackageStructureAnalyzer.GetDisplayName(structure.ToString(), _text);
        OverviewSize.Text = package.Size.ToFileSize();
        OverviewCompression.Text = package.Type is "NSZ" or "XCZ" or "NCZ" ? "NCZ" : "None";
        OverviewRatio.Text = "—";
        OverviewSignature.Text = _text.Signature_Unchecked;
        OverviewIntegrity.Text = _text.NcasIntegrity_Unchecked;
        OverviewPackageChoice.ItemsSource = package.Titles;
        OverviewPackageChoice.IsVisible = package.Titles.Count > 1;
        OverviewPackageChoice.SelectedIndex = package.Titles.Count > 0 ? 0 : -1;
        SetOverviewContent(package.Titles.FirstOrDefault());
    }

    private void OverviewPackageSelected(object? sender, SelectionChangedEventArgs e) =>
        SetOverviewContent(OverviewPackageChoice.SelectedItem as Content);

    private void SetOverviewContent(Content? content)
    {
        if (OverviewContentType == null) return;
        OverviewContentType.Text = content?.Type.ToString() ?? "—";
        OverviewTitleId.Text = content?.TitleId ?? "—";
        OverviewVersion.Text = content?.Version.Version.ToString() ?? "—";
        OverviewSystemVersion.Text = content?.MinimumSystemVersion.ToString() ?? "—";
        OverviewBaseId.Text = content?.ApplicationTitleId ?? "—";
        OverviewMasterKey.Text = content?.KeyGeneration is { } generation ? generation == 0 ? "MasterKey 0 (master_key_00, Key Generation 0)" : $"MasterKey {generation - 1} (master_key_{generation - 1:x2}, Key Generation {generation})" : "—";
        OverviewMinApp.Text = content?.Type == LibHac.Ncm.ContentMetaType.AddOnContent ? content.MinimumApplicationVersion.ToString() : "—";
        OverviewDistribution.Text = content?.Distribution ?? "—";
        OverviewBuildId.Text = content?.BuildId ?? "—";
        OverviewPermissions.Text = content?.FileSystemPermissions is { } permissions ? $"0x{permissions:X16}" : "—";
        OverviewAcid.Text = content?.AcidSignature ?? _text.Signature_Unchecked;
        OverviewSecurity.Text = content?.SecurityLevel switch
        {
            "Safe" => _text.Security_Safe,
            "Unsafe" => _text.Security_Unsafe,
            "Dangerous" => _text.Security_Dangerous,
            _ => _text.Security_Unavailable
        };
        OverviewDisplayVersion.Text = content?.NacpData?.DisplayVersion ?? "—";
        var languages = content?.NacpData?.Titles.Select((title, index) => (title, index))
            .Where(item => item.title != null)
            .Select(item =>
            {
                var key = "Lng_" + (NacpLanguage)item.index;
                var name = _text.TryGetValue(key, out var translated) ? translated : ((NacpLanguage)item.index).ToString();
                return new OverviewLanguage(name, item.title!);
            }).ToArray() ?? Array.Empty<OverviewLanguage>();
        OverviewLanguages.ItemsSource = languages;
        OverviewLanguages.SelectedIndex = languages.Length > 0 ? 0 : -1;
        SetOverviewLanguage(languages.FirstOrDefault());
    }

    private void OverviewLanguageSelected(object? sender, SelectionChangedEventArgs e) =>
        SetOverviewLanguage(OverviewLanguages.SelectedItem as OverviewLanguage);

    private void SetOverviewLanguage(OverviewLanguage? language)
    {
        OverviewTitleName.Text = language?.Title.Name ?? "—";
        OverviewPublisher.Text = language?.Title.Publisher ?? "—";
        OverviewIcon.Source = null;
        _overviewBitmap?.Dispose();
        _overviewBitmap = null;
        if (language?.Title.Icon is not { Length: > 0 } bytes) return;
        try
        {
            using var stream = new MemoryStream(bytes);
            _overviewBitmap = new Bitmap(stream);
            OverviewIcon.Source = _overviewBitmap;
        }
        catch (ArgumentException) { }
    }

    private void ClearOverview()
    {
        OverviewPackageChoice.ItemsSource = null;
        SetOverviewContent(null);
    }
}
