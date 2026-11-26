using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media;
using Emignatik.NxFileViewer.Services;
using NxFileViewer.Avalonia;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Views;

[Collection("Avalonia desktop")]
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
        var poll = JsonSerializer.Deserialize<CommunityPollService.Poll>(
            "{\"id\":1,\"question\":\"Test\",\"closed\":false,\"selectedOption\":null,\"options\":[]" + fields + "}",
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(expected, poll.AwaitingResponse);
    }

    [Fact]
    public Task ChoiceTextInheritsForegroundChanges() =>
        AvaloniaWorkspace.RunAsync(() =>
        {
            var window = new Window { Foreground = Brushes.White };
            try
            {
                var label = PollsWindow.PollText("Readable choice");
                window.Content = new StackPanel { Children = { label } };
                Assert.Equal(Colors.White, Assert.IsAssignableFrom<ISolidColorBrush>(label.Foreground).Color);
                window.Foreground = Brushes.Black;
                Assert.Equal(Colors.Black, Assert.IsAssignableFrom<ISolidColorBrush>(label.Foreground).Color);
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken);
}
