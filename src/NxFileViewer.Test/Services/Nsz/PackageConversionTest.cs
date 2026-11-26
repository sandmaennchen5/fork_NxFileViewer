using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Emignatik.NxFileViewer.Test.Views;
using NxFileViewer.Avalonia;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services.Nsz;

[Collection("Avalonia desktop")]
public sealed class PackageConversionTest
{
    [Theory]
    [InlineData("Compress", ".nsp", false)]
    [InlineData("Decompress", ".nsz", false)]
    [InlineData("Compress", ".nsp", true)]
    public Task RejectsUnverifiableOrCancelledInputBeforeStartingConversion(string action, string extension, bool cancel) =>
        AvaloniaWorkspace.RunAsync(() =>
        {
            var source = Path.Combine(ViewerDataDirectory.Root, "game" + extension);
            var package = new byte[16];
            "PFS0"u8.CopyTo(package);
            File.WriteAllBytes(source, package);
            var keys = Path.Combine(ViewerDataDirectory.Root, "prod.keys");
            File.WriteAllText(keys, "");
            File.WriteAllText(Path.Combine(ViewerDataDirectory.Root, "title.keys"), "");
            var destination = Directory.CreateDirectory(Path.Combine(ViewerDataDirectory.Root, "destination")).FullName;
            using var token = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            if (cancel) token.Cancel();
            var window = new MainWindow();
            try
            {
                string Convert() => window.RunPackageJob(action, [source], destination, "unused-engine", "", keys,
                    ViewerPreferences.Load(), (_, _) => { }, token.Token, null, null,
                    _ => Assert.Fail("Conversion started for invalid input."));
                if (cancel) Assert.ThrowsAny<OperationCanceledException>(() => Convert());
                else Assert.Throws<InvalidDataException>(() => Convert());
                Assert.Empty(Directory.GetFileSystemEntries(destination));
                Assert.Equal(package, File.ReadAllBytes(source));
                using var unlocked = File.Open(source, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken);
}
