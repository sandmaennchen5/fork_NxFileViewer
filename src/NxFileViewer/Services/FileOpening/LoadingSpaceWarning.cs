using System;
using System.IO;
using System.Linq;
using System.Windows;
using Emignatik.NxFileViewer.Localization;
using Emignatik.NxFileViewer.Views.Windows;

namespace Emignatik.NxFileViewer.Services.FileOpening;

public static class LoadingSpaceWarning
{
    public static bool IsDiskFull(Exception exception) =>
        exception is IOException && (exception.HResult & 0xffff) is 112 or 39 ||
        exception is AggregateException aggregate && aggregate.InnerExceptions.Any(IsDiskFull) ||
        exception.InnerException != null && IsDiskFull(exception.InnerException);

    public static void Show(string path)
    {
        Application.Current?.Dispatcher.Invoke(() =>
            ThemedDialog.Notice(LocalizationManager.Instance.Current.Keys.LoadingError_DiskFull +
                Environment.NewLine + Environment.NewLine + path,
                LocalizationManager.Instance.Current.Keys.AppTitle, MessageBoxImage.Warning));
    }
}
