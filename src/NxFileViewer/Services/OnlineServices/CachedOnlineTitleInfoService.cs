using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Emignatik.NxFileViewer.Settings;

namespace Emignatik.NxFileViewer.Services.OnlineServices;

public class CachedOnlineTitleInfoService : ICachedOnlineTitleInfoService
{
    private readonly IOnlineTitleInfoService _onlineTitleInfoService;

    private readonly ConcurrentDictionary<string, (IOnlineTitleInfo Title, DateTime Expires)> _memoryCache = new();
    private readonly IAppSettings? _settings;

    public CachedOnlineTitleInfoService(IOnlineTitleInfoService onlineTitleInfoService, IAppSettings? settings = null)
    {
        _onlineTitleInfoService = onlineTitleInfoService ?? throw new ArgumentNullException(nameof(onlineTitleInfoService));
        _settings = settings;
    }

    public bool IsEnabled { get; set; } = true;

    public async Task<IOnlineTitleInfo?> GetTitleInfoAsync(string titleId)
    {
        if (!IsEnabled)
            return await _onlineTitleInfoService.GetTitleInfoAsync(titleId);

        var cacheKey = $"{_settings?.TitleInfoProvider}|{_settings?.TitleDbRegion}|{_settings?.NLibApiUrl}|{_settings?.TitleInfoApiUrl}|{_settings?.AppLanguage}|{titleId.ToUpperInvariant()}";
        if (_memoryCache.TryGetValue(cacheKey, out var cachedTitleInfo) && DateTime.UtcNow < cachedTitleInfo.Expires)
            return cachedTitleInfo.Title;

        var newTitleInfo = await _onlineTitleInfoService.GetTitleInfoAsync(titleId);
        if (newTitleInfo != null)
            _memoryCache[cacheKey] = (newTitleInfo, DateTime.UtcNow.AddDays(1));

        return newTitleInfo;
    }

}
