using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;
using System.Threading.Tasks;

namespace Emignatik.NxFileViewer.Services.Mtp;

public sealed record MtpEntry(string Name, string Path, bool IsDirectory, bool IsDevice = false);
public interface IMtpBackend
{
    IReadOnlyList<MtpEntry> List(string? path);
    MtpEntry? Parent(string path);
    Stream OpenRead(string path);
}
public static class MtpSta
{
    public static Task<T> Run<T>(Func<T> action)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() => { try { completion.SetResult(action()); } catch (Exception ex) { completion.SetException(ex); } }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); return completion.Task;
    }
}
// Shell exposes MTP objects as virtual items; source access only binds read streams.
public sealed class ShellMtpBackend : IMtpBackend
{
    private const string Computer = "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}";
    private static readonly Guid EnumHandler = new("94F60519-2850-4924-AA5A-D15E84868039");
    private static readonly Guid StreamHandler = new("1CEBB3AB-7C10-499A-A417-92CA16C4CB83");
    public IReadOnlyList<MtpEntry> List(string? path)
    {
        var folder = Open(path ?? Computer);
        object? enumerator = null;
        try
        {
            var handler = EnumHandler; var iid = typeof(IEnumShellItems).GUID;
            Marshal.ThrowExceptionForHR(folder.BindToHandler(IntPtr.Zero, ref handler, ref iid, out enumerator));
            var items = (IEnumShellItems)enumerator;
            var result = new List<MtpEntry>();
            while (true)
            {
                var status = items.Next(1, out var item, out var fetched);
                Marshal.ThrowExceptionForHR(status);
                if (status != 0 || fetched == 0) break;
                try
                {
                    Marshal.ThrowExceptionForHR(item.GetAttributes(0x60400000, out var attributes));
                    if (path == null && ((attributes & 0x20000000) == 0 || (attributes & 0x40000000) != 0)) continue;
                    result.Add(Describe(item, path == null));
                }
                finally { Marshal.ReleaseComObject(item); }
            }
            return result;
        }
        finally { if (enumerator != null) Marshal.ReleaseComObject(enumerator); Marshal.ReleaseComObject(folder); }
    }
    public MtpEntry? Parent(string path)
    {
        var item = Open(path);
        try
        {
            if (item.GetParent(out var parent) != 0) return null;
            try { return Describe(parent); } finally { Marshal.ReleaseComObject(parent); }
        }
        finally { Marshal.ReleaseComObject(item); }
    }
    public Stream OpenRead(string path)
    {
        var item = Open(path);
        try
        {
            var handler = StreamHandler; var iid = typeof(IStream).GUID;
            Marshal.ThrowExceptionForHR(item.BindToHandler(IntPtr.Zero, ref handler, ref iid, out var stream));
            return new ShellReadStream((IStream)stream);
        }
        finally { Marshal.ReleaseComObject(item); }
    }
    private static MtpEntry Describe(IShellItem item, bool device = false)
    {
        Marshal.ThrowExceptionForHR(item.GetAttributes(0x20400000, out var attributes));
        var parsingName = Identity(item);
        var directory = (attributes & 0x20000000) != 0 && (attributes & 0x00400000) == 0;
        return new MtpEntry(directory || device ? Name(item, 0) : FileName(parsingName) ?? Name(item, 0), parsingName, directory, device);
    }
    private static string? FileName(string path)
    {
        var iid = typeof(IPropertyStore).GUID;
        IPropertyStore store;
        if (path.StartsWith("pidl:", StringComparison.Ordinal))
        {
            var pidl = Decode(path);
            try { if (SHGetPropertyStoreFromIDList(pidl, 0, ref iid, out store) < 0) return null; }
            finally { Marshal.FreeCoTaskMem(pidl); }
        }
        else if (SHGetPropertyStoreFromParsingName(path, IntPtr.Zero, 0, ref iid, out store) < 0) return null;
        try
        {
            var key = new PropertyKey { Format = new Guid("41CF5AE0-F75A-4806-BD87-59C7D9248EB9"), Id = 100 };
            var result = store.GetValue(ref key, out var value);
            try
            {
                if (result < 0 || value.Kind != 31) return null;
                var name = Marshal.PtrToStringUni(value.Data);
                return string.IsNullOrEmpty(name) ? null : name;
            }
            finally { PropVariantClear(ref value); }
        }
        finally { Marshal.ReleaseComObject(store); }
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct PropertyKey { public Guid Format; public uint Id; }
    [StructLayout(LayoutKind.Explicit, Size = 24)]
    private struct PropertyValue
    {
        [FieldOffset(0)] public ushort Kind;
        [FieldOffset(8)] public IntPtr Data;
    }
    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int GetAt(uint index, out PropertyKey key);
        [PreserveSig] int GetValue(ref PropertyKey key, out PropertyValue value);
        [PreserveSig] int SetValue(ref PropertyKey key, ref PropertyValue value);
        [PreserveSig] int Commit();
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHGetPropertyStoreFromParsingName(string path, IntPtr context, uint flags, ref Guid iid, out IPropertyStore store);
    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(ref PropertyValue value);
    private static string Name(IShellItem item, int kind)
    {
        Marshal.ThrowExceptionForHR(item.GetDisplayName(kind, out var value));
        try { return Marshal.PtrToStringUni(value) ?? throw new IOException("Missing MTP object name."); }
        finally { Marshal.FreeCoTaskMem(value); }
    }
    private static IShellItem Open(string path)
    {
        var iid = typeof(IShellItem).GUID;
        if (path.StartsWith("pidl:", StringComparison.Ordinal))
        {
            var pidl = Decode(path);
            try { Marshal.ThrowExceptionForHR(SHCreateItemFromIDList(pidl, ref iid, out var stored)); return stored; }
            finally { Marshal.FreeCoTaskMem(pidl); }
        }
        Marshal.ThrowExceptionForHR(SHCreateItemFromParsingName(path, IntPtr.Zero, ref iid, out var item)); return item;
    }
    // Portable-device parsing names are not always round-trippable (including virtual storage names).
    private static string Identity(IShellItem item)
    {
        Marshal.ThrowExceptionForHR(SHGetIDListFromObject(item, out var pidl));
        try
        {
            var size = ILGetSize(pidl);
            if (size is < 2 or > 65536) throw new InvalidDataException("Invalid MTP item identifier.");
            var bytes = new byte[size]; Marshal.Copy(pidl, bytes, 0, bytes.Length);
            return "pidl:" + Convert.ToBase64String(bytes);
        }
        finally { Marshal.FreeCoTaskMem(pidl); }
    }
    private static IntPtr Decode(string path)
    {
        var bytes = Convert.FromBase64String(path[5..]);
        if (bytes.Length is < 2 or > 65536 || bytes[^1] != 0 || bytes[^2] != 0) throw new InvalidDataException("Invalid MTP item identifier.");
        var pidl = Marshal.AllocCoTaskMem(bytes.Length); Marshal.Copy(bytes, 0, pidl, bytes.Length); return pidl;
    }
    [DllImport("shell32.dll")]
    private static extern int SHGetIDListFromObject([MarshalAs(UnmanagedType.IUnknown)] object item, out IntPtr pidl);
    [DllImport("shell32.dll")]
    private static extern uint ILGetSize(IntPtr pidl);
    [DllImport("shell32.dll")]
    private static extern int SHCreateItemFromIDList(IntPtr pidl, ref Guid iid, out IShellItem item);
    [DllImport("shell32.dll")]
    private static extern int SHGetPropertyStoreFromIDList(IntPtr pidl, uint flags, ref Guid iid, out IPropertyStore store);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHCreateItemFromParsingName(string path, IntPtr context, ref Guid iid, out IShellItem item);
    [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        [PreserveSig] int BindToHandler(IntPtr context, ref Guid handler, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out object value);
        [PreserveSig] int GetParent(out IShellItem parent);
        [PreserveSig] int GetDisplayName(int kind, out IntPtr name);
        [PreserveSig] int GetAttributes(uint mask, out uint attributes);
        [PreserveSig] int Compare(IShellItem other, uint hint, out int order);
    }
    [ComImport, Guid("70629033-E363-4A28-A567-0DB78006E6D7"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IEnumShellItems
    {
        [PreserveSig] int Next(uint count, out IShellItem item, out uint fetched);
        [PreserveSig] int Skip(uint count);
        [PreserveSig] int Reset();
        [PreserveSig] int Clone(out IEnumShellItems items);
    }
    private sealed class ShellReadStream(IStream source) : Stream
    {
        private readonly IntPtr _read = Marshal.AllocCoTaskMem(4);
        private bool _disposed;
        private long _position;
        public override bool CanRead => !_disposed;
        public override bool CanSeek => !_disposed;
        public override bool CanWrite => false;
        public override long Length { get { source.Stat(out var stat, 1); return stat.cbSize; } }
        public override long Position { get => _position; set => Seek(value, SeekOrigin.Begin); }
        public override int Read(byte[] buffer, int offset, int count)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var target = offset == 0 ? buffer : new byte[count];
            source.Read(target, count, _read); var read = Marshal.ReadInt32(_read);
            if (offset != 0) target.AsSpan(0, read).CopyTo(buffer.AsSpan(offset)); _position += read; return read;
        }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var target = origin == SeekOrigin.Begin ? offset : origin == SeekOrigin.Current ? checked(_position + offset) : checked(Length + offset);
            if (target < 0) throw new IOException("Negative MTP seek position.");
            if (target == _position) return _position;
            var position = Marshal.AllocCoTaskMem(8);
            try { source.Seek(target, 0, position); return _position = Marshal.ReadInt64(position); }
            finally { Marshal.FreeCoTaskMem(position); }
        }
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing)
        {
            if (!_disposed && disposing) { _disposed = true; Marshal.FreeCoTaskMem(_read); Marshal.ReleaseComObject(source); }
            base.Dispose(disposing);
        }
    }
}
