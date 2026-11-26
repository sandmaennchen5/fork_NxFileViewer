using System;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Controls;
using Emignatik.NxFileViewer.Test.Views;
using NxFileViewer.Avalonia;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services.Integrity;

[Collection("Avalonia desktop")]
public sealed class BatchHistoryArchiveTest
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ImportsLegacyScanWithPackageDetailsAndSkipsUnreadableArchives(bool zipped) =>
        AvaloniaWorkspace.RunAsync(() =>
        {
            var history = Directory.CreateDirectory(Path.Combine(ViewerDataDirectory.Root, "History")).FullName;
            var id = Guid.NewGuid();
            var file = Path.Combine(ViewerDataDirectory.Root, "missing.nsp");
            var scan = new
            {
                Id = id,
                UpdatedUtc = DateTime.UtcNow,
                Source = ViewerDataDirectory.Root,
                VerifyIntegrity = true,
                IncludeSubdirectories = true,
                Results = new[]
                {
                    new
                    {
                        FilePath = file,
                        FileType = "NSP",
                        Integrity = 2,
                        Packages = new[] { new { Type = "Application", TitleId = "0100000000000000" },
                            new { Type = "Patch", TitleId = "0100000000000800" } }
                    }
                }
            };
            if (zipped)
            {
                using var legacy = ZipFile.Open(Path.Combine(history, "legacy.zip"), ZipArchiveMode.Create);
                using var contents = legacy.CreateEntry("overview.json").Open();
                JsonSerializer.Serialize(contents, scan);
            }
            else File.WriteAllText(Path.Combine(history, "batch-integrity.json"), JsonSerializer.Serialize(new[] { scan }));
            File.WriteAllText(Path.Combine(history, "scan-" + Guid.NewGuid().ToString("N") + ".zip"), "broken");

            var window = new MainWindow();
            try
            {
                var choices = window.FindControl<ComboBox>("BatchHistoryChoice")!;
                Assert.Single(choices.Items);
                using var converted = ZipFile.OpenRead(Path.Combine(history, $"scan-{id:N}.zip"));
                using var contents = converted.GetEntry("scan.json")!.Open();
                using var stored = JsonDocument.Parse(contents);
                var row = stored.RootElement.GetProperty("Results")[0];
                Assert.Equal(file, row.GetProperty("Path").GetString());
                Assert.True(row.GetProperty("Verified").GetBoolean());
                var parts = row.GetProperty("StoredParts");
                Assert.Equal(2, parts.GetArrayLength());
                Assert.Equal("Base", parts[0].GetProperty("StoredMetadata").GetProperty("ContentType").GetString());
                Assert.Equal("0100000000000800", parts[1].GetProperty("StoredMetadata").GetProperty("TitleId").GetString());
                Assert.True(stored.RootElement.GetProperty("Options").GetProperty("Recursive").GetBoolean());
            }
            finally { window.Close(); }

            var reopened = new MainWindow();
            try { Assert.Single(reopened.FindControl<ComboBox>("BatchHistoryChoice")!.Items); }
            finally { reopened.Close(); }
        }, TestContext.Current.CancellationToken);
}
