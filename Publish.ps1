<#
    Default: show GUI using publish.ps1.conf/defaults; auto-build after 10 seconds without input.
    -Gui edits/saves the configuration. -GitHub builds all CI release artifacts.
    -ReleasePackages builds the Avalonia release and update metadata.
    -AppImage (Linux, appimagetool on PATH) or -Dmg (macOS) adds a native Avalonia package.
#>
[CmdletBinding(PositionalBinding = $false)]
param(
    [string]$OutputDirectory = "Publish",
    [string]$Version,
    [string]$PackageName = "NxFileViewerFork",
    [string]$ExecutableName,
    [string]$ExplorerPublisher = 'CN=NxFileViewer Fork',
    [string]$ExplorerCertificateThumbprint,
    [string]$ExplorerSdkBin,
    [string]$ExplorerPackageVersion,
    [string]$ExplorerTimestampServer = 'http://timestamp.digicert.com',
    [switch]$UnsignedExplorerPackages,
    [switch]$SkipExplorerPackages,
    [switch]$ViewerX64,
    [switch]$ViewerX86,
    [switch]$ViewerArm64,
    [switch]$ViewerX64WithRuntime,
    [switch]$ViewerX86WithRuntime,
    [switch]$ViewerArm64WithRuntime,
    [switch]$LogViewerX64,
    [switch]$LogViewerX86,
    [switch]$LogViewerArm64,
    [switch]$LogViewerX64WithRuntime,
    [switch]$LogViewerX86WithRuntime,
    [switch]$LogViewerArm64WithRuntime,
    [switch]$TransitionX64,
    [switch]$TransitionX86,
    [switch]$TransitionX64WithRuntime,
    [switch]$TransitionX86WithRuntime,
    [switch]$LogViewerOnly,
    [switch]$SkipLogViewer,
    [switch]$IncludeLegacyTransition = $true,
    [switch]$SkipAvalonia,
    [switch]$AvaloniaOnly,
    [switch]$AvaloniaViewer,
    [switch]$AvaloniaLogViewer,
    [switch]$AppImage,
    [switch]$Dmg,
    [ValidateSet('win-x64', 'win-x86', 'win-arm64', 'linux-x64', 'linux-arm64', 'osx-x64', 'osx-arm64')]
    [string[]]$AvaloniaRuntimeIdentifiers = @(),
    [switch]$WithDotNet,
    [switch]$WithoutDotNet,
    [switch]$Gui,
    [switch]$NoGui,
    [switch]$GitHub,
    [switch]$Wpf,
    [switch]$Avalonia,
    [switch]$All,
    [switch]$ReleasePackages,
    [ValidateSet('Explorer','Transition','Updater','UpdateManifest','AvaloniaZipMetadata','WpfMigration')][string[]]$Extras=@(),
    [ValidateSet('Zip','Folder','Both')][string]$ExplorerOutputFormat='Zip',
    [ValidateSet('Zip','Folder','Both')][string]$TransitionOutputFormat='Zip',
    [ValidateSet('x64','x86','arm64')][string[]]$ExtraWindowsArchitectures=@('x64','x86','arm64'),
    [ValidateSet('WPF','Avalonia')][string[]]$UpdateFrameworks=@('WPF','Avalonia'),
    [hashtable]$AvaloniaUpdateVersions=@{},
    [string]$ConfigFile,
    [string]$Profile,
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$AdditionalSwitches = @()
)

