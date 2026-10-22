using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Emignatik.NxFileViewer.Services.Updates;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services.Updates;

public sealed class ViewerSourceOptionsTest : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ViewerOptions-" + Guid.NewGuid().ToString("N"));
    private const string Github = "github:owner/repo";
    private const string Mirror = "manifest:https://mirror.example/index.json";
    public ViewerSourceOptionsTest() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handle) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => handle(request, token);
    }
    private void State(Dictionary<string, ViewerUpdateSourceOptions> options) => File.WriteAllText(
        Path.Combine(_root, ViewerUpdateMetadata.StateName), JsonSerializer.Serialize(
            new ViewerUpdateState(1, ViewerPackageLayout.ApplicationId, new[] { Github, Mirror }, Array.Empty<string>()) { UpdateSourceOptions = options }));
    private static HttpResponseMessage Release() => new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(
        new ViewerReleaseManifest(1, ViewerPackageLayout.ApplicationId, "4.2.0", new[] {
            new ViewerReleasePackage("x64", false, "Application.zip") { Sha256 = new string('1', 64) }
        }))) };

    [Fact]
    public async Task DisabledSourceMakesNoRequests()
    {
        State(new() { [Github] = new(false, 1) });
        using var client = new HttpClient(new Handler((request, _) => {
            Assert.Equal("mirror.example", request.RequestUri!.Host);
            return Task.FromResult(Release());
        }));
        var release = await new ViewerUpdateService(client, _root).CheckAsync(new Version(4, 1, 0), "x64", TestContext.Current.CancellationToken);
        Assert.Equal("4.2.0", release!.Tag);
    }

    [Fact]
    public async Task EverySourceCanBeDisabledWithoutFallback()
    {
        State(new() { [Github] = new(false), [Mirror] = new(false) });
        using var client = new HttpClient(new Handler((_, _) => throw new Exception("No request expected")));
        Assert.Null(await new ViewerUpdateService(client, _root).CheckAsync(new Version(4, 1, 0), "x64", TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("github:owner/repo")]
    [InlineData("manifest:https://slow.example/index.json")]
    public async Task SourceBudgetCancelsRequestAndChecksMirror(string first)
    {
        var state = new ViewerUpdateState(1, ViewerPackageLayout.ApplicationId, new[] { first, Mirror }, Array.Empty<string>()) {
            UpdateSourceOptions = new() { [first] = new(true, 1) }
        };
        File.WriteAllText(Path.Combine(_root, ViewerUpdateMetadata.StateName), JsonSerializer.Serialize(state));
        var cancelled = false;
        using var client = new HttpClient(new Handler(async (request, token) => {
            if (request.RequestUri!.Host == "mirror.example") return Release();
            try { await Task.Delay(Timeout.Infinite, token); }
            catch (OperationCanceledException) { cancelled = true; throw; }
            throw new Exception("Expected cancellation");
        }));
        var release = await new ViewerUpdateService(client, _root).CheckAsync(new Version(4, 1, 0), "x64", TestContext.Current.CancellationToken);
        Assert.True(cancelled);
        Assert.Equal("4.2.0", release!.Tag);
    }

    [Fact]
    public async Task UserCancellationDoesNotContinueToMirror()
    {
        State(new());
        using var cancel = new CancellationTokenSource();
        using var client = new HttpClient(new Handler(async (request, token) => {
            Assert.Equal("api.github.com", request.RequestUri!.Host);
            cancel.Cancel();
            await Task.Delay(Timeout.Infinite, token);
            return Release();
        }));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ViewerUpdateService(client, _root)
            .CheckAsync(new Version(4, 1, 0), "x64", cancel.Token));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(301)]
    public void RejectsInvalidTimeout(int seconds) => Assert.Throws<InvalidDataException>(() =>
        ViewerUpdateMetadata.ValidateSourceOptions(new[] { Github }, new() { [Github] = new(true, seconds) }));

    [Fact]
    public void RejectsUnknownOrDuplicateSourceOptions()
    {
        Assert.Throws<InvalidDataException>(() => ViewerUpdateMetadata.ValidateSourceOptions(new[] { Github }, new() { [Mirror] = new() }));
        Assert.Throws<InvalidDataException>(() => ViewerUpdateMetadata.ValidateSourceOptions(new[] { Github },
            new() { [Github] = new(), ["owner/repo"] = new() }));
    }

    [Fact]
    public void LegacyStateUsesDefaultsAndAliasesMatch()
    {
        State(new());
        var state = ViewerUpdateMetadata.ReadState(_root);
        Assert.Equal(new ViewerUpdateSourceOptions(true, 30), ViewerUpdateMetadata.SourceOptions(state, ViewerUpdateSource.Parse(Github)));
        state = state with { UpdateSourceOptions = new() { ["owner/repo"] = new(false, 12) } };
        ViewerUpdateMetadata.ValidateSourceOptions(state.UpdateSources, state.UpdateSourceOptions);
        Assert.Equal(new ViewerUpdateSourceOptions(false, 12), ViewerUpdateMetadata.SourceOptions(state, ViewerUpdateSource.Parse(Github)));
    }

    [Fact]
    public void InstalledPackageAndStagedStatePreserveOptions()
    {
        var package = new ViewerPackageManifest(1, ViewerPackageLayout.ApplicationId, "App.exe") {
            Version = "4.2.0", UpdateSources = new[] { Github }, UpdateSourceOptions = new() { [Github] = new(false, 17) }
        };
        var json = JsonSerializer.Serialize(package);
        File.WriteAllText(Path.Combine(_root, ViewerPackageLayout.ManifestName), json);
        Assert.Equal(new ViewerUpdateSourceOptions(false, 17), ViewerUpdateMetadata.ReadState(_root).UpdateSourceOptions[Github]);
        var directory = Path.Combine(_root, "staging");
        var payload = Path.Combine(directory, "payload");
        Directory.CreateDirectory(payload);
        File.WriteAllText(Path.Combine(payload, ViewerPackageLayout.ManifestName), json);
        var prepared = ViewerUpdateMetadata.Prepare(new PreparedViewerUpdate(directory, "App.exe"), _root, "4.1.0", "4.2.0", TestContext.Current.CancellationToken);
        var staged = JsonSerializer.Deserialize<ViewerUpdateState>(File.ReadAllText(Path.Combine(payload, ViewerUpdateMetadata.StateName)))!;
        Assert.Equal(new ViewerUpdateSourceOptions(false, 17), staged.UpdateSourceOptions[Github]);
        Assert.False(File.Exists(Path.Combine(_root, ViewerUpdateMetadata.StateName)));
        Assert.Contains(prepared.Files, file => file.Path == ViewerUpdateMetadata.StateName);
    }

    [Fact]
    public void MissingOptionFieldsUseDefaults()
    {
        var options = JsonSerializer.Deserialize<ViewerUpdateSourceOptions>("{}")!;
        Assert.Equal(new ViewerUpdateSourceOptions(true, 30), options);
    }
}
