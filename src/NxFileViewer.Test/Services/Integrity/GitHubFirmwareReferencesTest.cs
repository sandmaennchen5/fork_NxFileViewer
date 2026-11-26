using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Emignatik.NxFileViewer.Services;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services.Integrity;

public class GitHubFirmwareReferencesTest
{
    [Fact] public async Task LoadsFreshReferencesForEachCheckWithoutDiskCache()
    {
        var handler = new Handler();
        using var client = new HttpClient(handler);
        var first = await new RepositoryMirrors(client).FirmwareManifestsAsync(new("test/firmware", "main", "hashes"), TestContext.Current.CancellationToken);
        var second = await new RepositoryMirrors(client).FirmwareManifestsAsync(new("test/firmware", "main", "hashes"), TestContext.Current.CancellationToken);
        Assert.NotSame(first, second);
        Assert.Equal(4, handler.Requests);
    }
    [Theory] [InlineData("changed")] [InlineData("invalid")] [InlineData("offline")]
    public async Task InvalidOrUnavailableReferencesDoNotProduceVerifier(string failure)
    {
        using var client = new HttpClient(new Handler { Failure = failure });
        await Assert.ThrowsAsync<InvalidDataException>(() => new RepositoryMirrors(client).FirmwareManifestsAsync(new("test/firmware", "main", "hashes"), TestContext.Current.CancellationToken));
    }
    [Fact] public async Task CancellationIsNotSwallowed()
    {
        using var client = new HttpClient(new Handler());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new RepositoryMirrors(client).FirmwareManifestsAsync(new("test/firmware", "main", "hashes"), new CancellationToken(true)));
    }
    private sealed class Handler : HttpMessageHandler
    {
        public string? Failure { get; init; }
        public int Requests;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            Assert.True(request.Headers.CacheControl!.NoCache);
            if (Failure == "offline") return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            var manifest = Failure == "invalid" ? "{}" : JsonSerializer.Serialize(new { name = "new", file_count = 1,
                files = new System.Collections.Generic.Dictionary<string, object> { ["new.nca"] = new { size = 1, sha256 = new string('a', 64) } } });
            var bytes = Encoding.UTF8.GetBytes(manifest);
            var prefix = Encoding.ASCII.GetBytes("blob " + bytes.Length + "\0");
            var data = new byte[prefix.Length + bytes.Length];
            prefix.CopyTo(data, 0); bytes.CopyTo(data, prefix.Length);
            var sha = Convert.ToHexString(SHA1.HashData(data));
            var listing = JsonSerializer.Serialize(new[] { new { name = "new.json", type = "file", sha } });
            var text = request.RequestUri!.Host == "api.github.com" ? listing : Failure == "changed" ? manifest + " " : manifest;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(text) });
        }
    }
}
