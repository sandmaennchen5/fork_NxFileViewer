using System;
using Emignatik.NxFileViewer.Utils.MVVM.Localization;
using LibHac.Ncm;

namespace Emignatik.NxFileViewer.Localization.Keys;

public class LocalizationKeys_EN : LocalizationKeysBase, ILocalizationKeys
{
    public string PluginLog_Title => "Plugin log levels";
    public string PluginLog_Inherit => "Use global setting";
    public string PluginLog_Off => "Off";
    public string PluginLog_Tip => "The plugin level overrides the global minimum for messages from this plugin. Applies to the log view and saved session log.";

    public string Update_RollbackConfirm => "Restore the previous application version? NxFileViewer Fork will close and restart. Removing old versions also removes this recovery option.";
    public string Update_Cleanup => "Remove old versions";
    public string Update_CleanupConfirm => "Permanently remove these old versions or update backups?\n\n{0}\n\nThese rollback versions will no longer be available. The current version and configured custom executables are retained.";
    public string Update_CleanupEmpty => "No old versions eligible for cleanup found. Running, recently completed or failed application updates are retained.";
    public string Update_CleanupDone => "Removed {0} old version(s) or update backup(s).";
    public string BatchDetails_Show => "Show details pane";
    public string BatchDetails_Hide => "Hide details pane";
    public string BatchPackages_SummaryOrder => "Use original package order in combined summaries";
    public string BatchPackages_SummaryOrderTip => "Disabled (default): Values in each combined column are sorted independently of package order.\n\n- Package type: Base / Update / DLC.\n- Version, display version and minimum system version: highest first, e.g. 22.0.0 / 17.0.0 / 2.0.0. Version components are compared numerically.\n- Master key: highest key revision first, e.g. MasterKey 10 / MasterKey 9 / MasterKey 2.\n- Other text and IDs: alphabetically ascending (A–Z), ignoring case.\n\nEach column is sorted separately. Values at the same position in different columns do not necessarily belong to the same package.\n\nEnabled: Combined values retain the original order of packages in the file, as before.\n\nAlso applies to combined CSV/Excel exports. The order and numbering of individual package rows (::1, ::2, …) remain unchanged.";
    public string BatchPackages_Title => "Multiple packages";
    public string BatchPackages_Display => "Display multiple packages as individual rows (::1, ::2, …)";
    public string BatchPackages_Export => "Export multiple packages as individual rows (::1, ::2, …)";
    public string BatchPackages_Tip => "Each row shows one package’s metadata. File path, size, verification result and file actions refer to the entire container file. Display and export can be configured independently.";
    public string BatchExport_Title => "Batch export";
    public string BatchExport_VisibleOnly => "Export only visible columns in table order (otherwise: all columns in default order)";
    public string BatchExport_Format => "Default export format";
    public string BatchExport_NoColumns => "No columns are visible. Show at least one column before exporting.";
    public string BatchExport_InvalidExtension => "Choose a .csv or .xlsx file name.";
    public string Package_PublicationRecovery => "Publication was interrupted. Validated files have been kept in the following folders. A folder containing NXFV-INCOMPLETE.txt is not complete.";
    public string Features_PackageOperations => "Enable merge / split / NSP ↔ XCI (experimental)";
    public string Features_PackageOperationsDisabled => "This experimental feature is disabled. Enable it under Settings → Features and restart the application.";
    public string Package_Title => "Merge / Content split / NSP ↔ XCI (experimental)";
    public string Package_Engine => "Package engine";
    public string Package_RustPath => "cxfcxf / nscb_rust — executable";
    public string Package_KronosPath => "Kronos — portable folder (runtime and ztools)";
    public string Package_Tip => "Apply settings before starting an operation. Select one version per title. Sources are preserved; results are written to a new folder. NSZ compression uses the existing NSZ plugin.";
    public string Package_MergeNsp => "Merge → NSP";
    public string Package_MergeXci => "Merge → XCI";
    public string Package_ConvertNsp => "Convert → NSP";
    public string Package_ConvertXci => "Convert → XCI";
    public string Package_Split => "Split contents → NSP";
    public string Package_Destination => "Select output folder";
    public string Package_Inputs => "Select local NSP/XCI files";
    public string Package_Done => "Output folder: ";
    public string Package_Missing => "Selected engine is not configured or incomplete.";
    public string Package_Invalid => "Output validation failed. No result was published.";
    public string Package_Ready => "Ready";
    public string Package_Apply => "Uses saved settings: ";

    public string XciRepair_Title => "Repair XCI container";
    public string XciRepair_Inspect => "Checking XCI for known layout errors…";
    public string XciRepair_Prompt => "A known repairable container error was found in “{0}”: {1}\n\nCreate and open a repaired copy? The original will remain unchanged. The copy is fully verified before publication.";
    public string XciRepair_HeaderIssue => "The Gamecard header does not match the contents.";
    public string XciRepair_LayoutIssue => "Entry sizes after decompression are incorrect. The matching XCZ in the same folder allows correction.";
    public string XciRepair_SaveCopy => "Save repaired XCI copy";
    public string XciRepair_Copying => "Copying XCI for repair…";
    public string XciRepair_Verifying => "Verifying repaired XCI";
    public string XciRepair_NewFileRequired => "Choose a new filename ending in .xci. Existing files will not be overwritten.";
    public string XciRepair_SourceChanged => "The file or its XCZ source has changed since inspection. Please open it again.";
    public string XciRepair_VerificationFailed => "The repaired copy failed integrity verification.";
    public string XciRepair_Failed => "Repair did not complete. The original remains unchanged.\n\n{0}";
    public string Features_Nand => "Enable NAND features";
    public string Features_NandDisabled => "NAND features are disabled. Enable them in Settings and restart.";
    public string Features_NszPlugin => "Enable NSZ plugin";
    public string Features_NandPlugin => "Enable NxNandManager plugin";
    public string Features_Firmware => "Enable firmware verification";
    public string Features_FirmwareDisabled => "Firmware verification is disabled. Enable it in Settings and restart.";
    public string Features_Title => "Optional features";
    public string Features_Plugins => "Enable plugins (NSZ / NxNandManager)";
    public string Features_Mtp => "Enable MTP (experimental)";
    public string Features_Restart => "Changes take effect after restarting the application.";
    public string Features_PluginsDisabled => "Plugins are disabled. Enable them in Settings and restart.";

