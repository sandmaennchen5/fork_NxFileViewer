using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Emignatik.NxFileViewer.Localization;
using Emignatik.NxFileViewer.Services.Nand;
using Emignatik.NxFileViewer.Services.Nsz;
using Emignatik.NxFileViewer.Settings;

namespace Emignatik.NxFileViewer.Services.Updates;

public sealed record ComponentUpdateResult(string Name, string Status, bool UpdateAvailable);

public sealed class ComponentUpdateChecker(NszPluginManager nsz, NandPluginManager nand, IAppSettings settings,
    HttpClient? httpClient = null, string? applicationDirectory = null)
{
    public async Task<string> CheckAsync(CancellationToken token) => string.Join(Environment.NewLine, (await CheckResultsAsync(token)).Select(r => r.Name + ": " + r.Status));

    public async Task<ComponentUpdateResult[]> CheckResultsAsync(CancellationToken token, bool automatic = false)
    {
        var directory = applicationDirectory ?? AppContext.BaseDirectory;
        var keys = LocalizationManager.Instance.Current.Keys;
        var requests = GitHubUpdateRequests.For(httpClient);
        var notices = new System.Collections.Concurrent.ConcurrentDictionary<string, string>();
        async Task<ComponentUpdateResult> Check(string label, Func<Task<(string Status, bool Available)>> action)
        {
            try { var result = await action(); return new(label, result.Status + (notices.TryGetValue(label, out var notice) ? " — " + notice : ""), result.Available); }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (GitHubRateLimitException ex) { return new(label, ex.Message, false); }
            catch (Exception) { return new(label, keys.Update_Failed, false); }
        }
        async Task<JsonDocument> Get(string url, string label)
        {
            var response = await requests.GetAsync(url, automatic, token);
            if (response.Notice.Length > 0) notices[label] = response.Notice;
            return JsonDocument.Parse(response.Body);
        }
        async Task<(string Status, bool Available)> Plugin(string executable, string? version, string repository, string label)
        {
            if (!File.Exists(executable)) return (keys.Component_NotInstalled, false);
            if (version == null) return (keys.Component_CustomVersion, false);
            using var release = await Get("https://api.github.com/repos/" + repository + "/releases/latest", label);
            var latest = release.RootElement.GetProperty("tag_name").GetString()!;
            var available = IsNewer(version, latest);
            return (available ? string.Format(keys.Update_Available, latest) : keys.Update_Current, available);
        }
        var checks = new List<Task<ComponentUpdateResult>>();
        if (settings.NszPluginActive)
            checks.Add(Check("NSZ", () => Plugin(nsz.ExecutablePath, nsz.InstalledVersion, ExternalSources.Current.NszRepository, "NSZ")));
        if (settings.NandPluginActive)
            checks.Add(Check("NxNandManager", () => Plugin(nand.ExecutablePath, nand.InstalledVersion, ExternalSources.Current.NandRepository, "NxNandManager")));
        foreach (var region in new[] { settings.TitleDbRegion, "US.en" }.Distinct())
        {
            var captured = region;
            checks.Add(Check("Title DB " + region, async () =>
            {
                if (!Regex.IsMatch(captured, @"^[A-Z]{2}\.[a-z]{2}\z")) throw new InvalidDataException();
                using var entry = await Get(ExternalSources.Current.TitleDb.ApiUrl(captured + ".json"), "Title DB " + captured);
                var path = Path.Combine(ApplicationDataPaths.TitleDb(directory), captured + ".json");
                var available = !MatchesGitBlob(path, entry.RootElement.GetProperty("sha").GetString()!);
                return (available ? keys.Component_UpdateAvailable : keys.Update_Current, available);
            }));
        }
        if (settings.FirmwareCheckActive) checks.Add(Check(keys.DataUpdate_Firmware, async () =>
        {
            var mirrors = new RepositoryMirrors(httpClient, requests, automatic);
            var entries = await mirrors.FirmwareListingAsync(ExternalSources.Current.FirmwareHashes, token);
            if (mirrors.LastNotice.Length > 0) notices[keys.DataUpdate_Firmware] = mirrors.LastNotice;
            foreach (var entry in entries)
            {
                token.ThrowIfCancellationRequested();
                if (!MatchesGitBlob(Path.Combine(ApplicationDataPaths.FirmwareHashes(directory), entry.Name), entry.Sha)) return (keys.Component_UpdateAvailable, true);
            }
            return (keys.Update_Current, false);
        }));
        return await Task.WhenAll(checks);
    }

    public static bool IsNewer(string installed, string latest)
    {
        static Version? Parse(string value) => Version.TryParse(Regex.Match(value, @"\d+(?:\.\d+){1,3}").Value, out var version) ? version : null;
        var before = Parse(installed); var after = Parse(latest);
        return before != null && after != null ? after > before : !string.Equals(installed.TrimStart('v', 'V'), latest.TrimStart('v', 'V'), StringComparison.OrdinalIgnoreCase);
    }

    public static bool MatchesGitBlob(string path, string sha)
    {
        if (sha.Length != 40 || !sha.All(Uri.IsHexDigit)) throw new InvalidDataException("Invalid Git blob hash.");
        if (!File.Exists(path)) return false;
        using var file = File.OpenRead(path);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
        hash.AppendData(Encoding.ASCII.GetBytes("blob " + file.Length + "\0"));
        var buffer = new byte[65536];
        int read;
        while ((read = file.Read(buffer)) > 0) hash.AppendData(buffer.AsSpan(0, read));
        return Convert.ToHexString(hash.GetHashAndReset()).Equals(sha, StringComparison.OrdinalIgnoreCase);
    }
}
