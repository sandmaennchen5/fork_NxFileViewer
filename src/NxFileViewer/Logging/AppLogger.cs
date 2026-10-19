using System;
using Emignatik.NxFileViewer.Settings;
using Microsoft.Extensions.Logging;

namespace Emignatik.NxFileViewer.Logging;

public class AppLogger : ILogger
{
    private readonly IAppSettings _appSettings;
    private readonly LogHandler _logHandler;
    private readonly string? _categoryName;

    public AppLogger(IAppSettings appSettings, string? categoryName, LogHandler logHandler)
    {
        _appSettings = appSettings ?? throw new ArgumentNullException(nameof(appSettings));
        _logHandler = logHandler ?? throw new ArgumentNullException(nameof(logHandler));
        _categoryName = categoryName;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        var message = "";
        if (IsEnabled(LogLevel.Debug) && _categoryName != null)
            message = _categoryName + ": ";

        var formattedMessage = formatter(state, exception);

        message += formattedMessage;
        if (exception != null)
            message += $"{Environment.NewLine}{exception}";
        _logHandler(logLevel, message);
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        var minimum = GetMinimumLevel();
        return logLevel >= LogLevel.Trace && logLevel < LogLevel.None && logLevel >= minimum && minimum < LogLevel.None;
    }

    private LogLevel GetMinimumLevel()
    {
        var category = _categoryName ?? "";
        var pluginLevel = category.StartsWith("Emignatik.NxFileViewer.Services.Nsz.", StringComparison.Ordinal) ? _appSettings.NszLogLevel
            : category.StartsWith("Emignatik.NxFileViewer.Services.Nand.", StringComparison.Ordinal) || category == "Emignatik.NxFileViewer.Views.Windows.NandViewModel" ? _appSettings.NandLogLevel
            : category.StartsWith("Emignatik.NxFileViewer.Services.Mtp.", StringComparison.Ordinal) || category == "Emignatik.NxFileViewer.Views.Windows.SphairaMtpWindow" ? _appSettings.MtpLogLevel
            : category.StartsWith("Emignatik.NxFileViewer.Services.PackageOperations.", StringComparison.Ordinal) ? _appSettings.PackageLogLevel
            : PluginLogLevel.Inherit;
        return pluginLevel >= PluginLogLevel.Trace && pluginLevel <= PluginLogLevel.None
            ? (LogLevel)pluginLevel : _appSettings.LogLevel;
    }

    public IDisposable BeginScope<TState>(TState state) where TState : notnull
    {
        throw new NotSupportedException();
    }
}
