using System.IO;

namespace Emignatik.NxFileViewer.Models.TreeItems.Impl;

public sealed class SaveBackupFileItem(string path, bool isLikely = false) : ItemBase(null)
{
    public bool IsLikely { get; } = isLikely;
    public override string Name => Path.GetFileName(path);
    public override string DisplayName => Name;
    public override string LibHacTypeName => "SaveBackup";
    public override string Format => "SaveBackup";
}
