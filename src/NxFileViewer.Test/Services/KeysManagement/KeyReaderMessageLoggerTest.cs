using System;
using System.IO;
using System.Threading.Tasks;
using Emignatik.NxFileViewer.Services.KeysManagement;
using Emignatik.NxFileViewer.Test.Views;
using NxFileViewer.Avalonia;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services.KeysManagement;

[Collection("Avalonia desktop")]
public sealed class KeyReaderMessageLoggerTest
{
    [Fact]
    public Task KeyDiagnosticsReportNamesAndLineNumbersWithoutDisclosingValues() =>
        AvaloniaWorkspace.RunAsync(() =>
        {
            var file = Path.Combine(ViewerDataDirectory.Root, "fixture.keys");
            var syntheticValue = new string('a', 64);
            File.WriteAllLines(file, ["device_key_4x = " + syntheticValue,
                "bis_key_02 = " + syntheticValue, "sd_seed = invalid-synthetic-value"]);
            var window = new MainWindow();
            try
            {
                var result = KeyFileValidator.ValidateProdKeys(file);
                Assert.Equal(2, result.ValidEntryCount);
                Assert.Equal(ConsoleKeyStatus.Present, result.ConsoleKeys!["bis_key_02"]);
                Assert.Equal(ConsoleKeyStatus.InvalidFormat, result.ConsoleKeys["sd_seed"]);
                var description = window.DescribeKeyValidation(result);
                Assert.Contains("bis_key_02", description);
                Assert.Contains("sd_seed", description);
                Assert.DoesNotContain(syntheticValue, description, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("invalid-synthetic-value", description);
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken);
}
