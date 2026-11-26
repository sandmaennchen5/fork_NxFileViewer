using NxFileViewer.Avalonia;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services;

public sealed class ExplorerComponentUpdateTest
{
    [Theory]
    [InlineData(null, @"C:\Viewer\NxFileViewerFork.exe", false)]
    [InlineData(@"C:\Old\NxFileViewerFork.exe", @"C:\Viewer\NxFileViewerFork.exe", false)]
    [InlineData(@"c:\viewer\nxfileviewerfork.exe", @"C:\Viewer\NxFileViewerFork.exe", true)]
    [InlineData(null, null, false)]
    public void SettingsShowExplorerRegistrationOnlyForTheCurrentExecutable(string? registration, string? application, bool shown) =>
        Assert.Equal(shown, MainWindow.IsExplorerRegisteredForViewer(registration, application));
}
