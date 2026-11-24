using System.Threading;
using Emignatik.NxFileViewer.Models;
using Emignatik.NxFileViewer.Models.Overview;

namespace Emignatik.NxFileViewer.FileLoading;

internal static partial class NupReader
{
    public static NxFile Load(string path, CancellationToken token)
    {
        var root = ReadRoot(path, token, out var size);
        return new NxFile(path, root, new FileOverview(root) { FileSize = size });
    }
}
