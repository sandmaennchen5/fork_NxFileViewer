using System;
using System.Linq;
using System.Collections;
using Emignatik.NxFileViewer.Services.Integrity;
using Emignatik.NxFileViewer.Models.Overview;
using Xunit;
namespace Emignatik.NxFileViewer.Test.Services;
public sealed class BatchGameSortTest
{
    private static BatchIntegrityResult Row(string title, string game, string type, string id, string version = "0") =>
        new(title + version + ".nsp", "NSP", "", "", "", NcasIntegrity.Original, null)
        { Title = title, TitleId = id, BaseTitleId = game, ContentType = type, Version = version };
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FamiliesStayTogetherWithBaseThenUpdatesThenDlcAndOptionalAlphabeticalBaseTitles(bool alphabetical)
    {
        const string a = "0100000000000000", z = "0100000000002000";
        var zb = Row("Zelda", z, "Base", z); var zu = Row("Wrong update title", z, "Update", "0100000000002800");
        var ab = Row("Alpha", a, "Base", a); var au = Row("Wrong patch title", a, "Update", "0100000000000800");
        var ad = Row("Z DLC", a, "DLC", "0100000000001001");
        var input = new[] { zu, ad, zb, au, ab };
        var sorted = input.OrderBy(r => r, System.Collections.Generic.Comparer<BatchIntegrityResult>.Create((x,y) =>
            new BatchGameSortComparer(input, alphabetical).Compare(x,y))).ToArray();
        Assert.Equal(alphabetical ? new[] { ab, au, ad, zb, zu } : new[] { zb, zu, ab, au, ad }, sorted);
    }
}