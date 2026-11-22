using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Avalonia.Interactivity;
using Avalonia.Media;
namespace NxFileViewer.Avalonia;
public partial class MainWindow
{
    private sealed record SessionMessage(DateTime Time, string Level, string Message)
    {
        public string Text => $"{Time:HH:mm:ss}  [{Level}]  {Message}";
        public IBrush Background => Level switch { "ERROR" or "CRITICAL" => new SolidColorBrush(Color.Parse("#40252A")), "WARN" => new SolidColorBrush(Color.Parse("#40321C")), _ => Brushes.Transparent };
        public IBrush Brush => Level switch { "ERROR" or "CRITICAL" => Brushes.Salmon, "WARN" => Brushes.Orange, _ => Brushes.LightSkyBlue };
    }
    private readonly string _sessionLogPath = Path.Combine(AppContext.BaseDirectory, "Logs", "Avalonia", "Avalonia-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..8] + ".log");
    private bool _logRetentionApplied;
    private static int LogRank(string level) => level switch { "TRACE" => 0, "DEBUG" => 1, "WARN" => 3, "ERROR" => 4, "CRITICAL" => 5, "OFF" => 6, _ => 2 };
    private readonly List<SessionMessage> _sessionMessages = new();
    private void LogSession(string level, string message, string? plugin = null)
    {
        var minimum = plugin != null && _preferences.PluginLogLevels?.TryGetValue(plugin, out var configured) == true && !string.IsNullOrEmpty(configured) ? configured : _preferences.LogMinimumLevel;
        if (_closed || minimum == "OFF" || LogRank(level) < LogRank(minimum)) return;
        var entry = new SessionMessage(DateTime.Now, level, Emignatik.NxFileViewer.Logging.SessionFileLog.RedactSecrets(message));
        _sessionMessages.Add(entry);
        try
        {
            var folder = Path.GetDirectoryName(_sessionLogPath)!; Directory.CreateDirectory(folder);
            File.AppendAllText(_sessionLogPath, entry.Text + Environment.NewLine);
            if (!_logRetentionApplied)
            {
                _logRetentionApplied = true;
                foreach (var path in Directory.EnumerateFiles(folder, "Avalonia-*.log").OrderByDescending(File.GetLastWriteTimeUtc).Skip(Math.Clamp(_preferences.LogFileRetention, 1, 100))) File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { SettingsStatus.Text = ex.Message; }
        if (_sessionMessages.Count > 5000) _sessionMessages.RemoveAt(0);
        RefreshLog();
    }
    private async void CopySelectedLog(object? sender, RoutedEventArgs e)
    {
        if (Clipboard != null && SessionLog.SelectedItem is SessionMessage entry) await Clipboard.SetTextAsync(entry.Text);
    }
    private void LogFilterChanged(object? sender, RoutedEventArgs e) { if (SessionLog != null && LogInfo != null && LogWarn != null && LogError != null) RefreshLog(); }
    private void RefreshLog() => SessionLog.ItemsSource = _sessionMessages.Where(row => row.Level switch { "ERROR" => LogError.IsChecked == true, "WARN" => LogWarn.IsChecked == true, _ => LogInfo.IsChecked == true }).ToArray();
}
