using System;
using System.IO;
using System.Linq;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Emignatik.NxFileViewer.Services.KeysManagement;
namespace NxFileViewer.Avalonia;
public partial class MainWindow
{
    private async void ChooseTitleKeys(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "title.keys", FileTypeFilter = new[] { FilePickerFileTypes.All } });
        if (files.FirstOrDefault()?.TryGetLocalPath() is { } path) { TitleKeysPath.Text = path; ValidateKeys(sender, e); }
    }
    private void ValidateKeys(object? sender, RoutedEventArgs e)
    {
        try
        {
            KeyValidationText.Text = DescribeKeyValidation(KeyFileValidator.ValidateProdKeys(_keysPath));
            TitleKeyValidationText.Text = DescribeKeyValidation(KeyFileValidator.ValidateTitleKeys(TitleKeysPath.Text));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { KeyValidationText.Text = ex.Message; }
    }
    private string DescribeKeyValidation(KeyFileValidationResult result)
    {
        if (!result.FileExists) return _text.KeysValidation_MissingFile;
        var lines = new System.Collections.Generic.List<string> { string.Format(_text.KeysValidation_ValidEntries, result.ValidEntryCount) };
        if (result.MissingKeys.Count > 0) lines.Add(string.Format(_text.KeysValidation_MissingMasterKeys, string.Join(", ", result.MissingKeys)));
        if (result.InvalidKeys.Count > 0) lines.Add(string.Format(_text.KeysValidation_InvalidMasterKeys, string.Join(", ", result.InvalidKeys)));
        if (result.InvalidLineNumbers.Count > 0) lines.Add(string.Format(_text.KeysValidation_InvalidLines, string.Join(", ", result.InvalidLineNumbers)));
        if (result.HighestValidMasterKeyRevision is { } revision) lines.Add(string.Format(_text.KeysValidation_FirmwareEstimate, revision.ToString("X2"), MasterKeyFirmwareMap.GetSupportedFirmware(revision)));
        return string.Join(Environment.NewLine, lines);
    }
}
