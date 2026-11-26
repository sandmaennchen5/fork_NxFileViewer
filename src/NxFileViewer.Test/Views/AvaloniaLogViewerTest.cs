using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Emignatik.NxFileViewer.Test.Logging;
using Xunit;
using LogWindow = global::NxFileViewer.LogViewer.MainWindow;
using EntryLevel = global::NxFileViewer.LogViewer.EntryLevel;

namespace Emignatik.NxFileViewer.Test.Views;

[Collection("Avalonia desktop")]
public sealed class AvaloniaLogViewerTest
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<global::NxFileViewer.LogViewer.App>().UseHeadless(new AvaloniaHeadlessPlatformOptions());

    [Fact]
    public async Task SearchLevelAndMemoryFiltersUpdateTheDisplayedEntries()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        var session = HeadlessUnitTestSession.StartNew(typeof(AvaloniaLogViewerTest));
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, LogDocumentTest.Sample);
            await session.Dispatch<bool>(async () =>
            {
                var window = new LogWindow();
                try
                {
                    await window.OpenPathAsync(path);
                    var entries = window.FindControl<ListBox>("Entries")!;
                    var search = window.FindControl<TextBox>("SearchBox")!;
                    var level = window.FindControl<ComboBox>("LevelFilter")!;
                    var memory = window.FindControl<CheckBox>("MemoryOnly")!;
                    Assert.Equal(8, entries.Items.Count);

                    search.Text = "gameone.NSZ";
                    level.SelectedItem = level.Items.OfType<LogWindow.LevelChoice>().Single(x => x.Level == EntryLevel.Warning);
                    await WaitForCountAsync(entries, 2, timeout.Token);

                    memory.IsChecked = true;
                    await WaitForCountAsync(entries, 0, timeout.Token);

                    level.SelectedIndex = 0;
                    await WaitForCountAsync(entries, 1, timeout.Token);
                    var detail = window.FindControl<TextBox>("DetailText")!;
                    Assert.Contains("workingSetBytes=1024", detail.Text);
                    return true;
                }
                finally { window.Close(); }
            }, timeout.Token);
        }
        finally
        {
            await Task.Run(session.Dispose, CancellationToken.None);
            File.Delete(path);
        }
    }

    private static async Task WaitForCountAsync(ListBox entries, int count, CancellationToken token)
    {
        while (entries.Items.Count != count)
            await Task.Delay(20, token);
    }
}
