using System.Threading.Tasks;
namespace Emignatik.NxFileViewer.Services;
internal static partial class UsageStatisticsService
{
    internal static Task SendStartupAsync() => SendStartupAsync(App.Features);
}
