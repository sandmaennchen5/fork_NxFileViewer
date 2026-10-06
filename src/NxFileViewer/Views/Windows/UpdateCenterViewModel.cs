using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Emignatik.NxFileViewer.Localization;
using Emignatik.NxFileViewer.Services.BackgroundTask;
using Emignatik.NxFileViewer.Services.BackgroundTask.RunnableImpl;
using Emignatik.NxFileViewer.Services.Integrity;
using Emignatik.NxFileViewer.Services.Nsz;
using Emignatik.NxFileViewer.Services.OnlineServices;
using Emignatik.NxFileViewer.Services.Updates;
using Emignatik.NxFileViewer.Settings;
using Emignatik.NxFileViewer.Utils.MVVM;
using Emignatik.NxFileViewer.Utils.MVVM.Commands;
using Microsoft.Extensions.Logging;

namespace Emignatik.NxFileViewer.Views.Windows;

public sealed class UpdateCenterViewModel : ViewModelBase
{
    private readonly IMainBackgroundTaskRunnerService _background;
    private readonly ITitleDbUpdater _titles;
    private readonly ICachedOnlineTitleInfoService _cache;
    private readonly IAppSettings _settings;
    private readonly ILogger<UpdateCenterViewModel> _logger;
    private string _titleStatus = "";
    private string _firmwareStatus = "";
    public UpdateCenterViewModel(IMainBackgroundTaskRunnerService background, ITitleDbUpdater titles,
        ICachedOnlineTitleInfoService cache, IAppSettings settings, NszActions nsz,
        ViewerUpdateActions viewer, ILogger<UpdateCenterViewModel> logger)
    {
        _background = background; _titles = titles; _cache = cache; _settings = settings; _logger = logger;
        Nsz = nsz; Viewer = viewer;
        RefreshTitlesCommand = new RelayCommand(RefreshTitles, () => !background.IsRunning);
        CheckFirmwareCommand = new RelayCommand(() => RefreshFirmware(false), () => !background.IsRunning);
        SaveFirmwareCommand = new RelayCommand(() => RefreshFirmware(true), () => !background.IsRunning);
        background.PropertyChanged += (_, _) =>
        {
            RefreshTitlesCommand.TriggerCanExecuteChanged(true);
            CheckFirmwareCommand.TriggerCanExecuteChanged(true);
            SaveFirmwareCommand.TriggerCanExecuteChanged(true);
        };
    }
    public IMainBackgroundTaskRunnerService Background => _background;
    public NszActions Nsz { get; }
    public ViewerUpdateActions Viewer { get; }
    public RelayCommand RefreshTitlesCommand { get; }
    public RelayCommand CheckFirmwareCommand { get; }
    public RelayCommand SaveFirmwareCommand { get; }
    public string TitleStatus { get => _titleStatus; private set { _titleStatus = value; NotifyPropertyChanged(); } }
    public string FirmwareStatus { get => _firmwareStatus; private set { _firmwareStatus = value; NotifyPropertyChanged(); } }
    private async void RefreshTitles()
    {
        if (!RefreshTitlesCommand.CanExecute(null)) return;
        TitleStatus = LocalizationManager.Instance.Current.Keys.DataUpdate_Working;
        var region = _settings.TitleDbRegion;
        try
        {
            var count = await _background.RunAsync(new RunnableRelay<int>((progress, token) =>
            {
                progress.SetText("TitleDB: " + region);
                try
                {
                    var total = _titles.RefreshTitleDbAsync(region, token).GetAwaiter().GetResult();
                    if (region != "US.en") total += _titles.RefreshTitleDbAsync("US.en", token).GetAwaiter().GetResult();
                    return total;
                }
                finally { _cache.ClearCache(); }
            }) { SupportsCancellation = true });
            TitleStatus = string.Format(LocalizationManager.Instance.Current.Keys.DataUpdate_TitlesDone, region, count);
        }
        catch (OperationCanceledException) { TitleStatus = LocalizationManager.Instance.Current.Keys.Update_Cancelled; }
        catch (Exception ex) { TitleStatus = LocalizationManager.Instance.Current.Keys.Update_Failed + " " + ex.Message; _logger.LogWarning(ex, "TitleDB refresh failed."); }
    }
    private async void RefreshFirmware(bool save)
    {
        if (_background.IsRunning) return;
        FirmwareStatus = LocalizationManager.Instance.Current.Keys.DataUpdate_Working;
        try
        {
            var count = await _background.RunAsync(new RunnableRelay<int>((progress, token) =>
            {
                progress.SetText(LocalizationManager.Instance.Current.Keys.Firmware_LoadingOnline);
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                timeout.CancelAfter(TimeSpan.FromMinutes(2));
                var manifests = GitHubFirmwareReferences.LoadManifestsAsync(client, timeout.Token).GetAwaiter().GetResult();
                if (save) FirmwareReferenceStore.Save(manifests, AppContext.BaseDirectory, token);
                return manifests.Count;
            }) { SupportsCancellation = true });
            FirmwareStatus = string.Format(save ? LocalizationManager.Instance.Current.Keys.DataUpdate_FirmwareSaved :
                LocalizationManager.Instance.Current.Keys.DataUpdate_FirmwareChecked, count);
        }
        catch (OperationCanceledException) { FirmwareStatus = LocalizationManager.Instance.Current.Keys.Update_Cancelled; }
        catch (Exception ex) { FirmwareStatus = LocalizationManager.Instance.Current.Keys.Update_Failed + " " + ex.Message; _logger.LogWarning(ex, "Firmware reference refresh failed."); }
    }
}
