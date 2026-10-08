using System;
using Emignatik.NxFileViewer.Utils.MVVM.Localization;
using LibHac.Ncm;

namespace Emignatik.NxFileViewer.Localization.Keys;

public class LocalizationKeys_ES : LocalizationKeysBase, ILocalizationKeys
{
    public string Nand_Detected => "Firmas NAND detectadas. Integridad sin comprobar.";
    public string Nand_OpenSave => "Abrir partida guardada";
    public string Nand_LeaveSave => "Volver a la partición";
    public string Nand_ExportSave => "Exportar partida completa…";
    public string Nand_ExplorerTitle => "TÃ­tulo";
    public string Nand_ExplorerUserId => "ID de usuario";
    public string Nand_NameCandidate => "Candidato NAND identificado por el nombre. Consulte la informaciÃ³n NAND para confirmar y obtener detalles.";
    public string Nand_Installed => "Plugin NxNandManager instalado";
    public string Nand_CustomUpdate => "Borre y aplique la ruta EXE personalizada para usar descargas gestionadas.";
    public string Nand_Updating => "Descargando y comprobando NxNandManagerâ€¦";
    public string Nand_Open => "Abrir volcado NANDâ€¦";
    public string Nand_Info => "InformaciÃ³n NAND";
    public string Nand_Export => "Exportar particiÃ³nâ€¦";
    public string Nand_SettingsTip => "Deje vacÃ­a la ruta EXE para descargas gestionadas en Ajustes â†’ Updates â†’ Plugins â†’ NxNandManager. Un EXE personalizado no se reemplaza.";
    public string Nand_BisKeys => "Archivo de claves BIS (opcional; vacÃ­o = prod.keys activo de NxFileViewer)";
    public string Nand_Tip => "Abra un volcado NAND (primer archivo si estÃ¡ dividido). La exportaciÃ³n copia la particiÃ³n tal como estÃ¡, sin descifrar. Configure NxNandManager en Ajustes â†’ Plugins.";
    public string Nand_NewTarget => "Seleccione un archivo local nuevo. No se pueden reemplazar archivos existentes.";
    public string Nand_SourceMissing => "El archivo NAND de origen falta o no es local.";
    public string Nand_NotInstalled => "NxNandManager no estÃ¡ instalado. InstÃ¡lelo en Ajustes â†’ Updates â†’ Plugins.";
    public string Nand_OpenDrive => "Abrir unidadâ€¦";
    public string Nand_Explorer => "Explorador NAND";
    public string Nand_ExplorerUp => "Carpeta superior";
    public string Nand_ExportFile => "Exportar archivoâ€¦";
    public string Nand_ExplorerFolder => "Carpeta";
    public string Nand_DriveTip => "Seleccione una unidad NAND. Acceso de solo lectura. Los discos fÃ­sicos pueden requerir permisos de administrador.";
    public string Nand_KeysMissing => "El archivo de claves BIS configurado no existe.";
    public string Nand_ExportFailed => "NxNandManager no generÃ³ un archivo de particiÃ³n con contenido.";
    public string Nand_ExportDone => "ParticiÃ³n exportada.";
    public string Nand_Cancelled => "Cancelado o tiempo de consulta de informaciÃ³n agotado.";
    public string DataUpdate_Firmware => "Hashes de firmware";
    public string DataUpdate_Title => "Actualizaciones";
    public string DataUpdate_Titles => "Actualizar TitleDB";
    public string BatchNaming_Unchecked => "Sin comprobar";
    public string DataUpdate_LocalFirmwareVersion => "Referencias locales hasta el firmware {0}.";
    public string DataUpdate_NoLocalFirmware => "No hay referencias locales de firmware instaladas.";
    public string DataUpdate_TitleCatalogDate => "TitleDB {0}: cachÃ© local actualizada el {1}.";
    public string DataUpdate_TitleCatalogMissing => "TitleDB {0}: sin catÃ¡logo local.";
    public string DataUpdate_OnlineFirmwareVersion => "Referencias en lÃ­nea hasta el firmware {0}.";
    public string Keys_ExistingValidation => "Archivo existente:";
    public string Keys_IncomingValidation => "Archivo nuevo:";
    public string Keys_ReplaceDownloaded => "Â¿Usar las claves descargadas y reemplazar el archivo existente?";
    public string Keys_SaveTicketKeys => "Guardar claves de ticket faltantes en title.keys";
    public string Keys_TicketConflict => "Conflicto de clave de ticket para {0} en {1}: entrada existente conservada.";
    public string Keys_TicketSaved => "Clave de ticket para {0} guardada en {1}.";
    public string Tinfoil_StabilityHint => "Tinfoil puede estar temporalmente no disponible o ser inestable. Si falla, intÃ©ntelo mÃ¡s tarde o seleccione otra fuente.";
    public string DataUpdate_TitleTip => "Actualiza GitHub TitleDB para la regiÃ³n guardada y US en la carpeta del programa. El proveedor seleccionado no cambia.";
    public string DataUpdate_FirmwareTip => "Las comprobaciones descargan referencias sin cachÃ© permanente. Solo el botÃ³n separado guarda el paquete sin conexiÃ³n y conserva las referencias anteriores.";
    public string DataUpdate_CheckFirmware => "Comprobar hashes en lÃ­nea";
    public string DataUpdate_SaveFirmware => "Actualizar hashes sin conexiÃ³n";
    public string DataUpdate_Working => "Actualizandoâ€¦";
    public string DataUpdate_TitlesDone => "TitleDB {0} actualizada: {1} entradas con US.";
    public string DataUpdate_FirmwareSaved => "Hashes sin conexiÃ³n actualizados: {0} archivos.";
    public string DataUpdate_FirmwareChecked => "Hashes en lÃ­nea comprobados: {0} archivos, sin guardar.";
    public string Update_Title => "Actualizaciones del programa";
    public string Update_IncludePrereleases => "Incluir versiones preliminares en las actualizaciones del programa";
    public string Update_Prerelease => "VersiÃ³n preliminar";
    public string Update_Auto => "Buscar actualizaciones al iniciar";
    public string Update_Check => "Buscar actualizaciones";
    public string Update_Install => "Descargar e instalar";
    public string Update_Checking => "Buscando actualizacionesâ€¦";
    public string Update_Current => "No hay una versiÃ³n publicada mÃ¡s reciente.";
    public string Component_UpdateAvailable => "ActualizaciÃ³n disponible.";
    public string Component_NotInstalled => "No instalado.";
    public string Component_CustomVersion => "VersiÃ³n personalizada: comprobaciÃ³n automÃ¡tica no disponible.";
    public string Update_Available => "La versiÃ³n {0} estÃ¡ disponible.";
    public string Update_Failed => "La actualizaciÃ³n fallÃ³.";
    public string Update_Confirm => "Â¿Descargar e instalar la versiÃ³n {0}? NxFileViewer se reiniciarÃ¡. Se conservan claves, ajustes y plugins.";
    public string Update_Downloading => "Descargando y verificandoâ€¦";
    public string Update_Installing => "Instalandoâ€¦";
    public string Update_Cancelled => "ActualizaciÃ³n cancelada.";
    public string Nsz_Mode => "Modo de compresiÃ³n";
    public string Nsz_ModeAuto => "AutomÃ¡tico (NSZ: solid, XCZ: bloques)";
    public string Nsz_ModeSolid => "Solid / sin bloques";
    public string Nsz_ModeBlock => "CompresiÃ³n por bloques";
    public string Nsz_BlockSize => "TamaÃ±o de bloque";
    public string Nsz_ModeTip => "Solid comprime un poco mejor. Los bloques permiten lecturas rÃ¡pidas hacia atrÃ¡s y aleatorias. Estas opciones solo se aplican al comprimir.";
    public string Workspace_Plugins => "Complementos";
    public string Workspace_Home => "Inicio";
    public string Workspace_File => "VerificaciÃ³n de archivo";
    public string Workspace_Menu => "MenÃº principal";
    public string Nsz_Replace => "Reemplazar";
    public string Nsz_Number => "Guardar con numeraciÃ³n";
    public string TitlePage_Custom => "Personalizada";
    public string Info_WithRuntime => "Con .NET integrado";
    public string Info_WithoutRuntime => "Sin .NET integrado â€” requiere .NET 8 Desktop Runtime";
    public string Info_Description => "NxFileViewer muestra y verifica archivos de Nintendo Switch: NSP, NSZ, XCI, XCZ, NCA, ZIP y 7z, firmware y conversiÃ³n NSZ.";
    public string Info_Shortcuts => "Atajos de teclado";
    public string BatchHistory_Show => "Mostrar";
    public string BatchHistory_Title => "Ãšltimas 5 comprobaciones";
    public string BatchHistory_Resume => "Continuar";
    public string Dialog_Yes => "SÃ­";
    public string Dialog_No => "No";
    public string Nsz_Cancel => "Cancelar";
    public string Nsz_DeleteSourcePrompt => "Â¿Eliminar los archivos de origen tras convertir y verificar correctamente? Se conservan si falla.";
    public string Nsz_SourceDeleted => "Origen eliminado";
    public string Nsz_SourceDeleteFailed => "No se pudo eliminar el origen";
    public string Nsz_Compress => "Comprimir y verificarâ€¦";
    public string Nsz_Decompress => "Descomprimir y verificarâ€¦";
    public string Nsz_CompressValid => "Comprimir archivos vÃ¡lidosâ€¦";
    public string Nsz_DecompressValid => "Descomprimir archivos vÃ¡lidosâ€¦";
    public string Nsz_Update => "Instalar / actualizar pluginâ€¦";
    public string Nsz_Rollback => "Usar versiÃ³n anterior";
    public string Nsz_SelectDestination => "Seleccionar carpeta de destino";
    public string Nsz_PythonRuntimeFailed => "NSZ no pudo cargar su DLL Python integrada. FallÃ³ antes de comprobar claves o archivos. Selecciona otra CLI NSZ funcional en Ajustes â†’ Plugin nicoboss/nsz.";
    public string Nsz_NotInstalled => "Plugin nicoboss/nsz no estÃ¡ instalado.";
    public string Nsz_Updating => "Actualizando el plugin NSZâ€¦";
    public string Nsz_SourceSize => "TamaÃ±o original (bytes)";
    public string Nsz_OutputSize => "TamaÃ±o final (bytes)";
    public string Nsz_Verified => "Resultado verificado";
    public string Nsz_Summary => "{0} convertidos y verificados; {1} fallidos; {2} sin procesar. Se conservaron los originales.";
    public string Nsz_SettingsTip => "Ruta opcional de NSZ CLI. DÃ©jela vacÃ­a para gestionar automÃ¡ticamente las versiones oficiales. Se verifican fuente y resultado; se conservan los originales.";
    public string Nsz_CheckUpdates => "Buscar actualizaciones estables antes de convertir";
    public string Nsz_Level => "Nivel de compresiÃ³n (1â€“22)";
    public string Nsz_OutputExists => "El archivo de destino ya existe:";
    public string Nsz_SourceInvalid => "FallÃ³ la verificaciÃ³n del original:";
    public string Nsz_OutputInvalid => "FallÃ³ la verificaciÃ³n del resultado:";
    public string Nsz_OutputMissing => "NSZ no creÃ³ el archivo esperado.";
    public string Nsz_KeysMissing => "No se ha cargado prod.keys.";
    public string Nsz_Incompatible => "Esta CLI NSZ no admite las opciones necesarias.";
    public string Nsz_OfflineFallback => "ActualizaciÃ³n no disponible; se usa la versiÃ³n NSZ instalada.";
    public string Firmware_NoReferences => "Hashes no disponibles: GitHub inaccesible y referencias locales ausentes o invÃ¡lidas.";
    public string Firmware_LoadingOnline => "Cargando hashes de firmware desde GitHubâ€¦";
    public string Firmware_OnlineSource => "Fuente: GitHub (cargada para esta comprobaciÃ³n).";
    public string Firmware_OfflineSource => "GitHub no disponible. Se usan hashes incluidos; pueden faltar versiones recientes.";
    public string Firmware_BrowseZip => "Seleccionar ZIP / 7zâ€¦";
    public string Firmware_NcaMatches => "Esta NCA se incluye en estas versiones:";
    public string Firmware_Versions => "Version(es) del firmware";
    public string Firmware_Title => "VerificaciÃ³n de firmware";
    public string Firmware_Unknown => "No se encontrÃ³ una referencia de firmware.";
    public string Firmware_Summary => "{0}/{1} archivos vÃ¡lidos; faltantes: {2}, modificados: {3}, adicionales: {4}, duplicados: {5}.";
    public string Firmware_Missing => "Faltante";
    public string Firmware_Changed => "Modificado (tamaÃ±o/SHA-256)";
    public string Firmware_Renamed => "Nombre incorrecto (contenido coincidente)";
    public string Firmware_RenamedSummary => "Nombres incorrectos: {0}.";
    public string Firmware_Extra => "NCA adicional";
    public string Firmware_Duplicate => "Nombre duplicado";

