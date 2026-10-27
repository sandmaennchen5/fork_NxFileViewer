using System;
using System.IO;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Globalization;
using Microsoft.Win32;
using Emignatik.NxFileViewer.Services.FileAssociations;
using Xunit;

namespace Emignatik.NxFileViewer.Test.Services;

public sealed class ExplorerNativeMenuTest
{
    [Theory]
    [InlineData("x64", Machine.Amd64)]
    [InlineData("x86", Machine.I386)]
    [InlineData("arm64", Machine.Arm64)]
    public void EmbeddedExtensionMatchesExplorerArchitecture(string architecture, Machine machine)
    {
        using var pe = new PEReader(new MemoryStream(ExplorerIntegration.NativePayload(architecture)));
        Assert.Null(pe.PEHeaders.CorHeader);
        Assert.Equal(machine, pe.PEHeaders.CoffHeader.Machine);
    }

    [Fact]
    public void RealNativeCommandsHideFileActionsForMultiSelectionAndFolders()
    {
        // Load the DLL directly: no registration, Explorer restart, or user registry changes.
        var root = Path.Combine(Path.GetTempPath(), "NxExplorerTest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var dll = Path.Combine(root, "NxFileViewer.Explorer.dll");
        var architecture = RuntimeInformation.ProcessArchitecture switch { Architecture.X64 => "x64", Architecture.X86 => "x86", _ => "arm64" };
        File.WriteAllBytes(dll, ExplorerIntegration.NativePayload(architecture));
        var module = NativeLibrary.Load(dll);
        var initialized = CoInitializeEx(IntPtr.Zero, 0) >= 0;
        IntPtr factory = IntPtr.Zero, command = IntPtr.Zero, enumeration = IntPtr.Zero;
        try
        {
            var getFactory = Marshal.GetDelegateForFunctionPointer<GetClassObject>(NativeLibrary.GetExport(module, "DllGetClassObject"));
            var canUnload = Marshal.GetDelegateForFunctionPointer<CanUnload>(NativeLibrary.GetExport(module, "DllCanUnloadNow"));
            Assert.Equal(0, canUnload());
            var clsid = new Guid(ExplorerIntegration.ClassId);
            var factoryId = new Guid("00000001-0000-0000-C000-000000000046");
            Assert.Equal(0, getFactory(ref clsid, ref factoryId, out factory));
            var commandId = new Guid("A08CE4D0-FA25-44AB-B57C-C7B1C323E0B9");
            Assert.Equal(0, Method<CreateInstance>(factory, 3)(factory, IntPtr.Zero, ref commandId, out command));
            Assert.Equal(1, canUnload());
            Assert.Equal(0, Method<GetFlags>(command, 9)(command, out var flags));
            Assert.Equal(1u, flags); // ECF_HASSUBCOMMANDS
            Assert.Equal(0, Method<EnumCommands>(command, 10)(command, out enumeration));
            var first = Path.Combine(root, "A game ü.nsp"); var second = Path.Combine(root, "B game.xci");
            var compressed = Path.Combine(root, "compressed.NSZ");
            var compressedXci = Path.Combine(root, "compressed.xcz");
            File.WriteAllBytes(compressed, Array.Empty<byte>()); File.WriteAllBytes(compressedXci, Array.Empty<byte>());
            var unsupported = Path.Combine(root, "notes.txt");
            File.WriteAllBytes(first, Array.Empty<byte>()); File.WriteAllBytes(second, Array.Empty<byte>()); File.WriteAllText(unsupported, "");
            var titles = ExpectedTitles();
            Assert.Equal(0, Method<GetTitle>(command, 3)(command, IntPtr.Zero, out var rootTitle));
            try { Assert.Equal("NxFileViewer Fork", Marshal.PtrToStringUni(rootTitle)); }
            finally { Marshal.FreeCoTaskMem(rootTitle); }
            for (var index = 0; index < 12; index++)
            {
                Assert.Equal(0, Method<NextCommand>(enumeration, 3)(enumeration, 1, out var child, out var fetched));
                Assert.Equal(1u, fetched);
                try
                {
                    Assert.Equal(0, Method<GetTitle>(child, 3)(child, IntPtr.Zero, out var title));
                    try { Assert.Equal(titles[index], Marshal.PtrToStringUni(title)); }
                    finally { Marshal.FreeCoTaskMem(title); }
                    AssertState(child, new[] { first }, index == 5 || index is 8 or 9 ? 2u : 0u);
                    AssertState(child, new[] { first, second }, index < 2 || index == 5 ? 2u : 0u);
                    AssertState(child, new[] { root }, index < 2 || index >= 4 ? 2u : 0u);
                    AssertState(child, new[] { first, root }, index < 2 || index >= 4 ? 2u : 0u);
                    AssertState(child, new[] { compressed }, index == 4 || index >= 6 ? 2u : 0u);
                    AssertState(child, new[] { compressed, compressedXci }, index < 2 || index == 4 || index >= 6 ? 2u : 0u);
                    AssertState(child, new[] { first, compressed }, index < 2 || index >= 4 ? 2u : 0u);
                    AssertState(child, new[] { unsupported }, 2);
                }
                finally { Release(child); }
            }
            AssertState(command, new[] { unsupported }, 2);
            Assert.Equal(1, Method<NextCommand>(enumeration, 3)(enumeration, 1, out _, out var finished));
            Assert.Equal(0u, finished);
            Release(enumeration); enumeration = IntPtr.Zero;
            Release(command); command = IntPtr.Zero;
            Release(factory); factory = IntPtr.Zero;
            Assert.Equal(0, canUnload());
        }
        finally
        {
            if (enumeration != IntPtr.Zero) Release(enumeration);
            if (command != IntPtr.Zero) Release(command);
            if (factory != IntPtr.Zero) Release(factory);
            if (initialized) CoUninitialize();
            NativeLibrary.Free(module);
            Directory.Delete(root, true);
        }
    }

    private static void AssertState(IntPtr command, string[] paths, uint expected)
    {
        var ids = new IntPtr[paths.Length];
        IntPtr selection = IntPtr.Zero;
        try
        {
            for (var i = 0; i < paths.Length; i++) { ids[i] = ILCreateFromPath(paths[i]); Assert.NotEqual(IntPtr.Zero, ids[i]); }
            Assert.Equal(0, SHCreateShellItemArrayFromIDLists((uint)ids.Length, ids, out selection));
            Assert.Equal(0, Method<GetState>(command, 7)(command, selection, 1, out var state));
            Assert.Equal(expected, state);
        }
        finally { if (selection != IntPtr.Zero) Release(selection); foreach (var id in ids) if (id != IntPtr.Zero) Marshal.FreeCoTaskMem(id); }
    }
    private static string[] ExpectedTitles()
    {
        using var config = Registry.CurrentUser.OpenSubKey(ExplorerIntegration.ConfigurationKey);
        var culture = config?.GetValue("Language") as string;
        if (string.IsNullOrEmpty(culture) || culture.Equals("Auto", StringComparison.OrdinalIgnoreCase))
            culture = CultureInfo.GetCultureInfo(GetUserDefaultUILanguage()).Name;
        var language = culture.Split('-')[0].ToLowerInvariant();
        var original = language switch {
            "de" => new[] { "Im File öffnen", "Im File prüfen", "Im Batch öffnen", "Im Batch prüfen", "Komprimieren", "Dekomprimieren" },
            "fr" => new[] { "Ouvrir dans File", "Vérifier dans File", "Ouvrir dans Batch", "Vérifier dans Batch", "Compresser", "Décompresser" },
            "es" => new[] { "Abrir en File", "Verificar en File", "Abrir en Batch", "Verificar en Batch", "Comprimir", "Descomprimir" },
            _ => new[] { "Open in File", "Verify in File", "Open in Batch", "Verify in Batch", "Compress", "Decompress" }
        };
        var packageTitles = language switch {
            "de" => new[] { "Konvertieren zu NSP", "Konvertieren zu XCI", "Zusammenführen zu NSP", "Zusammenführen zu XCI", "Aufteilen zu NSP", "Aufteilen zu XCI" },
            "fr" => new[] { "Convertir en NSP", "Convertir en XCI", "Fusionner en NSP", "Fusionner en XCI", "Diviser en NSP", "Diviser en XCI" },
            "es" => new[] { "Convertir a NSP", "Convertir a XCI", "Combinar en NSP", "Combinar en XCI", "Dividir en NSP", "Dividir en XCI" },
            _ => new[] { "Convert to NSP", "Convert to XCI", "Merge to NSP", "Merge to XCI", "Split to NSP", "Split to XCI" }
        };
        return System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Concat(original, packageTitles));
    }
    [DllImport("kernel32.dll")] private static extern ushort GetUserDefaultUILanguage();
    private static T Method<T>(IntPtr instance, int slot) where T : Delegate =>
        Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(instance), slot * IntPtr.Size));
    private static void Release(IntPtr instance) => Method<ReleaseObject>(instance, 2)(instance);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetClassObject(ref Guid clsid, ref Guid iid, out IntPtr value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int CanUnload();
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate uint ReleaseObject(IntPtr self);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int CreateInstance(IntPtr self, IntPtr outer, ref Guid iid, out IntPtr value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int EnumCommands(IntPtr self, out IntPtr value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int NextCommand(IntPtr self, uint count, out IntPtr value, out uint fetched);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetTitle(IntPtr self, IntPtr items, out IntPtr title);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetState(IntPtr self, IntPtr items, int allowSlow, out uint state);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetFlags(IntPtr self, out uint flags);
    [DllImport("ole32.dll")] private static extern int CoInitializeEx(IntPtr reserved, uint mode);
    [DllImport("ole32.dll")] private static extern void CoUninitialize();
    [DllImport("shell32.dll", EntryPoint = "ILCreateFromPathW", CharSet = CharSet.Unicode)] private static extern IntPtr ILCreateFromPath(string path);
    [DllImport("shell32.dll")] private static extern int SHCreateShellItemArrayFromIDLists(uint count, [In] IntPtr[] items, out IntPtr selection);
}
