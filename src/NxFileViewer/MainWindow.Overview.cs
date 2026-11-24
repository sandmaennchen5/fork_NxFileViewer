using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Media.Imaging;
using Emignatik.NxFileViewer.FileLoading.QuickFileInfoLoading;
using Emignatik.NxFileViewer.Models.Overview;
using Emignatik.NxFileViewer.Services.PackageAnalysis;
using Emignatik.NxFileViewer.Utils;

namespace NxFileViewer.Avalonia;

public partial class MainWindow
{
    private sealed record OverviewLanguage(string Name, string AppName, string Publisher, byte[]? Icon)
    {
        public override string ToString() => Name;
    }

    private Bitmap? _overviewBitmap;
    private PackageFile? _overviewPackage;
    private sealed record OverviewPackageOption(int Number, Content Content)
    {
        public override string ToString() => $"{Number} {Content.Type}";
    }

    private void UpdateOverview(PackageFile package)
    {
        _overviewPackage = package;
        OverviewMissingKeys.IsVisible = package.MissingKeys.Length > 0;
        OverviewMissingKeysText.Text = string.Join(Environment.NewLine, package.MissingKeys);
        var multiPackage = package.IsMultiPackage;
        OverviewType.Text = package.DisplayType;
        var structure = package.Type is "NSP" or "NSZ"
            ? PackageStructureAnalyzer.AnalyzeNspNames(package.Entries.Select(entry => entry.Name)) : PackageStructure.Unknown;
        OverviewStructure.Text = PackageStructureAnalyzer.GetDisplayName(structure.ToString(), _text);
        OverviewSize.Text = package.Size.ToFileSize();
        OverviewCompression.Text = package.Compression ?? (package.Type is "NSZ" or "XCZ" or "NCZ" ? "NCZ" : "None");
        OverviewRatio.Text = package.UncompressedSize is > 0 and var original
            ? $"{(double)package.Size / original:P1} ({original.ToFileSize()})" : "—";
        OverviewSignature.Text = _text.Signature_Unchecked;
        OverviewIntegrity.Text = _text.NcasIntegrity_Unchecked;
        OverviewIntegrity.Classes.Remove("overview-success");
        OverviewPackageChoice.ItemsSource = package.Titles.Select((content, index) => new OverviewPackageOption(index + 1, content)).ToArray();
        OverviewMultiPackage.IsVisible = multiPackage;
        ToolTip.SetTip(OverviewMultiPackageTip, string.Format(_text.MultiContentPackageToolTip, OverviewType.Text));
        OverviewPackageChoice.SelectedIndex = package.Titles.Count > 0 ? 0 : -1;
        SetOverviewContent(package.Titles.FirstOrDefault());
        if (package.Homebrew is { } homebrew)
        {
            OverviewStructure.Text = _text.PackageStructure_Homebrew;
            OverviewBuildId.Text = homebrew.BuildId;
            OverviewDisplayVersion.Text = homebrew.DisplayVersion;
            var languages = homebrew.Titles.Select(title => new OverviewLanguage(
                OverviewLanguageName(title.Language), title.AppName, title.Publisher, title.Icon)).ToArray();
            OverviewLanguages.ItemsSource = languages;
            OverviewLanguages.SelectedIndex = languages.Length > 0 ? 0 : -1;
            SetOverviewLanguage(languages.FirstOrDefault() ?? new OverviewLanguage("", "", "", homebrew.Icon));
        }
        RefreshFilePackageActions();
    }

    private void OverviewPackageSelected(object? sender, SelectionChangedEventArgs e) =>
        SetOverviewContent((OverviewPackageChoice.SelectedItem as OverviewPackageOption)?.Content);

