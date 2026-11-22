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

public static class SupportIdentityClient
{
    private static readonly HttpClient Client = new(new HttpClientHandler { AllowAutoRedirect = false })
    {
        Timeout = TimeSpan.FromSeconds(20)
    };

    public static string Participant()
    {
        if (OperatingSystem.IsLinux()) return LinuxParticipant(File.ReadAllText, PersistentUserIdentity);
        if (OperatingSystem.IsMacOS()) return MacParticipant(PersistentUserIdentity);
        if (!OperatingSystem.IsWindows()) return HashIdentity("Unix.User", PersistentUserIdentity());
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

    internal static string LinuxParticipant(Func<string, string> read, Func<string> fallback)
    {
        foreach (var path in new[] { "/etc/machine-id", "/var/lib/dbus/machine-id" })
        {
            try
            {
                var value = read(path).Trim().ToLowerInvariant();
                if (System.Text.RegularExpressions.Regex.IsMatch(value, @"\A[a-f0-9]{32}\z") && value != new string('0', 32))
                    return HashIdentity("Linux", value);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
        }
        return HashIdentity("Linux.User", fallback());
    }

    private static string HashIdentity(string platform, string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("NxFileViewer.Support." + platform + ".v1\0" + value))).ToLowerInvariant();

    internal static string MacParticipant(Func<string> identity) => HashIdentity("MacOS.User", identity());

    internal static string UserIdentityDirectory(string? localData, string? profile, bool macOS)
    {
        var root = !string.IsNullOrWhiteSpace(localData) && Path.IsPathFullyQualified(localData) ? localData : null;
        if (root is null && !string.IsNullOrWhiteSpace(profile) && Path.IsPathFullyQualified(profile))
            root = macOS ? Path.Combine(profile,"Library","Application Support") : Path.Combine(profile,".local","share");
        if (root is null) throw new IOException("The user profile directory for the support identity is unavailable.");
        return Path.Combine(root,"NxFileViewerFork");
    }

    private static string PersistentUserIdentity()
    {
        // DoNotVerify also resolves a path when Application Support has not been created yet.
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify);
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile, Environment.SpecialFolderOption.DoNotVerify);
        if (string.IsNullOrWhiteSpace(profile)) profile = Environment.GetEnvironmentVariable("HOME");
        return PersistentUserIdentity(UserIdentityDirectory(local,profile,OperatingSystem.IsMacOS()));
    }

    internal static string PersistentUserIdentity(string directory)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "support-identity");
        if (!File.Exists(path))
        {
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, Guid.NewGuid().ToString("N"));
                try { File.Move(temporary, path); }
                catch (IOException) when (File.Exists(path)) { }
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        var value = File.ReadAllText(path).Trim();
        if (!Guid.TryParseExact(value, "N", out _)) throw new IOException("The saved support identity is invalid.");
        return value;
    }

    public static Uri? Endpoint(string? value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == "https" && uri.UserInfo.Length == 0 && uri.Query.Length == 0 && uri.Fragment.Length == 0 ? uri : null;
    public static async Task<string> RefreshAsync(string endpointValue, CancellationToken cancellation)
    {
        var endpoint = Endpoint(endpointValue)
            ?? throw new IOException("The support service is not configured.");
        using var response = await Client.PostAsJsonAsync(endpoint, new { mode = "identity", participant = Participant() }, cancellation);
        response.EnsureSuccessStatusCode();
        var profile = await response.Content.ReadFromJsonAsync<Profile>(cancellation);
        if (profile is null || !System.Text.RegularExpressions.Regex.IsMatch(profile.SupportId ?? "",
                "\\ANX-(?:[A-F0-9]{4}-){4}[A-F0-9]{4}\\z"))
            throw new IOException("The support service returned an invalid identity.");
        return profile.SupportId!;
    }

    public sealed record Profile(string SupportId);
}