    public override bool IsFallback => true;
    public override string DisplayName => "EspaÃ±ol";
    public override string CultureName => "es-ES";
    public override string LanguageAuto => "Auto";

    public string FileNotSupported_Log => "El Archivo Â«{0}Â» no es soportado.";
    public string OpenSdCard => "Abrir tarjeta SD";
    public string OpenFile_Filter => "Nintendo Switch files (*.nsp;*.nsz;*.xci;*.xcz;*.nca;*.nro;*.zip;*.7z;*.bin;*.img;*.00)|*.nsp;*.nsz;*.xci;*.xcz;*.nca;*.nro;*.zip;*.7z;*.bin;*.img;*.00|Paquetes de juegos Switch (*.nsp;*.nsz;*.xci;*.xcz)|*.nsp;*.nsz;*.xci;*.xcz|Archivos comprimidos (*.zip;*.7z)|*.zip;*.7z|Archivos de contenido Switch (*.nca)|*.nca|ImÃ¡genes NAND y volcados divididos (*.bin;*.img;*.00)|*.bin;*.img;*.00|All files (*.*)|*.*";
    public string MenuItem_File => "Archivo";
    public string MenuItem_Open => "Abrir...";
    public string MenuItem_OpenLast => "Abrir _reciente";
    public string MenuItem_Close => "Cerrar";
    public string MenuItem_Exit => "Salir";
    public string MenuItem_Tools => "Herramientas";
    public string MenuItem_CheckIntegrity => "Verificar _integridad";
    public string MenuItem_CheckDirectoryIntegrity => "Verificar integridad de carpetaâ€¦";
    public string BatchNaming_Title => "Nombres";
    public string BatchNaming_Matches => "Coincide";
    public string BatchNaming_Differs => "No coincide";
    public string BatchNaming_Check => "Comprobar nombres";
    public string BatchNaming_RenameAll => "Renombrar los que no coinciden";
    public string BatchNaming_Error => "Error de nombre";
    public string BatchIntegrity_Title => "VerificaciÃ³n de integridad por lotes";
    public string BatchIntegrity_SelectDirectory => "Seleccionar carpeta con archivos de Switch";
    public string BatchIntegrity_Browse => "Examinarâ€¦";
    public string BatchIntegrity_IncludeSubdirectories => "Incluir subcarpetas";
    public string BatchTable_Columns => "Columnasâ€¦";
    public string BatchTable_Search => "Buscar";
    public string BatchTable_All => "Todos";
    public string BatchTable_ResetFilters => "Restablecer filtros";
    public string BatchTable_ResetSort => "Restablecer orden";
    public string BatchIntegrity_File => "Archivo";
    public string BatchIntegrity_Path => "Ruta";
    public string BatchIntegrity_Error => "Error";
    public string BatchIntegrity_NszDataCorrupted => "El flujo de datos NSZ/NCZ comprimido estÃ¡ daÃ±ado o incompleto (fallÃ³ la descompresiÃ³n Zstandard).";
    public string BatchIntegrity_IntegrityFailed => "No se pudo completar la comprobaciÃ³n de integridad. Consulte el registro para obtener mÃ¡s detalles.";
    public string PackageStructure_Filesystem => "Sistema de archivos";
    public string Signature_Title => "Firma NCA";
    public string Signature_Passed => "Correcta";
    public string Signature_NotPassed => "Incorrecta";
    public string Signature_Unchecked => "Sin verificar";
    public string ToolTip_PackageStructure => "Estructura segÃºn Nx Game Info:\nScene (XCI): particiones Update, Normal y Secure.\nConvertido (XCI): solo Secure; tÃ­pico de NSP â†’ XCI.\nScene (NSP): legalinfo.xml, nacp.xml, programinfo.xml y cardspec.xml; tÃ­pico de releases BBB.\nHomebrew (NSP): authoringtoolinfo.xml presente.\nCDN (NSP): certificado (.cert) y ticket (.tik); tÃ­pico de volcados eShop CDN.\nConvertido (NSP): sin certificado ni ticket; tÃ­pico de XCI â†’ NSP.\nSistema de archivos: tÃ­tulos NAX0 instalados en una tarjeta SD Switch.\nIncompleto: solo contenido NCA. NSZ/XCZ siguen las reglas de su paquete; NCZ cuenta como NCA.";
    public string ToolTip_NcaSignature => "Correcta: firmas NCA vÃ¡lidas, esperadas en tÃ­tulos oficiales.\nIncorrecta: al menos una firma NCA invÃ¡lida; posible en homebrew, inesperada en tÃ­tulos oficiales.\nSin verificar: verificaciÃ³n NCA incompleta. Ejecute la comprobaciÃ³n de integridad.\nEsta indicaciÃ³n corresponde a las cabeceras NCA; ACID es una firma NPDM distinta.";
    public string ToolTip_Permission => "Seguro: sin acceso a servicios de archivos o bit 0x8000000000000000 desactivado.\nInseguro: acceso a servicios de archivos y bit 0x8000000000000000 activado (EraseMmc).\nPeligroso: acceso a servicios de archivos y mÃ¡scara 0xffffffffffffffff (todos los permisos).\nInseguro/Peligroso solo deberÃ­a aparecer en homebrew. Disponible solo para juegos base y actualizaciones. Esta clasificaciÃ³n no es una evaluaciÃ³n completa de seguridad.";
    public string ToolTip_AcidSignature => "Firma de la secciÃ³n ACID de main.npdm, independiente de la firma de la cabecera NCA.";
    public string PackageStructure_Title => "Estructura del paquete";
    public string PackageStructure_Scene => "Lanzamiento Scene";
    public string PackageStructure_Cdn => "Copia CDN";
    public string PackageStructure_Converted => "Convertido";
    public string PackageStructure_Homebrew => "Homebrew";
    public string PackageStructure_Incomplete => "Incompleto";
    public string PackageStructure_Unknown => "Desconocido";
    public string FileInfo_FileSize => "TamaÃ±o del archivo";
    public string FileInfo_CompressionRatio => "RelaciÃ³n de compresiÃ³n";
    public string FileInfo_Uncompressed => "sin comprimir";
    public string FileInfo_SystemUpdate => "ActualizaciÃ³n del sistema incluida (XCI)";
    public string BatchIntegrity_Export => "Exportar CSVâ€¦";
    public string BatchIntegrity_Start => "Iniciar verificaciÃ³n";
    public string BatchIntegrity_OpenSelected => "Abrir en comprobaciÃ³n de archivo";
    public string BatchIntegrity_MoveSelected => "Moverâ€¦";
    public string BatchIntegrity_MoveValid => "Mover archivos vÃ¡lidosâ€¦";
    public string BatchIntegrity_SelectMoveDestination => "Seleccionar destino para archivos vÃ¡lidos";
    public string BatchIntegrity_Moving => "Moviendo";
    public string MenuItem_Options => "Opciones";
    public string MenuItem_Settings => "Configuraciones";
    public string MenuItem_ReloadKeys => "Recargar llaves";
    public string MenuItem_OpenTitleWebPage => "Abrir sitio web del tÃ­tulo...";
    public string MenuItem_ShowRenameToolWindow => "Herramienta de renombrado...";

