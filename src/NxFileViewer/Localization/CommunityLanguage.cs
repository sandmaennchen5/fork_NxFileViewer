using System;
using Emignatik.NxFileViewer.Localization.Keys;
using Emignatik.NxFileViewer.Utils.MVVM.Localization;

namespace Emignatik.NxFileViewer.Localization;

internal static class CommunityLanguage
{
    public static string SelectedLanguage => LocalizationManager.Instance.Current.Keys.CultureName.Split('-')[0];

    public static bool IsGerman => LocalizationManager.Instance.Current.Keys.CultureName
        .StartsWith("de", StringComparison.OrdinalIgnoreCase);

    public static void Observe<T>(T target, Action<T> refresh) where T : class
    {
        var weak = new WeakReference<T>(target);
        void Changed(object? sender, LocalizationChangedHandlerArgs<ILocalizationKeys> args)
        {
            if (weak.TryGetTarget(out var current)) refresh(current);
            else LocalizationManager.Instance.LocalizationChanged -= Changed;
        }
        LocalizationManager.Instance.LocalizationChanged += Changed;
    }
}
