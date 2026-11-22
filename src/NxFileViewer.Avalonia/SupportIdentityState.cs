using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Emignatik.NxFileViewer.Services;
using Emignatik.NxFileViewer.Settings;

namespace NxFileViewer.Avalonia;

internal static class SupportIdentityState
{
    internal static string Cached(AppSettings settings) =>
        SupportIdentityClient.Endpoint(settings.ReportEndpoint) != null &&
        settings.SupportIdEndpoint == settings.ReportEndpoint &&
        Regex.IsMatch(settings.SupportId ?? "", @"\ANX-(?:[A-F0-9]{4}-){4}[A-F0-9]{4}\z") ? settings.SupportId ?? "" : "";

    internal static async Task RefreshAsync(AppSettings settings, CancellationToken cancellation,
        Func<string, CancellationToken, Task<string>>? fetch = null)
    {
        var endpoint = settings.ReportEndpoint;
        if (SupportIdentityClient.Endpoint(endpoint) == null) return;
        var id = await (fetch ?? SupportIdentityClient.RefreshAsync)(endpoint, cancellation);
        cancellation.ThrowIfCancellationRequested();
        if (settings.ReportEndpoint != endpoint) return;
        if (id == null || !Regex.IsMatch(id, @"\ANX-(?:[A-F0-9]{4}-){4}[A-F0-9]{4}\z"))
            throw new IOException("The support service returned an invalid identity.");
        settings.SupportId = id;
        settings.SupportIdEndpoint = endpoint;
    }
}
