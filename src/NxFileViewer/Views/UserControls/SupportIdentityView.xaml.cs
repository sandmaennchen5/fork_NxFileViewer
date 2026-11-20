using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Emignatik.NxFileViewer.Services;
using Emignatik.NxFileViewer.Views.Windows;

namespace Emignatik.NxFileViewer.Views.UserControls;

public partial class SupportIdentityView : UserControl
{
    private CancellationTokenSource? _refresh;
    private bool _german => Emignatik.NxFileViewer.Localization.CommunityLanguage.IsGerman;

    public SupportIdentityView()
    {
        InitializeComponent();
        RefreshCommunityText();
        Emignatik.NxFileViewer.Localization.CommunityLanguage.Observe(this, view => view.RefreshCommunityText());
        Loaded += async (_, _) =>
        {
            _refresh?.Cancel();
            var refresh = new CancellationTokenSource();
            _refresh = refresh;
            ShowId(SupportIdentityService.CachedSupportId());
            IdentityStatus.Text = Text("Support-ID wird geprüft …", "Checking support ID …");
            try
            {
                var supportId = await SupportIdentityService.RefreshAsync(refresh.Token);
                if (!refresh.IsCancellationRequested)
                {
                    ShowId(supportId);
                    IdentityStatus.Text = Text("Öffentlich in deinen Meldungen sichtbar.",
                        "Publicly visible in your reports.");
                }
            }
            catch (OperationCanceledException) when (refresh.IsCancellationRequested) { }
            catch (Exception error) when (error is IOException or HttpRequestException or OperationCanceledException or System.Text.Json.JsonException)
            {
                if (!refresh.IsCancellationRequested)
                    IdentityStatus.Text = Text("Support-ID konnte nicht geprüft werden. Eine angezeigte ID stammt aus dem letzten Abruf.",
                        "Could not verify support ID. Any displayed ID is from the last successful request.");
            }
            finally { if (ReferenceEquals(_refresh, refresh)) _refresh = null; refresh.Dispose(); }
        };
        Unloaded += (_, _) => { _refresh?.Cancel(); _refresh = null; };
    }

    private void RefreshCommunityText()
    {
        IdentityLabel.Content = Text("Support-ID", "Support ID");
        CopyButton.Content = Text("Kopieren", "Copy");
        SearchButton.Content = Text("Meine Meldungen suchen", "Find my reports");
        IdentityStatus.Text = Text("Öffentlich in deinen Meldungen sichtbar.", "Publicly visible in your reports.");
    }

    private string Text(string de, string en) => _german ? de : en;

    private void ShowId(string id)
    {
        IdentityText.Text = id;
        CopyButton.IsEnabled = SearchButton.IsEnabled = id.Length > 0;
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try { Clipboard.SetText(IdentityText.Text); }
        catch (System.Runtime.InteropServices.COMException)
        {
            IdentityStatus.Text = Text("Die Zwischenablage ist gerade nicht verfügbar.", "The clipboard is currently unavailable.");
        }
    }

    private void Search_Click(object sender, RoutedEventArgs e)
    {
        var url = SupportIdentityService.SearchUrl(IdentityText.Text);
        try { FeedbackWindow.OpenBrowser(url); }
        catch (System.ComponentModel.Win32Exception) { IdentityStatus.Text = url; }
    }
}