$ErrorActionPreference = "Stop"
# PowerShell normally uses one dash; also accept double-dash switch spellings.
foreach ($argument in $AdditionalSwitches) {
    if ($argument -notmatch '^--(?<name>[A-Za-z][A-Za-z0-9]*)$') { throw "Unknown argument: $argument" }
    $switchName = $Matches['name']
    $parameter = $MyInvocation.MyCommand.Parameters[$switchName]
    if ($null -eq $parameter -or $parameter.ParameterType -ne [System.Management.Automation.SwitchParameter]) { throw "Unknown switch: $argument" }
    Set-Variable -Name $switchName -Value ([switch]$true)
    $PSBoundParameters[$switchName] = [switch]$true
}
$SelectedExeVariants = @(
    'ViewerX64','ViewerX86','ViewerArm64','ViewerX64WithRuntime','ViewerX86WithRuntime','ViewerArm64WithRuntime',
    'LogViewerX64','LogViewerX86','LogViewerArm64','LogViewerX64WithRuntime','LogViewerX86WithRuntime','LogViewerArm64WithRuntime',
    'TransitionX64','TransitionX86','TransitionX64WithRuntime','TransitionX86WithRuntime'
) | Where-Object { $PSBoundParameters.ContainsKey($_) -and $PSBoundParameters[$_] }
# Config/menu orchestration is separate from the established full release builder below.
$nativePackage = $AppImage -or $Dmg
if ($AppImage -and $Dmg) { throw 'Run -AppImage on Linux and -Dmg on macOS in separate invocations.' }
if ($nativePackage -and ($SelectedExeVariants -or $LogViewerOnly -or $SkipAvalonia -or $ReleasePackages -or $Wpf -or $All)) {
    throw 'Use -AppImage or -Dmg with an Avalonia build.'
}
if ($Extras.Count -and ($Gui -or $GitHub -or $ReleasePackages -or $SelectedExeVariants -or $LogViewerOnly -or
    $AvaloniaOnly -or $AvaloniaViewer -or $AvaloniaLogViewer -or $nativePackage -or $Wpf -or $Avalonia -or $All -or $Profile)) {
    throw 'Use -Extras as a standalone publish mode.'
}
$individualSelection = $Extras.Count -or $SelectedExeVariants -or $LogViewerOnly -or $AvaloniaOnly -or $AvaloniaViewer -or $AvaloniaLogViewer
if (($Gui -or $GitHub) -and ($nativePackage -or $individualSelection -or $Wpf -or $Avalonia -or $All -or $ReleasePackages)) { throw 'Use -Gui or -GitHub without application/release selectors.' }
if ($Gui -and ($GitHub -or $NoGui)) { throw 'Choose -Gui, -NoGui or -GitHub.' }
if (($Gui -or $GitHub) -and ($SkipAvalonia -or $SkipLogViewer -or $AvaloniaRuntimeIdentifiers.Count -or $WithDotNet -or $WithoutDotNet)) { throw 'Use -Gui or -GitHub without additional target/runtime filters.' }
if (($Wpf -or $Avalonia -or $All) -and $SkipAvalonia) { throw 'Use -Wpf instead of combining application groups with -SkipAvalonia.' }
if ($individualSelection -and ($Wpf -or $Avalonia -or $All)) { throw 'Do not combine application groups and individual selectors.' }
if ($GitHub -and ($ConfigFile -or $Profile)) { throw '-GitHub does not load local configuration files or profiles.' }
if ($Profile -and ($nativePackage -or $individualSelection -or $ReleasePackages -or $SkipAvalonia)) { throw 'Use -Profile with the configured/menu modes or application groups.' }
if (!$nativePackage -and !$GitHub -and !$ReleasePackages -and !$individualSelection -and !$SkipAvalonia) {
    . (Join-Path $PSScriptRoot 'tools/Publish-Configuration.ps1')
    if (!$ConfigFile) { $ConfigFile = Join-Path $PSScriptRoot 'publish.ps1.conf' }
    $config = Read-PublishConfiguration $ConfigFile $Profile
    if ($All) { $config = New-PublishConfiguration }
    if ($Wpf -and !$Avalonia) { $config.Applications=@('WpfViewer','WpfLogViewer') }
    elseif ($Avalonia -and !$Wpf) { $config.Applications=@('AvaloniaViewer','AvaloniaLogViewer') }
    elseif ($Wpf -and $Avalonia) { $config.Applications=@('WpfViewer','WpfLogViewer','AvaloniaViewer','AvaloniaLogViewer') }
    if ($SkipLogViewer) { $config.Applications=@($config.Applications | Where-Object { $_ -notlike '*LogViewer' }) }
    if ($PSBoundParameters.ContainsKey('OutputDirectory')) { $config.OutputDirectory=$OutputDirectory }
    if ($AvaloniaRuntimeIdentifiers.Count) {
        $config.RuntimeIdentifiers=$AvaloniaRuntimeIdentifiers
        $config.PSObject.Properties.Remove('TargetOutputs')
    }
    if ($WithDotNet -or $WithoutDotNet) {
        $config.WithDotNet=[bool]$WithDotNet
        $config.WithoutDotNet=[bool]$WithoutDotNet
        foreach ($entry in $config.TargetOutputs.PSObject.Properties) {
            $row = $entry.Value
            $with = $row.WithDotNet
            $without = $row.WithoutDotNet
            $row.WithDotNet = if (!$WithDotNet) { 'Off' } elseif ($with -eq 'Off') { $without } else { $with }
            $row.WithoutDotNet = if (!$WithoutDotNet) { 'Off' } elseif ($without -eq 'Off') { $with } else { $without }
        }
    }
    $showMenu = $Gui -or (!$NoGui -and !$Wpf -and !$Avalonia -and !$All -and !$SkipLogViewer -and !$AvaloniaRuntimeIdentifiers.Count -and !$WithDotNet -and !$WithoutDotNet)
    if ($showMenu) {
        $config = Show-PublishConfigurationMenu $config $ConfigFile $Profile $PSScriptRoot $Version
        if ($null -eq $config) { return }
        $Version = $null
    }
    Invoke-PublishConfiguration $config $PSScriptRoot $Version
    return
}
$AvaloniaSelection = $nativePackage -or $AvaloniaOnly -or $AvaloniaViewer -or $AvaloniaLogViewer
if ($SkipAvalonia -and $AvaloniaSelection) { throw 'Do not combine -SkipAvalonia with an Avalonia-only selection.' }
if ($AvaloniaSelection -and ($SelectedExeVariants -or $LogViewerOnly -or ($SkipLogViewer -and !$nativePackage))) { throw 'Do not combine Avalonia-only choices with WPF-only selections.' }
if (!$AvaloniaSelection -and ($AvaloniaRuntimeIdentifiers.Count -or $WithDotNet -or $WithoutDotNet)) { throw 'Use Avalonia target/runtime filters with -AvaloniaOnly, -AvaloniaViewer or -AvaloniaLogViewer.' }
function Publish-AvaloniaSelection {
    $options = @{ OutputDirectory = $OutputDirectory; SkipFirmwareHashes = $true; AllTargets = $true; NoCopy = $true }
    if ($Version) { $options.Version = $Version }
    if ($AvaloniaRuntimeIdentifiers.Count) { $options.RuntimeIdentifiers = $AvaloniaRuntimeIdentifiers }
    if ($AvaloniaViewer -and !$AvaloniaLogViewer) { $options.Applications = @('Viewer') }
    elseif ($AvaloniaLogViewer -and !$AvaloniaViewer) { $options.Applications = @('LogViewer') }
    elseif ($SkipLogViewer) { $options.Applications = @('Viewer') }
    if ($WithDotNet -or $WithoutDotNet) {
        $options.RuntimeVariants = @($(if ($WithoutDotNet) { $false }); $(if ($WithDotNet) { $true }))
    }
    if ($nativePackage) {
        $options.NativePackage = if ($AppImage) { 'AppImage' } else { 'Dmg' }
        if (!$WithDotNet -and !$WithoutDotNet) { $options.RuntimeVariants = @($true) }
        if (!$AvaloniaOnly -and !$AvaloniaLogViewer -and !$Avalonia) { $options.Applications = @('Viewer') }
    }
    & (Join-Path $PSScriptRoot 'tools/Publish-Applications.ps1') -Framework Avalonia @options
}
if ($AvaloniaSelection) { Publish-AvaloniaSelection; return }
if ($ReleasePackages -and !$SkipAvalonia -and !$Wpf) {
    $releaseOptions = @{ OutputDirectory=$OutputDirectory; Applications=@('Viewer'); NoCopy=$true }
    if (!$SkipLogViewer) { $releaseOptions.Applications += 'LogViewer' }
    if ($Version) { $releaseOptions.Version = $Version }
    & (Join-Path $PSScriptRoot 'tools/Publish-Applications.ps1') -Framework Avalonia @releaseOptions
    $manifestOptions = @{
        OutputDirectory=$OutputDirectory
        UpdateFrameworks=@('Avalonia')
        Extras=@('UpdateManifest')
    }
    if ($Version) { $manifestOptions.Version = $Version }
    if (!$SkipExplorerPackages -and [Environment]::OSVersion.Platform -eq [PlatformID]::Win32NT) {
        $manifestOptions.Extras += 'Explorer'
        foreach ($option in @('ExplorerPackageVersion', 'ExplorerPublisher', 'ExplorerCertificateThumbprint', 'ExplorerSdkBin',
            'ExplorerTimestampServer', 'UnsignedExplorerPackages')) {
            if ($PSBoundParameters.ContainsKey($option)) { $manifestOptions[$option] = $PSBoundParameters[$option] }
        }
    }
    & $PSCommandPath @manifestOptions
    return
}
if ($SelectedExeVariants -and ($LogViewerOnly -or $SkipLogViewer)) {
    throw 'Do not combine individual EXE selections with -LogViewerOnly or -SkipLogViewer.'
}
if ($LogViewerOnly -and $SkipLogViewer) { throw 'Choose either -LogViewerOnly or -SkipLogViewer.' }
if ($UnsignedExplorerPackages -and $ExplorerCertificateThumbprint) { throw 'Choose either unsigned packages or a signing certificate.' }
if ($PackageName -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]{0,99}$' -or $PackageName.EndsWith('.')) { throw 'Supply a plain release package name.' }
& (Join-Path $PSScriptRoot 'src/config/Validate-ExternalSources.ps1')
& (Join-Path $PSScriptRoot 'src/config/Validate-NamingPresets.ps1')
& (Join-Path $PSScriptRoot 'src/config/Validate-DefaultSettings.ps1')
& (Join-Path $PSScriptRoot 'src/config/Validate-MasterKeys.ps1')
$AppName = $PackageName
$ProjectPath = Join-Path $PSScriptRoot "src/NxFileViewer.WPF/NxFileViewer.WPF.csproj"
$PublishedExecutableName = (Select-Xml -LiteralPath $ProjectPath -XPath "//Project/PropertyGroup/AssemblyName").Node.InnerText
if (!$PublishedExecutableName) { $PublishedExecutableName = [IO.Path]::GetFileNameWithoutExtension($ProjectPath) }
$PublishedExecutableName += '.exe'
if (!$ExecutableName) { $ExecutableName = $PublishedExecutableName }
if ([IO.Path]::GetFileName($ExecutableName) -cne $ExecutableName -or $ExecutableName -notmatch '(?i)\.exe$' -or
    $ExecutableName -in @('dotnet.exe','NxFileViewerFork.Open.exe') -or $ExecutableName.IndexOfAny([IO.Path]::GetInvalidFileNameChars()) -ge 0) {
    throw 'Supply a plain main executable filename ending in .exe.'
}
$AppVersion = if ($Version) { $Version } else { (Select-Xml -LiteralPath $ProjectPath -XPath "//Project/PropertyGroup/Version").Node.InnerText }
if ($AppVersion -notmatch '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?$') {
    throw 'Supply a version such as 4.2.0, 4.2.0-beta or 4.2.0-beta1, without a v prefix.'
}
$ManifestTemplatePath = Join-Path $PSScriptRoot 'src/config/package-manifest.json'
if ((Get-Item -LiteralPath $ManifestTemplatePath).Length -gt 65536) { throw 'Package manifest template is too large.' }
$PackageIdentity = Get-Content -LiteralPath $ManifestTemplatePath -Raw -Encoding UTF8 | ConvertFrom-Json
if ($PackageIdentity.SchemaVersion -ne 1 -or $PackageIdentity.ApplicationId -cne 'NxFileViewerFork') { throw 'Invalid package manifest identity.' }
if ($null -eq $PackageIdentity.UpdateSources -or @($PackageIdentity.UpdateSources).Count -gt 8 -or $null -eq $PackageIdentity.Migrations -or @($PackageIdentity.Migrations).Count -gt 128) { throw 'Invalid manifest sources or migrations.' }
foreach ($source in $PackageIdentity.UpdateSources) {
    if ([string]::IsNullOrWhiteSpace($source) -or $source.Length -gt 2048 -or $source -match '\s') { throw 'Invalid update source in template.' }
    $value=$source
    if ($value.StartsWith('github:',[StringComparison]::OrdinalIgnoreCase)) { $value=$value.Substring(7) }
    elseif ($value -match '^(gitlab|forgejo|manifest):') { $value=$value.Substring($value.IndexOf(':')+1) }
    if ($source -match '^(gitlab|forgejo|manifest):' -and !$value.StartsWith('https://',[StringComparison]::OrdinalIgnoreCase)) { throw 'Expected an HTTPS source address.' }
    if ($source -match '^github:' -and $value.StartsWith('https://',[StringComparison]::OrdinalIgnoreCase)) { throw 'GitHub sources use owner/repository.' }
    if ($value.StartsWith('https://',[StringComparison]::OrdinalIgnoreCase)) {
        $uri=$null
        if (![Uri]::TryCreate($value,[UriKind]::Absolute,[ref]$uri) -or $uri.Scheme -ne 'https' -or $uri.UserInfo -or $uri.Fragment) { throw 'Invalid HTTPS update source.' }
        if ($source -match '^(gitlab|forgejo):' -and ($uri.Query -or $uri.AbsolutePath.Trim('/').Split('/').Count -lt 2)) { throw 'Expected a forge repository or project API URL.' }
    } elseif ($value -notmatch '^[A-Za-z0-9_-][A-Za-z0-9_.-]*/[A-Za-z0-9_.-]+$' -or $value.Length -gt 201 -or @($value.Split('/') | Where-Object { $_ -in @('.','..') -or $_.EndsWith('.') }).Count) { throw 'Invalid GitHub repository in template.' }
}
if ($PackageIdentity.PSObject.Properties.Name -contains 'UpdateSourceOptions') {
    if ($null -eq $PackageIdentity.UpdateSourceOptions -or $PackageIdentity.UpdateSourceOptions -isnot [pscustomobject]) { throw 'Expected UpdateSourceOptions object.' }
    $properties = @($PackageIdentity.UpdateSourceOptions.PSObject.Properties)
    if ($properties.Count -gt 16) { throw 'Too many source options.' }
    foreach ($property in $properties) {
        if ($property.Name -cnotin $PackageIdentity.UpdateSources -or $null -eq $property.Value) { throw 'Source options must reference a listed source.' }
        $option = $property.Value
        if ($option.PSObject.Properties.Name -contains 'Enabled' -and $option.Enabled -isnot [bool]) { throw 'Enabled must be a JSON boolean.' }
        if ($option.PSObject.Properties.Name -contains 'TimeoutSeconds' -and
            (($option.TimeoutSeconds -isnot [int] -and $option.TimeoutSeconds -isnot [long]) -or $option.TimeoutSeconds -lt 1 -or $option.TimeoutSeconds -gt 300)) {
            throw 'TimeoutSeconds must be an integer between 1 and 300.'
        }
    }
}
function Assert-MigrationPath([string]$value) {
    $value=$value.Replace('\','/')
    if (!$value -or $value.Length -gt 1024 -or $value -match '[<>:"|?*\x00-\x1f]' -or $value -match '(?i)\.keys$|\.settings\.json$') { throw 'Unsafe migration path.' }
    $parts=$value.Split('/')
    if ($parts.Count -gt 32 -or $parts[0] -in @('Updates','.git','.codex') -or $value -in @('nxfileviewer-package.json','NxFileViewerFork.update-state.json')) { throw 'Protected migration path.' }
    foreach ($part in $parts) {
        if (!$part -or $part -in @('.','..') -or $part.EndsWith('.') -or $part.EndsWith(' ') -or $part.Split('.')[0] -match '^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])$') { throw 'Unsafe migration path component.' }
    }
    return $value
}
$MigrationIds = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($migration in $PackageIdentity.Migrations) {
    if ([string]::IsNullOrWhiteSpace($migration.Id) -or $migration.Id.Length -gt 128 -or !$MigrationIds.Add($migration.Id) -or
        $migration.WhenInstalledVersionBelow -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?$' -or
        $migration.ApplyFromTargetVersion -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?$' -or
        $null -eq $migration.Actions -or @($migration.Actions).Count -gt 128) { throw 'Invalid migration identity or version conditions.' }
    foreach ($action in $migration.Actions) {
        if ($action.Type -cnotin @('MoveDirectory','MoveFile') -or ($action.IfTargetExists -and $action.IfTargetExists -cne 'KeepExisting')) { throw 'Unsupported migration action.' }
        $from=Assert-MigrationPath $action.From; $to=Assert-MigrationPath $action.To
        if ($from -ieq $to -or $from.StartsWith($to+'/',[StringComparison]::OrdinalIgnoreCase) -or $to.StartsWith($from+'/',[StringComparison]::OrdinalIgnoreCase)) { throw 'Overlapping migration paths.' }
    }
}
$PackageIdentity | Add-Member -NotePropertyName Version -NotePropertyValue $AppVersion -Force
$PackageIdentity | Add-Member -NotePropertyName Executable -NotePropertyValue $ExecutableName -Force
$PackageIdentityJson = $PackageIdentity | ConvertTo-Json -Depth 16
$ApplicationReleasePackages = @()
if (!$ExplorerPackageVersion) {
    $ExplorerPackageVersion = (Select-Xml -LiteralPath $ProjectPath -XPath '//Project/PropertyGroup/ExplorerPackageVersion').Node.InnerText
}
if (!$ExplorerPackageVersion) {
    # MSIX versions are numeric; keep beta revisions below the final release.
    if ($AppVersion -notmatch '^(\d+)\.(\d+)\.(\d+)(?:-(.*))?$') { throw 'Supply -ExplorerPackageVersion for this application version.' }
    $major = $Matches[1]; $minor = $Matches[2]; $patch = $Matches[3]; $preRelease = $Matches[4]
    $revision = if (!$preRelease) { 65535 } elseif ($preRelease -match '(\d+)$') { [int]$Matches[1] } else { 0 }
    if ($revision -gt 65535) { throw 'Explorer package revision exceeds the MSIX limit.' }
    $ExplorerPackageVersion = "$major.$minor.$patch.$revision"
}
$OutDirRoot = if ([IO.Path]::IsPathRooted($OutputDirectory)) { [IO.Path]::GetFullPath($OutputDirectory) } else { [IO.Path]::GetFullPath((Join-Path $PSScriptRoot $OutputDirectory)) }
New-Item -ItemType Directory -Path $OutDirRoot -Force | Out-Null
# Isolated staging leaves existing extracted releases and user keys untouched.
$StagingRoot = Join-Path $OutDirRoot (".publish-" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $StagingRoot | Out-Null
function Write-PublishExtraPackage([string]$Name, [string[]]$Files, [string]$Format) {
    if ($Format -in @('Zip','Both')) {
        Compress-Archive -LiteralPath $Files -DestinationPath (Join-Path $OutDirRoot "$Name.zip") -Force
    }
    if ($Format -in @('Folder','Both')) {
        $directory = Join-Path $OutDirRoot $Name
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
        Copy-Item -LiteralPath $Files -Destination $directory -Force
        Write-Host "Folder: $directory"
    }
}
function Publish-TransitionPackage([string]$Architecture, [bool]$WithRuntime, [string]$TransitionVersion=$AppVersion) {
    $Variant = if ($WithRuntime) { '_self-contained' } else { '' }
    $transitionProject = Join-Path $PSScriptRoot 'src/NxFileViewer.Transition/NxFileViewer.Transition.csproj'
    $transitionDir = Join-Path $StagingRoot ("transition${Variant}_${Architecture}")
    dotnet publish $transitionProject --artifacts-path (Join-Path $StagingRoot "artifacts") -p:Version=$TransitionVersion -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=$WithRuntime -c Release --self-contained $WithRuntime -r "win-$Architecture" -o $transitionDir
    if ($LASTEXITCODE -ne 0) { throw 'Transition publishing failed.' }
    $transitionFiles = @(Get-ChildItem -LiteralPath $transitionDir -File)
    if ($transitionFiles.Count -ne 1 -or $transitionFiles[0].Name -ne 'NxFileViewer.exe') { throw 'Legacy transition must contain exactly one NxFileViewer.exe.' }
    Write-PublishExtraPackage "NxFileViewer_v${TransitionVersion}${Variant}_${Architecture}" $transitionFiles[0].FullName $TransitionOutputFormat
}

function Publish-ExplorerPackages([string[]]$Architectures=@('x64','arm64')) {
    $ExplorerOutput = Join-Path $StagingRoot 'explorer'
    New-Item -ItemType Directory -Path $ExplorerOutput -Force | Out-Null
    if (!$ExplorerSdkBin) { $ExplorerSdkBin = & (Join-Path $PSScriptRoot 'src/NxFileViewer.Explorer/Get-SdkBin.ps1') }
    $ownCertificate = $null
    if (!$UnsignedExplorerPackages -and !$ExplorerCertificateThumbprint) {
        $ownCertificate = & (Join-Path $PSScriptRoot 'src/NxFileViewer.Explorer/Get-SigningCertificate.ps1') -Publisher $ExplorerPublisher
        $ExplorerCertificateThumbprint = $ownCertificate.Thumbprint
        $ExplorerPublisher = $ownCertificate.Subject
        Write-Host "Using local signing certificate $ExplorerCertificateThumbprint (created: $($ownCertificate.Created))"
        $publicCertificate = Join-Path $ExplorerOutput 'NxFileViewerFork.Explorer.cer'
        [IO.File]::WriteAllBytes($publicCertificate, [Convert]::FromBase64String($ownCertificate.PublicCertificate))
        $trustScript = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'src/NxFileViewer.Explorer/Trust-ExplorerCertificate.ps1') -Raw
        $trustScript = $trustScript.Replace('@CERTIFICATE_THUMBPRINT@', $ExplorerCertificateThumbprint)
        [IO.File]::WriteAllText((Join-Path $ExplorerOutput 'Trust-ExplorerCertificate.ps1'), $trustScript, [Text.UTF8Encoding]::new($true))
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'src/NxFileViewer.Explorer/Trust-ExplorerCertificate.cmd') -Destination $ExplorerOutput -Force
        $instructions = @"
NxFileViewer Fork - modern Explorer menu

1. If you downloaded the setup ZIP, extract it into a folder.
2. Run Trust-ExplorerCertificate.cmd. Approve the Windows administrator prompt.
3. Check the publisher and thumbprint, then type YES to trust the public certificate.
4. Open NxFileViewer Fork normally. In Settings > Program, add the modern menu and select the MSIX from this folder.

This trust step is needed once per PC while the same certificate is used. Later updates do not require importing it again.
Certificate: $ExplorerPublisher
Thumbprint: $ExplorerCertificateThumbprint
Valid until: $($ownCertificate.NotAfter)
Only the public certificate is included. The private signing key is never distributed.
The helper trusts this certificate in Local Machine > Trusted People, not Trusted Root Certification Authorities.
You can remove the menu in NxFileViewer Fork. If you no longer want to trust this publisher, remove this exact certificate from Trusted People using certlm.msc.

Deutsch:
Falls als ZIP vorliegend, zuerst entpacken. Trust-ExplorerCertificate.cmd starten, die Windows-Adminabfrage bestaetigen und im angezeigten Fenster YES eingeben.
Danach NxFileViewer Fork normal starten und unter Einstellungen > Programm das moderne Menue mit der beiliegenden MSIX hinzufuegen.
"@
        [IO.File]::WriteAllText((Join-Path $ExplorerOutput 'Explorer-install.txt'), $instructions, [Text.UTF8Encoding]::new($true))
    }
    $packageOptions = @{
        Publisher = $ExplorerPublisher; Version = $ExplorerPackageVersion
        SdkBin = $ExplorerSdkBin; OutputDirectory = $ExplorerOutput
        TimestampServer = $ExplorerTimestampServer
    }
    if ($ExplorerCertificateThumbprint) { $packageOptions.CertificateThumbprint = $ExplorerCertificateThumbprint }
    else {
        $packageOptions.Unsigned = $true
        Write-Warning 'Explorer MSIX packages are unsigned because -UnsignedExplorerPackages was specified.'
    }
    foreach ($shellArchitecture in $Architectures) {
        Write-Host "Packaging Explorer menu ($shellArchitecture)"
        & (Join-Path $PSScriptRoot 'src/NxFileViewer.Explorer/Build-Package.ps1') @packageOptions -Architecture $shellArchitecture
        if ($ownCertificate) {
            $setupFiles = @(
                (Join-Path $ExplorerOutput "NxFileViewerFork.Explorer_${ExplorerPackageVersion}_${shellArchitecture}.msix"),
                $publicCertificate,
                (Join-Path $ExplorerOutput 'Trust-ExplorerCertificate.ps1'),
                (Join-Path $ExplorerOutput 'Trust-ExplorerCertificate.cmd'),
                (Join-Path $ExplorerOutput 'Explorer-install.txt')
            )
        } else {
            $suffix = if ($UnsignedExplorerPackages) { '.unsigned' } else { '' }
            $setupFiles = @(Join-Path $ExplorerOutput "NxFileViewerFork.Explorer_${ExplorerPackageVersion}_${shellArchitecture}${suffix}.msix")
        }
        Write-PublishExtraPackage "NxFileViewerFork.Exploder_${ExplorerPackageVersion}_win-$shellArchitecture" $setupFiles $ExplorerOutputFormat
    }
}

