using System;
using System.Runtime.InteropServices;
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
        var initialized = false;
        var item = IntPtr.Zero;
        try
        {
            Marshal.ThrowExceptionForHR(CoInitializeEx(IntPtr.Zero, 2)); // Apartment threaded.
            initialized = true;
            Marshal.ThrowExceptionForHR(SHParseDisplayName(path, IntPtr.Zero, out item, 0, out _));
            var shell = GetShellWindow();
            if (shell != IntPtr.Zero)
            {
                GetWindowThreadProcessId(shell, out var processId);
                if (processId != 0) AllowSetForegroundWindow(processId);
            }
            // cidl=0 selects the full item PIDL in its parent folder.
            Marshal.ThrowExceptionForHR(SHOpenFolderAndSelectItems(item, 0, IntPtr.Zero, 0));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, LocalizationManager.Instance.Current.Keys.OpenFileLocation_Failed_Log.SafeFormat(path, ex.Message));
        }
        finally
        {
            if (item != IntPtr.Zero) Marshal.FreeCoTaskMem(item);
            if (initialized) CoUninitialize();
        }
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHParseDisplayName(string name, IntPtr bindContext, out IntPtr item, uint attributesIn, out uint attributesOut);
    [DllImport("shell32.dll")]
    private static extern int SHOpenFolderAndSelectItems(IntPtr folder, uint count, IntPtr items, uint flags);
    [DllImport("ole32.dll")]
    private static extern int CoInitializeEx(IntPtr reserved, uint apartment);
    [DllImport("ole32.dll")]
    private static extern void CoUninitialize();
    [DllImport("user32.dll")]
    private static extern IntPtr GetShellWindow();
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AllowSetForegroundWindow(uint processId);
}