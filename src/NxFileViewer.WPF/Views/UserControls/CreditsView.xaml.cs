using System.IO;
using System.Reflection;
using System.Windows.Controls;

namespace Emignatik.NxFileViewer.Views.UserControls;

public partial class CreditsView : UserControl
{
    public CreditsView()
    {
        InitializeComponent();
        CreditsText.Text = ReadNotice("Viewer.Credits");
        LicensesText.Text = ReadNotice("Viewer.License") + "\n\n" + ReadNotice("Viewer.ThirdPartyNotices") + "\n\n" + ReadNotice("Viewer.IconProvenance");
    }

    private static string ReadNotice(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
            ?? throw new InvalidDataException($"Missing bundled notice: {name}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}