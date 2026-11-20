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
    private void InitializePreferences()
    {
        var preferences = new ViewerPreferences();
        try { preferences = ViewerPreferences.Load(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            SettingsStatus.Text = ex.Message;
        }
        SelectChoice(LanguageChoice, preferences.Language);
        SelectChoice(ThemeChoice, preferences.Theme);
        _keysPath = preferences.ProdKeysPath;
        SettingsKeysPath.Text = _keysPath;
        ApplyPreferences(preferences);
    }

    private static void SelectChoice(ComboBox choice, string value) =>
        choice.SelectedItem = choice.Items.OfType<ComboBoxItem>().FirstOrDefault(item => Equals(item.Tag, value))
            ?? choice.Items[0];

    private void ApplyPreferences(ViewerPreferences preferences)
    {
        _text = GetText(preferences.Language);
        DataContext = _text;
        RequestedThemeVariant = preferences.Theme switch
        {
            "Light" => ThemeVariant.Light,
            "Dark" => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };
    }

    private void SavePreferences(object? sender, RoutedEventArgs e)
    {
        var preferences = new ViewerPreferences(
            (LanguageChoice.SelectedItem as ComboBoxItem)?.Tag as string ?? "",
            (ThemeChoice.SelectedItem as ComboBoxItem)?.Tag as string ?? "",
            _keysPath);
        try
        {
            preferences.Save();
            ApplyPreferences(preferences);
            SettingsStatus.Text = "";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SettingsStatus.Text = ex.Message;
        }
    }
}
