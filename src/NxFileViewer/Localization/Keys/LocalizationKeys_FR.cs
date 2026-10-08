using System;
using Emignatik.NxFileViewer.Utils.MVVM.Localization;
using LibHac.Ncm;

namespace Emignatik.NxFileViewer.Localization.Keys;

public class LocalizationKeys_FR : LocalizationKeysBase, ILocalizationKeys
{
    public string Nand_Detected => "Signatures NAND dÃ©tectÃ©es. IntÃ©gritÃ© non vÃ©rifiÃ©e.";
    public string Nand_OpenSave => "Ouvrir la sauvegarde";
    public string Nand_LeaveSave => "Retour à la partition";
    public string Nand_ExportSave => "Exporter la sauvegarde…";
    public string Nand_ExplorerTitle => "Titre";
    public string Nand_ExplorerUserId => "ID utilisateur";
    public string Nand_NameCandidate => "Candidat NAND identifiÃ© par le nom du fichier. Consultez les informations NAND pour confirmer et obtenir les dÃ©tails.";
    public string Nand_Installed => "Plugin NxNandManager installÃ©";
    public string Nand_CustomUpdate => "Effacez puis appliquez le chemin EXE personnalisÃ© pour utiliser les tÃ©lÃ©chargements gÃ©rÃ©s.";
    public string Nand_Updating => "TÃ©lÃ©chargement et vÃ©rification de NxNandManagerâ€¦";
    public string Nand_Open => "Ouvrir un dump NANDâ€¦";
    public string Nand_Info => "Informations NAND";
    public string Nand_Export => "Exporter la partitionâ€¦";
    public string Nand_SettingsTip => "Laissez le chemin EXE vide pour les tÃ©lÃ©chargements gÃ©rÃ©s dans ParamÃ¨tres â†’ Updates â†’ Plugins â†’ NxNandManager. Un EXE personnalisÃ© reste inchangÃ©.";
    public string Nand_BisKeys => "Fichier de clÃ©s BIS (facultatif ; vide = prod.keys actif de NxFileViewer)";
    public string Nand_Tip => "Ouvrez un dump NAND (premier fichier pour un dump fractionnÃ©). Lâ€™export copie la partition telle quelle, sans dÃ©chiffrement. Configurez NxNandManager dans ParamÃ¨tres â†’ Plugins.";
    public string Nand_NewTarget => "Choisissez un nouveau fichier local. Les fichiers existants ne peuvent pas Ãªtre remplacÃ©s.";
    public string Nand_SourceMissing => "Le fichier source NAND est absent ou nâ€™est pas local.";
    public string Nand_NotInstalled => "NxNandManager non installÃ©. Installez-le dans ParamÃ¨tres â†’ Updates â†’ Plugins.";
    public string Nand_OpenDrive => "Ouvrir un lecteurâ€¦";
    public string Nand_Explorer => "Explorateur NAND";
    public string Nand_ExplorerUp => "Dossier parent";
    public string Nand_ExportFile => "Exporter le fichierâ€¦";
    public string Nand_ExplorerFolder => "Dossier";
    public string Nand_DriveTip => "SÃ©lectionnez un lecteur NAND. AccÃ¨s en lecture seule. Les disques physiques peuvent nÃ©cessiter des droits administrateur.";
    public string Nand_KeysMissing => "Le fichier de clÃ©s BIS configurÃ© est introuvable.";
    public string Nand_ExportFailed => "NxNandManager nâ€™a pas produit de fichier de partition non vide.";
    public string Nand_ExportDone => "Partition exportÃ©e.";
    public string Nand_Cancelled => "AnnulÃ© ou dÃ©lai de la requÃªte dâ€™informations dÃ©passÃ©.";
    public string DataUpdate_Firmware => "RÃ©fÃ©rences firmware";
    public string DataUpdate_Title => "Mises Ã  jour";
    public string DataUpdate_Titles => "Actualiser TitleDB";
    public string BatchNaming_Unchecked => "Non vÃ©rifiÃ©";
    public string DataUpdate_LocalFirmwareVersion => "RÃ©fÃ©rences locales jusqu'au firmware {0}.";
    public string DataUpdate_NoLocalFirmware => "Aucune rÃ©fÃ©rence locale de firmware installÃ©e.";
    public string DataUpdate_TitleCatalogDate => "TitleDB {0} : cache local actualisÃ© le {1}.";
    public string DataUpdate_TitleCatalogMissing => "TitleDB {0} : aucun catalogue local.";
    public string DataUpdate_OnlineFirmwareVersion => "RÃ©fÃ©rences en ligne jusqu'au firmware {0}.";
    public string Keys_ExistingValidation => "Fichier existant :";
    public string Keys_IncomingValidation => "Nouveau fichier :";
    public string Keys_ReplaceDownloaded => "Utiliser les clÃ©s tÃ©lÃ©chargÃ©es et remplacer le fichier existant ?";
    public string Keys_SaveTicketKeys => "Enregistrer les clÃ©s de ticket manquantes dans title.keys";
    public string Keys_TicketConflict => "Conflit de clÃ© de ticket pour {0} dans {1} : entrÃ©e existante conservÃ©e.";
    public string Keys_TicketSaved => "ClÃ© de ticket pour {0} enregistrÃ©e dans {1}.";
    public string Tinfoil_StabilityHint => "Tinfoil peut Ãªtre temporairement indisponible ou instable. En cas d echec, rÃ©essayez plus tard ou choisissez une autre source.";
    public string DataUpdate_TitleTip => "Actualise GitHub TitleDB pour la rÃ©gion enregistrÃ©e et US dans le dossier du programme. Le fournisseur sÃ©lectionnÃ© reste inchangÃ©.";
    public string DataUpdate_FirmwareTip => "Les vÃ©rifications tÃ©lÃ©chargent les rÃ©fÃ©rences sans cache permanent. Seul le bouton sÃ©parÃ© enregistre le paquet hors ligne et sauvegarde les anciennes rÃ©fÃ©rences.";
    public string DataUpdate_CheckFirmware => "VÃ©rifier les rÃ©fÃ©rences en ligne";
    public string DataUpdate_SaveFirmware => "Actualiser les rÃ©fÃ©rences hors ligne";
    public string DataUpdate_Working => "Actualisationâ€¦";
    public string DataUpdate_TitlesDone => "TitleDB {0} actualisÃ©e : {1} entrÃ©es avec US.";
    public string DataUpdate_FirmwareSaved => "RÃ©fÃ©rences hors ligne actualisÃ©es : {0} fichiers.";
    public string DataUpdate_FirmwareChecked => "RÃ©fÃ©rences en ligne vÃ©rifiÃ©es : {0} fichiers, rien enregistrÃ©.";
    public string Update_Title => "Mises Ã  jour du programme";
    public string Update_IncludePrereleases => "Inclure les prÃ©versions dans les mises Ã  jour du programme";
    public string Update_Prerelease => "PrÃ©version";
    public string Update_Auto => "Rechercher les mises Ã  jour au dÃ©marrage";
    public string Update_Check => "Rechercher les mises Ã  jour";
    public string Update_Install => "TÃ©lÃ©charger et installer";
    public string Update_Checking => "Recherche de mises Ã  jourâ€¦";
    public string Update_Current => "Aucune version publiÃ©e plus rÃ©cente.";
    public string Component_UpdateAvailable => "Mise Ã  jour disponible.";
    public string Component_NotInstalled => "Non installÃ©.";
    public string Component_CustomVersion => "Version personnalisÃ©e : vÃ©rification automatique indisponible.";
    public string Update_Available => "La version {0} est disponible.";
    public string Update_Failed => "Ã‰chec de la mise Ã  jour.";
    public string Update_Confirm => "TÃ©lÃ©charger et installer la version {0} ? NxFileViewer redÃ©marrera. Les clÃ©s, paramÃ¨tres et plugins sont conservÃ©s.";
    public string Update_Downloading => "TÃ©lÃ©chargement et vÃ©rificationâ€¦";
    public string Update_Installing => "Installationâ€¦";
    public string Update_Cancelled => "Mise Ã  jour annulÃ©e.";
    public string Nsz_Mode => "Mode de compression";
    public string Nsz_ModeAuto => "Automatique (NSZ : solid, XCZ : blocs)";
    public string Nsz_ModeSolid => "Solid / sans blocs";
    public string Nsz_ModeBlock => "Compression par blocs";
    public string Nsz_BlockSize => "Taille des blocs";
    public string Nsz_ModeTip => "Solid compresse un peu mieux. Les blocs permettent des lectures rapides en arriÃ¨re et alÃ©atoires. Ces options concernent uniquement la compression.";
    public string Workspace_Plugins => "Plugins";
    public string Workspace_Home => "Accueil";
    public string Workspace_File => "VÃ©rification de fichier";
    public string Workspace_Menu => "Menu principal";
    public string Nsz_Replace => "Remplacer";
    public string Nsz_Number => "Enregistrer avec un numÃ©ro";
    public string TitlePage_Custom => "PersonnalisÃ©e";
    public string Info_WithRuntime => "Avec .NET intÃ©grÃ©";
    public string Info_WithoutRuntime => "Sans .NET intÃ©grÃ© â€” nÃ©cessite .NET 8 Desktop Runtime";
    public string Info_Description => "NxFileViewer affiche et vÃ©rifie les fichiers Nintendo Switch : NSP, NSZ, XCI, XCZ, NCA, ZIP et 7z, le firmware et la conversion NSZ.";
    public string Info_Shortcuts => "Raccourcis clavier";
    public string BatchHistory_Show => "Afficher";
    public string BatchHistory_Title => "5 derniÃ¨res vÃ©rifications";
    public string BatchHistory_Resume => "Reprendre";
    public string Dialog_Yes => "Oui";
    public string Dialog_No => "Non";
    public string Nsz_Cancel => "Annuler";
    public string Nsz_DeleteSourcePrompt => "Supprimer les sources aprÃ¨s conversion et vÃ©rification rÃ©ussies ? Elles sont conservÃ©es en cas dâ€™Ã©chec.";
    public string Nsz_SourceDeleted => "Source supprimÃ©e";
    public string Nsz_SourceDeleteFailed => "Impossible de supprimer la source";
    public string Nsz_Compress => "Compresser et vÃ©rifierâ€¦";
    public string Nsz_Decompress => "DÃ©compresser et vÃ©rifierâ€¦";
    public string Nsz_CompressValid => "Compresser les fichiers validesâ€¦";
    public string Nsz_DecompressValid => "DÃ©compresser les fichiers validesâ€¦";
    public string Nsz_Update => "Installer / mettre Ã  jour le pluginâ€¦";
    public string Nsz_Rollback => "Utiliser la version prÃ©cÃ©dente";
    public string Nsz_SelectDestination => "Choisir le dossier de destination";
    public string Nsz_PythonRuntimeFailed => "NSZ ne peut pas charger sa DLL Python intÃ©grÃ©e. Le dÃ©marrage Ã©choue avant la vÃ©rification des clÃ©s ou fichiers. Choisissez une autre CLI NSZ fonctionnelle dans ParamÃ¨tres â†’ Plugin nicoboss/nsz.";
    public string Nsz_NotInstalled => "Plugin nicoboss/nsz non installÃ©.";
    public string Nsz_Updating => "Mise Ã  jour du plugin NSZâ€¦";
    public string Nsz_SourceSize => "Taille originale (octets)";
    public string Nsz_OutputSize => "Taille finale (octets)";
    public string Nsz_Verified => "RÃ©sultat vÃ©rifiÃ©";
    public string Nsz_Summary => "{0} convertis et vÃ©rifiÃ©s ; {1} Ã©checs ; {2} non traitÃ©s. Les originaux sont conservÃ©s.";
    public string Nsz_SettingsTip => "Chemin facultatif de la CLI NSZ. Laisser vide pour gÃ©rer automatiquement les versions officielles. Source et rÃ©sultat sont vÃ©rifiÃ©s ; les originaux sont conservÃ©s.";
    public string Nsz_CheckUpdates => "Rechercher les mises Ã  jour stables avant conversion";
    public string Nsz_Level => "Niveau de compression (1â€“22)";
    public string Nsz_OutputExists => "Le fichier cible existe dÃ©jÃ  :";
    public string Nsz_SourceInvalid => "Ã‰chec de vÃ©rification de la source :";
    public string Nsz_OutputInvalid => "Ã‰chec de vÃ©rification du rÃ©sultat :";
    public string Nsz_OutputMissing => "NSZ nâ€™a pas crÃ©Ã© le fichier attendu.";
    public string Nsz_KeysMissing => "Aucun fichier prod.keys chargÃ©.";
    public string Nsz_Incompatible => "Cette CLI NSZ ne prend pas en charge les options requises.";
    public string Nsz_OfflineFallback => "Mise Ã  jour indisponible ; utilisation de la version NSZ installÃ©e.";
    public string Firmware_NoReferences => "Empreintes indisponibles : GitHub inaccessible et rÃ©fÃ©rences locales absentes ou invalides.";
    public string Firmware_LoadingOnline => "Chargement des empreintes depuis GitHubâ€¦";
    public string Firmware_OnlineSource => "Source : GitHub (chargÃ©e pour cette vÃ©rification).";
    public string Firmware_OfflineSource => "GitHub indisponible. Empreintes fournies utilisÃ©es ; des firmwares rÃ©cents peuvent manquer.";
    public string Firmware_BrowseZip => "Choisir un ZIP / 7zâ€¦";
    public string Firmware_NcaMatches => "Cette NCA figure dans les versions suivantes :";
    public string Firmware_Versions => "Version(s) du firmware";
    public string Firmware_Title => "VÃ©rification du firmware";
    public string Firmware_Unknown => "Aucune rÃ©fÃ©rence de firmware correspondante.";
    public string Firmware_Summary => "{0}/{1} fichiers valides ; manquants : {2}, modifiÃ©s : {3}, supplÃ©mentaires : {4}, doublons : {5}.";
    public string Firmware_Missing => "Manquant";
    public string Firmware_Changed => "ModifiÃ© (taille/SHA-256)";
    public string Firmware_Renamed => "Nom incorrect (contenu identique)";
    public string Firmware_RenamedSummary => "Noms incorrects : {0}.";
    public string Firmware_Extra => "NCA supplÃ©mentaire";
    public string Firmware_Duplicate => "Nom en double";

