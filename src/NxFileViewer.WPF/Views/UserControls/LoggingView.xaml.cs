using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Emignatik.NxFileViewer.Logging;
using Microsoft.Extensions.Logging;

namespace Emignatik.NxFileViewer.Views.UserControls;

public partial class LoggingView : UserControl
{
    private Section? _currentFile;
    internal const int MaximumVisibleEntries = 2000;
    private int _visibleEntries;

    public static readonly DependencyProperty LogSourceProperty = DependencyProperty.Register(
        nameof(LogSource), typeof(ILogSource), typeof(LoggingView), new PropertyMetadata(default(ILogSource), OnChanged));

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (LoggingView)d;
        if (e.OldValue is ILogSource oldSource) oldSource.Log -= view.OnLog;
        if (e.NewValue is ILogSource newSource) newSource.Log += view.OnLog;
    }

    public ILogSource LogSource
    {
        get => (ILogSource)GetValue(LogSourceProperty);
        set => SetValue(LogSourceProperty, value);
    }

    public LoggingView()
    {
        InitializeComponent();
        Loaded += (_, _) => RichTextBoxLog.ScrollToEnd();
    }

    private void OnLog(LogLevel level, string message)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action(() => OnLog(level, message)));
            return;
        }

        // Opening-file markers exist in every localization, also with a debug category prefix.
        const string startMarker = "=====>";
        const string endMarker = "<=====";
        var start = message.IndexOf(startMarker, StringComparison.Ordinal);
        var end = message.LastIndexOf(endMarker, StringComparison.Ordinal);
        if (level == LogLevel.Information && start >= 0 && end > start &&
            string.IsNullOrWhiteSpace(message[(end + endMarker.Length)..]))
        {
            var path = message[(start + startMarker.Length)..end].Trim();
            if (!string.IsNullOrEmpty(path))
            {
                BeginFile(path);
                RichTextBoxLog.ScrollToEnd();
                return;
            }
        }

        var foreground = level >= LogLevel.Error ? "Log.Error.Foreground"
            : level == LogLevel.Warning ? "Log.Warning.Foreground"
            : level == LogLevel.Information ? "Log.Info.Foreground" : "Log.Muted";
        var paragraph = new Paragraph { Margin = new Thickness(0, 1, 0, 1), Padding = new Thickness(7, 3, 7, 3), BorderThickness = new Thickness(3, 0, 0, 0) };
        paragraph.SetResourceReference(Block.BorderBrushProperty, foreground);
        if (level >= LogLevel.Warning)
        {
            paragraph.SetResourceReference(TextElement.ForegroundProperty, foreground);
            paragraph.SetResourceReference(TextElement.BackgroundProperty,
                level >= LogLevel.Error ? "Log.Error.Background" : "Log.Warning.Background");
        }
        var time = new Run(DateTime.Now.ToString("HH:mm:ss") + "  ");
        time.SetResourceReference(TextElement.ForegroundProperty, "Log.Muted");
        paragraph.Inlines.Add(time);
        var label = new Run("[" + (level switch
        {
            LogLevel.Information => "INFO",
            LogLevel.Warning => "WARN",
            LogLevel.Error => "ERROR",
            LogLevel.Critical => "CRITICAL",
            LogLevel.Debug => "DEBUG",
            _ => "TRACE"
        }) + "]  ") { FontWeight = FontWeights.Bold };
        label.SetResourceReference(TextElement.ForegroundProperty, foreground);
        paragraph.Inlines.Add(label);
        paragraph.Inlines.Add(new Run(message));
        if (_currentFile == null) RichTextBoxLog.Document.Blocks.Add(paragraph);
        else _currentFile.Blocks.Add(paragraph);
        _visibleEntries++;
        TrimVisibleEntries();
        RichTextBoxLog.ScrollToEnd();
    }

    private void BeginFile(string path)
    {
        _currentFile = new Section
        {
            Margin = new Thickness(0, 12, 0, 4),
            Padding = new Thickness(8),
            BorderThickness = new Thickness(1)
        };
        _currentFile.SetResourceReference(Block.BorderBrushProperty, "Log.File.Border");
        var heading = new Paragraph { Padding = new Thickness(8), Margin = new Thickness(0, 0, 0, 6), KeepWithNext = true };
        heading.SetResourceReference(TextElement.BackgroundProperty, "Log.File.Background");
        var title = new Run(Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)))
        {
            FontWeight = FontWeights.Bold, FontSize = 14
        };
        title.SetResourceReference(TextElement.ForegroundProperty, "Log.Info.Foreground");
        heading.Inlines.Add(title);
        heading.Inlines.Add(new LineBreak());
        var location = new Run(DateTime.Now.ToString("HH:mm:ss") + "  " + path) { FontSize = 11 };
        location.SetResourceReference(TextElement.ForegroundProperty, "Log.Muted");
        heading.Inlines.Add(location);
        _currentFile.Blocks.Add(heading);
        RichTextBoxLog.Document.Blocks.Add(_currentFile);
        _visibleEntries++;
        TrimVisibleEntries();
    }

    private void TrimVisibleEntries(int maximumEntries = MaximumVisibleEntries)
    {
        // Keep the complete session on disk, without retaining every WPF paragraph in RAM.
        while (_visibleEntries > maximumEntries && RichTextBoxLog.Document.Blocks.FirstBlock is { } first)
        {
            if (first is Section section)
            {
                if (ReferenceEquals(section, _currentFile) && section.Blocks.Count > 1)
                {
                    section.Blocks.Remove(section.Blocks.FirstBlock.NextBlock);
                    _visibleEntries--;
                    continue;
                }
                _visibleEntries -= section.Blocks.Count;
                if (ReferenceEquals(section, _currentFile)) _currentFile = null;
            }
            else _visibleEntries--;
            RichTextBoxLog.Document.Blocks.Remove(first);
        }
    }

    private void MenuItemClearLogClick(object sender, RoutedEventArgs e)
    {
        RichTextBoxLog.Document.Blocks.Clear();
        _currentFile = null;
        _visibleEntries = 0;
    }
}