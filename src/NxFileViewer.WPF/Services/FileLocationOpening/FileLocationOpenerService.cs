using System;
using System.Windows;
using System.Windows.Threading;
using Emignatik.NxFileViewer.Localization;
using Microsoft.Extensions.Logging;

namespace Emignatik.NxFileViewer.Services.FileLocationOpening;

public class FileLocationOpenerService : IFileLocationOpenerService
{
    private readonly ILogger _logger;
    public FileLocationOpenerService(ILoggerFactory loggerFactory)
    {
        _logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger(GetType());
    }
    public void OpenFileLocationSafe(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;
        // Finish closing the context menu before handing focus to Explorer.
        // Otherwise WPF can reactivate the viewer after the shell window opens.
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher != null) dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => Open(filePath)));
        else Open(filePath);
    }
    private void Open(string path)
    {
        try
        {
            WindowsFileLocation.Reveal(path);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, LocalizationManager.Instance.Current.Keys.OpenFileLocation_Failed_Log.SafeFormat(path, ex.Message));
        }
    }
}