    public override bool IsFallback => true;
    public override string DisplayName => "FranÃ§ais";
    public override string CultureName => "fr-FR";
    public override string LanguageAuto => "Automatique";

    public string FileNotSupported_Log => "Fichier Â«{0}Â» non supportÃ©.";
    public string OpenSdCard => "Ouvrir une carte SD";
    public string OpenFile_Filter => "Fichiers Nintendo Switch (*.nsp;*.nsz;*.xci;*.xcz;*.nca;*.nro;*.zip;*.7z;*.bin;*.img;*.00)|*.nsp;*.nsz;*.xci;*.xcz;*.nca;*.nro;*.zip;*.7z;*.bin;*.img;*.00|Paquets de jeux Switch (*.nsp;*.nsz;*.xci;*.xcz)|*.nsp;*.nsz;*.xci;*.xcz|Archives (*.zip;*.7z)|*.zip;*.7z|Fichiers de contenu Switch (*.nca)|*.nca|Images NAND et dumps fractionnÃ©s (*.bin;*.img;*.00)|*.bin;*.img;*.00|Tous les fichiers (*.*)|*.*";
    public string MenuItem_File => "Fichier";
    public string MenuItem_Open => "Ouvrir...";
    public string MenuItem_OpenLast => "Ouvrir le _dernier";
    public string MenuItem_Close => "Fermer";
    public string MenuItem_Exit => "Q_uitter";
    public string MenuItem_Tools => "Outils";
    public string MenuItem_CheckIntegrity => "VÃ©rifier l'_intÃ©gritÃ©";
    public string MenuItem_CheckDirectoryIntegrity => "VÃ©rifier lâ€™intÃ©gritÃ© du dossierâ€¦";
    public string BatchNaming_Title => "Nommage";
    public string BatchNaming_Matches => "Conforme";
    public string BatchNaming_Differs => "Non conforme";
    public string BatchNaming_Check => "VÃ©rifier les noms";
    public string BatchNaming_RenameAll => "Renommer les fichiers non conformes";
    public string BatchNaming_Error => "Erreur de nommage";
    public string BatchIntegrity_Title => "VÃ©rification dâ€™intÃ©gritÃ© par lot";
    public string BatchIntegrity_SelectDirectory => "SÃ©lectionner le dossier des fichiers Switch";
    public string BatchIntegrity_Browse => "Parcourirâ€¦";
    public string BatchIntegrity_IncludeSubdirectories => "Inclure les sous-dossiers";
    public string BatchTable_Columns => "Colonnesâ€¦";
    public string BatchTable_Search => "Rechercher";
    public string BatchTable_All => "Tous";
    public string BatchTable_ResetFilters => "RÃ©initialiser les filtres";
    public string BatchTable_ResetSort => "RÃ©initialiser le tri";
    public string BatchIntegrity_File => "Fichier";
    public string BatchIntegrity_Path => "Chemin";
    public string BatchIntegrity_Error => "Erreur";
    public string BatchIntegrity_NszDataCorrupted => "Le flux de donnÃ©es NSZ/NCZ compressÃ© est endommagÃ© ou incomplet (Ã©chec de la dÃ©compression Zstandard).";
    public string BatchIntegrity_IntegrityFailed => "La vÃ©rification d'intÃ©gritÃ© n'a pas pu Ãªtre terminÃ©e. Consultez le journal pour plus de dÃ©tails.";
    public string PackageStructure_Filesystem => "SystÃ¨me de fichiers";
    public string Signature_Title => "Signature NCA";
    public string Signature_Passed => "RÃ©ussie";
    public string Signature_NotPassed => "Ã‰chouÃ©e";
    public string Signature_Unchecked => "Non vÃ©rifiÃ©e";
    public string ToolTip_PackageStructure => "Structure selon Nx Game Info :\nScene (XCI) : partitions Update, Normal et Secure.\nConverti (XCI) : uniquement Secure ; typique de NSP â†’ XCI.\nScene (NSP) : legalinfo.xml, nacp.xml, programinfo.xml et cardspec.xml ; typique des releases BBB.\nHomebrew (NSP) : authoringtoolinfo.xml prÃ©sent.\nCDN (NSP) : certificat (.cert) et ticket (.tik) ; typique des dumps eShop CDN.\nConverti (NSP) : sans certificat ni ticket ; typique de XCI â†’ NSP.\nSystÃ¨me de fichiers : titres NAX0 installÃ©s sur une carte SD Switch.\nIncomplet : contenus NCA uniquement. NSZ/XCZ suivent les rÃ¨gles du paquet correspondant ; NCZ compte comme NCA.";
    public string ToolTip_NcaSignature => "RÃ©ussie : signatures NCA valides, attendues pour les titres officiels.\nÃ‰chouÃ©e : au moins une signature NCA invalide ; possible pour les homebrews, anormal pour les titres officiels.\nNon vÃ©rifiÃ©e : vÃ©rification NCA non terminÃ©e. Lancez le contrÃ´le dâ€™intÃ©gritÃ©.\nCette indication concerne les en-tÃªtes NCA ; ACID est une signature NPDM distincte.";
    public string ToolTip_Permission => "SÃ»r : aucun accÃ¨s aux services de fichiers ou bit 0x8000000000000000 absent.\nNon sÃ»r : accÃ¨s aux services de fichiers et bit 0x8000000000000000 prÃ©sent (EraseMmc).\nDangereux : accÃ¨s aux services de fichiers et masque 0xffffffffffffffff (toutes les permissions).\nNon sÃ»r/Dangereux ne devrait concerner que les homebrews. Disponible uniquement pour les jeux de base et mises Ã  jour. Ce classement ne constitue pas une Ã©valuation complÃ¨te de sÃ©curitÃ©.";
    public string ToolTip_AcidSignature => "Signature de la section ACID de main.npdm, indÃ©pendante de la signature de lâ€™en-tÃªte NCA.";
    public string PackageStructure_Title => "Structure du paquet";
    public string PackageStructure_Scene => "Version Scene";
    public string PackageStructure_Cdn => "Copie CDN";
    public string PackageStructure_Converted => "Converti";
    public string PackageStructure_Homebrew => "Homebrew";
    public string PackageStructure_Incomplete => "Incomplet";
    public string PackageStructure_Unknown => "Inconnu";
    public string FileInfo_FileSize => "Taille du fichier";
    public string FileInfo_CompressionRatio => "Taux de compression";
    public string FileInfo_Uncompressed => "dÃ©compressÃ©";
    public string FileInfo_SystemUpdate => "Mise Ã  jour systÃ¨me incluse (XCI)";
    public string BatchIntegrity_Export => "Exporter CSVâ€¦";
    public string BatchIntegrity_Start => "DÃ©marrer";
    public string BatchIntegrity_OpenSelected => "Ouvrir dans la vÃ©rification de fichier";
    public string BatchIntegrity_MoveSelected => "DÃ©placerâ€¦";
    public string BatchIntegrity_MoveValid => "DÃ©placer les fichiers validesâ€¦";
    public string BatchIntegrity_SelectMoveDestination => "SÃ©lectionner la destination des fichiers valides";
    public string BatchIntegrity_Moving => "DÃ©placement";
    public string MenuItem_Options => "Options";
    public string MenuItem_Settings => "ParamÃ¨tres";
    public string MenuItem_ReloadKeys => "Recharger les clÃ©s";
    public string MenuItem_OpenTitleWebPage => "Ouvrir la page Web du titre...";
    public string MenuItem_ShowRenameToolWindow => "Outil de renommage...";

