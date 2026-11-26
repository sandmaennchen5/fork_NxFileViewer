using Avalonia.Input.Platform;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Emignatik.NxFileViewer.Logging;
using Microsoft.Extensions.Logging;
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
    private readonly string _sessionLogPath = Path.Combine(ViewerDataDirectory.Root, "Logs", "NxFileViewer-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..8] + ".log");
    private bool _logRetentionApplied;
    private static int LogRank(string level) => level switch { "TRACE" => 0, "DEBUG" => 1, "WARN" => 3, "ERROR" => 4, "CRITICAL" => 5, "OFF" => 6, _ => 2 };
    private readonly List<SessionMessage> _sessionMessages = new();
    private MemoryTraceDiagnostics? _memoryTrace;
    private bool _logRefreshPending;
    private string? _lastLogFile;

    private sealed class MemorySessionLogger(MainWindow owner) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => level == LogLevel.Trace && owner._preferences.LogMinimumLevel == "TRACE";
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(level)) return;
            var message = formatter(state, exception);
            Dispatcher.UIThread.Post(() => owner.LogSession("TRACE", message));
        }
    }

    internal void LogSession(string level, string message, string? plugin = null)
    {
        LogSessionEntries([(level, message)], EffectiveLogLevel(plugin));
    }

    private string EffectiveLogLevel(string? plugin) =>
        plugin != null && _preferences.PluginLogLevels?.TryGetValue(plugin, out var configured) == true &&
        !string.IsNullOrEmpty(configured) ? configured : _preferences.LogMinimumLevel;

    private void QueuePluginLog(string level, string? plugin, string message)
    {
        if (LogRank(EffectiveLogLevel(plugin)) > LogRank(level)) return;
        Dispatcher.UIThread.Post(() => LogSession(level, message, plugin));
    }

    private void LogFileSection(string path, IEnumerable<(string Level, string Message)>? messages = null) =>
        LogSessionEntries(messages ?? [], _preferences.LogMinimumLevel, path);

    private void QueueBatchFileTrace(string path, string message)
    {
        if (_preferences.LogMinimumLevel == "TRACE")
            Dispatcher.UIThread.Post(() => LogFileSection(path, [("TRACE", message)]));
    }

    private void LogBatchFile(BatchRow row)
    {
        var error = row.Integrity == _text.NcasIntegrity_Corrupted || row.Integrity == _text.NcasIntegrity_Error;
        var warning = row.Integrity == _text.NcasIntegrity_Modified || row.Integrity == _text.NcasIntegrity_Incomplete;
        var messages = new List<(string Level, string Message)>
        {
            (error ? "ERROR" : warning ? "WARN" : "INFO",
                $"{row.FileType} · {row.Size} · {_text.Title_FileInfo_Integrity}: {row.Integrity}")
        };
        if (row.Nand != null || row.FirmwareArchiveType != null || row.IsSaveBackup)
            messages.Add((error ? "ERROR" : "INFO", row.Summary));
        if (!string.IsNullOrWhiteSpace(row.MissingKeys))
            messages.Add(("WARN", _text.File_MissingKeysHeader + Environment.NewLine + row.MissingKeys));
        if (!string.IsNullOrWhiteSpace(row.Error) && row.Error != row.MissingKeys &&
            (row.VerificationChecks == null || row.Error == row.Package?.MetadataError || row.Package?.NeedsProdKeys == true))
            messages.Add((error ? "ERROR" : "WARN", row.Error));
        if (row.VerificationChecks != null) messages.AddRange(row.VerificationChecks.Select(DescribeCheckLog));
        LogFileSection(row.DisplayPath, messages);
    }

    private (string Level, string Message) DescribeCheckLog(NcaHashResult result) =>
        (result.Missing ? "WARN" : result.Error != null || result.Matches == false ? "ERROR"
            : result.Signature == LibHac.Common.Validity.Invalid ? "WARN" : "INFO", FormatCheck(result));

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
            if (_lastLogFile != safePath)
            {
                var name = Path.GetFileName(safePath.Split(" :: ", 2)[^1]);
                entries.Insert(0, new SessionMessage(time, entries.FirstOrDefault()?.Level ?? "INFO", name, safePath));
                _lastLogFile = safePath;
            }
        }
        else if (filePath == null && entries.Count > 0 && _lastLogFile != null)
        {
            entries.Insert(0, new SessionMessage(time, entries[0].Level, "Log", ""));
            _lastLogFile = null;
        }
        if (entries.Count == 0) return;
        if (_sessionMessages.Count == 0 && !entries[0].IsFileHeader && _lastLogFile is { } openFile)
        {
            var name = Path.GetFileName(openFile.Split(" :: ", 2)[^1]);
            _sessionMessages.Add(new SessionMessage(time, entries[0].Level, name, openFile));
        }
        _sessionMessages.AddRange(entries);
        try
        {
            var folder = Path.GetDirectoryName(_sessionLogPath)!; Directory.CreateDirectory(folder);
            File.AppendAllLines(_sessionLogPath, entries.Select(entry => entry.FilePath is { } path
                ? $"{entry.Time:HH:mm:ss}  [{entry.Level}]  =====> {path} <====="
                : entry.Text));
            if (!_logRetentionApplied) ApplyLogRetention();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { SettingsStatus.Text = ex.Message; }
        if (_sessionMessages.Count > 5000) _sessionMessages.RemoveRange(0, _sessionMessages.Count - 5000);
        if (LogTab.IsSelected && !_logRefreshPending)
        {
            _logRefreshPending = true;
            Dispatcher.UIThread.Post(() =>
            {
                _logRefreshPending = false;
                if (!_closed && LogTab.IsSelected) RefreshLog();
            }, DispatcherPriority.Background);
        }
    }
    private void ApplyLogRetention()
    {
        var folder = Path.GetDirectoryName(_sessionLogPath)!;
        if (!Directory.Exists(folder)) return;
        try
        {
            var paths = Directory.EnumerateFiles(folder, "*.log")
                .Where(path => System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(path), @"\A(?:NxFileViewer|Avalonia)-\d{8}-\d{6}-[a-f0-9]{8}\.log\z", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                .OrderByDescending(path => path == _sessionLogPath)
                .ThenByDescending(File.GetLastWriteTimeUtc)
                .Skip(Math.Clamp(_preferences.LogFileRetention, 1, 100)).ToArray();
            foreach (var path in paths) File.Delete(path);
            _logRetentionApplied = File.Exists(_sessionLogPath);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            SettingsStatus.Text = error.Message;
        }
    }
    private async void CopySelectedLog(object? sender, RoutedEventArgs e)
    {
        if (Clipboard != null && SessionLog.SelectedItem is SessionMessage entry) await Clipboard.SetTextAsync(entry.Text);
    }
    private void ClearLogView(object? sender, RoutedEventArgs e)
    {
        _sessionMessages.Clear();
        RefreshLog();
    }
    private void LogFilterChanged(object? sender, RoutedEventArgs e) { if (SessionLog != null) RefreshLog(); }
    private void RefreshLog()
    {
        bool MatchesFilter(SessionMessage row) => row.Level switch
        {
            "TRACE" => LogTrace.IsChecked == true,
            "DEBUG" => LogDebug.IsChecked == true,
            "WARN" => LogWarn.IsChecked == true,
            "ERROR" => LogError.IsChecked == true,
            "CRITICAL" => LogCritical.IsChecked == true,
            _ => LogInfo.IsChecked == true
        };
        var visible = new List<SessionMessage>();
        SessionMessage? header = null;
        foreach (var row in _sessionMessages)
        {
            if (row.IsFileHeader)
            {
                if (header != null && MatchesFilter(header)) visible.Add(header);
                header = row;
                continue;
            }
            if (!MatchesFilter(row)) continue;
            if (header != null) visible.Add(header);
            header = null;
            visible.Add(row);
        }
        if (header != null && MatchesFilter(header)) visible.Add(header);
        var rows = visible.ToArray();
        var selected = SessionLog.SelectedItem;
        SessionLog.ItemsSource = rows;
        if (selected != null && rows.Contains(selected)) SessionLog.SelectedItem = selected;
        Dispatcher.UIThread.Post(() =>
        {
            if (!_closed && LogTab.IsSelected && ReferenceEquals(SessionLog.ItemsSource, rows) && rows.Length > 0)
                SessionLog.ScrollIntoView(rows.Length - 1);
        }, DispatcherPriority.Loaded);
    }
}
