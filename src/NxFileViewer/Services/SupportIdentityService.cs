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
    private static readonly HttpClient Client = new(new HttpClientHandler { AllowAutoRedirect = false })
    {
        Timeout = TimeSpan.FromSeconds(20)
    };

    internal static string Participant()
    {
        if (!OperatingSystem.IsWindows())
            throw new IOException("A system identity provider is required for this platform.");
        try
        {
            using var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var key = machine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
            var value = key?.GetValue("MachineGuid") as string;
            if (!Guid.TryParse(value, out var guid))
                throw new IOException("The Windows installation identity is unavailable.");
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                "NxFileViewer.Support.Windows.v1\0" + guid.ToString("D")))).ToLowerInvariant();
        }
        catch (Exception error) when (error is UnauthorizedAccessException or System.Security.SecurityException)
        {
            throw new IOException("The Windows installation identity is unavailable.", error);
        }
    }

    internal static string CachedSupportId() =>
        App.Features.SupportIdEndpoint == App.Features.ReportEndpoint ? App.Features.SupportId : "";

    internal static async Task<string> RefreshAsync(CancellationToken cancellation)
    {
        var endpoint = CommunityPollService.Endpoint(App.Features.ReportEndpoint)
            ?? throw new IOException("The support service is not configured.");
        using var response = await Client.PostAsJsonAsync(endpoint, new { mode = "identity", participant = Participant() }, cancellation);
        response.EnsureSuccessStatusCode();
        var profile = await response.Content.ReadFromJsonAsync<Profile>(cancellation);
        if (profile is null || !System.Text.RegularExpressions.Regex.IsMatch(profile.SupportId ?? "",
                "\\ANX-(?:[A-F0-9]{4}-){4}[A-F0-9]{4}\\z"))
            throw new IOException("The support service returned an invalid identity.");
        App.Features.SupportId = profile.SupportId!;
        App.Features.SupportIdEndpoint = endpoint.OriginalString;
        return profile.SupportId!;
    }

    internal static string SearchUrl(string supportId) =>
        "https://github.com/sandmaennchen5/fork_NxFileViewer/issues?q=" +
        Uri.EscapeDataString("is:issue \"" + supportId + "\"");

    public sealed record Profile(string SupportId);
}