    public string Packages_Title => "Fichier a contenus multiples";
    public string DisplayVersion => "Version affichÃ©e";
    public string Presentation_Title => "PrÃ©sentation";
    public string ToolTip_AvailableLanguages => "Le titre, l'Ã©diteur et l'icÃ´ne peuvent changer selon la langue sÃ©lectionnÃ©e.";
    public string AvailableLanguages => "Langues";
    public string AppTitle => "Titre";
    public string Publisher => "Editeur";
    public string Security_Title => "SÃ©curitÃ© du programme";
    public string Security_Level => "Ã‰valuation";
    public string Security_FileSystemPermissions => "Autorisations du systÃ¨me de fichiers";
    public string Security_AcidSignature => "Signature ACID";
    public string Security_Safe => "SÃ»r";
    public string Security_Unsafe => "Non sÃ»r";
    public string Security_Dangerous => "Dangereux";
    public string Security_Unavailable => "Indisponible";
    public string Security_Details => "Services autorisÃ©s ({0}) : {1}";

    public string Lng_AmericanEnglish => "Americain";
    public string Lng_BritishEnglish => "Anglais";
    public string Lng_CanadianFrench => "Canadien";
    public string Lng_Dutch => "Allemand";
    public string Lng_French => "FranÃ§ais";
    public string Lng_German => "Germain";
    public string Lng_Italian => "Italien";
    public string Lng_Japanese => "Japonais";
    public string Lng_Korean => "KorÃ©en";
    public string Lng_LatinAmericanSpanish => "AmÃ©rique Latine";
    public string Lng_Portuguese => "Portuguais";
    public string Lng_Russian => "Russe";
    public string Lng_SimplifiedChinese => "Chinois SimplifiÃ©";
    public string Lng_Spanish => "Espagnol";
    public string Lng_TraditionalChinese => "Chinois Traditionnel";
    public string Lng_BrazilianPortuguese => "BrÃ©silien Portugais";
    public string Lng_Unknown => "Inconnue";

