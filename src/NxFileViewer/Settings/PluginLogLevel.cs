namespace Emignatik.NxFileViewer.Settings;

/// <summary>Overrides the global minimum for messages attributed to a plugin.</summary>
public enum PluginLogLevel
{
    Inherit = -1, Trace = 0, Debug = 1, Information = 2,
    Warning = 3, Error = 4, Critical = 5, None = 6
}