    public string Packages_Title => "Archivo Multipaquete";
    public string DisplayVersion => "VersiÃ³n mostrada";
    public string Presentation_Title => "RepresentaciÃ³n";
    public string ToolTip_AvailableLanguages => "El tÃ­tulo, Editor e Ã­cono pueden cambiar segÃºn el idioma seleccionado.";
    public string AvailableLanguages => "Idiomas";
    public string AppTitle => "TÃ­tulo";
    public string Publisher => "Editor";
    public string Security_Title => "Seguridad del programa";
    public string Security_Level => "EvaluaciÃ³n";
    public string Security_FileSystemPermissions => "Permisos del sistema de archivos";
    public string Security_AcidSignature => "Firma ACID";
    public string Security_Safe => "Seguro";
    public string Security_Unsafe => "No seguro";
    public string Security_Dangerous => "Peligroso";
    public string Security_Unavailable => "No disponible";
    public string Security_Details => "Servicios autorizados ({0}): {1}";

    public string Lng_AmericanEnglish => "InglÃ©s EE.UU.A.";
    public string Lng_BritishEnglish => "InglÃ©s";
    public string Lng_CanadianFrench => "FrancÃ©s de CanadÃ¡";
    public string Lng_Dutch => "HolandÃ©s";
    public string Lng_French => "FrancÃ©s";
    public string Lng_German => "AlemÃ¡n";
    public string Lng_Italian => "Italiano";
    public string Lng_Japanese => "JaponÃ©s";
    public string Lng_Korean => "Coreano";
    public string Lng_LatinAmericanSpanish => "EspaÃ±ol Latinoamericano";
    public string Lng_Portuguese => "PortuguÃ©s";
    public string Lng_Russian => "Ruso";
    public string Lng_SimplifiedChinese => "Chino Simplificado";
    public string Lng_Spanish => "EspaÃ±ol";
    public string Lng_TraditionalChinese => "Chino Tradicional";
    public string Lng_BrazilianPortuguese => "PortuguÃ©s Brasilero";
    public string Lng_Unknown => "Desconocido";