    public string SettingsView_Title => "ParamÃ¨tres";
    public string SettingsView_Button_Apply => "Appliquer";
    public string SettingsView_Button_Cancel => "Annuler";
    public string SettingsView_Button_Reset => "RÃ©initialiser";
    public string SettingsView_GroupBoxKeys => "ClÃ©s";
    public string SettingsView_Title_KeysEffectiveFilePath => "Chemin effectif";
    public string SettingsView_Title_KeysCustomFilePath => "Chemin personnalisÃ©";
    public string SettingsView_Title_KeysDownloadUrl => "URL de tÃ©lÃ©chargement";
    public string KeysValidation_MissingFile => "Aucun fichier trouvÃ©.";
    public string KeysValidation_ValidEntries => "Valide ({0} entrÃ©es).";
    public string KeysValidation_MissingMasterKeys => "ClÃ©s principales manquantes : {0}.";
    public string CnmtOverview_BaseTitleId => "ID du titre de base";
    public string CnmtOverview_MasterKey => "ClÃ© principale requise";
    public string CnmtOverview_MinimumApplicationVersion => "Version minimale de l'application (DLC)";
    public string CnmtOverview_Distribution => "Distribution";
    public string KeysValidation_InvalidMasterKeys => "ClÃ©s principales non valides : {0}.";
    public string KeysValidation_InvalidLines => "Lignes mal formÃ©es : {0}.";
    public string KeysValidation_EmptyFile => "Le fichier ne contient aucune entrÃ©e valide.";
    public string KeysValidation_FirmwareEstimate => "RÃ©vision valide la plus Ã©levÃ©e : {0} â€” prend en charge les contenus jusqu'au firmware {1}.";
    public string KeysValidation_UnsupportedMasterKeys => "Nouvelle rÃ©vision de clÃ© principale dÃ©tectÃ©e : {0}. Cette version du programme ne peut pas encore la valider ni l'associer Ã  un firmware ; une mise Ã  jour est nÃ©cessaire.";
    public string SettingsView_ToolTip_Keys => """
                                               Les clÃ©s sont obligatoires pour pouvoir ouvrir des fichiers Nintendo Switch chiffrÃ©s (XCI, NSP, ...).
                                               Chaque fichier Nintendo Switch officiel est chiffrÃ© avec des clÃ©s spÃ©ciques Ã  la version du firmware pour lequel il a Ã©tÃ© construit.

                                               Afin de pouvoir ouvrir n'importe quel fichier sans erreur, veuillez vous assurer de toujours possÃ©der un fichier "prod.keys" contenant l'ensemble de toutes les clÃ©s de tous les firmwares existants.

                                               Les fichiers de clÃ© doivent contenir une clÃ© par ligne, sous la forme Â«NOM_CLE = VALEUR_HEXADECIMALÂ»."
                                               """;

    public string SettingsView_ToolTip_ProdKeys => """
                                                   Ce fichier contient les clÃ©s communes Ã  toutes les consoles Switch. Ce fichier est requis pour ouvrir les contenus chiffrÃ©s.
                                                   Le programme cherchera la prÃ©sence du fichier dans l'ordre suivant:
                                                       1. le chemin dÃ©fini par ce paramÃ¨tre
                                                       2. le rÃ©pertoire courant du programme
                                                       3. le dossier Â«%UserProfile%\\.switchÂ»

                                                   Au dÃ©marrage, le programme peut automatiquement tÃ©lÃ©charger le fichier de clÃ©s quand aucun n'est trouvÃ© sur le systÃ¨me.
                                                   Le fichier de clÃ©s sera tÃ©lÃ©chargÃ© dans le rÃ©pertoire courant de l'application.
                                                   """;

    public string SettingsView_ToolTip_TitleKeys => """
                                                    Vous pouvez optionnellement spÃ©cifier un fichier contenant les clÃ©s spÃ©cifiques de certains jeux.
                                                    Le programme cherchera la prÃ©sence du fichier dans l'ordre suivant:
                                                        1. le chemin dÃ©fini par ce paramÃ¨tre
                                                        2. le rÃ©pertoire courant du programme
                                                        3. le dossier Â«%UserProfile%\\.switchÂ»

                                                    Au dÃ©marrage, le programme peut automatiquement tÃ©lÃ©charger le fichier de clÃ©s quand aucun n'est trouvÃ© sur le systÃ¨me.
                                                    Le fichier de clÃ©s sera tÃ©lÃ©chargÃ© dans le rÃ©pertoire courant de l'application.
                                                    """;

    public string SettingsView_LogFileRetention => "Conserver les journaux (1â€“100 lancements)";
    public string SettingsView_LogLevel => "Niveau de log";
    public string SettingsView_ToolTip_LogLevel => "Le niveau de log indique Ã  partir de quel niveau les messages sont loguÃ©s.";
    public string SettingsView_CheckBox_AlwaysReloadKeysBeforeOpen => "Toujours recharger les clÃ©s avant l'ouverture d'un fichier";
    public string SettingsView_CheckBox_InjectTicketKeys => "Injecter les clÃ©s depuis les fichiers ticket (*.tik)";
    public string SettingsView_Title_Language => "Langue";
    public string SettingsView_Title_Theme => "ThÃ¨me";
    public string SettingsView_Title_NczOptions => "ParamÃ¨tres NSZ/XCZ";
    public string SettingsView_ToolTip_NczBlockLessCompression => """
                                                                  Les fichiers NSZ ou XCZ sont composÃ©s de fichiers NCZ qui sont des fichiers NCA compressÃ©s.
                                                                  Les fichiers NCZ peuvent Ãªtre compressÃ©s sans utiliser la mÃ©thode de compression par bloc, ce qui rend impossible les accÃ¨s en lecture alÃ©atoire de faÃ§on efficace.
                                                                  Ainsi, si le fichier est volumineux et qu'il faut lire une petite partie vers la fin, il sera nÃ©cessaire de dÃ©compresser tout le flux jusqu'Ã  atteindre la partie souhaitÃ©e.
                                                                  Les gros fichiers peuvent donc prendre beaucoup de temps avant d'Ãªtre ouverts.
                                                                  PrivilÃ©giez l'utilisation de la compression par bloc pour les fichiers volumineux.
                                                                  Notez que si vous choisissez de ne pas permettre l'ouverture des fichiers compressÃ©s sans bloc, cela n'affectera pas les fonctionnalitÃ©s de contrÃ´le d'intÃ©gritÃ©.
                                                                  """;

