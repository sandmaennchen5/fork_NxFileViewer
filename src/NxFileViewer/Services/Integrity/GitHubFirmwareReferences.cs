using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Emignatik.NxFileViewer.Services.Updates;

namespace Emignatik.NxFileViewer.Services.Integrity;

/// <summary>Reads a verified, complete snapshot from the first usable firmware mirror.</summary>
public static class GitHubFirmwareReferences
{
    public static async Task<FirmwareIntegrityVerifier> LoadAsync(HttpClient client, CancellationToken cancellationToken, GitHubUpdateRequests? requests = null)
    {
        var manifests = await LoadManifestsAsync(client, cancellationToken, requests).ConfigureAwait(false);
        return new FirmwareIntegrityVerifier(manifests.Values);
    }
    public static Task<IReadOnlyDictionary<string, string>> LoadManifestsAsync(HttpClient client, CancellationToken cancellationToken, GitHubUpdateRequests? requests = null) =>
        new RepositoryMirrors(client, requests).FirmwareManifestsAsync(ExternalSources.Current.FirmwareHashes, cancellationToken);
}
