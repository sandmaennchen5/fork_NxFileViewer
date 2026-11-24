using System;
using System.ComponentModel;
using System.IO;
using Emignatik.NxFileViewer.Services.KeysManagement;
using Emignatik.NxFileViewer.Services.Nand;
using LibHac.Common.Keys;

namespace NxFileViewer.Avalonia;

public partial class MainWindow
{
    private NandCliPlugin CreateNandService()
    {
        var settings = _preferences.SharedSettings;
        var manager = new NandPluginManager(settings, Path.Combine(ViewerDataDirectory.Root, "Plugins", "NxNandManager"));
        return new NandCliPlugin(settings, manager: manager, keyProvider: new NandKeyProvider(
            _keysPath, ResolveKeyFile(_preferences.TitleKeysPath, "title.keys")));
    }

    private sealed class NandKeyProvider(string? prod, string? title) : IKeySetProviderService
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