    public string SettingsView_CheckBox_NczOpenBlocklessCompression => "Ouvrir les NCZ compressÃ©s sans bloc";
    public string SettingsView_Title_Integrity => "IntÃ©gritÃ©";
    public string SettingsView_CheckBox_IgnoreMissingDeltaFragments => "Ignorer les fragments de delta manquants";
    public string SettingsView_ToolTip_IgnoreMissingDeltaFragments => $"""
                                                                       Les fichiers de patch peuvent contenir des fichiers de mise Ã  jour incrÃ©mentielle (connu sous le nom de {ContentType.DeltaFragment}).
                                                                       Ces fragments ne sont pas obligatoires pour mettre Ã  jour une application, et sont parfois retirÃ©s volontairement.
                                                                       Cochez cette option si vous voulez ignorer les {ContentType.DeltaFragment} absents lors de la vÃ©rification de l'intÃ©gritÃ©.
                                                                       """;

    public string SettingsView_Miscellaneous => "Divers";
    public string SettingsView_ToolTip_OpenKeysLocation => "Ouvrir l'emplacement du fichier de clÃ©s.";
    public string SettingsView_ToolTip_BrowseKeys => "Parcourir...";
    public string SettingsView_ToolTip_DownloadKeys => "TÃ©lÃ©charger Ã  partir de l'URL spÃ©cifiÃ©e.";

    public string BrowseKeysFile_ProdTitle => "SÃ©lectionnez les clÃ©s \"prod\"";
    public string BrowseKeysFile_TitleTitle => "SÃ©lectionnez les clÃ©s \"title\"";
    public string BrowseKeysFile_Filter => "Fichier de clÃ©s (*.keys)|*.keys|Paquets de jeux Switch (*.nsp;*.nsz;*.xci;*.xcz)|*.nsp;*.nsz;*.xci;*.xcz|Archives (*.zip;*.7z)|*.zip;*.7z|Fichiers de contenu Switch (*.nca)|*.nca|Images NAND et dumps fractionnÃ©s (*.bin;*.img;*.00)|*.bin;*.img;*.00|Tous les fichiers (*.*)|*.*";

    public string SuspiciousFileExtension => "L'extension du fichier Â«{0}Â» semble invalide, Â«{1}Â» ou Â«{2}Â» Ã©tait attendu.";
    public string DragMeAFile => "Glisse moi un petit fichier supportÃ© par ici :)";
    public string MultipleFilesDragAndDropNotSupported => "L'ouverture de plusieurs fichiers n'est pas supportÃ©e, seul le premier sera ouvert.";

    public string CnmtOverview_Title => "Informations du package";
    public string CnmtOverview_TitleId => "ID du titre";
    public string CnmtOverview_ContentType => "Type";
    public string CnmtOverview_TitleVersion => "Version";
    public string CnmtOverview_MinimumSystemVersion => "Version minimum du systÃ¨me";
    public string CnmtOverview_BuildID => "Build ID";
    public string CnmtOverview_BuildID_NotAvailableBecauseSectionIsSparse => "Non disponible (contenu dispersÃ©)";
    public string CnmtOverview_IsDemo => "DÃ©mo";

    public string ContextMenu_SaveImage => "Sauvegarder...";
    public string CopyTitleImageError => "Echec de copie du fichier image: {0}";
    public string SaveTitleImageError => "Echec de sauvegarde du fichier image: {0}";

    public string SaveDialog_Title => "Sauvegarder sous";
    public string SaveDialog_ImageFilter => "Image";
    public string SaveDialog_AnyFileFilter => "Fichier";
    public string SaveFile_Error => "Echec de sauvegarde du fichier: {0}";

    public string ContextMenu_CopyImage => "Copier";

    public string TabOverview => "AperÃ§u";
    public string TabContent => "Contenu";
    public string GroupBoxStructure => "Structure";
    public string GroupBoxProperties => "PropriÃ©tÃ©s";

    public string ContextMenu_ShowItemErrors => "Montrer les erreurs...";
    public string ContextMenu_SaveSectionItem => "Sauvegarder le contenu de la section...";
    public string ContextMenu_SaveDirectoryItem => "Sauvegarder le rÃ©pertoire...";
    public string ContextMenu_SaveFileItem => "Sauvegarder le fichier...";
    public string ContextMenu_SavePartitionFileItem => "Sauvegarder le fichier de partition...";
    public string ContextMenu_SaveNcaFileRaw => "Sauvegarder le NCA brute...";
    public string ContextMenu_SaveNcaFilePlaintext => "Sauvegarder le NCA dÃ©chiffrÃ©...";

    public string SettingsLoadingError => "Echec du chargment des paramÃ¨tres: {0}";
    public string SettingsSavingError => "Echec de sauvegarde des paramÃ¨tres: {0}";

    public string LoadingError_DiskFull => "Espace disque insuffisant pour charger ou extraire le fichier. LibÃ©rez de la place sur le lecteur du dossier temporaire, puis rÃ©essayez. Le chargement a Ã©tÃ© arrÃªtÃ©.";
    public string LoadingError_Failed => "Echec de chargement du fichier Â«{0}Â»: {1}";
    public string LoadingError_FailedToCheckIfXciPartitionExists => "Echec de vÃ©rification de l'existence de la partition XCI: {0}";
    public string LoadingError_FailedToOpenXciPartition => "Echec d'ouverture de la partition XCI: {0}";
    public string LoadingError_FailedToLoadXciContent => "Echec de chargement du contenu du XCI: {0}";
    public string LoadingError_FailedToOpenPartitionFile => "Echec d'ouverture du fichier de partition: {0}";
    public string LoadingError_FailedToLoadNcaFile => "Echec de chargement du fichier NCA: {0}";
    public string LoadingError_FailedToLoadPartitionFileSystemContent => "Echec du chargement du contenu du fichier de partition systÃ¨me: {0}";
    public string LoadingError_FailedToCheckIfSectionCanBeOpened => "Echec de vÃ©rification de la possibilitÃ© d'ouverture de la section: {0}";
    public string LoadingError_FailedToOpenNcaSectionFileSystem => "Echec d'ouverture du contenu de la section Â«{0}Â» du NCA: {1}";
    public string LoadingError_FailedToLoadSectionContent => "Echec de chargement du contenu de la section: {0}";
    public string LoadingError_FailedToGetFileSystemDirectoryEntries => "Echec de rÃ©cupÃ©ration des entrÃ©es Â«rÃ©pertoireÂ» du systÃ¨me de fichier: {0}";
    public string LoadingError_FailedToOpenNacpFile => "Echec d'ouverture du fichier NACP: {0}";
    public string LoadingError_FailedToLoadNacpFile => "Echec de chargement du fichier NACP: {0}";
    public string LoadingError_FailedToOpenCnmtFile => "Echec d'ouverture du fichier CNMT: {0}";
    public string LoadingError_FailedToLoadCnmtFile => "Echec de chargement du fichier CNMT: {0}";
    public string LoadingError_FailedToLoadNcaContent => "Echec de chargement du contenu du NCA: {0}";
    public string LoadingError_FailedToLoadDirectoryContent => "Echec de chargement du contenu du rÃ©pertoire: {0}";
    public string LoadingError_FailedToLoadIcon_Log => "Echec de chargement de l'icÃ´ne: {0}";
    public string LoadingError_NcaFileMissing_Log => "L'entrÃ©e NCA Â«{0}Â» de type Â«{1}Â» est manquante.";
    public string LoadingError_NoCnmtFound_Log => "Auncun CNMT trouvÃ©!";
    public string LoadingError_NacpFileMissing_Log => "Fichier NACP Â«{0}Â» non trouvÃ©!";
    public string LoadingError_NcaMissingSection_Log => "Le fichier NCA de type de contenu Â«{0}Â» ne contient pas la section de type Â«{0}Â».";
    public string LoadingError_MainFileMissing_Log => "Fichier Â«{0}Â» non trouvÃ©!";
    public string LoadingError_IconMissing_Log => "Le fichier d'icÃ´ne Â«{0}Â» est manquant.";
    public string LoadingError_XciSecurePartitionNotFound_Log => "Partition sÃ©curisÃ©e XCI non trouvÃ©e!";
    public string LoadingError_FailedToGetNcaSectionFsHeader => "Echec de rÃ©cupÃ©ration de l'entÃªte du systÃ¨me de fichier NCA pour la section Â«{0}Â»: {1}";
    public string LoadingError_FailedToOpenMainFile => "Echec d'ouverture du fichier Main: {0}";
    public string LoadingError_FailedToLoadMainFile => "Echec de chargement du fichier Main: {0}";
    public string LoadingError_FailedToOpenNpdmFile => "Echec d'ouverture de main.npdm : {0}";
    public string LoadingError_FailedToLoadNpdmFile => "Echec de l'analyse de main.npdm : {0}";
    public string LoadingError_FailedToLoadTicketFile => "Echec de chargement du fichier ticket: {0}";
    public string LoadingError_FailedToLoadTitleIdKey => "Echec de chargement de la clÃ© du Title ID Ã  partir du fichier ticket Â«{0}Â»: {1}";
    public string LoadingError_NczBlocklessCompressionDisabled => "L'ouverture de NCZ sans compression par bloc est dÃ©sactivÃ© dans les paramÃ¨tres.";