    public string Nand_Detected => "NAND signatures detected. Integrity has not been checked.";
    public string Nand_OpenSave => "Open save";
    public string Nand_LeaveSave => "Back to partition";
    public string Nand_ExportSave => "Export entire save…";
    public string Nand_ExplorerTitle => "Title";
    public string Nand_ExplorerUserId => "User ID";
    public string Nand_NameCandidate => "NAND candidate identified by filename. Read NAND information to confirm and obtain extended details.";
    public string Nand_Installed => "NxNandManager plugin installed";
    public string Nand_CustomUpdate => "Clear and apply the custom EXE path to use managed downloads.";
    public string Nand_Updating => "Downloading and checking NxNandManager…";
    public string Nand_Open => "Open NAND dump…";
    public string Nand_Info => "NAND information";
    public string Nand_Export => "Export partition…";
    public string Nand_SettingsTip => "Leave the EXE path empty to use managed downloads under Settings → Updates → Plugins → NxNandManager. A custom EXE is never replaced.";
    public string Nand_BisKeys => "BIS key file (optional; empty = NxFileViewer Fork's active prod.keys)";
    public string Nand_Tip => "Open a NAND dump (first file for split dumps). Export copies the selected partition as stored, without decryption. Configure NxNandManager under Settings → Plugins.";
    public string Nand_NewTarget => "Select a new local output file. Existing files cannot be replaced.";
    public string Nand_SourceMissing => "NAND source file is missing or is not a local file.";
    public string Nand_NotInstalled => "NxNandManager is not installed. Install it under Settings → Updates → Plugins.";
    public string Nand_OpenDrive => "Open drive…";
    public string Nand_Explorer => "NAND Explorer";
    public string Nand_ExplorerUp => "Parent folder";
    public string Nand_ExportFile => "Export file…";
    public string Nand_ExplorerFolder => "Folder";
    public string Nand_DriveTip => @"Select a NAND drive (e.g. \\.\PhysicalDrive2). Access is read-only. Physical drives may require administrator rights.";
    public string Nand_KeysMissing => "The configured BIS key file does not exist.";
    public string Nand_ExportFailed => "NxNandManager did not produce a non-empty partition file.";
    public string Nand_ExportDone => "Partition exported.";
    public string Nand_Cancelled => "Cancelled or information request timed out.";
    public string DataUpdate_Firmware => "Firmware hashes";
    public string DataUpdate_Title => "Updates";
    public string DataUpdate_Titles => "Refresh TitleDB";
    public string BatchNaming_Unchecked => "Unchecked";
    public string DataUpdate_LocalFirmwareVersion => "Local hash references through firmware {0}.";
    public string DataUpdate_NoLocalFirmware => "No local firmware hash references installed.";
    public string DataUpdate_TitleCatalogDate => "TitleDB {0}: local cache updated {1}.";
    public string DataUpdate_TitleCatalogMissing => "TitleDB {0}: no local catalog.";
    public string DataUpdate_OnlineFirmwareVersion => "Online hash references through firmware {0}.";
    public string Keys_ExistingValidation => "Existing file:";
    public string Keys_IncomingValidation => "Incoming file:";
    public string Keys_ReplaceDownloaded => "Use the downloaded keys and replace the existing file?";
    public string Keys_SaveTicketKeys => "Save missing ticket keys to title.keys";
    public string Keys_TicketConflict => "Ticket key conflict for {0} in {1}: existing entry preserved.";
    public string Keys_TicketSaved => "Ticket key for {0} saved to {1}.";
    public string Tinfoil_StabilityHint => "Tinfoil may be temporarily unavailable or unstable. If requests fail, try again later or select another source.";
    public string DataUpdate_TitleTip => "Refreshes GitHub TitleDB for the saved region and US fallback in the program folder. The selected title provider stays unchanged.";
    public string DataUpdate_FirmwareTip => "Firmware checks still fetch fresh online hashes without a persistent cache. Only the separate button saves the offline package; previous local hashes are backed up.";
    public string DataUpdate_CheckFirmware => "Check online hashes";
    public string DataUpdate_SaveFirmware => "Update offline hashes";
    public string DataUpdate_Working => "Updating…";
    public string DataUpdate_TitlesDone => "TitleDB {0} refreshed: {1} catalog entries including US fallback.";
    public string DataUpdate_FirmwareSaved => "Offline hashes updated: {0} reference files.";
    public string DataUpdate_FirmwareChecked => "Online hashes checked: {0} reference files; nothing saved.";
    public string Update_Title => "Application updates";
    public string Update_IncludePrereleases => "Include pre-releases in application updates";
    public string Update_Prerelease => "Pre-release";
    public string Update_Auto => "Check for application updates on startup";
    public string Update_Check => "Check for updates";
    public string Update_Install => "Download and install";
    public string Update_RateLimited => "GitHub request limit reached. Try again after {0:g}.";
    public string Update_CachedAt => "Saved result from {0:g}.";
    public string Update_Checking => "Checking for updates…";
    public string Update_Current => "No newer published version available.";
    public string Component_UpdateAvailable => "Update available.";
    public string Component_NotInstalled => "Not installed.";
    public string Component_CustomVersion => "Custom version: automatic checking unavailable.";
    public string Update_Available => "Version {0} is available.";
    public string Update_Failed => "Update failed.";
    public string Update_Confirm => "Download and install version {0}? NxFileViewer Fork will restart. Keys, settings and plugins are retained.";
    public string Update_Downloading => "Downloading and verifying update…";
    public string Update_Installing => "Installing update…";
    public string Update_Cancelled => "Update cancelled.";
    public string Nsz_Mode => "Compression mode";
    public string Nsz_ModeAuto => "Automatic (NSZ: solid, XCZ: blocks)";
    public string Nsz_ModeSolid => "Solid / Blockless";
    public string Nsz_ModeBlock => "Block compression";
    public string Nsz_BlockSize => "Block size";
    public string Nsz_ModeTip => "Solid compresses slightly better. Blocks allow fast backward and random reads. Mode and block size apply only to compression.";
    public string Workspace_Plugins => "Plugins";
    public string Workspace_Home => "Home";
    public string Workspace_File => "File verification";
    public string Workspace_Menu => "Main menu";
    public string Nsz_Replace => "Replace";
    public string Nsz_Number => "Save with numbering";
    public string TitlePage_Custom => "Custom";
    public string Info_WithRuntime => "With bundled .NET";
    public string Info_WithoutRuntime => "Without bundled .NET — requires .NET 8 Desktop Runtime";
    public string Info_Description => "NxFileViewer Fork inspects and verifies Nintendo Switch files. Supports NSP, NSZ, XCI, XCZ, NCA, ZIP and 7z archives, firmware verification and NSZ conversion.";
    public string Info_Shortcuts => "Keyboard shortcuts";
    public string BatchHistory_Show => "Show";
    public string BatchHistory_LoadedNotice => "Loaded from history.\nThe logo was not saved.";
    public string BatchHistory_Title => "Recent batch checks";
    public string BatchHistory_SaveLogos => "Save logos in history";
    public string BatchHistory_Enable => "Save batch check history";
    public string BatchHistory_Limit => "Number of saved batch checks (1–100)";
    public string BatchHistory_Resume => "Resume";
    public string Dialog_Yes => "Yes";
    public string Dialog_No => "No";
    public string Nsz_Cancel => "Cancel";
    public string Nsz_DeleteSourcePrompt => "Delete source files after successful conversion and verification? They are kept on failure.";
    public string Nsz_SourceDeleted => "Source deleted";
    public string Nsz_SourceDeleteFailed => "Could not delete source";
    public string Nsz_Compress => "Compress and verify…";
    public string Nsz_Decompress => "Decompress and verify…";
    public string Nsz_CompressValid => "Compress valid files…";
    public string Nsz_DecompressValid => "Decompress valid files…";
    public string Nsz_Update => "Install / update plugin…";
    public string Nsz_Rollback => "Use previous version";
    public string Nsz_SelectDestination => "Select output directory";
    public string Nsz_PythonRuntimeFailed => "The external NSZ executable could not load its embedded Python DLL. Startup failed before keys or input files were checked. Select another NSZ CLI that runs on this computer in Settings → Plugin nicoboss/nsz.";
    public string Nsz_NotInstalled => "Plugin nicoboss/nsz is not installed.";
    public string Nsz_Updating => "Updating Plugin nicoboss/nsz…";
    public string Nsz_SourceSize => "Original size (bytes)";
    public string Nsz_OutputSize => "Output size (bytes)";
    public string Nsz_Verified => "Verified output";
    public string Nsz_Summary => "{0} converted and verified; {1} failed; {2} not processed. Originals were retained.";
    public string Nsz_SettingsTip => "Optional NSZ CLI path. Leave empty to manage official releases automatically. Original and output are checked; source files are kept.";
    public string Nsz_CheckUpdates => "Check for stable updates before conversion";
    public string Nsz_Level => "Compression level (1–22)";
    public string Nsz_OutputExists => "Output already exists:";
    public string Nsz_SourceInvalid => "Source integrity check failed:";
    public string Nsz_OutputInvalid => "Output integrity check failed:";
    public string Nsz_OutputMissing => "NSZ did not create the expected output.";
    public string Nsz_KeysMissing => "No prod.keys file is loaded.";
    public string Nsz_Incompatible => "This NSZ CLI does not support the required options.";
    public string Nsz_OfflineFallback => "Update unavailable; using installed NSZ version.";
    public string Firmware_NoReferences => "Firmware hashes unavailable: GitHub could not be loaded and local references are missing or invalid.";
    public string Firmware_LoadingOnline => "Loading firmware hashes from GitHub…";
    public string Firmware_OnlineSource => "Hash source: GitHub (loaded for this check).";
    public string Firmware_OfflineSource => "Notice: GitHub unavailable. Using bundled hashes; newer firmware may be missing.";
    public string Firmware_BrowseZip => "Select ZIP / 7z…";
    public string Firmware_NcaMatches => "This NCA is included in these firmware versions:";
    public string Firmware_Versions => "Firmware version(s)";
    public string Firmware_Title => "Firmware verification";
    public string Firmware_Unknown => "No matching firmware hash reference found.";
    public string Firmware_Summary => "{0}/{1} files valid; missing: {2}, changed: {3}, extra: {4}, duplicate: {5}.";
    public string Firmware_Missing => "Missing";
    public string Firmware_Changed => "Changed (size/SHA-256)";
    public string Firmware_Renamed => "Incorrect filename (content matches)";
    public string Firmware_RenamedSummary => "Incorrect filenames: {0}.";
    public string Firmware_Extra => "Extra NCA";
    public string Firmware_Duplicate => "Duplicate filename";

