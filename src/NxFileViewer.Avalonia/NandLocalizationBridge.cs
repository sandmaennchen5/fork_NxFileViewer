using Emignatik.NxFileViewer.Localization.Keys;
namespace Emignatik.NxFileViewer.Localization;
// Supplies the shared NAND readers with the frontend's selected UI language.
internal sealed class LocalizationManager
{
    internal static LocalizationManager Instance { get; } = new();
    internal LocalizationManager Current => this;
    internal ILocalizationKeys Keys { get; set; } = new LocalizationKeys_EN();
}