    public string LoadingInfo_TitleIdKeySuccessfullyInjected => "La clÃ© du Title ID Â«{0}={1}Â» trouvÃ©e dans le fichier ticket Â«{2}Â» a Ã©tÃ© ajoutÃ©e avec succÃ¨s dans le trousseau de clÃ©s.";
    public string LoadingWarning_TitleIdKeyReplaced => "La clÃ© du Title ID Â«{0}={1}Â» trouvÃ©e dans le fichier ticket Â«{2}Â» a Ã©tÃ© utilisÃ©e pour remplacer la clÃ© existante Â«{0}={2}Â» du trousseau de clÃ©s.";
    public string LoadingDebug_TitleIdKeyAlreadyExists => "La clÃ© du Title ID Â«{0}={1}Â» trouvÃ©e dans le fichier ticket Â«{2}Â» Ã©tait dÃ©jÃ  enregistrÃ©e dans le trousseau.";

    public string KeysFileUsed => "Fichier Â«{0}Â» utilisÃ©: {1}";
    public string NoneKeysFile => "[aucun]";

    public string Status_DownloadingFile => "TÃ©lÃ©chargement du fichier Â«{0}Â»...";
    public string Log_DownloadingFileFromUrl => "TÃ©lÃ©chargement du fichier Â«{0}Â» Ã  partir de l'URL Â«{1}Â»...";
    public string Log_FileSuccessfullyDownloaded => "Fichier Â«{0}Â» tÃ©lÃ©chargÃ© avec succÃ¨s.";
    public string Log_FailedToDownloadFileFromUrl => "Echec de tÃ©lÃ©chargement du fichier Â«{0}Â» Ã  partir de l'URL Â«{1}Â»: {2}";

    public string ToolTip_PatchNumber => "NumÃ©ro de patch {0}";
    public string Log_OpeningFile => "=====> {0} <=====";
    public string MainModuleIdTooltip => "Egalement connu sous le nom de Â«Build IDÂ» (ou BID).";
    public string ATaskIsAlreadyRunning => "Une tÃ¢che est dÃ©jÃ  en cours...";
    public string FileInfo_Title => "Fichier";
    public string Title_FileInfo_FileType => "Type";
    public string Title_FileInfo_Compression => "Compression";
    public string Title_FileInfo_Integrity => "IntÃ©gritÃ©";
    public string ToolTip_NcasIntegrity => $"""
                                            La vÃ©rification de l'intÃ©gritÃ© consiste Ã  vÃ©rifier l'intÃ©gritÃ© de chaque NCA (ou NCZ).

                                            Le rÃ©sultat de l'intÃ©gritÃ© peut Ãªtre l'une des valeurs suivantes:
                                            - {NcasIntegrity_NoNca}: Aucun NCA trouvÃ©.
                                            - {NcasIntegrity_Unchecked}: IntÃ©gritÃ© non vÃ©rifiÃ©e.
                                            - {NcasIntegrity_InProgress}: VÃ©rification de l'intÃ©gritÃ© en cours.
                                            - {NcasIntegrity_Original}: Tous les NCAs sont originaux (signature et hash ok).
                                            - {NcasIntegrity_Incomplete}: Tous les NCAs sont originaux, mais certains sont manquants.
                                            - {NcasIntegrity_Modified}: Au moins un NCA est modifiÃ© (signature pas ok, mais hash ok).
                                            - {NcasIntegrity_Corrupted}: Au moins un NCA est corrompu (hash incorrect).
                                            - {NcasIntegrity_Error}: Une erreur est survenue pendant la vÃ©rification de l'intÃ©gritÃ©.

                                            Le dÃ©tail de chaque NCA analysÃ© peut Ãªtre trouvÃ© dans l'onglet Â«ContenuÂ».
                                            """;

    public string AvailableContents => "Contenus:";
    public string MultiContentPackageToolTip => "Le package contient plusieurs contenus (Â«{0}Â» dÃ©tectÃ©).";

    public string NcasIntegrity_Error_NcaMissing => "L'intÃ©gritÃ© du NCA Â«{0}Â» ne peut Ãªtre vÃ©rifiÃ©e, NCA manquant.";
    public string NcasIntegrity_Error_Log => "Echec de vÃ©rification de l'intÃ©gritÃ© des NCAs: {0}";
    public string NcaIntegrity_GetOriginalNcaError => "Echec de rÃ©cupÃ©ration du NCA original: {0}";
    public string NcaIntegrity_GetOriginalNcaError_Log => "Echec de rÃ©cupÃ©ration du NCA original Ã  partir du NCA Â«{0}Â»: {1}";

    public string NcaHeaderSignature_Valid_Log => "La signature de l'entÃªte du NCA Â«{0}Â» est valide.";
    public string NcaHeaderSignature_Invalid => "La vÃ©rification de la signature de l'entÃªte du NCA a Ã©chouÃ© avec le statut Â«{0}Â».";
    public string NcaHeaderSignature_Invalid_Log => "La vÃ©rification de la signature de l'entÃªte du NCA Â«{0}Â» a Ã©chouÃ© avec le statut Â«{1}Â».";
    public string NcaHeaderSignature_Error => "Echec de vÃ©rification de la signature de l'entÃªte du NCA: {0}.";
    public string NcaHeaderSignature_Error_log => "Echec de vÃ©rification de la signature de l'entÃªte du NCA Â«{0}Â»: {1}";

