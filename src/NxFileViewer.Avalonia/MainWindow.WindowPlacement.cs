using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;

namespace NxFileViewer.Avalonia;

public partial class MainWindow
{
    private SavedWindowPlacement? _normalPlacement;
    private bool _placementReady;
    private bool _lastPlacementMaximized;

    private void InitializeWindowPlacement()
    {
        if (_preferences.SharedSettings.RememberWindowPlacement && IsValidPlacement(_preferences.WindowPlacement))
            WindowStartupLocation = WindowStartupLocation.Manual;
        Opened += (_, _) =>
        {
            var saved = _preferences.SharedSettings.RememberWindowPlacement ? _preferences.WindowPlacement : null;
            if (!IsValidPlacement(saved)) saved = null;
            var screen = saved is null ? Screens.ScreenFromWindow(this)
                : Screens.ScreenFromPoint(new PixelPoint(saved.Left, saved.Top));
            screen ??= Screens.Primary;
            if (screen is not null)
            {
                var area = screen.WorkingArea;
                var scale = screen.Scaling;
                // Reserve space for native decorations and keep the title bar reachable.
                Width = Math.Max(1, Math.Min(saved?.Width ?? Width, area.Width / scale - 16));
                Height = Math.Max(1, Math.Min(saved?.Height ?? Height, area.Height / scale - 48));
                var right = area.Right - (int)Math.Ceiling(Width * scale);
                var bottom = area.Bottom - (int)Math.Ceiling((Height + 32) * scale);
                Position = new PixelPoint(
                    Math.Clamp(saved?.Left ?? area.X + (area.Width - (int)(Width * scale)) / 2, area.X, Math.Max(area.X, right)),
                    Math.Clamp(saved?.Top ?? area.Y + (area.Height - (int)(Height * scale)) / 2, area.Y, Math.Max(area.Y, bottom)));
            }
            _placementReady = true;
            CaptureNormalPlacement();
            if (saved?.IsMaximized == true) WindowState = WindowState.Maximized;
        };
        PositionChanged += (_, _) => CaptureNormalPlacement();
        SizeChanged += (_, _) => CaptureNormalPlacement();
        PropertyChanged += (_, args) =>
        {
            if (!_placementReady || args.Property != WindowStateProperty) return;
            if (WindowState == WindowState.Maximized) _lastPlacementMaximized = true;
            else if (WindowState == WindowState.Normal) _lastPlacementMaximized = false;
        };
        Closing += (_, _) =>
        {
            if (!_placementReady || !_preferences.SharedSettings.RememberWindowPlacement || _normalPlacement is null) return;
            _preferences = _preferences with { WindowPlacement = _normalPlacement with { IsMaximized = _lastPlacementMaximized } };
            try { _preferences.Save(); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                LogSession("WARN", "Could not save window placement: " + error.Message);
            }
        };
    }

    private static bool IsValidPlacement(SavedWindowPlacement? saved) => saved is not null &&
        double.IsFinite(saved.Width) && double.IsFinite(saved.Height) && saved.Width > 0 && saved.Height > 0;

    private void CaptureNormalPlacement()
    {
        if (!_placementReady || WindowState != WindowState.Normal || !double.IsFinite(Width) || !double.IsFinite(Height)) return;
        _normalPlacement = new SavedWindowPlacement(Position.X, Position.Y, Width, Height, false);
    }
}
