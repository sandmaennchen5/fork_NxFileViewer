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

namespace NxFileViewer.Avalonia;

public partial class MainWindow
{
    private readonly TitleDbCatalog _titleDb = new(ApplicationDataPaths.TitleDb(AppContext.BaseDirectory));
    private ConcurrentDictionary<(string Region, string Repository), IReadOnlyDictionary<string, OnlineTitleInfo>> _localTitleDbs = new();
    private string? _titleDbRepository;
    private string TitleDbRegion => string.IsNullOrWhiteSpace(_preferences.SharedSettings.TitleDbRegion) ? "US.en" : _preferences.SharedSettings.TitleDbRegion;

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
        ++_renameVersion;
        foreach (var item in TitleDbRepositoryChoice.Items.OfType<ComboBoxItem>())
        {
            var source = TitleDbSources.Resolve(item.Tag as string);
            item.Content = TitleDbSources.Label(source, TitleDbSources.CachedDate(source, TitleDbRegion));
        }
    }

    private IReadOnlyDictionary<string, OnlineTitleInfo> LocalTitleDb()
    {
        var repository = TitleDbSources.Resolve(_titleDbRepository).Repository;
        return _localTitleDbs.GetOrAdd((TitleDbRegion, repository), key => TitleDbCatalog.ReadLocal(key.Region, key.Repository));
    }

    private async void TitleDbRepositoryOpened(object? sender, EventArgs e)
    {
        var region = TitleDbRegion;
        await Task.WhenAll(TitleDbRepositoryChoice.Items.OfType<ComboBoxItem>().Select(async item =>
        {
            var source = TitleDbSources.Resolve(item.Tag as string);
            var date = await TitleDbSources.UpdateDateAsync(source, region);
            if (!_closed && region == TitleDbRegion)
                item.Content = TitleDbSources.Label(source, date);
        }));
    }

    private async void RefreshTitleDb(object? sender, RoutedEventArgs e)
    {
        var source = TitleDbSources.Resolve((TitleDbRepositoryChoice.SelectedItem as ComboBoxItem)?.Tag as string);
        var region = TitleDbRegion;
        RefreshTitleDbButton.IsEnabled = false;
        TitleDbStatus.Text = _text.DataUpdate_Working;
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
            var count = await _titleDb.RefreshAsync(source, region, client, CancellationToken.None);
            TitleDbStatus.Text = source.Repository + " · " + string.Format(_text.DataUpdate_TitlesDone, region, count);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException or IOException or UnauthorizedAccessException)
        {
            TitleDbStatus.Text = _text.Update_Failed + " " + ex.Message;
        }
        finally
        {
            _localTitleDbs = new();
            RefreshTitleDbButton.IsEnabled = true;
        }
        var date = await TitleDbSources.UpdateDateAsync(source, region);
        if (!_closed && region == TitleDbRegion)
        {
            var item = TitleDbRepositoryChoice.Items.OfType<ComboBoxItem>().First(choice => Equals(choice.Tag, source.Repository));
            item.Content = TitleDbSources.Label(source, date);
        }
    }
}