    public string SettingsView_Title => "Configuraciones";
    public string SettingsView_Button_Apply => "Aplicar";
    public string SettingsView_Button_Cancel => "Cancelar";
    public string SettingsView_Button_Reset => "Valores Predeterminados";
    public string SettingsView_GroupBoxKeys => "Llaves";
    public string SettingsView_Title_KeysEffectiveFilePath => "Ruta Actual";
    public string SettingsView_Title_KeysCustomFilePath => "Ruta personalizada";
    public string SettingsView_Title_KeysDownloadUrl => "URL de descarga";
    public string KeysValidation_MissingFile => "No se encontrÃ³ ningÃºn archivo.";
    public string KeysValidation_ValidEntries => "VÃ¡lido ({0} entradas).";
    public string KeysValidation_MissingMasterKeys => "Claves maestras que faltan: {0}.";
    public string CnmtOverview_BaseTitleId => "ID del tÃ­tulo base";
    public string CnmtOverview_MasterKey => "Clave maestra necesaria";
    public string CnmtOverview_MinimumApplicationVersion => "VersiÃ³n mÃ­nima de la aplicaciÃ³n (DLC)";
    public string CnmtOverview_Distribution => "DistribuciÃ³n";
    public string KeysValidation_InvalidMasterKeys => "Claves maestras no vÃ¡lidas: {0}.";
    public string KeysValidation_InvalidLines => "LÃ­neas con formato incorrecto: {0}.";
    public string KeysValidation_EmptyFile => "El archivo no contiene entradas vÃ¡lidas.";
    public string KeysValidation_FirmwareEstimate => "RevisiÃ³n vÃ¡lida mÃ¡s alta: {0} â€” admite contenido hasta el firmware {1}.";
    public string KeysValidation_UnsupportedMasterKeys => "Se detectÃ³ una nueva revisiÃ³n de clave maestra: {0}. Esta versiÃ³n del programa aÃºn no puede validarla ni asociarla a un firmware; es necesario actualizar la aplicaciÃ³n.";
    public string SettingsView_ToolTip_Keys => """
                                               Las llaves son necesarias para poder abrir archivos del formato Nintendo Switch encriptados (XCI, NSP, ...).
                                               Cada archivo oficial con formato Nintendo Switch estÃ¡ encriptado con las llaves especÃ­ficas del firmware con que fueron construidos.

                                               AsegÃºrese de contar con el archivo de llaves Â«prod.keysÂ» mÃ¡s actualizado, para poder abrir archivos con el formato Nintendo Switch sin errores.

                                               El archivo deberÃ¡ contener una llave por lÃ­nea, con el formato Â«NOMBRE_LLAVE = VALOR_HEXADECIMALÂ».
                                               """;
    public string SettingsView_ToolTip_ProdKeys => """
                                                   Este archivo contiene las llaves comunes a todas las consolas Switch.  Este archivo se requiere para poder leer el contenido de tÃ­tulos encriptados.
                                                   NXFileViewer buscarÃ¡ por Ã©ste archivo en las siguientes rutas en orden:
                                                       1. La ruta definida en esta configuraciÃ³n.
                                                       2. La carpeta donde se encuentra el programa.
                                                       3. La carpeta Â«%UserProfile%\\.switchÂ»

                                                   Al iniciar, NXFileViewer puede descargar de forma automÃ¡tica el archivo de llaves si no se encuentra uno en el sistema.
                                                   El archivo se descargarÃ¡ en la carpeta donde se encuentra el programa.
                                                   """;

    public string SettingsView_ToolTip_TitleKeys => """
                                                    Alternativamente se puede indicar la ruta de un archivo que contenga las llaves especÃ­ficas de juegos.
                                                    NXFileViewer buscarÃ¡ por Ã©ste archivo en las siguientes rutas en orden:
                                                        1. La ruta definida en esta configuraciÃ³n.
                                                        2. La carpeta donde se encuentra el programa.
                                                        3. La carpeta Â«%UserProfile%\\.switchÂ»

                                                    Al iniciar, NXFileViewer puede descargar de forma automÃ¡tica el archivo de llaves si no se encuentra uno en el sistema.
                                                    El archivo se descargarÃ¡ en la carpeta donde se encuentra el programa.
                                                    """;

    public string SettingsView_LogFileRetention => "Conservar registros (1â€“100 inicios)";
    public string SettingsView_LogLevel => "Nivel de depuraciÃ³n";
    public string SettingsView_ToolTip_LogLevel => "El nivel de depuraciÃ³n indica el mÃ­nimo nivel de registros a crear en la bitÃ¡cora de eventos.";
    public string SettingsView_CheckBox_AlwaysReloadKeysBeforeOpen => "Siempre recargar las llaves antes de abrir un archivo";
    public string SettingsView_CheckBox_InjectTicketKeys => "Inyectar llaves desde los archivos de tiquetes (*.tik)";
    public string SettingsView_Title_Language => "Idioma";
    public string SettingsView_Title_Theme => "Tema";
    public string SettingsView_Title_NczOptions => "Configuraciones NSZ/XCZ";

    public string SettingsView_ToolTip_NczBlockLessCompression => """
                                                                  Los archivos NSZ o XCZ estÃ¡n compuestos por archivos del tipo NCZ que a su vez son archivos NCA comprimidos.
                                                                  Los archivos NCZ se pueden comprimir con y sin el mÃ©todo de compresiÃ³n por bloques, lo que hace la lectura aleatoria de datos imposible.
                                                                  Por lo tanto, si el archivo es grande y se requiere extraer un pedazo pequeÃ±o cercano al final del archivo, serÃ¡ necesario descomprimirlo completamente.
                                                                  Por ende, archivos grandes pueden tomar un tiempo en ser abiertos.
                                                                  Se exhorta usar compresiÃ³n por bloques para archivos grandes.
                                                                  El no seleccionar la opciÃ³n Â«Permitir abrir archivos NCZ sin mÃ©todo de compresiÃ³n de bloquesÂ», no afectarÃ¡ las caracterÃ­sticas de verificaciÃ³n de integridad.
                                                                  """;

