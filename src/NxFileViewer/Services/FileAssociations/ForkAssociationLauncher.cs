using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using Microsoft.Win32;
namespace Emignatik.NxFileViewer.Services.FileAssociations;

public static class ForkAssociationLauncher
{
    public const string FileName = "NxFileViewerFork.Open.exe";
    public static string PathFor(string executable) => Path.Combine(Path.GetDirectoryName(Path.GetFullPath(executable))!, FileName);
    public static byte[] Payload() {
        using var stream = typeof(ForkAssociationLauncher).Assembly.GetManifestResourceStream("Viewer.AssociationLauncher")
            ?? throw new InvalidOperationException("Embedded association launcher missing.");
        using var bytes = new MemoryStream(); stream.CopyTo(bytes); return bytes.ToArray();
    }
    public static string Ensure(string executable) {
        _ = Emignatik.NxFileViewer.Services.Updates.ViewerPackageLayout.ExecutableName(Path.GetFileName(executable));
        if (!File.Exists(executable)) throw new FileNotFoundException("NxFileViewer Fork executable not found.", executable);
        var target = PathFor(executable); var payload = Payload();
        var reference = Path.Combine(Path.GetDirectoryName(target)!, "NxFileViewerFork.Open.target");
        var name = Path.GetFileName(executable);
        if (!File.Exists(reference) || File.ReadAllText(reference) != name)
        {
            var referenceTemp = reference + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try { File.WriteAllText(referenceTemp, name, new System.Text.UTF8Encoding(false)); File.Move(referenceTemp, reference, true); }
            finally { if (File.Exists(referenceTemp)) File.Delete(referenceTemp); }
        }
        if (File.Exists(target) && new FileInfo(target).Length == payload.Length &&
            SHA256.HashData(File.ReadAllBytes(target)).AsSpan().SequenceEqual(SHA256.HashData(payload))) return target;
        var temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try {
            File.WriteAllBytes(temporary, payload);
            File.Move(temporary, target, true);
        } finally { if (File.Exists(temporary)) File.Delete(temporary); }
        return target;
    }
    public static void RefreshInstalled() {
        var executable = Environment.ProcessPath;
        if (executable == null || !Path.GetExtension(executable).Equals(".exe", StringComparison.OrdinalIgnoreCase) || Path.GetFileName(executable).Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase)) return;
        var launcher = PathFor(executable);
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\" + ForkFileAssociations.ProgId + @"\shell\open\command");
        var registered = key?.GetValue("") as string;
        if (File.Exists(launcher) || registered == "\"" + launcher + "\" \"%1\"") Ensure(executable);
        if (registered == "\"" + launcher + "\" \"%1\"") ForkFileAssociations.Register(executable);
    }
}
