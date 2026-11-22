using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Emignatik.NxFileViewer.Logging;

internal static class SessionEnvironment
{
    internal static EnvironmentDetails Capture()
    {
        var assembly = typeof(SessionEnvironment).Assembly;
        var ui = assembly.GetReferencedAssemblies().Any(reference => reference.Name?.StartsWith("Avalonia", StringComparison.Ordinal) == true)
            ? "Avalonia"
            : assembly.GetReferencedAssemblies().Any(reference => reference.Name == "PresentationFramework") ? "WPF" : "Unknown";
        var platform = OperatingSystem.IsWindows() ? "Windows" : OperatingSystem.IsLinux() ? "Linux"
            : OperatingSystem.IsMacOS() ? "macOS" : RuntimeInformation.OSDescription;
        var os = RuntimeInformation.OSDescription;
        if (OperatingSystem.IsWindows())
        {
            try
            {
                using var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                using var key = machine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
                var product = key?.GetValue("ProductName") as string ?? "Windows";
                if (Environment.OSVersion.Version.Build >= 22000 && product.StartsWith("Windows 10", StringComparison.Ordinal))
                    product = product.Replace("Windows 10", "Windows 11");
                os = $"{product} {key?.GetValue("DisplayVersion")} ({RuntimeInformation.OSDescription}; revision {key?.GetValue("UBR")})";
            }
            catch (Exception error) when (error is UnauthorizedAccessException or System.Security.SecurityException or System.IO.IOException) { }
        }
        var distribution = Services.Updates.ViewerDistribution.IsSelfContained
            ? "self-contained (bundled .NET)" : "framework-dependent (system .NET)";
        return new EnvironmentDetails(platform, os, RuntimeInformation.ProcessArchitecture.ToString(),
            RuntimeInformation.OSArchitecture.ToString(), ui, distribution,
            RuntimeInformation.FrameworkDescription, RuntimeInformation.RuntimeIdentifier);
    }

    internal static string Describe()
    {
        var details = Capture();
        return $"OS: {details.OsDescription}; Platform: {details.Platform}; Process: {details.ProcessArchitecture}; " +
            $"OS architecture: {details.OsArchitecture}; UI: {details.UiFramework}; Distribution: {details.Distribution}; " +
            $"Runtime: {details.RuntimeVersion}; RID: {details.RuntimeIdentifier}.";
    }

    public sealed record EnvironmentDetails(string Platform, string OsDescription, string ProcessArchitecture,
        string OsArchitecture, string UiFramework, string Distribution, string RuntimeVersion, string RuntimeIdentifier);
}