    public string SettingsView_CheckBox_NczOpenBlocklessCompression => "Permitir abrir archivos NCZ sin mÃ©todo de compresiÃ³n de bloques";
    public string SettingsView_Title_Integrity => "Integridad";
    public string SettingsView_CheckBox_IgnoreMissingDeltaFragments => "Ignorar fragmentos delta perdidos";
    public string SettingsView_ToolTip_IgnoreMissingDeltaFragments => $"""
                                                                      Los archivos de parches pueden contener archivos completos de actualizaciÃ³n o archivos de actualizaciÃ³n incrementales (conocidos como Â«{ContentType.DeltaFragment}Â»).
                                                                      Dichos fragmentos no son obligatorios para la actualizaciÃ³n de un aplicativo y algunas veces son removidos.
                                                                      Seleccione Ã©sta opciÃ³n si desea ignorar que los fragmentos Â«{ContentType.DeltaFragment}Â» no existen durante la verificaciÃ³n de integridad.
                                                                      """;

    public string SettingsView_Miscellaneous => "MiscelÃ¡neos";
    public string SettingsView_ToolTip_OpenKeysLocation => "Abrir ubicaciÃ³n del archivo.";
    public string SettingsView_ToolTip_BrowseKeys => "Buscar...";
    public string SettingsView_ToolTip_DownloadKeys => "Descargar del URL indicado.";

    public string BrowseKeysFile_ProdTitle => "Seleccionar archivo de llaves Â«prod.keysÂ»";
    public string BrowseKeysFile_TitleTitle => "Seleccionar archivo de llaves Â«title.keysÂ»";
    public string BrowseKeysFile_Filter => "Archivos de llaves (*.keys)|*.keys|Todos los archivos (*.*)|*.*";

    public string SuspiciousFileExtension => "La extensiÃ³n del archivo Â«{0}Â» no parece ser vÃ¡lida, Se esperaba Â«{1}Â» o Â«{2}Â».";
    public string DragMeAFile => "Arrastre aquÃ­ un archivo soportado 8-)";
    public string MultipleFilesDragAndDropNotSupported => "No se soporta arrastrar y soltar mÃºltiples archivos, sÃ³lo se abrirÃ¡ el primer archivo.";

    public string CnmtOverview_Title => "InformaciÃ³n del Paquete";
    public string CnmtOverview_TitleId => "ID del tÃ­tulo";
    public string CnmtOverview_ContentType => "Tipo";
    public string CnmtOverview_TitleVersion => "VersiÃ³n";
    public string CnmtOverview_MinimumSystemVersion => "VersiÃ³n mÃ­nima del sistema";
    public string CnmtOverview_BuildID => "Build ID";
    public string CnmtOverview_BuildID_NotAvailableBecauseSectionIsSparse => "No disponible (poco contenido)";
    public string CnmtOverview_IsDemo => "Demo";

    public string ContextMenu_SaveImage => "Guardar...";
    public string CopyTitleImageError => "Error al copiar imagen de tÃ­tulo: {0}";
    public string SaveTitleImageError => "Error al guardar imagen de tÃ­tulo: {0}";

    public string SaveDialog_Title => "Guardar como";
    public string SaveDialog_ImageFilter => "Imagen";
    public string SaveDialog_AnyFileFilter => "Archivo";
    public string SaveFile_Error => "Error al guardar archivo: {0}";

    public string ContextMenu_CopyImage => "Copiar";

    public string TabOverview => "DescripciÃ³n";
    public string TabContent => "Contenido";
    public string GroupBoxStructure => "Estructura";
    public string GroupBoxProperties => "Propiedades";

    public string ContextMenu_ShowItemErrors => "Mostrar errores...";
    public string ContextMenu_SaveSectionItem => "Guardar secciÃ³n de contenido...";
    public string ContextMenu_SaveDirectoryItem => "Guardar directorio...";
    public string ContextMenu_SaveFileItem => "Guardar Archivo...";
    public string ContextMenu_SavePartitionFileItem => "Guardar archivo de particiÃ³n...";
    public string ContextMenu_SaveNcaFileRaw => "Guardar archivo NCA sin procesar...";
    public string ContextMenu_SaveNcaFilePlaintext => "Guardar archivo NCA en texto plano...";

    public string SettingsLoadingError => "Error al cargar configuraciones: {0}";
    public string SettingsSavingError => "Error al guardar configuraciones: {0}";

    public string LoadingError_DiskFull => "No hay suficiente espacio en disco para cargar o extraer el archivo. Libere espacio en la unidad de la carpeta temporal e intÃ©ntelo de nuevo. La carga se ha detenido.";
    public string LoadingError_Failed => "Error al cargar el archivo Â«{0}Â»: {1}";
    public string LoadingError_FailedToCheckIfXciPartitionExists => "Error al verificar que la particiÃ³n XCI existe: {0}";
    public string LoadingError_FailedToOpenXciPartition => "Error al abrir la particiÃ³n XCI: {0}";
    public string LoadingError_FailedToLoadXciContent => "Error al abrir el contenido XCI: {0}";
    public string LoadingError_FailedToOpenPartitionFile => "Error al abrir el archivo de particiÃ³n: {0}";
    public string LoadingError_FailedToLoadNcaFile => "Error al cargar el archivo NCA: {0}";
    public string LoadingError_FailedToLoadPartitionFileSystemContent => "Error al cargar contenido de la particiÃ³n de sistema de archivos: {0}";
    public string LoadingError_FailedToCheckIfSectionCanBeOpened => "Error al verificar si la secciÃ³n puede ser abierta: {0}";
    public string LoadingError_FailedToOpenNcaSectionFileSystem => "Error al abrir el contenido de la secciÃ³n NCA Â«{0}Â»: {1}";
    public string LoadingError_FailedToLoadSectionContent => "Error al cargar el contenido de la secciÃ³n: {0}";
    public string LoadingError_FailedToGetFileSystemDirectoryEntries => "Error al cargar las entradas de archivo del directorio de sistema: {0}";
    public string LoadingError_FailedToOpenNacpFile => "Error al abrir el archivo NACP: {0}";
    public string LoadingError_FailedToLoadNacpFile => "Error al cargar el archivo NACP: {0}";
    public string LoadingError_FailedToOpenCnmtFile => "Error al abrir el archivo CNMT: {0}";
    public string LoadingError_FailedToLoadCnmtFile => "Error al cargar el archivo CNMT: {0}";
    public string LoadingError_FailedToLoadNcaContent => "Error al cargar el contenido NCA: {0}";
    public string LoadingError_FailedToLoadDirectoryContent => "Error al cargar el contenido del directorio: {0}";
    public string LoadingError_FailedToLoadIcon_Log => "Error al cargar el Ã­cono: {0}";
    public string LoadingError_NcaFileMissing_Log => "No existe La entrada NCA Â«{0}Â» del tipo Â«{1}Â».";
    public string LoadingError_NoCnmtFound_Log => "Â¡No se encontrÃ³ entrada CNMT!";
    public string LoadingError_NacpFileMissing_Log => "Â¡No se encontrÃ³ el archivo NACP Â«{0}!";
    public string LoadingError_NcaMissingSection_Log => "A el contenido NCA del tipo Â«{0}Â» le falta la secciÃ³n del tipo Â«{0}Â».";
    public string LoadingError_MainFileMissing_Log => "Â¡No se encontrÃ³ el archivo Â«{0}Â»!";
    public string LoadingError_IconMissing_Log => "No se encuentra el archivo de Ã­cono Â«{0}Â».";
    public string LoadingError_XciSecurePartitionNotFound_Log => "Â¡No se encontrÃ³ la particiÃ³n segura XCI!";
    public string LoadingError_FailedToGetNcaSectionFsHeader => "Error al obtener el encabezado de sistema NCA para la secciÃ³n Â«{0}Â»: {1}";
    public string LoadingError_FailedToOpenMainFile => "Error al abrir el Archivo Principal: {0}";
    public string LoadingError_FailedToLoadMainFile => "Error al cargar el Archivo Principal: {0}";
    public string LoadingError_FailedToOpenNpdmFile => "No se pudo abrir main.npdm: {0}";
    public string LoadingError_FailedToLoadNpdmFile => "No se pudo analizar main.npdm: {0}";
    public string LoadingError_FailedToLoadTicketFile => "Error al cargar el archivo de tiquete: {0}";
    public string LoadingError_FailedToLoadTitleIdKey => "Error al cargar el ID de TÃ­tulo desde el archivo de tiquete Â«{0}Â»: {1}";
    public string LoadingError_NczBlocklessCompressionDisabled => "La apertura de archivos NCZ con compresiÃ³n sin bloques estÃ¡ deshabilitada en las configuraciones.";

