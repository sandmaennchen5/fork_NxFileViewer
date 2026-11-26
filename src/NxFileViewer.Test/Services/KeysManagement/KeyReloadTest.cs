using System.IO;
using System.Threading.Tasks;
using Emignatik.NxFileViewer.Services.KeysManagement;
using Emignatik.NxFileViewer.Test.Views;
using NxFileViewer.Avalonia;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services.KeysManagement;

[Collection("Avalonia desktop")]
public sealed class KeyReloadTest
{
    [Fact]
    public Task ReloadDiscoversAndRevalidatesKeysCopiedIntoProgramDirectory() =>
        AvaloniaWorkspace.RunAsync(() =>
        {
            var program = ViewerDataDirectory.Root;
            foreach (var name in new[] { "prod.keys", "title.keys" })
            {
                var path = Path.Combine(program, name);
                File.WriteAllText(path, "malformed synthetic line");
                Assert.Equal(path, MainWindow.ResolveKeyFile(null, name));
                System.Func<string?, KeyFileValidationResult> validate = name == "prod.keys" ? KeyFileValidator.ValidateProdKeys : KeyFileValidator.ValidateTitleKeys;
                Assert.NotEmpty(validate(path).InvalidLineNumbers);
                File.WriteAllText(path, "");
                Assert.Empty(validate(path).InvalidLineNumbers);
                File.Delete(path);
                Assert.NotEqual(path, MainWindow.ResolveKeyFile(null, name));
            }
        }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ExplicitSettingsPathRetainsPriority() =>
        AvaloniaWorkspace.RunAsync(() =>
        {
            var program = ViewerDataDirectory.Root;
            var custom = Path.Combine(program, "custom.keys");
            File.WriteAllText(custom, "");
            foreach (var name in new[] { "prod.keys", "title.keys" })
            {
                File.WriteAllText(Path.Combine(program, name), "");
                Assert.Equal(custom, MainWindow.ResolveKeyFile(custom, name));
            }
        }, TestContext.Current.CancellationToken);
}
