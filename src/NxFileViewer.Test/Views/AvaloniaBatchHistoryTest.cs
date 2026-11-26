using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using NxFileViewer.Avalonia;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Views;

[Collection("Avalonia desktop")]
public sealed class AvaloniaBatchHistoryTest
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure(() => new global::NxFileViewer.Avalonia.App(null, null, null, null))
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());

    [Fact]
    public async Task ResumeRestoresOptionsAndOnlyReusesUnchangedFiles()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        var session = HeadlessUnitTestSession.StartNew(typeof(AvaloniaBatchHistoryTest));
        var data = Directory.CreateTempSubdirectory("nx-batch-history-");
        var previousRoot = AppContext.GetData("NxFileViewer.DataDirectory");
        AppContext.SetData("NxFileViewer.DataDirectory", data.FullName);
        try
        {
            await session.Dispatch<bool>(async () =>
            {
                var source = data.CreateSubdirectory("source");
                var history = Path.Combine(ViewerDataDirectory.Root, "History");
                var window = new MainWindow();
                MainWindow? reopened = null;
                try
                {
                    var nested = source.CreateSubdirectory("nested");
                    var unchanged = Path.Combine(source.FullName, "unchanged.nsp");
                    var changed = Path.Combine(nested.FullName, "changed.nsp");
                    var added = Path.Combine(nested.FullName, "added.nsp");
                    var package = new byte[16];
                    "PFS0"u8.CopyTo(package);
                    File.WriteAllBytes(unchanged, package);
                    File.WriteAllBytes(changed, package);

                    var start = window.FindControl<Button>("BatchStartButton")!;
                    window.FindControl<TextBox>("BatchInputPath")!.Text = source.FullName;
                    window.FindControl<CheckBox>("BatchRecursive")!.IsChecked = true;
                    await UntilAsync(() => start.IsEnabled, timeout.Token);
                    Click(start);
                    await UntilAsync(() => start.IsEnabled && Directory.Exists(history) &&
                        Directory.GetFiles(history, "scan-*.zip").Any(), timeout.Token);
                    var archivePath = Assert.Single(Directory.GetFiles(history, "scan-*.zip"));
                    using (var zip = ZipFile.Open(archivePath, ZipArchiveMode.Update))
                    {
                        var entry = zip.GetEntry("scan.json")!;
                        JsonNode scan;
                        using (var stream = entry.Open()) scan = JsonNode.Parse(stream)!;
                        var rows = scan["Results"]!.AsArray();
                        Assert.Equal(2, rows.Count);
                        foreach (var row in rows)
                        {
                            Assert.False(row!["HasError"]!.GetValue<bool>());
                            Assert.NotNull(row["SourceFingerprint"]);
                            row["Summary"] = "Saved result";
                        }
                        entry.Delete();
                        using var writer = new StreamWriter(zip.CreateEntry("scan.json").Open());
                        writer.Write(scan.ToJsonString());
                    }

                    File.SetLastWriteTimeUtc(changed, File.GetLastWriteTimeUtc(changed).AddMinutes(-2));
                    File.WriteAllBytes(added, package);
                    window.Close();
                    reopened = new MainWindow();
                    var recursive = reopened.FindControl<CheckBox>("BatchRecursive")!;
                    recursive.IsChecked = false;
                    var resume = reopened.FindControl<Button>("BatchResumeButton")!;
                    Click(resume);
                    var resumedStart = reopened.FindControl<Button>("BatchStartButton")!;
                    await UntilAsync(() => recursive.IsChecked == true && resumedStart.IsEnabled, timeout.Token);

                    using var updated = ZipFile.OpenRead(archivePath);
                    using var metadata = updated.GetEntry("scan.json")!.Open();
                    var results = JsonNode.Parse(metadata)!["Results"]!.AsArray();
                    var byName = results.ToDictionary(row => Path.GetFileName(row!["Path"]!.GetValue<string>())!);
                    Assert.Equal(3, byName.Count);
                    Assert.Equal("Saved result", byName["unchanged.nsp"]!["Summary"]!.GetValue<string>());
                    Assert.NotEqual("Saved result", byName["changed.nsp"]!["Summary"]!.GetValue<string>());
                    Assert.NotEqual("Saved result", byName["added.nsp"]!["Summary"]!.GetValue<string>());
                    return true;
                }
                finally
                {
                    reopened?.Close();
                    window.Close();
                }
            }, timeout.Token);
        }
        finally
        {
            await Task.Run(session.Dispose, CancellationToken.None);
            AppContext.SetData("NxFileViewer.DataDirectory", previousRoot);
            data.Delete(recursive: true);
        }
    }

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static async Task UntilAsync(Func<bool> condition, CancellationToken cancellationToken)
    {
        while (!condition())
            await Task.Delay(20, cancellationToken);
    }
}

[CollectionDefinition("Avalonia desktop", DisableParallelization = true)]
public sealed class AvaloniaDesktopCollection;