    public override bool IsFallback => true;
    public override string DisplayName => "English";
    public override string CultureName => "en-US";
    public override string LanguageAuto => "Auto";

    public string FileNotSupported_Log => "File «{0}» not supported.";
    public string OpenSdCard => "Open SD card";
    public string OpenFile_Filter => "Nintendo Switch files (*.nsp;*.nsz;*.xci;*.xcz;*.nca;*.nro;*.zip;*.7z;*.bin;*.img;*.00)|*.nsp;*.nsz;*.xci;*.xcz;*.nca;*.nro;*.zip;*.7z;*.bin;*.img;*.00|Switch game packages (*.nsp;*.nsz;*.xci;*.xcz)|*.nsp;*.nsz;*.xci;*.xcz|Archives (*.zip;*.7z)|*.zip;*.7z|Switch content files (*.nca)|*.nca|NAND images and split dumps (*.bin;*.img;*.00)|*.bin;*.img;*.00|All files (*.*)|*.*";
    public string MenuItem_File => "File";
    public string MenuItem_Open => "Open...";
    public string MenuItem_OpenLast => "Open _last";
    public string MenuItem_Close => "Close";
    public string MenuItem_Exit => "E_xit";
    public string MenuItem_Tools => "Tools";
    public string MenuItem_CheckIntegrity => "Check _integrity";
    public string MenuItem_CheckDirectoryIntegrity => "Check folder integrity…";
    public string BatchNaming_Title => "Naming";
    public string BatchNaming_Matches => "Matches";
    public string BatchNaming_Differs => "Does not match";
    public string BatchNaming_Check => "Check naming";
    public string BatchNaming_RenameAll => "Rename all mismatches";
    public string BatchNaming_Error => "Naming error";
    public string BatchIntegrity_Title => "Batch file verification";
    public string BatchIntegrity_SelectDirectory => "Select folder containing Switch files";
    public string BatchIntegrity_Browse => "Browse…";
    public string BatchIntegrity_IncludeSubdirectories => "Include subdirectories";
    public string BatchTable_Games => "Games";
    public string BatchTable_Type => "Type";
    public string BatchTable_ConfirmResetFiltersAndSort => "Reset all search and filter settings and the batch table sorting?";
    public string BatchTable_ConfirmResetColumns => "Reset the visibility, order and width of all columns to their defaults? The saved column layout will be replaced.";
    public string BatchTable_OpenExplorer => "Open in Explorer";
    public string BatchTable_ResetColumns => "Reset columns";
    public string BatchTable_ConversionStatus => "Conversion status";
    public string BatchTable_OutputPath => "Output path";
    public string BatchTable_Columns => "Columns…";
    public string BatchTable_Search => "Search";
    public string BatchTable_All => "All";
    public string BatchTable_ResetFiltersAndSort => "Reset filters and sorting";
    public string BatchTable_ResetFilters => "Reset filters";
    public string BatchTable_ResetSort => "Reset sorting";
    public string BatchIntegrity_File => "File";
    public string BatchIntegrity_Path => "Path";
    public string BatchIntegrity_Error => "Error";
    public string BatchIntegrity_NszDataCorrupted => "The compressed NSZ/NCZ data stream is corrupted or incomplete (Zstandard decompression failed).";
    public string BatchIntegrity_IntegrityFailed => "The integrity check could not be completed. See the log for details.";
    public string PackageStructure_Filesystem => "Filesystem";
    public string Signature_Title => "NCA signature";
    public string Signature_Passed => "Passed";
    public string Signature_NotPassed => "Not passed";
    public string Signature_Unchecked => "Not checked";
    public string ToolTip_PackageStructure => "Structure following Nx Game Info:\nScene (XCI): Update, Normal and Secure partitions.\nConverted (XCI): Secure partition only; typical of NSP → XCI conversions.\nScene (NSP): legalinfo.xml, nacp.xml, programinfo.xml and cardspec.xml; typical of BBB releases.\nHomebrew (NSP): authoringtoolinfo.xml present.\nCDN (NSP): certificate (.cert) and ticket (.tik); typical of eShop CDN dumps.\nConverted (NSP): NCA/NCZ content plus other files, without a ticket/certificate or the Scene/Homebrew markers. This is a structural classification, not proof of the package’s origin.\nFilesystem: installed NAX0 titles on a Switch SD card.\nNCA contents only: the package contains only .nca/.ncz files, with no ticket, certificate or XML extras. This can occur in repacked or converted packages and does not prove that game data is missing.\nIncomplete: no NCA/NCZ content, a ticket without a certificate (or vice versa), or an XCI partition layout outside the Scene/Converted rules. This describes the container structure; use integrity verification to check for missing or modified game content.\nNSZ/XCZ follow their package rules; NCZ counts as NCA.";
    public string ToolTip_NcaSignature => "Passed: valid NCA signatures, as expected for official titles.\nNot passed: at least one invalid NCA signature; possible for homebrew, unexpected for official titles.\nNot checked: NCA signature verification has not completed. Run the integrity check.\nThis reports NCA header signatures; ACID is a separate NPDM signature.";
    public string ToolTip_Permission => "Safe: no filesystem service access or bit 0x8000000000000000 unset.\nUnsafe: filesystem service access and bit 0x8000000000000000 set (EraseMmc).\nDangerous: filesystem service access and mask 0xffffffffffffffff (all permissions).\nUnsafe/Dangerous should only occur for homebrew, not official games. Available for Base and Update only. This classifies permissions and is not a complete security assessment.";
    public string ToolTip_AcidSignature => "The ACID signature is a digital seal for the permissions section in main.npdm. This section describes which system services and file-system functions the program may access.\n\nMeaning of the displayed status:\n- Valid: The signature matches the checked ACID data and the verification key used.\n- Invalid: Signature verification failed. The data may have been modified or may not match the signature. This alone does not prove malware.\n- Unchecked: The signature was not checked. Its validity cannot be inferred from this status.\n- Empty field: No ACID information is available for this display.\n\nHow the checks differ:\n- ACID signature: checks the program’s signed permissions section.\n- NCA signature: checks the NCA header.\n- Integrity: checks file contents against their hashes.\n\nA valid ACID signature does not automatically mean that all file contents are unchanged or that the program is harmless.";
    public string PackageStructure_Title => "Package structure";
    public string PackageStructure_Scene => "Scene release";
    public string PackageStructure_Cdn => "CDN rip";
    public string PackageStructure_Converted => "Converted";
    public string PackageStructure_Homebrew => "Homebrew";
    public string PackageStructure_NcaOnly => "NCA contents only";
    public string PackageStructure_Incomplete => "Incomplete";
    public string PackageStructure_Unknown => "Unknown";
    public string FileInfo_FileSize => "File size";
    public string FileInfo_CompressionRatio => "Compression ratio";
    public string FileInfo_Uncompressed => "uncompressed";
    public string FileInfo_SystemUpdate => "Included system update (XCI)";
    public string BatchIntegrity_Export => "Export…";
    public string BatchIntegrity_Start => "Start check";
    public string BatchIntegrity_OpenSelected => "Open in file check";
    public string BatchIntegrity_MoveSelected => "Move…";
    public string BatchIntegrity_MoveValid => "Move valid files…";
    public string BatchIntegrity_SelectMoveDestination => "Select destination for valid files";
    public string BatchIntegrity_Moving => "Moving";
    public string MenuItem_Options => "Options";
    public string MenuItem_Settings => "Settings";
    public string MenuItem_ReloadKeys => "Reload keys";
    public string MenuItem_OpenTitleWebPage => "Open title Web page...";
    public string MenuItem_ShowRenameToolWindow => "Renaming tool...";

