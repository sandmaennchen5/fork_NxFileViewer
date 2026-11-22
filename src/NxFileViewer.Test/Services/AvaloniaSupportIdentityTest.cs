using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Emignatik.NxFileViewer.Settings;
using NxFileViewer.Avalonia;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services;

public sealed class AvaloniaSupportIdentityTest
{
    private const string Endpoint = "https://example.test/report.php";
    private const string Identity = "NX-1234-5678-9ABC-DEF0-1234";
    private static AppSettings Settings() => new() { ReportEndpoint = Endpoint, SupportId = Identity, SupportIdEndpoint = Endpoint };

    [Fact]
    public async Task RefreshStoresIdInTheActiveSettings()
    {
        var settings = new AppSettings { ReportEndpoint = Endpoint };
        await SupportIdentityState.RefreshAsync(settings, CancellationToken.None, (endpoint, _) =>
        { Assert.Equal(Endpoint, endpoint); return Task.FromResult(Identity); });
        Assert.Equal(Identity, SupportIdentityState.Cached(settings));
        Assert.Equal(Endpoint, settings.SupportIdEndpoint);
    }

    [Fact]
    public async Task OfflineFailurePreservesCachedIdentity()
    {
        var settings = Settings();
        await Assert.ThrowsAsync<IOException>(() => SupportIdentityState.RefreshAsync(settings, CancellationToken.None,
            (_, _) => Task.FromException<string>(new IOException("offline"))));
        Assert.Equal(Identity, SupportIdentityState.Cached(settings));
    }

    [Fact]
    public async Task ChangedEndpointDoesNotReceiveTheOldResponse()
    {
        var settings = Settings();
        await SupportIdentityState.RefreshAsync(settings, CancellationToken.None, (_, _) =>
        { settings.ReportEndpoint = "https://other.test/report.php"; return Task.FromResult(Identity); });
        Assert.Equal("", SupportIdentityState.Cached(settings));
        Assert.Equal(Endpoint, settings.SupportIdEndpoint);
    }

    [Fact]
    public async Task DisabledEndpointDoesNotFallBackToDefaultService()
    {
        var settings = Settings(); settings.ReportEndpoint = "";
        await SupportIdentityState.RefreshAsync(settings, CancellationToken.None, (_, _) => throw new Exception("Unexpected request"));
        Assert.Equal("", SupportIdentityState.Cached(settings));
    }

    [Fact]
    public async Task InvalidResponseCannotReplaceCachedIdentity()
    {
        var settings = Settings();
        await Assert.ThrowsAsync<IOException>(() => SupportIdentityState.RefreshAsync(settings, CancellationToken.None,
            (_, _) => Task.FromResult("invalid")));
        Assert.Equal(Identity, SupportIdentityState.Cached(settings));
    }

    [Fact]
    public async Task CancellationCannotReplaceCachedIdentity()
    {
        var settings = Settings(); using var cancellation = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => SupportIdentityState.RefreshAsync(settings, cancellation.Token,
            (_, _) => { cancellation.Cancel(); return Task.FromResult("NX-AAAA-BBBB-CCCC-DDDD-EEEE"); }));
        Assert.Equal(Identity, SupportIdentityState.Cached(settings));
    }
}
