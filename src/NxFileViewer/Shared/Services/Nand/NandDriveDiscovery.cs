using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
namespace Emignatik.NxFileViewer.Services.Nand;

public sealed record NandDriveChoice(string Path, string Name, ulong Size = 0)
{
    public string DisplayName => (string.IsNullOrWhiteSpace(Name) ? Path : Name + " — " + Path)
        + (Size == 0 ? "" : $" ({Size / 1073741824d:F2} GiB)");
}

// Inventory only: listing must never depend on raw-read permissions or a drive letter.
public static class NandDriveDiscovery
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint QueryDosDevice(string? device, [Out] char[] target, uint length);
    public static IReadOnlyList<string> PhysicalPaths(IEnumerable<string> deviceNames) => deviceNames
        .Where(name => Regex.IsMatch(name, @"^PhysicalDrive[0-9]+$", RegexOptions.IgnoreCase))
        .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(name => ulong.Parse(name[13..]))
        .Select(name => @"\\.\" + name).ToArray();
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public static IReadOnlyList<NandDriveChoice> List()
    {
        var choices = new Dictionary<string, NandDriveChoice>(StringComparer.OrdinalIgnoreCase);
        for (var size = 16384; size <= 1024 * 1024; size *= 2) {
            var buffer = new char[size];
            var count = QueryDosDevice(null, buffer, (uint)buffer.Length);
            if (count != 0) {
                foreach (var path in PhysicalPaths(new string(buffer, 0, (int)count).Split('\0', StringSplitOptions.RemoveEmptyEntries)))
                    choices[path] = new(path, "");
                break;
            }
            if (Marshal.GetLastWin32Error() != 122) break;
        }
        // WMI supplies friendly model and capacity, including offline/unlettered disks.
        object? locator = null, service = null, result = null;
        try {
            var type = Type.GetTypeFromProgID("WbemScripting.SWbemLocator");
            if (type != null) {
                locator = Activator.CreateInstance(type);
                service = InvokeCom(locator!, "ConnectServer", BindingFlags.InvokeMethod, ".", @"root\cimv2");
                result = InvokeCom(service!, "ExecQuery", BindingFlags.InvokeMethod, "SELECT DeviceID, Model, Size FROM Win32_DiskDrive");
                foreach (var disk in result as System.Collections.IEnumerable ?? Array.Empty<object>()) {
                    try {
                        var path = ReadDiskProperty(disk, "DeviceID");
                        if (path.StartsWith(@"\\.\PhysicalDrive", StringComparison.OrdinalIgnoreCase)) {
                            var name = ReadDiskProperty(disk, "Model");
                            ulong.TryParse(ReadDiskProperty(disk, "Size"), out ulong bytes);
                            choices[path] = new(path, name.Trim(), bytes);
                        }
                    } finally { Release(disk); }
                }
            }
        } catch (Exception ex) when (ex is COMException or TargetInvocationException or MissingMemberException or UnauthorizedAccessException) { }
        finally { Release(result); Release(service); Release(locator); }
        foreach (var drive in DriveInfo.GetDrives()) {
            if (drive.DriveType is DriveType.Network or DriveType.NoRootDirectory) continue;
            var path = @"\\.\" + drive.Name.TrimEnd('\\');
            choices.TryAdd(path, new(path, drive.Name));
        }
        return choices.Values.OrderByDescending(choice => choice.Name.Contains("hekate", StringComparison.OrdinalIgnoreCase)).ToArray();
    }
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static string ReadDiskProperty(object disk, string name)
    {
        var properties = InvokeCom(disk, "Properties_", BindingFlags.GetProperty);
        object? property = null;
        try
        {
            property = InvokeCom(properties!, "Item", BindingFlags.InvokeMethod | BindingFlags.GetProperty, name);
            return Convert.ToString(InvokeCom(property!, "Value", BindingFlags.GetProperty)) ?? "";
        }
        finally { Release(property); Release(properties); }
    }

    private static object? InvokeCom(object target, string member, BindingFlags operation, params object[] arguments) =>
        target.GetType().InvokeMember(member, operation | BindingFlags.OptionalParamBinding, null, target, arguments);

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static void Release(object? value) { if (value != null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value); }
}