    public string Packages_Title => "Multi-package file";
    public string DisplayVersion => "Display Version";
    public string Presentation_Title => "Presentation";
    public string ToolTip_AvailableLanguages => "Title, Publisher and Icon may change according to the selected language.";
    public string AvailableLanguages => "Languages";
    public string AppTitle => "Title";
    public string Publisher => "Publisher";
    public string Security_Title => "Program security";
    public string Security_Level => "Assessment";
    public string Security_FileSystemPermissions => "File-system permissions";
    public string Security_AcidSignature => "ACID signature";
    public string Security_Safe => "Safe";
    public string Security_Unsafe => "Unsafe";
    public string Security_Dangerous => "Dangerous";
    public string Security_Unavailable => "Unavailable";
    public string Security_Details => "Authorized services ({0}): {1}";

    public string Lng_AmericanEnglish => "American";
    public string Lng_BritishEnglish => "English";
    public string Lng_CanadianFrench => "Canadian French";
    public string Lng_Dutch => "Dutch";
    public string Lng_French => "French";
    public string Lng_German => "German";
    public string Lng_Italian => "Italian";
    public string Lng_Japanese => "Japanese";
    public string Lng_Korean => "Korean";
    public string Lng_LatinAmericanSpanish => "Latin America";
    public string Lng_Portuguese => "Portuguese";
    public string Lng_Russian => "Russian";
    public string Lng_SimplifiedChinese => "Simplified Chinese";
    public string Lng_Spanish => "Spanish";
    public string Lng_TraditionalChinese => "Traditional Chinese";
    public string Lng_BrazilianPortuguese => "Brazilian Portuguese";
    public string Lng_Unknown => "Unknown";

    public string SettingsView_Title => "Settings";
    public string SettingsView_Button_Apply => "Apply";
    public string SettingsView_Button_Cancel => "Cancel";
    public string SettingsView_Button_Reset => "Reset";
    public string SettingsView_GroupBoxKeys => "Keys";
    public string SettingsView_Title_KeysEffectiveFilePath => "Effective path";
    public string SettingsView_Title_KeysCustomFilePath => "Custom path";
    public string SettingsView_Title_KeysDownloadUrl => "Download URL";
    public string KeysValidation_MissingFile => "No file found.";
    public string KeysValidation_ValidEntries => "Valid ({0} entries).";
    public string KeysValidation_MissingMasterKeys => "Missing master keys: {0}.";
    public string CnmtOverview_BaseTitleId => "Base title ID";
    public string CnmtOverview_MasterKey => "Required master key";
    public string CnmtOverview_MinimumApplicationVersion => "Minimum application version (DLC)";
    public string CnmtOverview_Distribution => "Distribution";
    public string KeysValidation_InvalidMasterKeys => "Invalid master keys: {0}.";
    public string KeysValidation_InvalidLines => "Malformed lines: {0}.";
    public string KeysValidation_EmptyFile => "The file contains no valid entries.";
    public string KeysValidation_FirmwareEstimate => "Highest valid revision: {0} — supports content up to firmware {1}.";
    public string KeysValidation_UnsupportedMasterKeys => "New master-key revision detected: {0}. This program version cannot validate it or map it to a firmware yet; an application update is required.";
    public string SettingsView_ToolTip_Keys => """
                                               Keys are mandatory to open encrypted Nintendo Switch files (XCI, NSP, ...).
                                               Each official Nintendo Switch file is encrypted with keys specific to the Switch firmware version it was built for.

                                               In order to open any Nintendo Switch file without any error, always ensure to have an up-to-date "prod.keys" file with all the keys of all existing firmwares.

                                               File should contain one key per line, in form of «KEY_NAME = HEXADECIMAL_VALUE».
                                               """ + "\n\n" + Keys_CopyToSwitchHelp;
    public string SettingsView_ToolTip_ProdKeys => """
                                                   This file contains common keys used by all Switch devices. This file is required for opening encrypted title contents.
                                                   The program will search this file in the following order:
                                                       1. the path defined by this setting
                                                       2. the current program's directory
                                                       3. the «%UserProfile%\\.switch» directory

                                                   Key files are downloaded only through the download buttons in the Keys tab.
                                                   Keys are never downloaded automatically at startup.
                                                   """;

    public string SettingsView_ToolTip_TitleKeys => """
                                                    You can optionally specify a file containing game-specific keys.
                                                    The program will search this file in the following locations:
                                                        1. the path defined by this setting
                                                        2. the current program's directory
                                                        3. the «%UserProfile%\\.switch» directory

                                                    Key files are downloaded only through the download buttons in the Keys tab.
                                                    Keys are never downloaded automatically at startup.
                                                    """;

    public string SettingsView_LogFileRetention => "Keep log files (1–100 launches)";
    public string SettingsView_LogLevel => "Log level";
    public string SettingsView_ToolTip_LogLevel => "The log level specifies the minimum level to log.";
    public string SettingsView_CheckBox_AlwaysReloadKeysBeforeOpen => "Always reload keys before opening a file";
    public string SettingsView_CheckBox_InjectTicketKeys => "Inject keys from ticket files (*.tik)";
    public string SettingsView_Title_Language => "Language";
    public string SettingsView_Title_Theme => "Theme";
    public string SettingsView_Title_NczOptions => "NSZ/XCZ settings";

    public string SettingsView_ToolTip_NczBlockLessCompression => """
                                                                  NSZ or XCZ file are composed of NCZ files which are NCA compressed files.
                                                                  NCZ files can be compressed without using the block compression method, which makes efficient random read access impossible.
                                                                  Thus, if the file is large and a small part needs to be read towards the end, it will be necessary to decompress the entire stream to reach the desired part.
                                                                  Large files can therefore take a long time to open.
                                                                  Prefer the use of block compression for large files.
                                                                  Note that if you choose not to allow the opening of blockless compressed NCZ files, it will not affect the integrity check features.
                                                                  """;

