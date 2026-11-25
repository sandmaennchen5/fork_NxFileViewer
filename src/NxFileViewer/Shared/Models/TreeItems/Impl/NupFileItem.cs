using System.IO;
using System;
using System.Collections.Generic;

namespace Emignatik.NxFileViewer.Models.TreeItems.Impl;

internal sealed class NupFileItem(string path) : ItemBase(null)
{
    public string FilePath { get; } = path;
    public NupContentItem ContentRoot { get; } = new(null, Path.GetFileName(path), "NUP");
    public NupPackageItem? PrimaryPackage { get; set; }
    public override string Name => Path.GetFileName(FilePath);
    public override string DisplayName => Name;
    public override string LibHacTypeName => "NUP";
    public override string Format => "NUP";
}

internal sealed partial class NupPackageItem(NupFileItem parent, string titleId, uint version, byte kind, uint directory) : ItemBase(parent)
{
    public byte Kind { get; } = kind;
    public uint Directory { get; } = directory;
    public string TitleId { get; } = titleId;
    public uint Version { get; } = version;
    public string Title { get; set; } = "";
    public string Publisher { get; set; } = "";
    public byte[]? Icon { get; set; }
    public string DisplayVersion { get; set; } = "";
    public string BuildId { get; set; } = "";
    public override string Name => TitleId;
    public override string DisplayName => $"{Format} · {TitleId} · v{Version}";
    public override string LibHacTypeName => "NUP";
    public override string Format => Kind switch
    {
        0 => "Base",
        1 => "Update",
        2 => "DLC",
        3 => "DLC Update",
        _ => "System"
    };

}

internal sealed class NupContentItem(ItemBase? parent, string name, string format, long? size = null) : ItemBase(parent)
{
    internal uint? DirectoryId { get; set; }
    internal Action? LoadChildren { get; set; }
    public long? Size { get; } = size;
    public override string Name => name;
    public override string DisplayName => Name;
    public override string LibHacTypeName => Format;
    public override string Format => format;

    public override IReadOnlyList<IItem> ChildItems
    {
        get
        {
            var load = LoadChildren;
            LoadChildren = null;
            try { load?.Invoke(); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                Errors.Add(Category.Loading, ex.Message);
            }
            return base.ChildItems;
        }
    }
}
