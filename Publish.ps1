<#
    Builds application and standalone LogViewer ZIPs, firmware hashes, and optional Explorer MSIX packages (x64/ARM64).
    By default Explorer MSIX packages use a persistent local signing certificate.
#>
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
    [switch]$LogViewerOnly,
    [switch]$SkipLogViewer,
    [switch]$IncludeLegacyTransition = $true
)

$ErrorActionPreference = "Stop"
if ($LogViewerOnly -and $SkipLogViewer) { throw 'Choose either -LogViewerOnly or -SkipLogViewer.' }
if ($UnsignedExplorerPackages -and $ExplorerCertificateThumbprint) { throw 'Choose either unsigned packages or a signing certificate.' }
if ($PackageName -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]{0,99}$' -or $PackageName.EndsWith('.')) { throw 'Supply a plain release package name.' }
$AppName = $PackageName
$ProjectPath = Join-Path $PSScriptRoot "src/NxFileViewer/NxFileViewer.csproj"
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
$ManifestTemplatePath = Join-Path $PSScriptRoot 'update/package-manifest.json'
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
$ReleasePackages = @()
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
$OutDirRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot $OutputDirectory))
New-Item -ItemType Directory -Path $OutDirRoot -Force | Out-Null
# Isolated staging leaves existing extracted releases and user keys untouched.
$StagingRoot = Join-Path $OutDirRoot (".publish-" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $StagingRoot | Out-Null
try {
    if (!$SkipLogViewer) {
        $LogViewerProject = Join-Path $PSScriptRoot 'src/NxFileViewer.LogViewer/NxFileViewer.LogViewer.csproj'
        foreach ($ViewerArchitecture in @('x64', 'x86')) {
            foreach ($ViewerWithRuntime in @($false, $true)) {
                $ViewerVariant = if ($ViewerWithRuntime) { '_self-contained' } else { '' }
                $ViewerReleaseName = "NxFileViewerFork.LogViewer_v${AppVersion}${ViewerVariant}_${ViewerArchitecture}"
                $ViewerReleaseDir = Join-Path $StagingRoot $ViewerReleaseName
                Write-Host "Publishing $ViewerReleaseName"
                dotnet publish $LogViewerProject -p:Version=$AppVersion -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=$ViewerWithRuntime -c Release --self-contained $ViewerWithRuntime -r "win-$ViewerArchitecture" -o $ViewerReleaseDir
                if ($LASTEXITCODE -ne 0) { throw "LogViewer publishing failed ($LASTEXITCODE)." }
                if (!(Test-Path -LiteralPath (Join-Path $ViewerReleaseDir 'NxFileViewerFork.LogViewer.exe'))) { throw 'Published LogViewer executable missing.' }
                Compress-Archive -LiteralPath @(Get-ChildItem -LiteralPath $ViewerReleaseDir -Force | ForEach-Object FullName) -DestinationPath (Join-Path $OutDirRoot "$ViewerReleaseName.zip") -Force
            }
        }
    }
    if ($LogViewerOnly) { return }
    foreach ($Architecture in @("x64", "x86")) {
        foreach ($WithRuntime in @($false, $true)) {
            $Variant = if ($WithRuntime) { "_self-contained" } else { "" }
            $ReleaseName = "${AppName}_v${AppVersion}${Variant}_${Architecture}"
            $ReleaseDir = Join-Path $StagingRoot $ReleaseName
            Write-Host "Publishing $ReleaseName"
            dotnet publish $ProjectPath -p:Version=$AppVersion -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=$WithRuntime -c Release --self-contained $WithRuntime -r "win-$Architecture" -o $ReleaseDir
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
            $ReleasePackages += [pscustomobject]@{ Architecture=$Architecture; SelfContained=[bool]$WithRuntime; AssetName="$ReleaseName.zip"; Sha256=(Get-FileHash -LiteralPath (Join-Path $OutDirRoot "$ReleaseName.zip") -Algorithm SHA256).Hash }
            if ($IncludeLegacyTransition) {
                $transitionProject = Join-Path $PSScriptRoot 'src/NxFileViewer.Transition/NxFileViewer.Transition.csproj'
                $transitionDir = Join-Path $StagingRoot ("transition${Variant}_${Architecture}")
                dotnet publish $transitionProject -p:Version=$AppVersion -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=$WithRuntime -c Release --self-contained $WithRuntime -r "win-$Architecture" -o $transitionDir
                if ($LASTEXITCODE -ne 0) { throw 'Transition publishing failed.' }
                $transitionFiles = @(Get-ChildItem -LiteralPath $transitionDir -File)
                if ($transitionFiles.Count -ne 1 -or $transitionFiles[0].Name -ne 'NxFileViewer.exe') { throw 'Legacy transition must contain exactly one NxFileViewer.exe.' }
                Compress-Archive -LiteralPath $transitionFiles[0].FullName -DestinationPath (Join-Path $OutDirRoot "NxFileViewer_v${AppVersion}${Variant}_${Architecture}.zip") -Force
            }
        }
    }

    $ReleaseDescriptor = [ordered]@{ SchemaVersion=1; ApplicationId='NxFileViewerFork'; Version=$AppVersion; Prerelease=$AppVersion.Contains('-'); Packages=$ReleasePackages }
    [IO.File]::WriteAllText((Join-Path $OutDirRoot 'nxfileviewer-update.json'), ($ReleaseDescriptor | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
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

    if (!$SkipExplorerPackages) {
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

1. Extract the setup ZIP into a folder.
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
ZIP entpacken, Trust-ExplorerCertificate.cmd starten, die Windows-Adminabfrage bestaetigen und im angezeigten Fenster YES eingeben.
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
        foreach ($shellArchitecture in @('x64', 'arm64')) {
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
                Compress-Archive -LiteralPath $setupFiles -DestinationPath (Join-Path $OutDirRoot "NxFileViewerFork.Explorer_${ExplorerPackageVersion}_${shellArchitecture}_setup.zip") -Force
            } else {
                $suffix = if ($UnsignedExplorerPackages) { '.unsigned' } else { '' }
                $package = Join-Path $ExplorerOutput "NxFileViewerFork.Explorer_${ExplorerPackageVersion}_${shellArchitecture}${suffix}.msix"
                Compress-Archive -LiteralPath $package -DestinationPath (Join-Path $OutDirRoot "NxFileViewerFork.Explorer_${ExplorerPackageVersion}_${shellArchitecture}_setup.zip") -Force
            }
        }
    }
}
finally {
    $ResolvedStaging = [IO.Path]::GetFullPath($StagingRoot)
    if ($ResolvedStaging.StartsWith($OutDirRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -and
        [IO.Path]::GetFileName($ResolvedStaging).StartsWith(".publish-")) {
        Remove-Item -LiteralPath $ResolvedStaging -Recurse -Force
    }
}
