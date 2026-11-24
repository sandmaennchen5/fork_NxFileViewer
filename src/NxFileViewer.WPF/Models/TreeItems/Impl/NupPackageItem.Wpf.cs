using Emignatik.NxFileViewer.Services.Integrity;

namespace Emignatik.NxFileViewer.Models.TreeItems.Impl;

internal sealed partial class NupPackageItem
{
    public BatchPackageSummary ToSummary() => new(Title, TitleId, Format, Version.ToString())
    {
        Publisher = Publisher,
        DisplayVersion = DisplayVersion,
        BuildId = BuildId
    };
}