    public string NcaHash_VerificationStart_Log => ">>> La vÃ©rification du hash des NCAs dÃ©bute...";
    public string NcaHash_VerificationEnd_Log => ">>> La vÃ©rification du hash des NCAs est terminÃ©e.";
    public string NcaHash_NcaItem_CantExtractHashFromName => "Echec d'extraction du hash attendu Ã  partir du nom du NCA.";
    public string NcaHash_CantExtractHashFromName_Log => "Echec d'extraction du hash attendu Ã  partir du nom du NCA Â«{0}Â».";
    public string NcaHash_Valid_Log => "Le hash du NCA Â«{0}Â» est valide.";
    public string NcaHash_NcaItem_Invalid => "Hash non valide.";
    public string NcaHash_Invalid_Log => "Le hash du NCA Â«{0}Â» n'est pas valide.";
    public string NcaHash_NcaItem_Exception => "Echec de vÃ©rification du hash: {0}";
    public string NcaHash_Exception_Log => "Echec de vÃ©rification du hash du NCA Â«{0}Â»: {1}";
    public string NcaHash_ProgressText => "Hashage du NCA {0}/{1}...";

    public string CancelAction => "Annuler";
    public string Status_Ready => "PrÃªt.";
    public string LoadingFile_PleaseWait => "Chargement, veuillez patienter...";

    public string NcasIntegrity_NoNca => "Aucun NCA";
    public string NcasIntegrity_Unchecked => "Non vÃ©rifiÃ©";
    public string NcasIntegrity_InProgress => "En cours";
    public string NcasIntegrity_Original => "Original";
    public string NcasIntegrity_Incomplete => "Incomplet";
    public string NcasIntegrity_Modified => "ModifiÃ©";
    public string NcasIntegrity_Corrupted => "Corrompu";
    public string NcasIntegrity_Error => "Erreur";
    public string NcasIntegrity_Unknown => "Inconnu";

    public string Status_SavingFile => "Sauvegarde du fichier Â«{0}Â»...";

    public string KeysLoading_Starting_Log => ">>> Chargement des clÃ©s...";
    public string KeysLoading_Successful_Log => ">>> ClÃ©s chargÃ©es avec succÃ¨s.";
    public string KeysLoading_UnusedKey_Log => "Information : la clÃ© supplÃ©mentaire Â«{0}Â» nâ€™est pas utilisÃ©e par cette version du programme.";
    public string KeysLoading_Error => "Echec de chargement des clÃ©s: {0}.";
    public string WarnNoProdKeysFileFound => "Aucun fichier Â«prod.keysÂ» trouvÃ©.";
    public string InvalidSetting_KeysFileNotFound => "Le fichier de clÃ© Â«{0}Â» dÃ©fini dans les paramÃ¨tres n'existe pas.";
    public string InvalidSetting_BufferSizeInvalid => "La taille du buffer Â«{0}Â» dÃ©fini dans les paramÃ¨tres n'est pas valide, la valeur doit Ãªtre strictement supÃ©rieure Ã  0.";
    public string InvalidSetting_LanguageNotFound => "La langue Â«{0}Â» dÃ©finie dans les paramÃ¨tres n'existe pas.";

    public string ToolTip_KeyMissing => "La clÃ© Â«{0}Â» de type Â«{1}Â» est manquante.";

    public string MenuItem_CopyTextToClipboard => "Copier";
    public string ContextMenu_OpenFileLocation => "Ouvrir l'emplacement...";
    public string OpenFileLocation_Failed_Log => "Echec d'ouverture de l'emplacement du fichier Â«{0}Â»: {1}";
    public string SettingsView_TitlePageUrl => "URL du titre";
    public string SettingsView_TitleInfoApiUrl => "URL de lâ€™API des titres";
    public string SettingsView_TitleInfoProvider => "Source des noms de titres";
    public string SettingsView_TitleDbRegion => "RÃ©gion / langue TitleDB";
    public string SettingsView_TitleDbCacheTip => "TitleDB est conservÃ©e localement et actualisÃ©e chaque jour. Le cache reste disponible en cas de panne. Les titres manquants sont aussi recherchÃ©s dans US.en.";
    public string BatchIntegrity_FileType => "Type de fichier";
    public string BatchIntegrity_PackageType => "Type de paquet";
    public string BatchIntegrity_ShowOnlyErrors => "Afficher uniquement les fichiers dÃ©fectueux";
    public string OpenTitleWebPage_Failed => "Echec d'ouverture de la page Web: {0}";

    public string Log_DownloadFileCanceled => "TÃ©lÃ©chargement annulÃ©.";
    public string Log_SaveToDirCanceled => "Sauvegarde du rÃ©pertoire annulÃ©.";
    public string Log_SaveFileCanceled => "Sauvegarde du fichier annulÃ©.";
    public string Log_SaveStorageCanceled => "Sauvegarde du stockage annulÃ©.";
    public string Log_NcasIntegrityCanceled => "IntÃ©gritÃ© des NCAs annulÃ©.";

    public string RenamingTool_TargetDirectory => "Dossier cible (vide = dossier actuel)";
    public string RenamingTool_FolderTip => "Utilisez / pour les dossiers, par exemple DLC/{WTitle}.{Ext:L}.";
    public string RenamingTool_OldName => "Ancien nom";
    public string RenamingTool_NewName => "Nouveau nom";
    public string RenamingTool_StatusError => "Erreur";
    public string RenamingTool_StatusUnchanged => "InchangÃ©";
    public string RenamingTool_StatusSimulation => "Simulation";
    public string RenamingTool_StatusRenamed => "RenommÃ©";
    public string RenamingTool_WindowTitle => "Outil de renommage";
    public string RenamingTool_Patterns => "Patterns";
    public string RenamingTool_ApplicationPattern => "Pattern d'application";
    public string RenamingTool_PatchPattern => "Pattern de patch";
    public string RenamingTool_AddonPattern => "Pattern d'add-on";
    public string RenamingTool_InputPath => "Chemin d'entrÃ©e";
    public string RenamingTool_FileFilters => "Filtres";
    public string RenamingTool_ToolTip_Patterns =>
        $$"""
         Dossier cible et sous-dossiers:
           Un dossier cible vide utilise le dossier actuel du fichier.
           SÃ©parez les sous-dossiers par /, par exemple:
             DLC/{WTitle}.{Ext:L}
             {WAppTitle}/DLC/{WTitle}.{Ext:L}
           Utilisez uniquement des dossiers relatifs, sans chemin absolu ni .. .
           La simulation affiche les chemins complets sans crÃ©er de dossiers.
           Le renommage crÃ©e les dossiers manquants sans Ã©craser les fichiers.

         Syntaxe d'un mot clÃ©:
            {<MotClÃ©>[:<Format>]}

         Le format est facultatif, et peut valoir:
            - U: Majuscule
            - L: Minuscule

         Exemples:
            {Title} => Le titre original
            {Title:U} => Le titre en majuscule

         Liste des mots clÃ©s:
           â€¢ TitleId:
              - L'id du contenu.
           â€¢ AppId:
              - L'id de l'{{nameof(ContentMetaType.Application)}} correspondante (pour des contenus de type {{nameof(ContentMetaType.Application)}}, cette valeur est Ã©gale Ã  {TitleId}).
           â€¢ PatchId:
              - Si le contenu est une {{nameof(ContentMetaType.Application)}}, cette valeur est Ã©gale Ã  l'id du contenu de {{nameof(ContentMetaType.Patch)}} correspondant, sinon zÃ©ro.
           â€¢ PatchNum:
              - Si le contenu est une {{nameof(ContentMetaType.Application)}}, cette valeur vaut gÃ©nÃ©ralement 0.
              - Si le contenu est un {{nameof(ContentMetaType.Patch)}}, cette valeur correspond au numÃ©ro du patch.
              - Si le contenu est un {{nameof(ContentMetaType.AddOnContent)}}, cette valeur correspond au numÃ©ro de patch de l'add-on.
           â€¢ Title:
              - Le premier titre parmi la liste des titres dÃ©finis.
              - Cette valeur n'existe que pour des contenus de type {{nameof(ContentMetaType.Application)}} ou {{nameof(ContentMetaType.Patch)}}, mais pas pour des contenus de type {{nameof(ContentMetaType.AddOnContent)}}.
           â€¢ Ext:
              - L'extension correspondant au type de fichier dÃ©tectÃ©.
           â€¢ VerNum:
              - Le numÃ©ro de version du contenu.
           â€¢ VerDsp:
              - La version affichÃ©e.
           â€¢ WTitle:
              - Le titre du contenu rÃ©cupÃ©rÃ© depuis Internet.
           â€¢ WAppTitle:
              - Le titre de l'{{nameof(ContentMetaType.Application)}} correspondante, rÃ©cupÃ©rÃ© depuis Internet.

         Utilisez \{ ou \} pour Ã©crire littÃ©ralement les caractÃ¨res { ou }.
         """;