    public string SettingsView_CheckBox_NczOpenBlocklessCompression => "Open NCZ compressed without block";
    public string SettingsView_Title_Integrity => "Integrity";
    public string SettingsView_CheckBox_IgnoreMissingDeltaFragments => "Ignore missing delta fragments";
    public string SettingsView_ToolTip_IgnoreMissingDeltaFragments => $"""
                                                                      Patch files can contain full update files and incremental update files (known as {ContentType.DeltaFragment}).
                                                                      Those fragments are not mandatory to update an application and are sometimes intentionally removed.
                                                                      Check this option if you want to ignore missing {ContentType.DeltaFragment} while checking integrity.
                                                                      """;

    public string SettingsView_Miscellaneous => "Misc.";
    public string SettingsView_ToolTip_OpenKeysLocation => "Open keys file location.";
    public string SettingsView_ToolTip_BrowseKeys => "Browse...";
    public string SettingsView_ToolTip_DownloadKeys => "Download from specified URL.";

    public string BrowseKeysFile_ProdTitle => "Select \"prod\" keys file";
    public string BrowseKeysFile_TitleTitle => "Select \"title\" keys file";
    public string BrowseKeysFile_Filter => "Keys files (*.keys)|*.keys|Switch game packages (*.nsp;*.nsz;*.xci;*.xcz)|*.nsp;*.nsz;*.xci;*.xcz|Archives (*.zip;*.7z)|*.zip;*.7z|Switch content files (*.nca)|*.nca|NAND images and split dumps (*.bin;*.img;*.00)|*.bin;*.img;*.00|All files (*.*)|*.*";

    public string SuspiciousFileExtension => "File extension «{0}» seems invalid, «{1}» or «{2}» was expected.";
    public string DragMeAFile => "Drag me any supported file here :)";
    public string MultipleFilesDragAndDropNotSupported => "Multiple files drag and drop not supported, only the first file will be opened.";

    public string CnmtOverview_Title => "Package information";
    public string CnmtOverview_TitleId => "Title ID";
    public string CnmtOverview_ContentType => "Type";
    public string CnmtOverview_TitleVersion => "Version";
    public string CnmtOverview_MinimumSystemVersion => "Minimum system version";
    public string CnmtOverview_BuildID => "Build ID";
    public string CnmtOverview_BuildID_NotAvailableBecauseSectionIsSparse => "Not available (content sparse)";
    public string CnmtOverview_IsDemo => "Demo";

    public string ContextMenu_SaveImage => "Save...";
    public string CopyTitleImageError => "Failed to copy title image: {0}";
    public string SaveTitleImageError => "Failed to save title image: {0}";

    public string SaveDialog_Title => "Save as";
    public string SaveDialog_ImageFilter => "Image";
    public string SaveDialog_AnyFileFilter => "File";
    public string SaveFile_Error => "Failed to save file: {0}";

    public string ContextMenu_CopyImage => "Copy";

    public string TabOverview => "Overview";
    public string TabContent => "Content";
    public string GroupBoxStructure => "Structure";
    public string GroupBoxProperties => "Properties";

    public string ContextMenu_ShowItemErrors => "Show errors...";
    public string ContextMenu_SaveSectionItem => "Save section content...";
    public string ContextMenu_SaveDirectoryItem => "Save directory...";
    public string ContextMenu_SaveFileItem => "Save file...";
    public string ContextMenu_SavePartitionFileItem => "Save partition file...";
    public string ContextMenu_SaveNcaFileRaw => "Save NCA raw...";
    public string ContextMenu_SaveNcaFilePlaintext => "Save NCA plaintext...";

    public string SettingsLoadingError => "Failed to load settings: {0}";
    public string SettingsSavingError => "Failed to save settings: {0}";

    public string LoadingError_DiskFull => "There is not enough disk space to load or extract the file. Free space on the drive containing the temporary folder and try again. Loading has been stopped.";
    public string LoadingError_Failed => "Failed to load file «{0}»: {1}";
    public string LoadingError_FailedToCheckIfXciPartitionExists => "Failed to check if XCI partition exists: {0}";
    public string LoadingError_FailedToOpenXciPartition => "Failed to open XCI partition: {0}";
    public string LoadingError_FailedToLoadXciContent => "Failed to load XCI content: {0}";
    public string LoadingError_FailedToOpenPartitionFile => "Failed to open partition file: {0}";
    public string LoadingError_FailedToLoadNcaFile => "Failed to load NCA file: {0}";
    public string LoadingError_FailedToLoadPartitionFileSystemContent => "Failed to load partition file system content: {0}";
    public string LoadingError_FailedToCheckIfSectionCanBeOpened => "Failed to check if section can be opened: {0}";
    public string LoadingError_FailedToOpenNcaSectionFileSystem => "Failed to open content of NCA section «{0}»: {1}";
    public string LoadingError_FailedToLoadSectionContent => "Failed to load section content: {0}";
    public string LoadingError_FailedToGetFileSystemDirectoryEntries => "Failed to get file system directory entries: {0}";
    public string LoadingError_FailedToOpenNacpFile => "Failed to open NACP file: {0}";
    public string LoadingError_FailedToLoadNacpFile => "Failed to load NACP file: {0}";
    public string LoadingError_FailedToOpenCnmtFile => "Failed to open CNMT file: {0}";
    public string LoadingError_FailedToLoadCnmtFile => "Failed to load CNMT file: {0}";
    public string LoadingError_FailedToLoadNcaContent => "Failed to load NCA content: {0}";
    public string LoadingError_FailedToLoadDirectoryContent => "Failed to load directory content: {0}";
    public string LoadingError_FailedToLoadIcon_Log => "Failed to load icon: {0}";
    public string LoadingError_NcaFileMissing_Log => "NCA entry «{0}» of type «{1}» missing.";
    public string LoadingError_NoCnmtFound_Log => "No CNMT entry found!";
    public string LoadingError_NacpFileMissing_Log => "NACP file «{0}» not found!";
    public string LoadingError_NcaMissingSection_Log => "NCA of content type «{0}» is missing section of type «{0}».";
    public string LoadingError_MainFileMissing_Log => "File «{0}» not found!";
    public string LoadingError_IconMissing_Log => "Expected icon file «{0}» missing.";
    public string LoadingError_XciSecurePartitionNotFound_Log => "XCI secure partition not found!";
    public string LoadingError_FailedToGetNcaSectionFsHeader => "Failed to get NCA file system header of section «{0}»: {1}";
    public string LoadingError_FailedToOpenMainFile => "Failed to open Main file: {0}";
    public string LoadingError_FailedToLoadMainFile => "Failed to load Main file: {0}";
    public string LoadingError_FailedToOpenNpdmFile => "Failed to open main.npdm: {0}";
    public string LoadingError_FailedToLoadNpdmFile => "Failed to parse main.npdm: {0}";
    public string LoadingError_FailedToLoadTicketFile => "Failed to load ticket file: {0}";
    public string LoadingError_FailedToLoadTitleIdKey => "Failed to load Title ID key from ticket file «{0}»: {1}";
    public string LoadingError_NczBlocklessCompressionDisabled => "Opening of NCZ with blockless compression is disabled in the settings.";

    public string LoadingInfo_TitleIdKeySuccessfullyInjected => "Title ID key «{0}={1}» found in ticket file «{2}» successfully added to the set of keys.";
    public string LoadingWarning_TitleIdKeyReplaced => "Title ID key «{0}={1}» found in ticket file «{2}» has been used as replacement of the existing title ID key «{0}={2}» found in the set of keys.";
    public string LoadingDebug_TitleIdKeyAlreadyExists => "Title ID key «{0}={1}» found in ticket file «{2}» was already registered in the set of keys.";

