using System;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Media;
using Emignatik.NxFileViewer.Logging;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Views;

public sealed class PollInteractionTest
{
    [Theory]
    [InlineData("", true)]
    [InlineData(",\"declined\":true", false)]
    [InlineData(",\"hasResponded\":true", false)]
    [InlineData(",\"needsResponse\":false", false)]
    [InlineData(",\"canParticipate\":false", false)]
    public void ReminderRespectsTheServerParticipationState(string fields, bool expected)
    {
        var type = typeof(SessionFileLog).Assembly.GetType("Emignatik.NxFileViewer.Services.CommunityPollService+Poll", true)!;
        var model = JsonSerializer.Deserialize("{\"id\":1,\"question\":\"Test\",\"closed\":false,\"selectedOption\":null,\"options\":[]" + fields + "}", type, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(expected, (bool)type.GetProperty("AwaitingResponse")!.GetValue(model)!);
    }

    [Fact]
    public void ChoiceTextTracksDarkAndLightThemeResources()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var type = typeof(SessionFileLog).Assembly.GetType("Emignatik.NxFileViewer.Views.Windows.PollsWindow", true)!;
                var text = (TextBlock)type.GetMethod("PollText", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, new object[] { "Readable choice", 14d })!;
                var root = new StackPanel();
                root.Resources["Control.Static.Foreground"] = Brushes.White;
                root.Children.Add(text);
                Assert.Equal(Colors.White, ((SolidColorBrush)text.Foreground).Color);
                root.Resources["Control.Static.Foreground"] = Brushes.Black;
                Assert.Equal(Colors.Black, ((SolidColorBrush)text.Foreground).Color);
            }
            catch (Exception error) { failure = error; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)), "Theme test did not complete.");
        if (failure is not null) throw failure;
    }
}