    private void SetOverviewContent(Content? content)
    {
        if (OverviewContentType == null) return;
        OverviewContentType.Text = content?.Type.ToString() ?? "—";
        OverviewTitleId.Text = content?.TitleId ?? "—";
        var isSinglePackage = _overviewPackage?.Titles.Count == 1;
        FileTitlePageButton.IsVisible = content != null && isSinglePackage;
        OverviewTitlePageButton.IsVisible = content != null && OverviewMultiPackage.IsVisible;
        OverviewVersion.Text = content?.Version?.Version.ToString() ?? "—";
        OverviewSystemVersion.Text = content?.MinimumSystemVersion?.ToString() ?? "—";
        OverviewBaseId.Text = content?.ApplicationTitleId ?? "—";
        OverviewMasterKey.Text = content?.KeyGeneration is { } generation ? generation == 0 ? "MasterKey 0 (master_key_00, Key Generation 0)" : $"MasterKey {generation - 1} (master_key_{generation - 1:x2}, Key Generation {generation})" : "—";
        OverviewMinApp.Text = content?.Type == LibHac.Ncm.ContentMetaType.AddOnContent ? content.MinimumApplicationVersion?.ToString() ?? "—" : "—";
        OverviewDistribution.Text = content?.Distribution ?? "—";
        OverviewBuildId.Text = content?.BuildId ?? "—";
        OverviewDemo.IsChecked = content?.NacpData?.IsDemo == true;
        OverviewPermissions.Text = content?.FileSystemPermissions is { } permissions ? $"0x{permissions:X16}" : "—";
        OverviewAcid.Text = content?.AcidSignature ?? _text.Signature_Unchecked;
        OverviewSecurity.Text = content?.SecurityLevel switch
        {
            "Safe" => _text.Security_Safe,
            "Unsafe" => _text.Security_Unsafe,
            "Dangerous" => _text.Security_Dangerous,
            _ => _text.Security_Unavailable
        };
        OverviewSecurity.Classes.Set("overview-success", content?.SecurityLevel == "Safe");
        OverviewDisplayVersion.Text = content?.NacpData?.DisplayVersion ?? "—";
        var languages = GetOverviewLanguages(content);
        OverviewLanguages.ItemsSource = languages;
        OverviewLanguages.SelectedIndex = languages.Length > 0 ? 0 : -1;
        SetOverviewLanguage(languages.FirstOrDefault());
    }

    private OverviewLanguage[] GetOverviewLanguages(Content? content) =>
        content?.NacpData?.Titles.Select((title, index) => (title, index))
            .Where(item => item.title != null)
            .Select(item =>
            {
                var name = OverviewLanguageName((NacpLanguage)item.index);
                return new OverviewLanguage(name, item.title!.Name, item.title.Publisher, item.title.Icon);
            }).ToArray() ?? Array.Empty<OverviewLanguage>();
    private string OverviewLanguageName(NacpLanguage language) =>
        _text.TryGetValue("Lng_" + language, out var translated) ? translated : language.ToString();

    private async void CopyOverviewMissingKeys(object? sender, RoutedEventArgs e)
    {
        if (Clipboard != null && OverviewMissingKeysText.Text is { Length: > 0 } missing)
            await Clipboard.SetTextAsync(missing);
    }

    private async void CopyOverviewIcon(object? sender, RoutedEventArgs e)
    {
        if (Clipboard != null && OverviewIcon.Source is Bitmap bitmap)
            await Clipboard.SetBitmapAsync(bitmap);
    }

    private async void SaveOverviewIcon(object? sender, RoutedEventArgs e)
    {
        if (OverviewIcon.Source is not Bitmap bitmap) return;
        using var buffer = new MemoryStream();
        bitmap.Save(buffer, PngBitmapEncoderOptions.Default);
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = _text.ContextMenu_SaveImage, SuggestedFileName = "title.png", DefaultExtension = "png",
            ShowOverwritePrompt = true, FileTypeChoices = [new FilePickerFileType("PNG") { Patterns = ["*.png"] }]
        });
        if (file?.TryGetLocalPath() is not { } path) return;
        try { await File.WriteAllBytesAsync(path, buffer.ToArray()); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { StatusText.Text = error.Message; }
    }

    private void OverviewLanguageSelected(object? sender, SelectionChangedEventArgs e) =>
        SetOverviewLanguage(OverviewLanguages.SelectedItem as OverviewLanguage);

    private void SetOverviewLanguage(OverviewLanguage? language)
    {
        OverviewTitleName.Text = language?.AppName ?? "—";
        OverviewPublisher.Text = language?.Publisher ?? "—";
        OverviewIcon.Source = null;
        _overviewBitmap?.Dispose();
        _overviewBitmap = null;
        if (language?.Icon is not { Length: > 0 } bytes) return;
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
        _overviewPackage = null;
        OverviewMissingKeys.IsVisible = false;
        OverviewMissingKeysText.Text = "";
        OverviewMultiPackage.IsVisible = false;
        OverviewPackageChoice.ItemsSource = null;
        SetOverviewContent(null);
        RefreshFilePackageActions();
    }

    private void RefreshFilePackageActions()
    {
        var supported = _preferences.NszActive && _overviewPackage is { } package &&
            !Emignatik.NxFileViewer.Services.Mtp.MtpDirectSource.IsDirect(package.Path);
        FileCompressButton.IsVisible = supported && _overviewPackage?.Type is "NSP" or "XCI";
        FileDecompressButton.IsVisible = supported && _overviewPackage?.Type is "NSZ" or "XCZ";
        FileCompressButton.IsEnabled = FileCompressButton.IsVisible && _batchCancellation == null;
        FileDecompressButton.IsEnabled = FileDecompressButton.IsVisible && _batchCancellation == null;
    }
}