    public string KeysFileUsed => "«{0}» file used: {1}";
    public string NoneKeysFile => "[none]";

    public string Status_DownloadingFile => "Downloading file «{0}»...";
    public string Log_DownloadingFileFromUrl => "Downloading «{0}» from URL «{1}»...";
    public string Log_FileSuccessfullyDownloaded => "File «{0}» successfully downloaded.";
    public string Log_FailedToDownloadFileFromUrl => "Failed to download «{0}» from URL «{1}»: {2}";

    public string ToolTip_PatchNumber => "Patch number {0}";
    public string Log_OpeningFile => "=====> {0} <=====";
    public string MainModuleIdTooltip => "Also known as «Build ID» (or BID).";
    public string ATaskIsAlreadyRunning => "A task is already running...";
    public string FileInfo_Title => "File";
    public string Title_FileInfo_FileType => "Type";
    public string Title_FileInfo_Compression => "Compression";
    public string Title_FileInfo_Integrity => "Integrity";
    public string ToolTip_NcasIntegrity => $"""
                                           Integrity check consists in verifying the integrity of each NCA (or NCZ).

                                           Integrity result can be any of the following:
                                           - {NcasIntegrity_NoNca}: No NCA file found.
                                           - {NcasIntegrity_Unchecked}: Integrity not checked.
                                           - {NcasIntegrity_InProgress}: Integrity check in progress.
                                           - {NcasIntegrity_Original}: All NCAs are original (signature and hash ok).
                                           - {NcasIntegrity_Incomplete}: All NCAs are original, but some are missing.
                                           - {NcasIntegrity_Modified}: At least one NCA is modified (signature is not ok, but hash is ok).
                                           - {NcasIntegrity_Corrupted}: At least one NCA is corrupted (hash invalid).
                                           - {NcasIntegrity_Error}: An error occurred during the integrity check.

                                           The detail of each analyzed NCA can be found in the «Content» tab.
                                           """;

    public string AvailableContents => "Contents:";
    public string MultiContentPackageToolTip => "Current package contains multiple contents («{0}» detected).";

    public string NcasIntegrity_Error_NcaMissing => "Integrity of NCA «{0}» can't be verified, NCA missing.";
    public string NcasIntegrity_Error_Log => "Failed to verify NCAs integrity: {0}";
    public string NcaIntegrity_GetOriginalNcaError => "Failed to get original NCA: {0}";
    public string NcaIntegrity_GetOriginalNcaError_Log => "Failed to get original NCA from NCA «{0}»: {1}";

    public string NcaHeaderSignature_Valid_Log => "Header signature of NCA «{0}» is valid.";
    public string NcaHeaderSignature_Invalid => "NCA header signature verification failed with status «{0}».";
    public string NcaHeaderSignature_Invalid_Log => "Header signature verification of NCA «{0}» failed with status «{1}».";
    public string NcaHeaderSignature_Error => "Failed to verify NCA header signature: {0}.";
    public string NcaHeaderSignature_Error_log => "Failed to verify signature of NCA header «{0}»: {1}";

    public string NcaHash_VerificationStart_Log => ">>> NCAs hash verification starting...";
    public string NcaHash_VerificationEnd_Log => ">>> NCAs hash verification finished.";
    public string NcaHash_NcaItem_CantExtractHashFromName => "Failed to extract expected hash from NCA name.";
    public string NcaHash_CantExtractHashFromName_Log => "Failed to extract expected hash from NCA name «{0}».";
    public string NcaHash_Valid_Log => "Hash of NCA «{0}» is valid.";
    public string NcaHash_NcaItem_Invalid => "Hash is not valid.";
    public string NcaHash_Invalid_Log => "Hash of NCA «{0}» is not valid.";
    public string NcaHash_NcaItem_Exception => "Failed to verify hash: {0}";
    public string NcaHash_Exception_Log => "Failed to verify hash of NCA «{0}»: {1}";
    public string NcaHash_ProgressText => "Hashing NCA {0}/{1}...";

    public string CancelAction => "Cancel";
    public string Status_Ready => "Ready.";
    public string LoadingFile_PleaseWait => "Loading, please wait...";

    public string NcasIntegrity_NoNca => "No NCA";
    public string NcasIntegrity_Unchecked => "Unchecked";
    public string NcasIntegrity_InProgress => "In progress";
    public string NcasIntegrity_Original => "Original";
    public string NcasIntegrity_Incomplete => "Incomplete";
    public string NcasIntegrity_Modified => "Modified";
    public string NcasIntegrity_Corrupted => "Corrupted";
    public string NcasIntegrity_Error => "Error";
    public string NcasIntegrity_Unknown => "Unknown";

    public string Status_SavingFile => "Saving file «{0}»...";

    public string KeysLoading_Starting_Log => ">>> Loading Keys...";
    public string KeysLoading_Successful_Log => ">>> Keys successfully loaded.";
    public string KeysLoading_UnusedKey_Log => "Note: Additional key «{0}» is not used by this program version.";
    public string KeysLoading_Error => "Failed to load keys: {0}.";
    public string WarnNoProdKeysFileFound => "No «prod.keys» file found.";
    public string InvalidSetting_KeysFileNotFound => "Keys file «{0}» defined in the settings doesn't exist.";
    public string InvalidSetting_BufferSizeInvalid => "Buffer size «{0}» defined in the settings is not valid, value should be strictly greater than 0.";
    public string InvalidSetting_LanguageNotFound => "Language «{0}» defined in the settings doesn't exist.";

    public string ToolTip_KeyMissing => "Key «{0}» of type «{1}» is missing.";

    public string MenuItem_CopyTextToClipboard => "Copy";
    public string ContextMenu_OpenFileLocation => "Open location...";
    public string OpenFileLocation_Failed_Log => "Failed to open location of file «{0}»: {1}";
    public string SettingsView_TitlePageUrl => "Title page URL";
    public string SettingsView_TitleInfoApiUrl => "Title information API URL";
    public string SettingsView_TitleInfoProvider => "Title name source";
    public string SettingsView_TitleDbRegion => "TitleDB region / language";
    public string SettingsView_TitleDbCacheTip => "TitleDB is stored locally and refreshed daily. Cached data remains available during outages. Missing titles are also looked up in US.en.";
    public string BatchIntegrity_FileType => "File type";
    public string BatchIntegrity_PackageType => "Package type";
    public string BatchIntegrity_ShowOnlyErrors => "Show faulty files only";
    public string OpenTitleWebPage_Failed => "Failed to open title Web page: {0}";

    public string Log_DownloadFileCanceled => "Download canceled.";
    public string Log_SaveToDirCanceled => "Directory saving canceled.";
    public string Log_SaveFileCanceled => "File saving canceled.";
    public string Log_SaveStorageCanceled => "Storage saving canceled.";
    public string Log_NcasIntegrityCanceled => "NCAs integrity canceled.";

