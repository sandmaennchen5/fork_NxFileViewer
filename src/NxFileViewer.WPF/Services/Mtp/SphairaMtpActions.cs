using System;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Emignatik.NxFileViewer.FileLoading;
using Emignatik.NxFileViewer.Localization;
using Emignatik.NxFileViewer.Services.BackgroundTask;
using Emignatik.NxFileViewer.Services.BackgroundTask.RunnableImpl;
using Emignatik.NxFileViewer.Services.FileOpening;
using Emignatik.NxFileViewer.Views.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace Emignatik.NxFileViewer.Services.Mtp;

public static class SphairaMtpActions
{
    public static async Task<string?> SelectAndImport(IServiceProvider services, IMainBackgroundTaskRunnerService runner, bool batch = false)
    {
        if (!services.GetRequiredService<Settings.IAppSettings>().MtpActive || runner.IsRunning) return null;
        var logger = services.GetService<ILoggerFactory>()?.CreateLogger("Emignatik.NxFileViewer.Services.Mtp.SphairaMtpActions");
        try
        {
            var dialog = new SphairaMtpWindow(services.GetService<ILogger<SphairaMtpWindow>>()) { Owner = Application.Current?.MainWindow };
            if (dialog.ShowDialog() != true || dialog.Selection == null) return null;
            var selected = dialog.Selection;
            var cacheDirectory = dialog.CacheDirectory;
            var direct = dialog.DirectRead;
            var areas = batch ? SdBatchAreas.From(services.GetRequiredService<Settings.IAppSettings>()) : null;
            return await runner.RunAsync(new RunnableRelay<string>((reporter, token) =>
                direct ? services.GetRequiredService<MtpImportService>().PrepareDirect(selected, reporter, token, areas) :
                services.GetRequiredService<MtpImportService>().Import(selected, reporter, token, cacheDirectory, areas).Run())
                { SupportsCancellation = true, SupportProgress = true });
        }
        catch (OperationCanceledException) { logger?.LogInformation("MTP import canceled."); return null; }
        catch (Exception ex)
        {
            logger?.LogError(ex, "MTP import failed.");
            if (LoadingSpaceWarning.IsDiskFull(ex)) LoadingSpaceWarning.Show("Sphaira (MTP)");
            else ThemedDialog.Notice(ex.Message, LocalizationManager.Instance.Current.Keys.Mtp_Title, MessageBoxImage.Error);
            return null;
        }
    }
    public static string SinglePath(string path)
    {
        if (Directory.Exists(path)) {
            var direct = Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).FirstOrDefault(MtpDirectSource.IsDirect);
            if (direct != null) return direct;
        }
        if (!Directory.Exists(path) || SdCardSource.FindContents(path) != null || Directory.EnumerateFiles(path, "*.nca").Any()) return path;
        return Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).FirstOrDefault(file => MtpImportService.Supports(Path.GetFileName(file)))
            ?? throw new InvalidDataException(LocalizationManager.Instance.Current.Keys.Mtp_NoFiles);
    }
}