function Write-ReleaseDescriptor($ApplicationReleasePackages, [switch]$IncludeExplorer, [switch]$RequireExplorerPackages, $AvaloniaPackages=@()) {
    $ReleaseDescriptor = [ordered]@{ SchemaVersion=1; ApplicationId='NxFileViewerFork'; Version=$AppVersion; Prerelease=$AppVersion.Contains('-'); Packages=@($ApplicationReleasePackages | Where-Object Architecture -ne 'arm64'); Arm64Packages=@($ApplicationReleasePackages | Where-Object Architecture -eq 'arm64') }
    $ExplorerReleasePackages = @()
    if ($IncludeExplorer) {
        foreach ($architecture in @('x64','arm64')) {
            $assetName = "NxFileViewerFork.Exploder_${ExplorerPackageVersion}_win-$architecture.zip"
            $zip = Join-Path $OutDirRoot $assetName
            if (!(Test-Path -LiteralPath $zip -PathType Leaf)) {
                if ($RequireExplorerPackages) { throw "Explorer ZIP missing after packaging: $assetName" }
                continue
            }
            $ExplorerReleasePackages += [ordered]@{ Architecture=$architecture; SelfContained=$false; AssetName=$assetName; Sha256=(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash }
        }
    }
    $ReleaseDescriptor.ExplorerPackages = $ExplorerReleasePackages
    $ReleaseDescriptor.AvaloniaPackages = @($AvaloniaPackages)
    [IO.File]::WriteAllText((Join-Path $OutDirRoot 'nxfileviewer-update.json'), ($ReleaseDescriptor | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))

    $checksums = @{}
    foreach ($package in @($ApplicationReleasePackages) + @($ExplorerReleasePackages) + @($AvaloniaPackages)) {
        $checksums[$package.AssetName] = $package.Sha256
    }
    $entries = @(Get-ChildItem -LiteralPath $OutDirRoot -Filter '*.zip' -File | Sort-Object Name | ForEach-Object {
        $framework = if ($_.Name -like '*_Avalonia_*' -or $_.Name -match '^NxFileViewerFork(?:\.LogViewer)?_v[^_]+_(?:win|linux|osx)-') { 'Avalonia' } else { 'WPF' }
        $hash = if ($checksums.ContainsKey($_.Name)) { $checksums[$_.Name] } else { (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
        [ordered]@{ AssetName=$_.Name; Framework=$framework; Sha256=$hash }
    })
    $index = [ordered]@{ SchemaVersion=1; Packages=$entries }
    [IO.File]::WriteAllText((Join-Path $OutDirRoot 'nxfileviewer-packages.json'), ($index | ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($false))
}

function Get-ExistingApplicationReleasePackages([switch]$Migration) {
    foreach ($architecture in @('x64','x86','arm64')) {
        foreach ($withRuntime in @($false,$true)) {
            $variant = if ($withRuntime) { '_self-contained' } else { '' }
            $runtime = if ($withRuntime) { 'with-net' } else { 'without-net' }
            $assetName = if ($Migration) { "${AppName}_v${AppVersion}${variant}_${architecture}.zip" } else { "${AppName}.WPF_v${AppVersion}_win-$architecture-$runtime.zip" }
            $zip = Join-Path $OutDirRoot $assetName
            if (Test-Path -LiteralPath $zip -PathType Leaf) {
                [pscustomobject]@{ Architecture=$architecture; SelfContained=$withRuntime; AssetName=$assetName; Sha256=(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash }
            }
        }
    }
}

function Publish-WpfMigrationPackages {
    foreach ($architecture in @('x64','x86','arm64')) {
        foreach ($withRuntime in @($false,$true)) {
            $runtime = if ($withRuntime) { 'with-net' } else { 'without-net' }
            $legacyVariant = if ($withRuntime) { '_self-contained' } else { '' }
            $package = Join-Path $OutDirRoot "NxFileViewerFork_v${AppVersion}_win-$architecture-$runtime.zip"
            if (!(Test-Path -LiteralPath $package -PathType Leaf)) { throw "Migration package missing: $package" }
            $legacyName = "${AppName}_v${AppVersion}${legacyVariant}_$architecture.zip"
            $legacyPath = Join-Path $OutDirRoot $legacyName
            if ((Test-Path -LiteralPath $legacyPath) -and
                (Get-FileHash -LiteralPath $package).Hash -ne (Get-FileHash -LiteralPath $legacyPath).Hash) {
                throw "Existing package conflicts with the migration alias: $legacyName"
            }
            Copy-Item -LiteralPath $package -Destination $legacyPath -Force
        }
    }
}

# Resolve the SDK using src/global.json for every dotnet invocation.
Push-Location -LiteralPath (Join-Path $PSScriptRoot 'src')
try {
    if ($Extras.Count) {
        . (Join-Path $PSScriptRoot 'tools/Publish-Configuration.ps1')
        $migration = 'WpfMigration' -in $Extras
        $writeUpdateManifest = $migration -or 'UpdateManifest' -in $Extras -or 'Updater' -in $Extras
        $explorerArchitectures = @($ExtraWindowsArchitectures | Where-Object { $_ -in @('x64','arm64') } | Select-Object -Unique)
        $transitionArchitectures = @($ExtraWindowsArchitectures | Where-Object { $_ -in @('x64','x86') } | Select-Object -Unique)
        if ('Explorer' -in $Extras) {
            if (!$explorerArchitectures.Count) { Write-Warning 'Explorer requires a Windows x64 or ARM64 target.' }
            elseif ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) { throw 'Explorer packaging requires Windows.' }
            else { Publish-ExplorerPackages $explorerArchitectures }
        }
        if ('Transition' -in $Extras) {
            if (!$transitionArchitectures.Count) { Write-Warning 'Transition requires a Windows x64 or x86 target.' }
            $transitionVersion = if ($Version) { $AppVersion } else {
                (Select-Xml -LiteralPath (Join-Path $PSScriptRoot 'src/NxFileViewer.Transition/NxFileViewer.Transition.csproj') -XPath '/Project/PropertyGroup/Version').Node.InnerText
            }
            if ($transitionVersion -notmatch '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?$') { throw "Invalid Transition version: $transitionVersion" }
            foreach ($architecture in $transitionArchitectures) {
                foreach ($withRuntime in @($false,$true)) { Publish-TransitionPackage $architecture $withRuntime $transitionVersion }
            }
        }
        if ($migration) { Publish-WpfMigrationPackages }
        $writeAvaloniaZipMetadata = 'AvaloniaZipMetadata' -in $Extras
        if ($writeUpdateManifest -or $writeAvaloniaZipMetadata) {
            $found = $false
            $AvaloniaReleasePackages = @()
            if ($writeUpdateManifest -and ($migration -or 'WPF' -in $UpdateFrameworks)) {
                $ApplicationReleasePackages = @(Get-ExistingApplicationReleasePackages -Migration:$migration)
            }
            if ($writeAvaloniaZipMetadata -or 'Avalonia' -in $UpdateFrameworks) {
                . (Join-Path $PSScriptRoot 'tools/Publish-Output.ps1')
                foreach ($avaloniaName in @('NxFileViewerFork','NxFileViewerFork.LogViewer')) {
                    $appVersion = if ($AvaloniaUpdateVersions.ContainsKey($avaloniaName)) { $AvaloniaUpdateVersions[$avaloniaName] } elseif ($Version) { $AppVersion } else {
                        $project = if ($avaloniaName -eq 'NxFileViewerFork') { 'NxFileViewer.Avalonia' } else { 'NxFileViewer.LogViewer.Avalonia' }
                        (Select-Xml -LiteralPath (Join-Path $PSScriptRoot (Get-ViewerProjectPath $project)) -XPath '/Project/PropertyGroup/Version').Node.InnerText
                    }
                    if ($appVersion -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?$') { throw "Invalid Avalonia version: $appVersion" }
                    $pattern = '^' + [regex]::Escape("${avaloniaName}_v${appVersion}") + '_(?:win|linux|osx)-(?:x64|x86|arm64)-(?:with|without)-net\.zip$'
                    foreach ($zip in Get-ChildItem -LiteralPath $OutDirRoot -File -Filter '*.zip' | Where-Object Name -match $pattern) {
                        $marker = if ($writeAvaloniaZipMetadata) {
                            Write-Host "Update metadata: $($zip.FullName).avalonia.json"
                            Write-AvaloniaUpdateChecksum $zip.FullName -PassThru
                        } else {
                            Get-AvaloniaUpdateChecksum $zip.FullName
                        }
                        if ($writeUpdateManifest -and 'Avalonia' -in $UpdateFrameworks) { $AvaloniaReleasePackages += $marker }
                        $found = $true
                    }
                }
            }
            if ($writeUpdateManifest -and ($ApplicationReleasePackages.Count -or $AvaloniaReleasePackages.Count)) {
                Write-ReleaseDescriptor $ApplicationReleasePackages -IncludeExplorer -AvaloniaPackages $AvaloniaReleasePackages
                Write-Host "Update manifest: $(Join-Path $OutDirRoot 'nxfileviewer-update.json')"
                $found = $true
            }
            if (!$found) { Write-Warning "No matching application ZIPs found in $OutDirRoot. Build the ZIPs before generating update metadata." }
        }
        return
    }
    if ($SelectedExeVariants) {
        foreach ($selection in $SelectedExeVariants) {
            $architecture = if ($selection -match 'Arm64') { 'arm64' } elseif ($selection -match 'X64') { 'x64' } else { 'x86' }
            $withRuntime = $selection.EndsWith('WithRuntime')
            $suffix = if ($withRuntime) { '_self-contained' } else { '' }
            if ($selection.StartsWith('LogViewer')) {
                $selectedProject = Join-Path $PSScriptRoot 'src/NxFileViewer.LogViewer.WPF/NxFileViewer.LogViewer.WPF.csproj'
                $builtExe = 'NxFileViewerFork.LogViewer.exe'
                $targetExe = $builtExe
                $releaseName = "NxFileViewerFork.LogViewer_v$($AppVersion)$($suffix)_$architecture"
            } elseif ($selection.StartsWith('Transition')) {
                $selectedProject = Join-Path $PSScriptRoot 'src/NxFileViewer.Transition/NxFileViewer.Transition.csproj'
                $builtExe = 'NxFileViewer.exe'
                $targetExe = $builtExe
                $releaseName = "NxFileViewer_v$($AppVersion)$($suffix)_$architecture"
            } else {
                $selectedProject = $ProjectPath
                $builtExe = $PublishedExecutableName
                $targetExe = $ExecutableName
                $releaseName = "$($AppName)_v$($AppVersion)$($suffix)_$architecture"
            }
            $selectedStaging = Join-Path $StagingRoot $selection
            Write-Host "Publishing $selection"
            dotnet publish $selectedProject --artifacts-path (Join-Path $StagingRoot "artifacts") -p:Version=$AppVersion -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=$withRuntime -c Release --self-contained $withRuntime -r "win-$architecture" -o $selectedStaging
            if ($LASTEXITCODE -ne 0) { throw "Publishing $selection failed ($LASTEXITCODE)." }
            $exe = Join-Path $selectedStaging $builtExe
            if (!(Test-Path -LiteralPath $exe -PathType Leaf)) { throw "Published executable missing: $builtExe" }
            $selectedOutput = Join-Path $OutDirRoot $releaseName
            New-Item -ItemType Directory -Path $selectedOutput -Force | Out-Null
            Copy-Item -LiteralPath $exe -Destination (Join-Path $selectedOutput $targetExe) -Force
            Write-Host ("EXE: " + (Join-Path $selectedOutput $targetExe))
        }
        return
    }
    if (!$SkipLogViewer) {
        $LogViewerProject = Join-Path $PSScriptRoot 'src/NxFileViewer.LogViewer.WPF/NxFileViewer.LogViewer.WPF.csproj'
        foreach ($ViewerArchitecture in @('x64', 'x86', 'arm64')) {
            foreach ($ViewerWithRuntime in @($false, $true)) {
                $viewerRuntime = if ($ViewerWithRuntime) { 'with-net' } else { 'without-net' }
                $ViewerReleaseName = "NxFileViewerFork.WPF.LogViewer_v${AppVersion}_win-$ViewerArchitecture-$viewerRuntime"
                $ViewerReleaseDir = Join-Path $StagingRoot $ViewerReleaseName
                Write-Host "Publishing $ViewerReleaseName"
                dotnet publish $LogViewerProject --artifacts-path (Join-Path $StagingRoot "artifacts") -p:Version=$AppVersion -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=$ViewerWithRuntime -c Release --self-contained $ViewerWithRuntime -r "win-$ViewerArchitecture" -o $ViewerReleaseDir
                if ($LASTEXITCODE -ne 0) { throw "LogViewer publishing failed ($LASTEXITCODE)." }
                if (!(Test-Path -LiteralPath (Join-Path $ViewerReleaseDir 'NxFileViewerFork.LogViewer.exe'))) { throw 'Published LogViewer executable missing.' }
                Compress-Archive -LiteralPath @(Get-ChildItem -LiteralPath $ViewerReleaseDir -Force | ForEach-Object FullName) -DestinationPath (Join-Path $OutDirRoot "$ViewerReleaseName.zip") -Force
            }
        }
    }
    if ($LogViewerOnly) { return }
    foreach ($Architecture in @("x64", "x86", "arm64")) {
        foreach ($WithRuntime in @($false, $true)) {
            $runtime = if ($WithRuntime) { 'with-net' } else { 'without-net' }
            $ReleaseName = "${AppName}.WPF_v${AppVersion}_win-$Architecture-$runtime"
            $ReleaseDir = Join-Path $StagingRoot $ReleaseName
            Write-Host "Publishing $ReleaseName"
            dotnet publish $ProjectPath --artifacts-path (Join-Path $StagingRoot "artifacts") -p:Version=$AppVersion -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=$WithRuntime -c Release --self-contained $WithRuntime -r "win-$Architecture" -o $ReleaseDir
            if ($LASTEXITCODE -ne 0) { throw "Publishing $Architecture failed ($LASTEXITCODE)." }
            if (Test-Path -LiteralPath (Join-Path $ReleaseDir "Plugins/fwhashes")) {
                throw "Firmware hashes must not be included in application archives."
            }
            if ($ExecutableName -cne $PublishedExecutableName) {
                Move-Item -LiteralPath (Join-Path $ReleaseDir $PublishedExecutableName) -Destination (Join-Path $ReleaseDir $ExecutableName)
            }
            if (!(Test-Path -LiteralPath (Join-Path $ReleaseDir $ExecutableName))) { throw 'Published main executable missing.' }
            $identity = $PackageIdentityJson
            [IO.File]::WriteAllText((Join-Path $ReleaseDir 'nxfileviewer-package.json'), $identity, [Text.UTF8Encoding]::new($false))
            $applicationFiles = @($ExecutableName, 'nxfileviewer-package.json', 'LICENSE.txt')
            $publishedFiles = @(Get-ChildItem -LiteralPath $ReleaseDir -Force)
            if ($publishedFiles.Count -ne $applicationFiles.Count -or
                @($publishedFiles | Where-Object { $_.PSIsContainer -or $_.Name -cnotin $applicationFiles }).Count -gt 0) {
                throw 'Application package must contain only the main executable, nxfileviewer-package.json and LICENSE.txt.'
            }
            Compress-Archive -LiteralPath @($applicationFiles | ForEach-Object { Join-Path $ReleaseDir $_ }) -DestinationPath (Join-Path $OutDirRoot "$ReleaseName.zip") -Force
            $ApplicationReleasePackages += [pscustomobject]@{ Architecture=$Architecture; SelfContained=[bool]$WithRuntime; AssetName="$ReleaseName.zip"; Sha256=(Get-FileHash -LiteralPath (Join-Path $OutDirRoot "$ReleaseName.zip") -Algorithm SHA256).Hash }
            if ($IncludeLegacyTransition -and $Architecture -ne 'arm64') {
                Publish-TransitionPackage $Architecture $WithRuntime
            }
        }
    }


    # The add-on extracts to Plugins/fwhashes next to either architecture's executable.
    $HashRoot = Join-Path $StagingRoot "firmware-hashes"
    $HashDirectory = Join-Path $HashRoot "Plugins/fwhashes"
    New-Item -ItemType Directory -Path $HashDirectory -Force | Out-Null
    $HashFiles = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot "fw/hashes") -Filter "*.json" -File)
    if ($HashFiles.Count -eq 0) { throw "No firmware hash references found." }
    foreach ($HashFile in $HashFiles) {
        Copy-Item -LiteralPath $HashFile.FullName -Destination $HashDirectory
    }
    Compress-Archive -LiteralPath (Join-Path $HashRoot "Plugins") -DestinationPath (Join-Path $OutDirRoot "${AppName}_v${AppVersion}_firmware-hashes.zip") -Force

    if (!$SkipExplorerPackages) { Publish-ExplorerPackages }
    Write-ReleaseDescriptor $ApplicationReleasePackages -IncludeExplorer:(!$SkipExplorerPackages) -RequireExplorerPackages:(!$SkipExplorerPackages)
}
finally {
    Pop-Location
    $ResolvedStaging = [IO.Path]::GetFullPath($StagingRoot)
    if ($ResolvedStaging.StartsWith($OutDirRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -and
        [IO.Path]::GetFileName($ResolvedStaging).StartsWith(".publish-")) {
        Remove-Item -LiteralPath $ResolvedStaging -Recurse -Force
    }
}

# Individual WPF EXE switches and -LogViewerOnly return above. The full build
# also includes Avalonia; the tag-release workflow passes -SkipAvalonia because
# its native OS jobs already build those packages separately for beta tags.
if (!$SkipAvalonia) {
    Publish-AvaloniaSelection
    $manifestOptions = @{ OutputDirectory=$OutDirRoot; Extras=@('UpdateManifest'); ExplorerPackageVersion=$ExplorerPackageVersion }
    if ($Version) { $manifestOptions.Version = $Version }
    & $PSCommandPath @manifestOptions
}