    public string LoadingInfo_TitleIdKeySuccessfullyInjected => "Se encontrÃ³ la llave de tÃ­tulo ID Â«{0}={1}Â» en el archivo de tiquete Â«{2}Â», Se adicionÃ³ satisfactoriamente al conjunto de llaves.";
    public string LoadingWarning_TitleIdKeyReplaced => "Se encontrÃ³ la llave de tÃ­tulo ID Â«{0}={1}Â» en el archivo de tiquete Â«{2}Â», se ha usado como reemplazo de la llave de tÃ­tulo ID Â«{0}={2}Â» que habÃ­a en el conjunto de llaves.";
    public string LoadingDebug_TitleIdKeyAlreadyExists => "Se encontrÃ³ la llave de tÃ­tulo ID Â«{0}={1}Â» en el archivo de tiquete Â«{2}Â», y ya estaba registrada en el conjunto de llaves existente.";

    public string KeysFileUsed => "Â«{0}Â» archivo utilizado: {1}";
    public string NoneKeysFile => "[ninguno]";

    public string Status_DownloadingFile => "Descargando el archivo Â«{0}Â»...";
    public string Log_DownloadingFileFromUrl => "Descargando Â«{0}Â» desde el URL Â«{1}Â»...";
    public string Log_FileSuccessfullyDownloaded => "Archivo Â«{0}Â» descargado correctamente.";
    public string Log_FailedToDownloadFileFromUrl => "Error al descargar Â«{0}Â» desde el URL Â«{1}Â»: {2}";

    public string ToolTip_PatchNumber => "NÃºmero de parche {0}";
    public string Log_OpeningFile => "=====> {0} <=====";
    public string MainModuleIdTooltip => "TambiÃ©n conocido como Â«Build IDÂ» (or BID).";
    public string ATaskIsAlreadyRunning => "Ya existe una tarea ejecutÃ¡ndose...";
    public string FileInfo_Title => "Archivo";
    public string Title_FileInfo_FileType => "Tipo";
    public string Title_FileInfo_Compression => "CompresiÃ³n";
    public string Title_FileInfo_Integrity => "Integridad";
    public string ToolTip_NcasIntegrity => $"""
                                           Una verificaciÃ³n de integridad consiste en verificar la integridad de cada NCA (o NCZ).

                                           El resultado de la VerificaciÃ³n de Integridad puede ser uno de los siguientes:
                                           - {NcasIntegrity_NoNca}: No se encuentra archivo NCA.
                                           - {NcasIntegrity_Unchecked}: VerificaciÃ³n de Integridad no realizada.
                                           - {NcasIntegrity_InProgress}: VerificaciÃ³n de Integridad en progreso.
                                           - {NcasIntegrity_Original}: Todos los NCAs son originales (las firmas son correctas).
                                           - {NcasIntegrity_Incomplete}: Todos los NCAs son originales, sin embargo, algunos no pueden ser encontrados.
                                           - {NcasIntegrity_Modified}: Por lo menos un NCA ha sido modificado (la firma no es correcta pero el hash es correcto).
                                           - {NcasIntegrity_Corrupted}: Por lo menos un NCA estÃ¡ corrupto (el hash es invÃ¡lido).
                                           - {NcasIntegrity_Error}: Ha ocurrido un error durante la verificaciÃ³n de Integridad.

                                           Los detalles del anÃ¡lisis de cada NCA se encuentran en la ficha Â«{TabContent}Â».
                                           """;

    public string AvailableContents => "Contenidos:";
    public string MultiContentPackageToolTip => "El paquete actual consta de mÃºltiples contenidos (se detectaron Â«{0}Â»).";

    public string NcasIntegrity_Error_NcaMissing => "La integridad del NCA Â«{0}Â» no puede ser verificada, No existe el NCA.";
    public string NcasIntegrity_Error_Log => "Error al verificar la integridad de los NCAs: {0}";
    public string NcaIntegrity_GetOriginalNcaError => "Error al obtener el NCA original: {0}";
    public string NcaIntegrity_GetOriginalNcaError_Log => "Error al obtener el NCA original del NCA Â«{0}Â»: {1}";

    public string NcaHeaderSignature_Valid_Log => "La firma del encabezado del NCA Â«{0}Â» es invÃ¡lida.";
    public string NcaHeaderSignature_Invalid => "La verificaciÃ³n de la firma del encabezado del NCA fallÃ³ con el cÃ³digo de estado Â«{0}Â».";
    public string NcaHeaderSignature_Invalid_Log => "La verificaciÃ³n de la firma del NCA Â«{0}Â» fallÃ³ con el cÃ³digo de estado Â«{1}Â».";
    public string NcaHeaderSignature_Error => "Error al verificar el encabezado de la firma NCA: {0}.";
    public string NcaHeaderSignature_Error_log => "Error al verificar la firma del encabezado NCA Â«{0}Â»: {1}";

