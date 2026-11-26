using System.Globalization;
using System.Threading.Tasks;
using NxFileViewer.Avalonia;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Views;

[Collection("Avalonia desktop")]
public sealed class CommunityLanguageTest
{
    [Theory]
    [InlineData("de-DE", "en", "Polls")]
    [InlineData("en-US", "de", "Umfragen")]
    public Task SelectedLanguageOverridesWindowsLanguage(string systemLanguage, string appLanguage, string expected) =>
        AvaloniaWorkspace.RunAsync(() =>
        {
            var previous = CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(systemLanguage);
                (ViewerPreferences.Load() with { Language = appLanguage }).Save();
                var main = new MainWindow();
                try
                {
                    var polls = main.CreatePollsWindow();
                    try { Assert.Equal(expected, polls.Title); }
                    finally { polls.Close(); }
                }
                finally { main.Close(); }
            }
            finally { CultureInfo.CurrentUICulture = previous; }
        }, TestContext.Current.CancellationToken);
}
