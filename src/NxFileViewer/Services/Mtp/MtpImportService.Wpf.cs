using System;
using System.IO;
using Emignatik.NxFileViewer.Services.GlobalEvents;
namespace Emignatik.NxFileViewer.Services.Mtp;
public sealed partial class MtpImportService
{
    public MtpImportService(IAppEvents events, Services.FileOpening.IFileOpeningService opening) : this(new ShellMtpBackend(), Path.Combine(AppContext.BaseDirectory, "Temp", "MTP"))
    { events.AppShuttingDown += () => { opening.SafeClose(); Cleanup(); }; }
}
