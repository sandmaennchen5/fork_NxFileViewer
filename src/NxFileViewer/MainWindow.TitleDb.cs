using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Emignatik.NxFileViewer.Services;
using Emignatik.NxFileViewer.Services.OnlineServices;
using Emignatik.NxFileViewer.Settings;

namespace NxFileViewer.Avalonia;

public partial class MainWindow
{
    private readonly TitleDbCatalog _titleDb = new(ApplicationDataPaths.TitleDb(ViewerDataDirectory.Root));
    private ConcurrentDictionary<(string Region, string Repository), IReadOnlyDictionary<string, OnlineTitleInfo>> _localTitleDbs = new();
    private string? _titleDbRepository;
    private static readonly HttpClient RenameTitleClient = new() { Timeout = TimeSpan.FromSeconds(5) };
    private CachedOnlineTitleInfoService? _onlineTitleNames;
    private string TitleDbRegion => NormalizeTitleDbRegion(_preferences.SharedSettings.TitleDbRegion);

    private static string NormalizeTitleDbRegion(string? region) =>
        region != null && System.Text.RegularExpressions.Regex.IsMatch(region, @"^[A-Z]{2}\.[a-z]{2}\z")
            ? region : TitleDbSources.FallbackRegion;

    private ComboBox CreateTitleDbRegionChoice()
    {
        var region = NormalizeTitleDbRegion(_editedSharedSettings.TitleDbRegion);
        var choice = new ComboBox
        {
            ItemsSource = TitleDbSources.Regions.Contains(region)
                ? TitleDbSources.Regions : TitleDbSources.Regions.Append(region).ToArray(),
            SelectedItem = region,
            HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Left
        };
        _editedSharedSettings.TitleDbRegion = region;
        choice.SelectionChanged += (_, _) =>
        {
            if (choice.SelectedItem is not string selected) return;
            _editedSharedSettings.TitleDbRegion = selected;
            RefreshTitleDbSourceLabels(selected);
        };
        return choice;
    }

    private void InitializeTitleDbChoice(ViewerPreferences preferences)
    {
        if (TitleDbRepositoryChoice.Items.Count == 0)
            foreach (var source in TitleDbSources.All)
                TitleDbRepositoryChoice.Items.Add(new ComboBoxItem { Tag = source.Repository });
        SelectChoice(TitleDbRepositoryChoice, TitleDbSources.Resolve(preferences.TitleDbRepository).Repository);
    }

    private void ApplyTitleDbPreferences(ViewerPreferences preferences)
    {
        _titleDbRepository = TitleDbSources.Resolve(preferences.TitleDbRepository).Repository;
        var configured = preferences.SharedSettings;
        var sources = ExternalSources.Current;
        var lookup = new AppSettings
        {
            AppLanguage = configured.AppLanguage,
            TitleInfoProvider = configured.TitleInfoProvider,
            TitleInfoApiUrl = configured.TitleInfoProvider == TitleInfoProvider.Custom
                ? configured.TitleInfoApiUrl ?? "" : sources.TitleInfoApiUrl,
            NLibApiUrl = sources.NLibApiUrl
        };
        _onlineTitleNames = lookup.TitleInfoProvider == TitleInfoProvider.GitHubTitleDb ? null :
            new CachedOnlineTitleInfoService(new OnlineTitleInfoService(lookup, httpClient: RenameTitleClient,
                cacheDirectory: ApplicationDataPaths.TitleDb(ViewerDataDirectory.Root)), lookup);
        ++_renameVersion;
        RefreshTitleDbSourceLabels(TitleDbRegion);
    }

    private string? LookupTitleName(string titleId)
    {
        if (titleId.Length != 16 || !titleId.All(Uri.IsHexDigit) || titleId.All(c => c == '0')) return null;
        var online = _onlineTitleNames;
        if (online != null)
            return online.GetTitleInfoAsync(titleId).GetAwaiter().GetResult()?.Name;
        return LocalTitleDb().TryGetValue(titleId, out var title) ? title.Name : null;
    }

    private Func<string, string?> CreateRenameTitleLookup()
    {
        var names = new ConcurrentDictionary<string, Lazy<string?>>(StringComparer.OrdinalIgnoreCase);
        return id => names.GetOrAdd(id, key => new Lazy<string?>(() => LookupTitleName(key))).Value;
    }

    private void OpenTitlePage(object? sender, RoutedEventArgs e)
    {
        OpenTitlePageForId(OverviewTitleId.Text);
    }

