using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Headless;
using NxFileViewer.Avalonia;

namespace Emignatik.NxFileViewer.Test.Views;

internal static class AvaloniaWorkspace
{
    internal static Task RunAsync(Action action, CancellationToken token) =>
        RunAsync(() => { action(); return Task.CompletedTask; }, token);

    internal static async Task RunAsync(Func<Task> action, CancellationToken token)
    {
        var data = Directory.CreateTempSubdirectory("nx-workspace-");
        var previousRoot = AppContext.GetData("NxFileViewer.DataDirectory");
        try
        {
            AppContext.SetData("NxFileViewer.DataDirectory", data.FullName);
            new ViewerPreferences { CheckUpdatesOnStart = false }.Save();
            var session = HeadlessUnitTestSession.StartNew(typeof(AvaloniaBatchHistoryTest));
            try { await session.Dispatch<bool>(async () => { await action(); return true; }, token); }
            finally { await Task.Run(session.Dispose, CancellationToken.None); }
        }
        finally
        {
            AppContext.SetData("NxFileViewer.DataDirectory", previousRoot);
            data.Delete(recursive: true);
        }
    }
}
