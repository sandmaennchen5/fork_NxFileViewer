using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Emignatik.NxFileViewer.Services;
using Emignatik.NxFileViewer.Services.Updates;
using Emignatik.NxFileViewer.Test.Views;
using NxFileViewer.Avalonia;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services;

[Collection("Avalonia desktop")]
public sealed class ComponentUpdateCheckerTest
{
    [Fact]
    public Task FailedFirmwareCheckDoesNotHideTitleDbUpdateOrInstallFiles() =>
        AvaloniaWorkspace.RunAsync(async () =>
        {
            using var client = new HttpClient(new TitleMetadataHandler());
            var window = new MainWindow();
            try
            {
                var requests = new GitHubUpdateRequests(client);
                await window.CheckDataUpdatesAsync(requests, automatic: false,
                    TestContext.Current.CancellationToken, new RepositoryMirrors(client, requests));
                var title = window.FindControl<TextBlock>("UpdateTitleDbAvailable")!;
                var firmware = window.FindControl<TextBlock>("UpdateFirmwareAvailable")!;
                Assert.Contains("update-available", title.Classes);
                Assert.DoesNotContain("update-available", firmware.Classes);
                Assert.False(string.IsNullOrWhiteSpace(firmware.Text));
                Assert.True(window.FindControl<Control>("HomeExtraUpdateText")!.IsVisible);
                Assert.False(Directory.Exists(ApplicationDataPaths.TitleDb(ViewerDataDirectory.Root)));
                Assert.False(Directory.Exists(ApplicationDataPaths.FirmwareHashes(ViewerDataDirectory.Root)));
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken);

    private sealed class TitleMetadataHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var file = Path.GetFileName(request.RequestUri!.AbsolutePath);
            var isTitleCatalog = file.EndsWith(".json", System.StringComparison.OrdinalIgnoreCase);
            return Task.FromResult(isTitleCatalog
                ? new HttpResponseMessage(HttpStatusCode.OK)
                    { Content = new StringContent("{\"sha\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"}") }
                : new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        }
    }
}