    private void OpenTitlePageForId(string? titleId)
    {
        if (titleId is not { Length: 16 } || !titleId.All(Uri.IsHexDigit)) return;
        try
        {
            var address = _preferences.SharedSettings.TitlePageUrl.Replace("{TitleId}", titleId, StringComparison.OrdinalIgnoreCase);
            if (!Uri.TryCreate(address, UriKind.Absolute, out var url) || url.Scheme is not ("http" or "https"))
                throw new ArgumentException(_text.SettingsView_TitlePageUrl);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            StatusText.Text = string.Format(_text.OpenTitleWebPage_Failed, error.Message);
        }
    }

    private void RefreshTitleDbSourceLabels(string region)
    {
        foreach (var item in TitleDbRepositoryChoice.Items.OfType<ComboBoxItem>())
        {
            var source = TitleDbSources.Resolve(item.Tag as string);
            item.Content = TitleDbSources.Label(source, TitleDbSources.CachedDate(source, region));
        }
    }

    private IReadOnlyDictionary<string, OnlineTitleInfo> LocalTitleDb()
    {
        var repository = TitleDbSources.Resolve(_titleDbRepository).Repository;
        return _localTitleDbs.GetOrAdd((TitleDbRegion, repository), key =>
        {
            var regional = TitleDbCatalog.ReadLocal(key.Region, key.Repository, ViewerDataDirectory.Root);
            if (key.Region == TitleDbSources.FallbackRegion) return regional;
            var merged = new Dictionary<string, OnlineTitleInfo>(regional, StringComparer.OrdinalIgnoreCase);
            foreach (var entry in TitleDbCatalog.ReadLocal(TitleDbSources.FallbackRegion, key.Repository, ViewerDataDirectory.Root))
                merged.TryAdd(entry.Key, entry.Value);
            return merged;
        });
    }

    private async void TitleDbRepositoryOpened(object? sender, EventArgs e)
    {
        var region = NormalizeTitleDbRegion(_editedSharedSettings.TitleDbRegion);
        await Task.WhenAll(TitleDbRepositoryChoice.Items.OfType<ComboBoxItem>().Select(async item =>
        {
            var source = TitleDbSources.Resolve(item.Tag as string);
            var date = await TitleDbSources.UpdateDateAsync(source, region);
            if (!_closed && region == NormalizeTitleDbRegion(_editedSharedSettings.TitleDbRegion))
                item.Content = TitleDbSources.Label(source, date);
        }));
    }

    private async void RefreshTitleDb(object? sender, RoutedEventArgs e)
    {
        var fromUpdates = ReferenceEquals(sender, UpdateTitleDbButton);
        var source = TitleDbSources.Resolve(fromUpdates
            ? _preferences.TitleDbRepository
            : (TitleDbRepositoryChoice.SelectedItem as ComboBoxItem)?.Tag as string);
        var region = fromUpdates ? TitleDbRegion : NormalizeTitleDbRegion(_editedSharedSettings.TitleDbRegion);
        if (!RefreshTitleDbButton.IsEnabled) return;
        RefreshTitleDbButton.IsEnabled = UpdateTitleDbButton.IsEnabled = false;
        TitleDbStatus.Text = _text.DataUpdate_Working;
        if (fromUpdates) UpdateStatus.Text = TitleDbStatus.Text;
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
            using var workspaceJob = LockWorkspace(SettingsTab);
            var count = await _titleDb.RefreshAsync(source, region, client, CancellationToken.None);
            if (region != TitleDbSources.FallbackRegion)
                count += await _titleDb.RefreshAsync(source, TitleDbSources.FallbackRegion, client, CancellationToken.None);
            if (source.Repository == TitleDbSources.Resolve(_preferences.TitleDbRepository).Repository && region == TitleDbRegion)
            {
                SetUpdateMessage(UpdateTitleDbAvailable, null);
                RefreshHomeUpdateNotice();
            }
            TitleDbStatus.Text = source.Repository + " · " + string.Format(_text.DataUpdate_TitlesDone, region, count);
            if (fromUpdates) UpdateStatus.Text = TitleDbStatus.Text;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException or IOException or UnauthorizedAccessException)
        {
            TitleDbStatus.Text = _text.Update_Failed + " " + ex.Message;
            if (fromUpdates) UpdateStatus.Text = TitleDbStatus.Text;
        }
        finally
        {
            _localTitleDbs = new();
            RefreshTitleDbButton.IsEnabled = UpdateTitleDbButton.IsEnabled = true;
            if (!_closed) RefreshUpdateDataSummaries();
        }
        var date = await TitleDbSources.UpdateDateAsync(source, region);
        if (!_closed && region == NormalizeTitleDbRegion(_editedSharedSettings.TitleDbRegion))
        {
            var item = TitleDbRepositoryChoice.Items.OfType<ComboBoxItem>().First(choice => Equals(choice.Tag, source.Repository));
            item.Content = TitleDbSources.Label(source, date);
        }
    }
}