    public string NcaHash_VerificationStart_Log => ">>> VerificaciÃ³n del hash de los NCAs ha iniciado...";
    public string NcaHash_VerificationEnd_Log => ">>> VerificaciÃ³n del hash de los NCAs ha terminado.";
    public string NcaHash_NcaItem_CantExtractHashFromName => "Error al extraer el has esperado del nombre del NCA.";
    public string NcaHash_CantExtractHashFromName_Log => "Error al extraer el has esperado del nombre del NCA Â«{0}Â».";
    public string NcaHash_Valid_Log => "El hash del NCA Â«{0}Â» no es vÃ¡lido.";
    public string NcaHash_NcaItem_Invalid => "El hash es invÃ¡lido.";
    public string NcaHash_Invalid_Log => "El hash del NCA Â«{0}Â» no es vÃ¡lido.";
    public string NcaHash_NcaItem_Exception => "Error al verificar el hash: {0}";
    public string NcaHash_Exception_Log => "Error al verificar el hash del NCA Â«{0}Â»: {1}";
    public string NcaHash_ProgressText => "Verificando el hash del NCA {0}/{1}...";

    public string CancelAction => "Cancelar";
    public string Status_Ready => "Completado.";
    public string LoadingFile_PleaseWait => "Por favor espere, Cargando...";

    public string NcasIntegrity_NoNca => "Sin NCA";
    public string NcasIntegrity_Unchecked => "No verificado";
    public string NcasIntegrity_InProgress => "En progreso";
    public string NcasIntegrity_Original => "Original";
    public string NcasIntegrity_Incomplete => "Incompleto";
    public string NcasIntegrity_Modified => "Modificado";
    public string NcasIntegrity_Corrupted => "Corrupto";
    public string NcasIntegrity_Error => "Error";
    public string NcasIntegrity_Unknown => "Desconocido";

    public string Status_SavingFile => "Guardando archivo Â«{0}Â»...";

    public string KeysLoading_Starting_Log => ">>> Cargando llaves...";
    public string KeysLoading_Successful_Log => ">>> Llaves cargadas.";
    public string KeysLoading_UnusedKey_Log => "InformaciÃ³n: esta versiÃ³n del programa no utiliza la clave adicional Â«{0}Â».";
    public string KeysLoading_Error => "Error al cargar llaves: {0}.";
    public string WarnNoProdKeysFileFound => "No se encontrÃ³ el archivo Â«prod.keysÂ».";
    public string InvalidSetting_KeysFileNotFound => "El archivo de llaves Â«{0}Â» definido en las configuraciones, no existe.";
    public string InvalidSetting_BufferSizeInvalid => "El tamaÃ±o de Buffer Â«{0}Â» definido en las configuraciones no es un valor vÃ¡lido, debe ser mayor a 0.";
    public string InvalidSetting_LanguageNotFound => "El idioma Â«{0}Â» definido en las configuraciones no existe.";

    public string ToolTip_KeyMissing => "No existe La llave Â«{0}Â» del tipo Â«{1}Â».";

    public string MenuItem_CopyTextToClipboard => "Copiar";
    public string ContextMenu_OpenFileLocation => "Abrir ubicaciÃ³n del archivo...";
    public string OpenFileLocation_Failed_Log => "Error al abrir la ubicaciÃ³n del archivo Â«{0}Â»: {1}";
    public string SettingsView_TitlePageUrl => "URL de la pÃ¡gina de TÃ­tulos";
    public string SettingsView_TitleInfoApiUrl => "URL de API de informaciÃ³n de tÃ­tulos";
    public string SettingsView_TitleInfoProvider => "Fuente de nombres de tÃ­tulos";
    public string SettingsView_TitleDbRegion => "RegiÃ³n / idioma TitleDB";
    public string SettingsView_TitleDbCacheTip => "TitleDB se guarda localmente y se actualiza a diario. La cachÃ© sigue disponible si el servicio falla. Los tÃ­tulos ausentes tambiÃ©n se buscan en US.en.";
    public string BatchIntegrity_FileType => "Tipo de archivo";
    public string BatchIntegrity_PackageType => "Tipo de paquete";
    public string BatchIntegrity_ShowOnlyErrors => "Mostrar solo archivos defectuosos";
    public string OpenTitleWebPage_Failed => "Error al abrir la pÃ¡gina Web de tÃ­tulos: {0}";

    public string Log_DownloadFileCanceled => "Descarga cancelada.";
    public string Log_SaveToDirCanceled => "Guardado de directorio cancelado.";
    public string Log_SaveFileCanceled => "Guardado de archivo cancelado.";
    public string Log_SaveStorageCanceled => "Guardado de almacenamiento cancelado.";
    public string Log_NcasIntegrityCanceled => "VerificaciÃ³n de Integridad de NCAs cancelada.";