    public string RenamingTool_TargetDirectory => "Target folder (empty = current folder)";
    public string RenamingTool_FolderTip => "Use / for folders in patterns, e.g. DLC/{WTitle}.{Ext:L}.";
    public string RenamingTool_OldName => "Old name";
    public string RenamingTool_NewName => "New name";
    public string RenamingTool_StatusError => "Error";
    public string RenamingTool_StatusUnchanged => "Unchanged";
    public string RenamingTool_StatusSimulation => "Simulation";
    public string RenamingTool_StatusRenamed => "Renamed";
    public string RenamingTool_WindowTitle => "Renaming tool";
    public string RenamingTool_Patterns => "Patterns";
    public string RenamingTool_ApplicationPattern => "Application pattern";
    public string RenamingTool_PatchPattern => "Patch pattern";
    public string RenamingTool_AddonPattern => "Add-on pattern";
    public string RenamingTool_InputPath => "Input path";
    public string RenamingTool_FileFilters => "Filters";
    public string RenamingTool_ToolTip_Patterns =>
        $$"""
         Target folder and subfolders:
           An empty target folder uses the file's current folder.
           Separate subfolders with / in the pattern, for example:
             DLC/{WTitle}.{Ext:L}
             {WAppTitle}/DLC/{WTitle}.{Ext:L}
           Use relative folders only; absolute paths and .. are not allowed.
           Simulation shows full paths without creating folders.
           Renaming creates missing folders and does not overwrite files.

         Keyword syntax:
            {<Keyword>[:<Format>]}

         Format is optional and can be:
         - U: Upper-case
         - L: Lower-case

         Examples:
           {Title} => The original title
           {Title:U} => The upper-case title

         Supported keywords:
           • TitleId:
              - The content id.
           • AppId:
              - The id of the corresponding {{nameof(ContentMetaType.Application)}} (for {{nameof(ContentMetaType.Application)}} contents, this value is equal to the {TitleId}).
           • PatchId:
              - If content is an {{nameof(ContentMetaType.Application)}}, this value is equal to the id of the corresponding {{nameof(ContentMetaType.Patch)}} content, otherwise zero.
           • PatchNum:
              - If content is an {{nameof(ContentMetaType.Application)}}, value is generally 0.
              - If content is a {{nameof(ContentMetaType.Patch)}}, value corresponds to the patch number.
              - If content is an {{nameof(ContentMetaType.AddOnContent)}}, value corresponds to the add-on patch number.
           • Title:
              - The first title among the list of declared titles.
              - This value exists only for contents of type {{nameof(ContentMetaType.Application)}} or {{nameof(ContentMetaType.Patch)}}, but not for {{nameof(ContentMetaType.AddOnContent)}}.
           • Ext:
              - The extension corresponding to the detected file type.
           • VerNum:
              - The content version number.
           • VerDsp:
              - The displayed version.
           • WTitle:
              - The content title retrieved from the Internet.
           • WAppTitle:
              - The title of the corresponding {{nameof(ContentMetaType.Application)}}, retrieved from the Internet.

         Use \{ or \} to write the literal chars { or }.
         """;
    public string RenamingTool_ToolTip_BasePattern => $"The pattern to use for contents of type {nameof(ContentMetaType.Application)}.";
    public string RenamingTool_ToolTip_PatchPattern => $"The pattern to use for contents of type {nameof(ContentMetaType.Patch)}.";
    public string RenamingTool_ToolTip_AddonPattern => $"The pattern to use for contents of type {nameof(ContentMetaType.AddOnContent)}.";
    public string RenamingTool_Button_Cancel => "Cancel";
    public string RenamingTool_Button_Rename => "Rename";
    public string RenamingTool_GroupBoxInput => "Input";
    public string RenamingTool_GroupBoxNamingSettings => "Naming settings";
    public string RenamingTool_BrowseDirTitle => "Select a directory";
    public string RenamingTool_GroupBoxOutput => "Output";
    public string RenamingTool_Miscellaneous => "Miscellaneous";
    public string RenamingTool_InvalidWindowsCharReplacement => "Replace unauthorized Windows characters with";
    public string RenamingTool_ReplaceWhiteSpaceChars => "Replace white space chars";
    public string RenamingTool_ReplaceWhiteSpaceCharsWith => "Replace white space chars with";
    public string RenamingTool_Simulation => "Simulation";
    public string RenamingTool_AutoCloseOpenedFile => "Auto close opened file";
    public string RenamingTool_IncludeSubDirectories => "Include subdirectories";
    public string RenamingTool_ContentTypeNotSupported => "Content type «{0}» not supported.";
    public string RenamingTool_SuperPackageNotSupported => "Super package not supported.";
    public string RenamingTool_LogNbFilesToRename => ">>> {0} file(s) to rename...";
    public string RenamingTool_LogSimulationMode => $"[SIMULATION] ";
    public string RenamingTool_LogFileRenamed => $"• {{0}}File renamed from{Environment.NewLine}\t«{{1}}» to{Environment.NewLine}\t«{{2}}».";
    public string RenamingTool_LogFileAlreadyNamedProperly => "• {0}«{1}» already named correctly.";
    public string RenamingTool_LogFailedToRenameFile => "• {0}«{1}»Renaming failed: {2}";
    public string RenamingTool_LogRenamingFailed => "Renaming failed: {0}";
    public string RenamingTool_BadInvalidFileNameCharReplacement => "The replacement string «{0}» (for invalid file name chars), can't contain invalid char «{1}».";

