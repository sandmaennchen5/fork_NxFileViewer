using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Emignatik.NxFileViewer.Services.OnlineServices;

internal sealed partial class TitleDbCatalog(string cacheDirectory, Action<string>? logWarning = null)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, (Dictionary<string, OnlineTitleInfo> Titles, DateTime RefreshAfter)> _catalogs = new();

    public async Task<OnlineTitleInfo?> FindAsync(GitHubContentLocation source, string region, string titleId, HttpClient client)
    {
        if (!Regex.IsMatch(region, "^[A-Z]{2}\\.[a-z]{2}$"))
            throw new ArgumentException("Invalid TitleDB region.", nameof(region));
        var path = TitleDbSources.CatalogPath(cacheDirectory, source, region);
        await _gate.WaitAsync();
        try
        {
            var now = DateTime.UtcNow;
            if (_catalogs.TryGetValue(path, out var cached) && now < cached.RefreshAfter)
                return cached.Titles.GetValueOrDefault(titleId);

            Dictionary<string, OnlineTitleInfo>? titles = cached.Titles;
            DateTime refreshed = DateTime.MinValue;
            if (titles == null && File.Exists(path))
            {
                try
                {
                    titles = Parse(await File.ReadAllTextAsync(path));
                    refreshed = File.GetLastWriteTimeUtc(path);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
                {
                    logWarning?.Invoke("Unable to read TitleDB cache: " + ex.Message);
                }
            }
            if (titles == null || now - refreshed >= TimeSpan.FromDays(1))
            {
                try
                {
                    var json = await client.GetStringAsync(source.RawUrl(region + ".json"));
                    titles = Parse(json); // Never replace a good cache with invalid JSON.
                    refreshed = now;
                    try
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                        var tempPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                        try
                        {
                            await File.WriteAllTextAsync(tempPath, json);
                            File.Move(tempPath, path, overwrite: true);
                        }
                        finally { if (File.Exists(tempPath)) File.Delete(tempPath); }
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        logWarning?.Invoke("Unable to save TitleDB cache: " + ex.Message);
                    }
                }
                catch (Exception ex) when (titles != null && ex is HttpRequestException or TaskCanceledException or JsonException)
                {
                    logWarning?.Invoke("TitleDB update unavailable: " + ex.Message + ". Using cached titles.");
                    _catalogs[path] = (titles!, now.AddMinutes(5));
                    return titles!.GetValueOrDefault(titleId);
                }
            }
            _catalogs[path] = (titles!, refreshed.AddDays(1));
            return titles!.GetValueOrDefault(titleId);
        }
        finally { _gate.Release(); }
    }

    public async Task<int> RefreshAsync(GitHubContentLocation source, string region, HttpClient client, CancellationToken token)
    {
        if (!Regex.IsMatch(region, "^[A-Z]{2}\\.[a-z]{2}$")) throw new ArgumentException("Invalid TitleDB region.");
        var path = TitleDbSources.CatalogPath(cacheDirectory, source, region);
        await _gate.WaitAsync(token);
        try
        {
            var json = await client.GetStringAsync(source.RawUrl(region + ".json"), token);
            var titles = Parse(json);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                await File.WriteAllTextAsync(temporary, json, token);
                token.ThrowIfCancellationRequested();
                File.Move(temporary, path, overwrite: true);
                _catalogs[path] = (titles, DateTime.UtcNow.AddDays(1));
                return titles.Count;
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        finally { _gate.Release(); }
    }

}