    public string RenamingTool_TargetDirectory => "Carpeta de destino (vacÃ­o = carpeta actual)";
    public string RenamingTool_FolderTip => "Use / para carpetas, por ejemplo DLC/{WTitle}.{Ext:L}.";
    public string RenamingTool_OldName => "Nombre anterior";
    public string RenamingTool_NewName => "Nombre nuevo";
    public string RenamingTool_StatusError => "Error";
    public string RenamingTool_StatusUnchanged => "Sin cambios";
    public string RenamingTool_StatusSimulation => "SimulaciÃ³n";
    public string RenamingTool_StatusRenamed => "Renombrado";
    public string RenamingTool_WindowTitle => "Herramienta de renombrado";
    public string RenamingTool_Patterns => "Patrones";
    public string RenamingTool_ApplicationPattern => "PatrÃ³n de aplicativo";
    public string RenamingTool_PatchPattern => "PatrÃ³n de parche";
    public string RenamingTool_AddonPattern => "PatrÃ³n de adiciones";
    public string RenamingTool_InputPath => "Ruta con archivos";
    public string RenamingTool_FileFilters => "Filtros";
    public string RenamingTool_ToolTip_Patterns =>
        $$"""
         Carpeta de destino y subcarpetas:
           Si el destino estÃ¡ vacÃ­o, se usa la carpeta actual del archivo.
           Separe las subcarpetas con /, por ejemplo:
             DLC/{WTitle}.{Ext:L}
             {WAppTitle}/DLC/{WTitle}.{Ext:L}
           Use carpetas relativas; no se permiten rutas absolutas ni .. .
           La simulaciÃ³n muestra rutas completas sin crear carpetas.
           Al renombrar se crean carpetas sin sobrescribir archivos existentes.

         SintÃ¡xis de las llaves:
            {<Llave>[:<Formato>]}

         El formato opcional puede ser:
         - U: MayÃºsculas
         - L: MinÃºsculas

         Ejemplos:
           {Title} => TÃ­tulo original
           {Title:U} => TÃ­tulo en mayÃºsculas

         Llaves soportadas:
           â€¢ TitleId:
              - El identificador del contenido.
           â€¢ AppId:
              - El identificador  correspondiente a {{nameof(ContentMetaType.Application)}} (para los contenidos {{nameof(ContentMetaType.Application)}}, Ã©ste valor es igual a {TitleId}).
           â€¢ PatchId:
              - Si el contenido es {{nameof(ContentMetaType.Application)}}, este valor serÃ¡ igual al identificador del contenido correspondiente {{nameof(ContentMetaType.Patch)}}, o de lo contrario cero.
           â€¢ PatchNum:
              - Si el contenido es una {{nameof(ContentMetaType.Application)}}, normalmente el valor es 0.
              - Si el contenido es un {{nameof(ContentMetaType.Patch)}}, el valor corresponde al nÃºmero del parche.
              - Si el contenido es un {{nameof(ContentMetaType.AddOnContent)}}, el valor corresponde al nÃºmero de adiciÃ³n del parche.
           â€¢ Title:
              - El primer tÃ­tulo de los tÃ­tulos declarados.
              - Este valor existe solo en contenidos del tipo {{nameof(ContentMetaType.Application)}} o {{nameof(ContentMetaType.Patch)}}, pero no para los tipos {{nameof(ContentMetaType.AddOnContent)}}.
           â€¢ Ext:
              - La extensiÃ³n correspondiente al tipo de archivo detectado.
           â€¢ VerNum:
              - El nÃºmero de versiÃ³n del contenido.
           â€¢ VerDsp:
              - La versiÃ³n a mostrar.
           â€¢ WTitle:
              - El tÃ­tulo consultado desde la Internet.
           â€¢ WAppTitle:
              - El tÃ­tulo de la {{nameof(ContentMetaType.Application)}} correspondiente, consultado desde la Internet.

         Utilice las secuencias \{ o \} para escribir caracteres literales { o }.
         """;
    public string RenamingTool_ToolTip_BasePattern => $"El patrÃ³n a utilizar para contenidos del tipo {nameof(ContentMetaType.Application)}.";
    public string RenamingTool_ToolTip_PatchPattern => $"El patrÃ³n a utilizar para contenidos del tipo {nameof(ContentMetaType.Patch)}.";
    public string RenamingTool_ToolTip_AddonPattern => $"El patrÃ³n a utilizar para contenidos del tipo {nameof(ContentMetaType.AddOnContent)}.";
    public string RenamingTool_Button_Cancel => "Cancelar";
    public string RenamingTool_Button_Rename => "Renombrar";
    public string RenamingTool_GroupBoxInput => "Archivos origen";
    public string RenamingTool_GroupBoxNamingSettings => "ConfirmaciÃ³n de nombrado";
    public string RenamingTool_BrowseDirTitle => "Seleccione una carpeta";
    public string RenamingTool_GroupBoxOutput => "Resultado";
    public string RenamingTool_Miscellaneous => "MiscelÃ¡neos";
    public string RenamingTool_InvalidWindowsCharReplacement => "CarÃ¡cter para reemplazar sÃ­mbolos invÃ¡lidos:";
    public string RenamingTool_ReplaceWhiteSpaceChars => "Reemplazar espacios en blanco";
    public string RenamingTool_ReplaceWhiteSpaceCharsWith => "Reemplazar espacios en blanco con:";
    public string RenamingTool_Simulation => "Simular";
    public string RenamingTool_AutoCloseOpenedFile => "Cerrar archivos abiertos";
    public string RenamingTool_IncludeSubDirectories => "Incluir subcarpetas";
    public string RenamingTool_ContentTypeNotSupported => "No se soporta contenido del tipo Â«{0}Â».";
    public string RenamingTool_SuperPackageNotSupported => "Los sÃºper paquetes no son soportados.";
    public string RenamingTool_LogNbFilesToRename => ">>> faltan {0} archivo(s) para renombrar...";
    public string RenamingTool_LogSimulationMode => $"[SIMULACIÃ“N] ";
    public string RenamingTool_LogFileRenamed => $"â€¢ {{0}}Archivo renombrado{Environment.NewLine}\tde: Â«{{1}}Â» a: {Environment.NewLine}\tÂ«{{2}}Â».";
    public string RenamingTool_LogFileAlreadyNamedProperly => "â€¢ {0}Â«{1}Â» Ya tenÃ­an el nombre seleccionado.";
    public string RenamingTool_LogFailedToRenameFile => "â€¢ {0}Â«{1}Â»Renombrado fallido: {2}";
    public string RenamingTool_LogRenamingFailed => "Renombrado fallido: {0}";
    public string RenamingTool_BadInvalidFileNameCharReplacement => "La cadena para renombrar Â«{0}Â» Contiene el carÃ¡cter invÃ¡lido Â«{1}Â».";

    public string Exception_UnexpectedDelimiter => "Delimitador invÃ¡lido {0} se encontrÃ³ en la posiciÃ³n {1}, cÃ¡mbielo por {2}{0}.";
    public string Exception_EndDelimiterMissing => "No se encuentra el delimitador final {0}.";
    public string FileRenaming_PatternKeywordUnknown => "La llave Â«{0}Â» es desconocida, las llaves permitidas son: Â«{1}Â».";
    public string FileRenaming_EmptyPatternNotAllowed => "El patrÃ³n no puede estar vacÃ­o.";
    public string FileRenaming_PatternKeywordNotAllowed => "No se permite la llave Â«{0}Â» en patrones del tipo Â«{1}Â».";
    public string FileRenaming_StringOperatorUnknown => "No se reconoce el operador Â«{0}Â» los operadores permitidos son: Â«{1}Â».";
    public string FileRenaming_EmptyDirectoryNotAllowed => "La carpeta origen no puede estar vacÃ­a.";
    public string Window_Tip_Title => "Consejo";
    public string Nsz_Installed => "Plugin NSZ instalado";
    public string Nsz_CustomExecutable => "Ejecutable personalizado";
    public string Settings_Program => "Programa";
    public string Nsz_PhaseSource => "Verificar origen";
    public string Nsz_PhaseOutput => "Verificar resultado";
    public string Nsz_PhasePublish => "Guardar resultado";
    public string Batch_IncludeArchives => "Incluir ZIP / 7z";
    public string Batch_Scan => "Escanear archivos";
    public string Batch_VerifyAll => "Verificar todos";
    public string File_SaveBackupSuspected => "Copia de partida guardada (probable)";
    public string Batch_MultiPackageDetails => "Mostrar u ocultar paquetes incluidos";
    public string File_SaveBackup => "Copia de partida guardada";
    public string File_MissingKeys => "Faltan claves necesarias. No se puede leer todo el contenido. Revise prod.keys / title.keys.";
    public string File_CopyMissingKeys => "Copiar nombres de claves faltantes";
    public string Keys_ProgramFolder => "Carpeta del programa";
    public string Keys_SharedFolder => "Perfil de usuario (.switch)";
    public string Keys_InUse => "En uso";
    public string Keys_DownloadAll => "Descargar claves";
    public string Keys_DownloadHost => "IP / nombre del servidor";
    public string Keys_DownloadHostTip => "{IP} en las URL se sustituye por esta direcciÃ³n. Destino: ruta personalizada si estÃ¡ definida; si no, carpeta del programa.";
    public string Keys_CopyToSwitch => @"Copiar claves actuales a %USERPROFILE%\.switch";
    public string Keys_ReplaceShared => "Â¿Reemplazar claves existentes? Se conservarÃ¡n los archivos originales.";
    public string Keys_SharedCopied => "Claves disponibles en la carpeta compartida .switch.";
    public string Batch_ScanAndVerify => "Escanear archivos y verificar integridad";
}
