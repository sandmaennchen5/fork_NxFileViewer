using System;
using System.ComponentModel;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Emignatik.NxFileViewer.Services.BackgroundTask;
using Emignatik.NxFileViewer.Utils;
using Emignatik.NxFileViewer.Services.KeysManagement;
using Emignatik.NxFileViewer.Services.Nand;
using LibHac.Common.Keys;

namespace NxFileViewer.Avalonia;

public partial class MainWindow
{
    private sealed record NandReport(string Information, IReadOnlyList<NandExplorerPartition> Partitions);

    private NandReport ReadNandReport(string source, NandDetectionResult? detection, bool requestPlugin,
        IProgressReporter progress, CancellationToken token)
    {
        var service = CreateNandService();
        IReadOnlyList<NandExplorerPartition> partitions = [];
        var lines = new List<string>();
        if (detection != null) lines.Add(detection.Information);
        try { partitions = service.ExplorerPartitions(source, token); }
        catch (Exception error) when (IsNandExplorerError(error))
        {
            if (detection is { Partitions.Count: 1 } single)
                partitions = [new NandExplorerPartition(single.Partitions[0], 0, single.Size)];
            else lines.Add(error.Message);
        }

        foreach (var partition in partitions)
        {
            var first = partition.Offset / 512;
            var last = first + (partition.Size - 1) / 512;
            var format = partition.Name switch
            {
                "BOOT0" or "BOOT1" or "PRODINFO" => "RAW",
                "PRODINFOF" or "SAFE" or "SYSTEM" or "USER" => "FAT32",
                _ when partition.Name.StartsWith("BCPKG2-", StringComparison.Ordinal) => "RAW",
                _ => "—"
            };
            lines.Add($"{partition.Name} · {partition.Size.ToFileSize()} · {format} · LBA {first}–{last} (0x{first:X}–0x{last:X})");
        }

        try
        {
            if (OperatingSystem.IsWindows() && service.IsEnabled && (requestPlugin || service.IsInstalled))
            {
                var details = service.ReadInformation(source, progress, token);
                if (!string.IsNullOrWhiteSpace(details))
                    lines.Add("NxNandManager" + Environment.NewLine + Emignatik.NxFileViewer.Logging.SessionFileLog.RedactSecrets(details));
            }
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            lines.Add("NxNandManager · " + _text.Nand_Cancelled);
        }
        catch (Exception error) when (IsNandExplorerError(error) || error is Win32Exception)
        {
            lines.Add("NxNandManager · " + error.Message);
        }
        return new NandReport(string.Join(Environment.NewLine + Environment.NewLine, lines), partitions);
    }

    private NandCliPlugin CreateNandService()
    {
        var settings = _preferences.SharedSettings;
        var manager = new NandPluginManager(settings, Path.Combine(ViewerDataDirectory.Root, "Plugins", "NxNandManager"));
        return new NandCliPlugin(settings, manager: manager, keyProvider: new NandKeyProvider(
            _keysPath, ResolveKeyFile(_preferences.TitleKeysPath, "title.keys")));
    }

    internal sealed class NandKeyProvider(string? prod, string? title) : IKeySetProviderService
    {
        public event PropertyChangedEventHandler? PropertyChanged { add { } remove { } }
        public string AppDirProdKeysFilePath => Path.Combine(ViewerDataDirectory.Root, "prod.keys");
        public string AppDirTitleKeysFilePath => Path.Combine(ViewerDataDirectory.Root, "title.keys");
        public string? ActualProdKeysFilePath => prod;
        public string? ActualTitleKeysFilePath => title;
        public KeyFileValidationResult ProdKeysValidation => KeyFileValidator.ValidateProdKeys(prod);
        public KeyFileValidationResult TitleKeysValidation => KeyFileValidator.ValidateTitleKeys(title);
        public void Reset() { }

        public KeySet GetKeySet(bool forceReload = false)
        {
            var keys = KeySet.CreateDefaultKeySet();
            ExternalKeyReader.ReadKeyFile(keys, File.Exists(prod) ? prod : null, File.Exists(title) ? title : null, null, null);
            return keys;
        }
    }
}
