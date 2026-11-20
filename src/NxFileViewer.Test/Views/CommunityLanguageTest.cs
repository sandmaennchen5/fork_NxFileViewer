using System.Globalization;
using Emignatik.NxFileViewer.Localization;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Views;

[Collection("Workspace UI")]
public sealed class CommunityLanguageTest
{
    [Theory]
    [InlineData("de-DE", "en-US", false)]
    [InlineData("en-US", "de-DE", true)]
    public void SelectedLanguageOverridesWindowsLanguage(string systemLanguage, string appLanguage, bool expectedGerman)
    {
        var manager = LocalizationManager.Instance;
        var previous = manager.Current;
        var previousCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(systemLanguage);
            manager.Current = manager.AvailableLocalizations.FindByCultureName(appLanguage)!;
            var type = typeof(LocalizationManager).Assembly.GetType("Emignatik.NxFileViewer.Localization.CommunityLanguage")!;
            Assert.Equal(expectedGerman, (bool)type.GetProperty("IsGerman")!.GetValue(null)!);

            manager.Current = manager.AvailableLocalizations.FindByCultureName(expectedGerman ? "en-US" : "de-DE")!;
            Assert.Equal(!expectedGerman, (bool)type.GetProperty("IsGerman")!.GetValue(null)!);
        }
        finally
        {
            manager.Current = previous;
            CultureInfo.CurrentUICulture = previousCulture;
        }
    }
}
