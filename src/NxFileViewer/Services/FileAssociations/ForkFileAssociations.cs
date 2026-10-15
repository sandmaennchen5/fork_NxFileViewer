using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Emignatik.NxFileViewer.Services.FileAssociations;

public sealed record AssociationEntry(string Key, string Name, string Value);
public static class ForkFileAssociations
{
    public const string ProgId = "NxFileViewerFork.File";
    public const string ApplicationName = "NxFileViewer Fork";
    public static IReadOnlyList<string> Extensions { get; } = Array.AsReadOnly(new[] { ".nsp", ".nsz", ".xci", ".xcz", ".nro" });
    public static IReadOnlyList<AssociationEntry> Entries(string executable)
    {
        executable = Path.GetFullPath(executable);
        if (!Path.GetExtension(executable).Equals(".exe", StringComparison.OrdinalIgnoreCase) ||
            Path.GetFileName(executable).Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase) || executable.Contains('"'))
            throw new ArgumentException("Select the NxFileViewer executable.", nameof(executable));
        const string classes = @"Software\Classes\";
        const string capabilities = @"Software\NxFileViewerFork\Capabilities";
        var launcher = ForkAssociationLauncher.PathFor(executable);
        var application = classes + @"Applications\" + ForkAssociationLauncher.FileName;
        var icon = "\"" + executable + "\",0";
        var entries = new List<AssociationEntry> {
            new(application, "FriendlyAppName", ApplicationName),
            new(application + @"\DefaultIcon", "", icon),
            new(application + @"\shell\open\command", "", "\"" + launcher + "\" \"%1\""),
            new(classes + ProgId, "", ApplicationName),
            new(classes + ProgId + @"\DefaultIcon", "", icon),
            new(classes + ProgId + @"\shell\open\command", "", "\"" + launcher + "\" \"%1\""),
            new(classes + ProgId + @"\Application", "ApplicationName", ApplicationName),
            new(classes + ProgId + @"\Application", "ApplicationIcon", icon),
            new(capabilities, "ApplicationName", ApplicationName),
            new(capabilities, "ApplicationDescription", "Nintendo Switch file viewer"),
            new(capabilities, "ApplicationIcon", icon),
            new(@"Software\RegisteredApplications", ApplicationName, capabilities)
        };
        foreach (var extension in Extensions) {
            entries.Add(new(application + @"\SupportedTypes", extension, ""));
            entries.Add(new(classes + extension + @"\OpenWithProgids", ProgId, ""));
            entries.Add(new(capabilities + @"\FileAssociations", extension, ProgId));
        }
        return entries;
    }
    public static void RegisterCurrentExecutable()
    {
        Register(Environment.ProcessPath ?? throw new InvalidOperationException("Executable path unavailable."));
    }
    public static void Register(string executable)
    {
        if (!File.Exists(executable)) throw new FileNotFoundException("Executable not found.", executable);
        ForkAssociationLauncher.Ensure(executable);
        foreach (var entry in Entries(executable)) {
            using var key = Registry.CurrentUser.CreateSubKey(entry.Key);
            key.SetValue(entry.Name, entry.Value, RegistryValueKind.String);
        }
        // Earlier builds labeled the shared executable-name entry as the fork.
        // Remove only our label when its command still points to this exact viewer.
        using (var legacy = Registry.CurrentUser.OpenSubKey(@"Software\Classes\Applications\" + Path.GetFileName(executable), true)) {
            using var command = legacy?.OpenSubKey(@"shell\open\command");
            if (legacy?.GetValue("FriendlyAppName") as string == ApplicationName &&
                command?.GetValue("") as string == "\"" + Path.GetFullPath(executable) + "\" \"%1\"")
                legacy.DeleteValue("FriendlyAppName", false);
        }
        SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero); // SHCNE_ASSOCCHANGED
    }
    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(uint eventId, uint flags, IntPtr item1, IntPtr item2);
}
