using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Emignatik.NxFileViewer.Models.Overview;

namespace Emignatik.NxFileViewer.Models.TreeItems.Impl;

public sealed class NroFileItem : ItemBase
{
    public NroFileItem(string path, CancellationToken token = default) : base(null)
    {
        Name = Path.GetFileName(path);
        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream);
        if (stream.Length < 0x80) throw new InvalidDataException("Truncated NRO header.");
        stream.Position = 0x10;
        if (reader.ReadUInt32() != 0x304f524e) throw new InvalidDataException("Invalid NRO0 header.");
        reader.ReadUInt32();
        var size = reader.ReadUInt32();
        if (size < 0x80 || size > stream.Length) throw new InvalidDataException("Invalid NRO size.");
        stream.Position = 0x40;
        BuildId = Convert.ToHexString(reader.ReadBytes(32)).ToLowerInvariant();
        if (size == stream.Length) return; // Assets are optional.
        if (stream.Length - size < 0x38) throw new InvalidDataException("Truncated NRO asset header.");
        stream.Position = size;
        if (reader.ReadUInt32() != 0x54455341) throw new InvalidDataException("Invalid NRO ASET header.");
        if (reader.ReadUInt32() != 0) throw new InvalidDataException("Unsupported NRO asset version.");
        var iconOffset = reader.ReadUInt64(); var iconSize = reader.ReadUInt64();
        var nacpOffset = reader.ReadUInt64(); var nacpSize = reader.ReadUInt64();
        var romOffset = reader.ReadUInt64(); var romSize = reader.ReadUInt64();
        ValidateRange(romOffset, romSize);
        Icon = ReadAsset(iconOffset, iconSize, 16 * 1024 * 1024);
        var nacp = ReadAsset(nacpOffset, nacpSize, 1024 * 1024);
        if (nacp.Length == 0) return;
        if (nacp.Length < 0x4000) throw new InvalidDataException("Truncated NRO NACP.");
        for (var i = 0; i < 16; i++)
        {
            var title = Text(nacp, i * 0x300, 0x200);
            if (title.Length > 0) Titles.Add(new TitleInfo(new NacpTitleEntry(title, Text(nacp, i * 0x300 + 0x200, 0x100)), (NacpLanguage)i) { Icon = Icon });
        }
        DisplayVersion = Text(nacp, 0x3060, 0x10);

        void ValidateRange(ulong offset, ulong length)
        {
            if (length == 0) return;
            if (offset < 0x38 || offset > (ulong)(stream.Length - size) || length > (ulong)(stream.Length - size) - offset)
                throw new InvalidDataException("NRO asset range exceeds file bounds.");
        }
        byte[] ReadAsset(ulong offset, ulong length, int maximum)
        {
            token.ThrowIfCancellationRequested();
            ValidateRange(offset, length);
            if (length > (ulong)maximum) throw new InvalidDataException("NRO metadata asset is too large.");
            if (length == 0) return [];
            stream.Position = checked(size + (long)offset);
            var result = new byte[(int)length]; stream.ReadExactly(result); return result;
        }
    }
    private static string Text(byte[] bytes, int offset, int length) => Encoding.UTF8.GetString(bytes, offset, length).Split('\0')[0];
    public List<TitleInfo> Titles { get; } = [];
    public byte[]? Icon { get; }
    public string BuildId { get; }
    public string DisplayVersion { get; } = "";
    public override string Name { get; }
    public override string DisplayName => Name;
    public override string LibHacTypeName => "NRO0";
    public override string Format => "NRO";
}
