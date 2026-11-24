using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace Emignatik.NxFileViewer.Services;

internal static class SupportIdentityService
{
    internal static string Participant() => SupportIdentityClient.Participant();
    internal static string CachedSupportId() => App.Features.SupportIdEndpoint == App.Features.ReportEndpoint ? App.Features.SupportId : "";
    internal static async Task<string> RefreshAsync(CancellationToken cancellation)
    {
        var endpoint = App.Features.ReportEndpoint;
        var id = await SupportIdentityClient.RefreshAsync(endpoint, cancellation);
        App.Features.SupportId = id; App.Features.SupportIdEndpoint = endpoint;
        return id;
    }
    internal static string SearchUrl(string supportId) =>
        "https://github.com/sandmaennchen5/fork_NxFileViewer/issues?q=" +
        Uri.EscapeDataString("is:issue \"" + supportId + "\"");

}
