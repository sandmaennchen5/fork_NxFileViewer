using System.Collections.Generic;
using System.Linq;
using NxFileViewer.Avalonia;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services;

public sealed class BatchGameSortTest
{
    private static MainWindow.BatchRow Row(string title, string game, string type, string id, string version = "0") =>
        new(title + version + ".nsp", "")
        {
            StoredMetadata = new Dictionary<string, string>
            {
                ["Title"] = title, ["TitleId"] = id, ["BaseTitleId"] = game,
                ["ContentType"] = type, ["Version"] = version
            }
        };

    [Fact]
    public void AlphabeticalSortUsesBaseTitleAndKeepsItsUpdatesAndDlcTogether()
    {
        const string alpha = "0100000000000000", zelda = "0100000000002000";
        var gameZ = Row("Zelda", zelda, "Base", zelda);
        var updateZ = Row("Different update title", zelda, "Update", "0100000000002800");
        var gameA = Row("Alpha", "0000000000000000", "Base", alpha);
        var older = Row("Wrong patch title", alpha, "Update", "0100000000000800", "9");
        var newer = Row("Wrong patch title", alpha, "Update", "0100000000000800", "10");
        var dlcA = Row("Z DLC", alpha, "DLC", "0100000000001001");

        Assert.Equal(new[] { gameA.Path, newer.Path, older.Path, dlcA.Path, gameZ.Path, updateZ.Path },
            MainWindow.SortBatchGameFamilies([updateZ, dlcA, gameZ, older, newer, gameA]).Select(row => row.Path));
    }
}
