using System;
using System.IO;
using System.Threading;
using NxFileViewer.Avalonia;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services;

public sealed class PackageOutputPublisherTest : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("nx-package-output-");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SingleOutputReplacesAnExistingFileOnlyWhenConfirmed(bool replace)
    {
        var staged = _root.CreateSubdirectory("stage").FullName;
        var source = Path.Combine(staged, "game.nsp");
        var target = Path.Combine(_root.FullName, "game.nsp");
        File.WriteAllText(source, "validated");
        File.WriteAllText(target, "existing");
        string Publish() => MainWindow.PublishPackageOutput(staged, _root.FullName, "Decompress",
            new(target, replace), TestContext.Current.CancellationToken);
        if (replace)
        {
            Assert.Equal(target, Publish());
            Assert.False(File.Exists(source));
            Assert.Equal("validated", File.ReadAllText(target));
        }
        else
        {
            Assert.Throws<IOException>(() => Publish());
            Assert.Equal("validated", File.ReadAllText(source));
            Assert.Equal("existing", File.ReadAllText(target));
        }
    }

    [Fact]
    public void SplitPublishesAllStagedOutputsTogetherInANewFolder()
    {
        var staged = _root.CreateSubdirectory("stage").FullName;
        File.WriteAllText(Path.Combine(staged, "base.nsp"), "base");
        File.WriteAllText(Path.Combine(staged, "update.nsp"), "update");
        var destination = MainWindow.PublishPackageOutput(staged, _root.FullName, "Split", null,
            TestContext.Current.CancellationToken);
        Assert.False(Directory.Exists(staged));
        Assert.Equal("base", File.ReadAllText(Path.Combine(destination, "base.nsp")));
        Assert.Equal("update", File.ReadAllText(Path.Combine(destination, "update.nsp")));
    }

    [Fact]
    public void CancelledPublicationRetainsStagedFiles()
    {
        var staged = _root.CreateSubdirectory("stage").FullName;
        File.WriteAllText(Path.Combine(staged, "base.nsp"), "base");
        Assert.ThrowsAny<OperationCanceledException>(() => MainWindow.PublishPackageOutput(staged, _root.FullName,
            "Split", null, new CancellationToken(true)));
        Assert.Single(_root.GetDirectories());
        Assert.Single(Directory.GetFiles(staged));
    }

    public void Dispose() => _root.Delete(recursive: true);
}
