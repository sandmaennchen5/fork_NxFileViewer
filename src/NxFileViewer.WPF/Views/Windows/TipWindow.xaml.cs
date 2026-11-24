using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Emignatik.NxFileViewer.Styling.Theme;
using Emignatik.NxFileViewer.Utils.Interop;
using Microsoft.Extensions.DependencyInjection;

namespace Emignatik.NxFileViewer.Views.Windows;

public partial class TipWindow : Window
{
    public TipWindow()
    {
        InitializeComponent();
        App.ServiceProvider.GetService<IThemeService>()?.RegisterWindow(this);
    }

    public void ShowTip(string text, FrameworkElement anchor)
    {
        var screenPoint = anchor.PointToScreen(new Point());
        var transform = PresentationSource.FromVisual(anchor)?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        var workArea = SystemParameters.WorkArea;
        var reference = new NativeMethods.RECT((int)screenPoint.X, (int)screenPoint.Y, (int)screenPoint.X + 1, (int)screenPoint.Y + 1);
        if (NativeMethods.TryGetNearestWorkArea(reference, out var work))
            workArea = new Rect(transform.Transform(new Point(work.Left, work.Top)), transform.Transform(new Point(work.Right, work.Bottom)));
        Configure(text, workArea, transform.Transform(screenPoint));
        Show();
        Activate();
        TextBox.ScrollToHome();
    }

    public void Configure(string text, Rect workArea, Point anchor)
    {
        TextBox.Text = text;
        var availableWidth = Math.Max(1, workArea.Width - 24);
        var availableHeight = Math.Max(1, workArea.Height - 24);
        MinWidth = Math.Min(320, availableWidth);
        MinHeight = Math.Min(140, availableHeight);
        MaxWidth = availableWidth;
        MaxHeight = availableHeight;
        Width = Math.Min(820, availableWidth);
        TextBox.Measure(new Size(Math.Max(1, Width - 48), double.PositiveInfinity));
        Height = Math.Clamp(TextBox.DesiredSize.Height + SystemParameters.CaptionHeight + 48, MinHeight, MaxHeight);
        Left = Math.Clamp(anchor.X, workArea.Left + 12, workArea.Right - Width - 12);
        Top = Math.Clamp(anchor.Y, workArea.Top + 12, workArea.Bottom - Height - 12);
    }

    private void TipKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { Close(); e.Handled = true; }
    }
}