    public string Exception_UnexpectedDelimiter => "Unexpected delimiter {0} found at position {1}, use {2}{0} instead.";
    public string Exception_EndDelimiterMissing => "End delimiter {0} is missing.";
    public string FileRenaming_PatternKeywordUnknown => "Keyword «{0}» is unknown, allowed keywords are «{1}».";
    public string FileRenaming_EmptyPatternNotAllowed => "Pattern can't be empty.";
    public string FileRenaming_PatternKeywordNotAllowed => "Keyword «{0}» not allowed for patters of type «{1}».";
    public string FileRenaming_StringOperatorUnknown => "Operator «{0}» is not recognized, allowed operators are «{1}».";
    public string FileRenaming_EmptyDirectoryNotAllowed => "Input directory can't be empty.";
    public string Window_Tip_Title => "Tip";
    public string Nsz_Installed => "Plugin nicoboss/nsz installed";
    public string Nsz_CustomExecutable => "Custom executable";
    public string Settings_AutoVerifyBatchDrop => "Files dropped into batch verification";
    public string Settings_AutoVerifyBatchDropTip => "Applies to multiple files, folders and mixed selections, and individual files dropped onto the batch tab. Loads and verifies integrity instead of only loading. Verification can be cancelled.";
    public string Settings_AutoVerifyProgram => "Files opened inside the program";
    public string Settings_AutoVerifyProgramTip => "Applies to Open file and individual files opened by drag and drop. Integrity verification starts after loading and can be cancelled. Batch checks and history keep their own workflows.";
    public string Settings_AutoVerifyWindows => "Files opened from Windows";
    public string Settings_AutoVerifyWindowsTip => "Applies to double-click and Open with through Windows file associations, and a file path supplied at program startup. This setting is independent of opening files inside the program.";
    public string Settings_GroupGeneral => "General";
    public string Settings_GroupHistory => "History";
    public string Settings_GroupTitleInfo => "Title information";
    public string Settings_GroupLogging => "Logging";
    public string Settings_AutoVerifyTitle => "Automatic integrity verification";
    public string BatchHistory_RetentionTip => "The limit immediately affects the displayed history. The next time a batch is saved, only the newest entries up to this limit are retained. Checked files are not deleted.";
    public string Settings_Program => "Program";
    public string Nsz_PhaseSource => "Verify source";
    public string Nsz_PhaseLayout => "Checking and correcting XCZ partition layout…";
    public string Nsz_PhaseOutput => "Verify output";
    public string Nsz_PhasePublish => "Publish output";
    public string Batch_IncludeArchives => "Include ZIP / 7z";
    public string Batch_Scan => "Scan file list";
    public string Batch_VerifyAll => "Verify integrity of all files";
    public string File_SaveBackupSuspected => "Save backup (suspected)";
    public string Mtp_Title => "Sphaira (MTP)";
    public string Mtp_Tip => "Connect Switch by USB and enable MTP in Sphaira. Open the device, then select microSD, Contents, a file or a folder. Supported contents are copied to the selected cache; the Switch remains unchanged. SD contents require matching keys.";
    public string Mtp_Devices => "Refresh devices";
    public string Mtp_Select => "Use selection";
    public string Mtp_SelectContent => "Select a file or content folder on the device.";
    public string Mtp_Listing => "Sphaira (MTP): reading folder…";
    public string Mtp_NoFiles => "No supported files in this selection.";
    public string Mtp_NoDevice => "No entries found. Check the MTP connection or refresh devices.";
    public string Mtp_Copying => "Sphaira (MTP): copying {0} — file {1}/{2} — {3:N0}/{4:N0} bytes";
    public string Mtp_Cache => "Cache for the local copy";
    public string Archive_Reading => "Reading archive: {0}";
    public string Archive_Extracting => "Extracting / copying: {0} — file {1}/{2} — {3:N0}/{4:N0} bytes";
    public string Batch_GroupFiles => "Group by folder / archive";
    public string Batch_MultiPackageDetails => "Show or hide contained packages";
    public string File_SaveBackup => "Save backup";
    public string File_MissingKeysHeader => "Missing keys";
    public string File_MissingTitleKeysHeading => "Title keys missing (ticket / title.keys)";
    public string File_MissingTitleKeyEntry => "Missing title key · Rights ID: {0}";
    public string File_MissingTitleKeysHelp => "Title keys belong to the listed Rights ID and come from its matching ticket (.tik) or title.keys. A newer master key in prod.keys does not replace them. Check the matching ticket and, when applicable, enable injecting keys from tickets.";
    public string File_MissingSystemKeysHelp => "For missing system or console keys, check the specific names in the key files in use (for example prod.keys). A high master-key revision alone does not guarantee that every required key is present.";
    public string File_MissingKeys => "Required keys are missing. Some contents cannot be fully read.";
    public string File_CopyMissingKeys => "Copy missing key names";
    public string Keys_ProgramFolder => "Program directory";
    public string Keys_SharedFolder => "User profile (.switch)";
    public string Keys_InUse => "In use";
    public string Keys_DownloadAll => "Download keys";
    public string Keys_DownloadHost => "Download IP / hostname";
    public string Keys_DownloadHostTip => "{IP} in the download URLs is replaced with this address. Destination: custom path when set, otherwise the program directory.";
    public string Keys_CopyToSwitchHelp => @"Optional: Copies the currently used prod.keys and, if available, title.keys to %USERPROFILE%\.switch. Programs and other NxFileViewer Fork installations that support this shared search location can use the files there. Portable installations can keep their keys in their own program folder; an extra copy provides no additional decryption capability. The originals are retained. Later changes are not synchronized automatically. You are asked before existing destination files are replaced.";
    public string Keys_CopyToSwitch => @"Copy current keys to %USERPROFILE%\.switch";
    public string Keys_ReplaceShared => "Replace existing keys? Source files will be kept.";
    public string Keys_SharedCopied => "Keys are available in the shared .switch folder.";
    public string Batch_ScanAndVerify => "Scan file list and verify integrity";
    public string Mtp_Direct => "Read directly (experimental; otherwise local cache)";
    public string Mtp_DirectTip => "No upfront copy for NSP/NSZ, XCI/XCZ and individual NCA. Windows may cache internally; seeking can take a long time and cannot be cancelled while blocked. Use local cache for archives, NAND and SD structures.";
    public string Mtp_DirectUnsupported => "This selection requires local cache (archive, NAND or split NCA). Disable direct reading.";
    public string SdBatch_Title => "Switch SD: batch scan areas";
    public string SdBatch_Enable => "Check selected areas only";
    public string SdBatch_Tip => "Applies when opening a whole Switch SD card, including MTP. Unselected areas are skipped and not copied via MTP. Individually opened files or subfolders can still be checked.";
    public string SdBatch_Custom => "Custom areas (one path per line)";
    public string SdBatch_CustomTip => "In addition to the checkboxes. Folders or files relative to the SD root, e.g. /atmosphere, /backup/saves or /apps/tool.nro. Delete a line to remove it. No drive paths, wildcards or .. allowed.";
    public string SdBatch_InvalidPath => "Invalid SD path:";
    public string SdBatch_SourceAuto => "Detect automatically";
    public string SdBatch_SourceFolder => "Regular folder";
    public string SdBatch_SourceSd => "Switch SD";
    public string SdBatch_SourceTip => "Batch source type, including MTP. Auto detects Nintendo, emuMMC or hbmenu.nro at the root. Switch SD applies the SD area filter regardless of contents. Regular folder bypasses the SD area filter. Enable the area filter in Settings.";
    public string Nand_DriveRefresh => "Refresh drives";
    public string Nand_DriveAccessDenied => "The device was detected, but Windows denied read access. Run NxFileViewer Fork as administrator and select the drive again. Access remains read-only.";
    public string Associations_Title => "Windows file associations";
    public string Associations_Remove => "Remove file associations";
    public string Associations_Removed => "NxFileViewer Fork file-association registration was removed. If it was the default app, select another app using Open with in Windows. Explorer context menus can be removed separately.";
    public string Explorer_Title => "Explorer context menu";
    public string Explorer_Tip => "NxFileViewer Fork offers Batch and File actions: open without hashing, or verify with hashing. Multiple files and folders offer Batch only. Works when the app is closed. Classic registration is per user without administrator rights; on Windows 11 use Show more options. Add again after moving the app. Changes take effect immediately.";
    public string Explorer_AddClassic => "Add / update classic menu";
    public string Explorer_RemoveClassic => "Remove classic menu";
    public string Explorer_ModernTip => "Downloads the matching Explorer setup from GitHub and checks its version independently of the app. Confirm certificate trust and administrator access on first use. Windows may also show the menu in the classic menu; no additional classic registration is created. Preview releases follow the update setting. A local MSIX can be selected for offline installation.";
    public string Explorer_AddModern => "Install / update modern menu";
    public string Explorer_RemoveModern => "Remove modern menu";
    public string Explorer_Local => "Select local MSIX…";
    public string Explorer_Trusting => "Please confirm certificate trust in the opened window.";
    public string Explorer_NoPackage => "No matching Explorer setup found in GitHub releases. Enable preview updates if needed, or select a local MSIX.";
    public string Explorer_Done => "Explorer integration was updated. If the menu has not changed yet, restart Explorer or Windows.";
    public string Associations_Register => "Register NxFileViewer Fork";
    public string Associations_Tip => "Registers a separate fork identity for .nsp, .nsz, .xci, .xcz and .nro using the current executable path. Then choose NxFileViewer Fork in Windows: Open with → Choose another app → Always. Register again after moving the app. This button takes effect immediately.";
    public string Associations_Done => "NxFileViewer Fork was registered for .nsp, .nsz, .xci, .xcz and .nro. Select it as the default using Open with → Choose another app → Always.";
    public string Home_PluginLaunchTitle => "Launch plugins directly";
    public string Home_PluginLaunchTip => "Opens the installed program in its own window.";
    public string Home_NandDescription => "Manage NAND, partitions and saves";
    public string Home_NszDescription => "Compress and decompress game files";
    public string Home_LaunchExternal => "Open externally ↗";
    public string Home_NandLaunchTip => "Starts NxNandManager separately with configured keys. Available when installed and no background task is running.";
    public string Home_NszLaunchTip => "Starts the NSZ interface separately with active prod.keys. Requires an installed NSZ GUI build and no running background task.";
}
