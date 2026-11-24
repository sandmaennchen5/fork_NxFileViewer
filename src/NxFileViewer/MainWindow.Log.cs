using Avalonia.Input.Platform;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Avalonia.Interactivity;
using Avalonia.Media;
namespace NxFileViewer.Avalonia;
public partial class MainWindow
{
    private sealed record SessionMessage(DateTime Time, string Level, string Message, string? FilePath = null)
    {
        public bool IsFileHeader => FilePath != null;
        public bool IsDetail => !IsFileHeader;
        public string Text => IsFileHeader ? $"{Message}{Environment.NewLine}{HeaderDetails}" : $"{Time:HH:mm:ss}  [{Level}]  {Message}";
        public string HeaderDetails => $"{Time:HH:mm:ss}  {FilePath}";
        public IBrush Background => Level switch { "ERROR" or "CRITICAL" => new SolidColorBrush(Color.Parse("#40252A")), "WARN" => new SolidColorBrush(Color.Parse("#40321C")), _ => Brushes.Transparent };
        public IBrush Brush => Level switch { "ERROR" or "CRITICAL" => Brushes.Salmon, "WARN" => Brushes.Orange, _ => Brushes.LightSkyBlue };
    }
    private readonly string _sessionLogPath = Path.Combine(ViewerDataDirectory.Root, "Logs", "Avalonia-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..8] + ".log");
    private bool _logRetentionApplied;
    private static int LogRank(string level) => level switch { "TRACE" => 0, "DEBUG" => 1, "WARN" => 3, "ERROR" => 4, "CRITICAL" => 5, "OFF" => 6, _ => 2 };
    private readonly List<SessionMessage> _sessionMessages = new();
    private void LogSession(string level, string message, string? plugin = null)
    {
        var minimum = plugin != null && _preferences.PluginLogLevels?.TryGetValue(plugin, out var configured) == true && !string.IsNullOrEmpty(configured) ? configured : _preferences.LogMinimumLevel;
        LogSessionEntries([(level, message)], minimum);
    }

    private void LogFileSection(string path, IEnumerable<(string Level, string Message)>? messages = null) =>
        LogSessionEntries(messages ?? [], _preferences.LogMinimumLevel, path);

    private void LogBatchFile(BatchRow row)
    {
        var lines = row.Summary.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        LogFileSection(row.DisplayPath, lines.Select(line => (row.HasError ? "ERROR" : "INFO", line)));
    }

    private void LogSessionEntries(IEnumerable<(string Level, string Message)> messages, string minimum, string? filePath = null)
    {
        if (_closed || minimum == "OFF") return;
        var time = DateTime.Now;
        var entries = messages.Where(message => LogRank(message.Level) >= LogRank(minimum))
            .Select(message => new SessionMessage(time, message.Level,
                Emignatik.NxFileViewer.Logging.SessionFileLog.RedactSecrets(message.Message))).ToList();
        if (filePath != null && (entries.Count > 0 || LogRank(minimum) <= LogRank("INFO")))
        {
            var safePath = Emignatik.NxFileViewer.Logging.SessionFileLog.RedactSecrets(filePath);
            var name = Path.GetFileName(safePath.Split(" :: ", 2)[^1]);
            entries.Insert(0, new SessionMessage(time, entries.FirstOrDefault()?.Level ?? "INFO", name, safePath));
        }
        if (entries.Count == 0) return;
        _sessionMessages.AddRange(entries);
        try
        {
            var folder = Path.GetDirectoryName(_sessionLogPath)!; Directory.CreateDirectory(folder);
            File.AppendAllLines(_sessionLogPath, entries.Select(entry => entry.FilePath is { } path
                ? $"{entry.Time:HH:mm:ss}  [{entry.Level}]  {path}"
                : entry.Text));
            if (!_logRetentionApplied)
            {
                _logRetentionApplied = true;
                foreach (var path in Directory.EnumerateFiles(folder, "Avalonia-*.log").OrderByDescending(File.GetLastWriteTimeUtc).Skip(Math.Clamp(_preferences.LogFileRetention, 1, 100))) File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { SettingsStatus.Text = ex.Message; }
        if (_sessionMessages.Count > 5000) _sessionMessages.RemoveRange(0, _sessionMessages.Count - 5000);
        RefreshLog();
    }
    private async void CopySelectedLog(object? sender, RoutedEventArgs e)
    {
        if (Clipboard != null && SessionLog.SelectedItem is SessionMessage entry) await Clipboard.SetTextAsync(entry.Text);
    }
    private void LogFilterChanged(object? sender, RoutedEventArgs e) { if (SessionLog != null && LogInfo != null && LogWarn != null && LogError != null) RefreshLog(); }
    private void RefreshLog() => SessionLog.ItemsSource = _sessionMessages.Where(row => row.Level switch
    {
        "ERROR" or "CRITICAL" => LogError.IsChecked == true,
        "WARN" => LogWarn.IsChecked == true,
        _ => LogInfo.IsChecked == true
    }).ToArray();
}