    public string RenamingTool_ToolTip_BasePattern => $"Le pattern Ã  utiliser pour des contenus de type {nameof(ContentMetaType.Application)}.";
    public string RenamingTool_ToolTip_PatchPattern => $"Le pattern Ã  utiliser pour des contenus de type {nameof(ContentMetaType.Patch)}.";
    public string RenamingTool_ToolTip_AddonPattern => $"Le pattern Ã  utiliser pour des contenus de type {nameof(ContentMetaType.AddOnContent)}.";
    public string RenamingTool_Button_Cancel => "Annuler";
    public string RenamingTool_Button_Rename => "Renommer";
    public string RenamingTool_GroupBoxInput => "EntrÃ©e";
    public string RenamingTool_GroupBoxNamingSettings => "ParamÃ¨tres de nommage";
    public string RenamingTool_BrowseDirTitle => "SÃ©lectionnez un rÃ©pertoire";
    public string RenamingTool_GroupBoxOutput => "Sortie";
    public string RenamingTool_Miscellaneous => "Divers";
    public string RenamingTool_InvalidWindowsCharReplacement => "Remplacer les caractÃ¨res Windows non autorisÃ©s avec";
    public string RenamingTool_ReplaceWhiteSpaceChars => "Remplacer les espaces blancs";
    public string RenamingTool_ReplaceWhiteSpaceCharsWith => "Remplacer les espaces blancs avec";
    public string RenamingTool_Simulation => "Simulation";
    public string RenamingTool_AutoCloseOpenedFile => "Fermer automatiquement le fichier ouvert";
    public string RenamingTool_IncludeSubDirectories => "Inclure les sous rÃ©pertoires";
    public string RenamingTool_ContentTypeNotSupported => "Type de contenu Â«{0}Â» non supportÃ©.";
    public string RenamingTool_SuperPackageNotSupported => "Super package non supportÃ©.";
    public string RenamingTool_LogNbFilesToRename => ">>> {0} fichier(s) Ã  renommer...";

    public string RenamingTool_LogSimulationMode => "[SIMULATION] ";
    public string RenamingTool_LogFileRenamed => $"â€¢ {{0}}Ficher renommÃ© de{Environment.NewLine}\tÂ«{{1}}Â» Ã {Environment.NewLine}\tÂ«{{2}}Â».";
    public string RenamingTool_LogFileAlreadyNamedProperly => "â€¢ {0}Â«{1}Â» dÃ©jÃ  nommÃ© correctement.";
    public string RenamingTool_LogFailedToRenameFile => "â€¢ {0}Â«{1}Â»Echec de renommage: {2}";
    public string RenamingTool_LogRenamingFailed => "Echec de renommage: {0}";
    public string RenamingTool_BadInvalidFileNameCharReplacement => "La chaine de remplacement Â«{0}Â» (caractÃ¨res interdits dans les noms de fichiers), ne peut contenir le caractÃ¨re interdit Â«{1}Â».";

    public string Exception_UnexpectedDelimiter => "DÃ©limiteur {0} non attendu Ã  la position {1}, utilisez {2}{0} Ã  la place.";
    public string Exception_EndDelimiterMissing => "Le dÃ©limiteur de fin {0} est manquant.";
    public string FileRenaming_PatternKeywordUnknown => "Mot clÃ© Â«{0}Â» inconnu, liste des mots clÃ©s autorisÃ©s: Â«{1}Â».";
    public string FileRenaming_EmptyPatternNotAllowed => "La pattern ne peut Ãªtre vide.";
    public string FileRenaming_PatternKeywordNotAllowed => "Le mot clÃ© Â«{0}Â» n'est pas autorisÃ© pour les patterns de type Â«{1}Â».";
    public string FileRenaming_StringOperatorUnknown => "L'opÃ©rateur Â«{0}Â» n'est pas reconnu, les opÃ©rateurs autorisÃ©s sont Â«{1}Â».";
    public string FileRenaming_EmptyDirectoryNotAllowed => "Le rÃ©pertoire d'entrÃ©e ne peut Ãªtre vide.";
    public string Window_Tip_Title => "Astuce";
    public string Nsz_Installed => "Plugin NSZ installÃ©";
    public string Nsz_CustomExecutable => "ExÃ©cutable personnalisÃ©";
    public string Settings_Program => "Programme";
    public string Nsz_PhaseSource => "VÃ©rifier la source";
    public string Nsz_PhaseOutput => "VÃ©rifier le rÃ©sultat";
    public string Nsz_PhasePublish => "Enregistrer le rÃ©sultat";
    public string Batch_IncludeArchives => "Inclure ZIP / 7z";
    public string Batch_Scan => "Analyser les fichiers";
    public string Batch_VerifyAll => "VÃ©rifier tous les fichiers";
    public string File_SaveBackupSuspected => "Sauvegarde de jeu (supposÃ©e)";
    public string Batch_MultiPackageDetails => "Afficher ou masquer les paquets inclus";
    public string File_SaveBackup => "Sauvegarde de jeu";
    public string File_MissingKeys => "ClÃ©s requises manquantes. Le contenu ne peut pas Ãªtre entiÃ¨rement lu. VÃ©rifiez prod.keys / title.keys.";
    public string File_CopyMissingKeys => "Copier les noms des clÃ©s manquantes";
    public string Keys_ProgramFolder => "Dossier du programme";
    public string Keys_SharedFolder => "Profil utilisateur (.switch)";
    public string Keys_InUse => "UtilisÃ©";
    public string Keys_DownloadAll => "TÃ©lÃ©charger les clÃ©s";
    public string Keys_DownloadHost => "IP / nom dâ€™hÃ´te du tÃ©lÃ©chargement";
    public string Keys_DownloadHostTip => "{IP} dans les URL est remplacÃ© par cette adresse. Destination : chemin personnalisÃ© si dÃ©fini, sinon dossier du programme.";
    public string Keys_CopyToSwitch => @"Copier les clÃ©s actuelles vers %USERPROFILE%\.switch";
    public string Keys_ReplaceShared => "Remplacer les clÃ©s existantes ? Les fichiers sources seront conservÃ©s.";
    public string Keys_SharedCopied => "ClÃ©s disponibles dans le dossier partagÃ© .switch.";
    public string Batch_ScanAndVerify => "Analyser les fichiers et vÃ©rifier leur intÃ©gritÃ©";
}
