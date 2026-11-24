# Internal common publisher used by Publish.ps1 and native release jobs.
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][ValidateSet('WPF','Avalonia')][string]$Framework,
    [string]$OutputDirectory='Publish', [string]$Version,
    [ValidateSet('win-x64','win-x86','win-arm64','linux-x64','linux-arm64','osx-x64','osx-arm64')][string[]]$RuntimeIdentifiers=@(),
    [ValidateSet('Viewer','LogViewer')][string[]]$Applications=@('Viewer','LogViewer'),
    [bool[]]$RuntimeVariants=@($false,$true),
    [switch]$ExecutableOnly, [string]$CopyTo, [string]$CopyRuntime='win-x64',
    [Nullable[bool]]$CopyRuntimeIncludesDotNet=$null,
    [switch]$SkipFirmwareHashes, [switch]$AllTargets, [switch]$NoCopy,
    [ValidateSet('AppImage','Dmg')][string]$NativePackage,
    [switch]$SkipAvaloniaZipMetadata,
    [string[]]$FormatTargets=@(),
    $TargetOutputs,
    [hashtable]$ApplicationVersions=@{}
)
$ErrorActionPreference='Stop'
$RepositoryRoot=Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'Publish-Configuration.ps1')
$Framework=if($Framework -ieq 'WPF'){'WPF'}else{'Avalonia'}
$CreateZipArchives=!$ExecutableOnly
$CopyExecutableToTestDirectory=([bool]$CopyTo -and !$NoCopy)
$TestDirectory=$CopyTo
$CopyRuntimeIdentifier=$CopyRuntime
$CopyWithDotNet=if($null -eq $CopyRuntimeIncludesDotNet){$false}else{[bool]$CopyRuntimeIncludesDotNet}
$applicationNames = Get-PublishApplicationNames
if($Framework -eq 'WPF'){
    $projects=@(@{Project='NxFileViewer';Name='NxFileViewerFork';Kind='Viewer'},@{Project='NxFileViewer.LogViewer';Name='NxFileViewerFork.LogViewer';Kind='LogViewer'})
    $defaultRids=@('win-x64','win-x86','win-arm64')
}else{
    $projects=@(@{Project='NxFileViewer.Avalonia';Name='NxFileViewerFork';Kind='Viewer'},@{Project='NxFileViewer.LogViewer.Avalonia';Name='NxFileViewerFork.LogViewer';Kind='LogViewer'})
    $defaultRids=@('win-x64','win-x86','win-arm64','linux-x64','linux-arm64','osx-x64','osx-arm64')
}
$projects=@($projects | Where-Object {$_.Kind -in $Applications})
$nativeFormatTargets = @($FormatTargets | Where-Object { $_ -match '-(appimage|dmg)$' })
if ($nativeFormatTargets.Count) {
    $NativePackage = if ($IsLinux) { 'AppImage' } else { 'Dmg' }
}
if ($NativePackage) {
    if ($Framework -ne 'Avalonia') { throw 'Native packages require Avalonia publishing.' }
    if ($NativePackage -eq 'AppImage') {
        if (!$IsLinux) { throw 'AppImage packaging requires Linux.' }
        $nativeTool = (Get-Command appimagetool -CommandType Application -ErrorAction Stop | Select-Object -First 1).Source
        $nativeRids = @('linux-x64','linux-arm64')
    } else {
        if (!$IsMacOS) { throw 'DMG packaging requires macOS.' }
        $nativeTool = '/usr/bin/hdiutil'
        $nativeRids = @('osx-x64','osx-arm64')
    }
    if (!$RuntimeIdentifiers.Count) { $RuntimeIdentifiers = $nativeRids }
    $nativeRidsToBuild = if ($nativeFormatTargets.Count) { $nativeFormatTargets -replace '-(appimage|dmg)$' } else { $RuntimeIdentifiers }
    foreach ($rid in $nativeRidsToBuild) {
        if ($rid -notin $nativeRids) { throw "$NativePackage packaging does not support $rid." }
    }
}
if(!$RuntimeIdentifiers.Count){$RuntimeIdentifiers=$defaultRids}
foreach($rid in $RuntimeIdentifiers){if($rid -notin $defaultRids){throw "Unsupported $Framework target: $rid"}}

$targets = @(foreach ($rid in ($RuntimeIdentifiers | Select-Object -Unique)) {
    foreach ($variant in ($RuntimeVariants | Select-Object -Unique)) {
        $target = [pscustomobject]@{ Rid=$rid; WithRuntime=[bool]$variant; Binary='Off'; Native='Off' }
        if ($null -ne $TargetOutputs) {
            $variantName = if ($variant) { 'WithDotNet' } else { 'WithoutDotNet' }
            if ($rid -in $FormatTargets -and $TargetOutputs.$rid.$variantName) {
                $target.Binary = $TargetOutputs.$rid.$variantName
            }
            $nativeKey = "$rid-$NativePackage"
            if ($nativeKey -in $nativeFormatTargets -and $TargetOutputs.$nativeKey.$variantName) {
                $target.Native = $TargetOutputs.$nativeKey.$variantName
            }
            if ($target.Binary -eq 'Off' -and $target.Native -eq 'Off') { continue }
        }
        $target
    }
})
$CopyExecutables=@(foreach($app in $projects){
    $exe=if($app.Project -like '*LogViewer*'){'NxFileViewerFork.LogViewer'}else{'NxFileViewerFork'}
    $exe+=$(if($CopyRuntimeIdentifier -like 'win-*'){'.exe'}else{''})
    @{Project=$app.Project;Name=$exe}
})
. (Join-Path $PSScriptRoot 'Publish-Output.ps1')
$sizes=[Collections.Generic.List[object]]::new()
$copies=[Collections.Generic.List[object]]::new()
if (!$projects.Count -or !$targets.Count) { throw 'Select at least one application, architecture and .NET variant in the selected configuration.' }
if ($CopyExecutableToTestDirectory) {
    if ([string]::IsNullOrWhiteSpace($TestDirectory)) { throw 'Configure TestDirectory or pass -CopyTo.' }
    if (!@($targets | Where-Object { $_.Rid -eq $CopyRuntimeIdentifier -and $_.WithRuntime -eq $CopyWithDotNet }).Count) { throw 'The copy architecture/.NET variant must be selected for building.' }
    $copyNames = @($CopyExecutables | Where-Object { $_.Project -in $projects.Project } | ForEach-Object {
        if (![string]::IsNullOrEmpty($_.Name)) { $_.Name }
        else {
            $copyProject = Join-Path $RepositoryRoot (Get-ViewerProjectPath $_.Project)
            $copyAssembly = (Select-Xml -LiteralPath $copyProject -XPath '/Project/PropertyGroup/AssemblyName').Node.InnerText
            if (!$copyAssembly) { $copyAssembly = $_.Project }
            $copyAssembly + $(if ($CopyRuntimeIdentifier.StartsWith('win-')) { '.exe' } else { '' })
        }
    })
    if (@($copyNames | Select-Object -Unique).Count -ne $copyNames.Count) { throw 'Copy destination names must be distinct.' }
}
& (Join-Path $RepositoryRoot 'src/config/Validate-ExternalSources.ps1')
$useProjectVersion = !$Version
if (!$Version) {
    $viewerProject = if ($Framework -eq 'Avalonia') { 'NxFileViewer.Avalonia' } else { 'NxFileViewer' }
    $viewer = Join-Path $RepositoryRoot (Get-ViewerProjectPath $viewerProject)
    $Version = (Select-Xml -LiteralPath $viewer -XPath '/Project/PropertyGroup/Version').Node.InnerText
}
if ($Version -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?$') {
    throw 'Supply a version such as 4.2.0 or 4.2.0-beta1.'
}

$outputPath = if ([IO.Path]::IsPathRooted($OutputDirectory)) { $OutputDirectory } else { Join-Path $RepositoryRoot $OutputDirectory }
$output = (New-Item -ItemType Directory -Path $outputPath -Force).FullName
function Write-PackageZip([string]$directory, [string]$zip) {
    if ([Environment]::OSVersion.Platform -eq [PlatformID]::Win32NT) {
        Compress-Archive -LiteralPath @(Get-ChildItem -LiteralPath $directory | ForEach-Object FullName) -DestinationPath $zip -Force
    } else {
        if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
        # Native zip preserves executable permissions for Linux and macOS apphosts.
        Push-Location -LiteralPath $directory
        try {
            & zip -qr $zip .
            if ($LASTEXITCODE -ne 0) { throw "Packaging $zip failed ($LASTEXITCODE)." }
        } finally { Pop-Location }
    }
}
function Set-ZipUnixCreator([string]$zip,[string[]]$executableNames) {
    # Compress-Archive on Windows sets the ZIP creator to DOS. Unix readers can
    # ignore the high permission bits unless these entries identify a Unix host.
    $stream = [IO.File]::Open($zip,[IO.FileMode]::Open,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
    $reader = [IO.BinaryReader]::new($stream,[Text.Encoding]::UTF8,$true)
    try {
        $stream.Position = $stream.Length - 22
        $end = $reader.ReadBytes(22)
        if ([BitConverter]::ToUInt32($end,0) -ne 0x06054b50 -or [BitConverter]::ToUInt16($end,20) -ne 0) { throw 'Unexpected ZIP end record.' }
        $count = [BitConverter]::ToUInt16($end,10)
        [long]$position = [BitConverter]::ToUInt32($end,16)
        for($index=0;$index -lt $count;$index++) {
            $stream.Position = $position
            $header = $reader.ReadBytes(46)
            if($header.Length -ne 46 -or [BitConverter]::ToUInt32($header,0) -ne 0x02014b50){throw 'Invalid ZIP central directory.'}
            $nameLength=[BitConverter]::ToUInt16($header,28)
            $extraLength=[BitConverter]::ToUInt16($header,30)
            $commentLength=[BitConverter]::ToUInt16($header,32)
            $entryName=[Text.Encoding]::UTF8.GetString($reader.ReadBytes($nameLength))
            if($entryName -in $executableNames){$stream.Position=$position+5;$stream.WriteByte(3)}
            $position += 46 + $nameLength + $extraLength + $commentLength
        }
    }finally{$reader.Dispose();$stream.Dispose()}
}
$stage = (New-Item -ItemType Directory -Path (Join-Path $output ('.publish-applications-' + [guid]::NewGuid().ToString('N')))).FullName
Push-Location -LiteralPath (Join-Path $RepositoryRoot 'src')
try {
    $publishedVersions = [Collections.Generic.List[string]]::new()
    foreach ($app in $projects) {
        $project = Join-Path $RepositoryRoot (Get-ViewerProjectPath $app.Project)
        $appVersion = if ($ApplicationVersions.ContainsKey($app.Project)) { $ApplicationVersions[$app.Project] } elseif ($useProjectVersion) {
            (Select-Xml -LiteralPath $project -XPath '/Project/PropertyGroup/Version').Node.InnerText
        } else { $Version }
        if ($appVersion -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?$') { throw "Invalid version for $($app.Project): $appVersion" }
        if ($appVersion -notin $publishedVersions) { $publishedVersions.Add($appVersion) }
        $assemblyName = (Select-Xml -LiteralPath $project -XPath '/Project/PropertyGroup/AssemblyName' | Select-Object -Last 1).Node.InnerText
        if (!$assemblyName) { $assemblyName = $app.Project }
        foreach ($target in $targets) {
            $rid = $target.Rid
            $withRuntime = $target.WithRuntime
            $frameworkSuffix = if ($Framework -eq 'Avalonia') { '_Avalonia' } else { '' }
            $dotnetVariant = if ($withRuntime) { 'with-net' } else { 'without-net' }
            $prefix = $applicationNames[$Framework+$app.Kind]
            $name = "${prefix}_v${appVersion}_$rid-$dotnetVariant"
            $publish = Join-Path $stage $name
            # Isolate restore assets from IDE builds and other publish runs.
            dotnet publish $project --artifacts-path (Join-Path $stage "artifacts") -c Release -r $rid --self-contained $withRuntime "-p:Version=$appVersion" -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true "-p:EnableCompressionInSingleFile=$withRuntime" -o $publish
            if ($LASTEXITCODE -ne 0) { throw "Publishing $name failed ($LASTEXITCODE)." }
            if ($app.Project -eq 'NxFileViewer' -or ($app.Project -eq 'NxFileViewer.Avalonia' -and $rid.StartsWith('win-'))) {
                $identity = Get-Content -LiteralPath (Join-Path $RepositoryRoot 'src/config/package-manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
                if ($identity.SchemaVersion -ne 1 -or $identity.ApplicationId -cne 'NxFileViewerFork') { throw 'Invalid package manifest identity.' }
                $identity | Add-Member -NotePropertyName Version -NotePropertyValue $appVersion -Force
                $identity | Add-Member -NotePropertyName Executable -NotePropertyValue ($assemblyName + '.exe') -Force
                [IO.File]::WriteAllText((Join-Path $publish 'nxfileviewer-package.json'), ($identity | ConvertTo-Json -Depth 16), [Text.UTF8Encoding]::new($false))
            }
            $executableName = $assemblyName + $(if ($rid.StartsWith('win-')) { '.exe' } else { '' })
            $executable = Join-Path $publish $executableName
            if (!(Test-Path -LiteralPath $executable -PathType Leaf)) { throw "Published executable missing: $executable" }
            if ($Framework -eq 'Avalonia') {
                Get-ChildItem -LiteralPath $publish -File -Filter '*.pdb' |
                    ForEach-Object { Remove-Item -LiteralPath $_.FullName }
                $marker = [ordered]@{ SchemaVersion=1; Framework='Avalonia'; Version=$appVersion; RuntimeIdentifier=$rid; SelfContained=[bool]$withRuntime; Executable=$executableName }
                [IO.File]::WriteAllText((Join-Path $publish 'nxfileviewer-avalonia.json'), ($marker | ConvertTo-Json), [Text.UTF8Encoding]::new($false))
                if ($rid.StartsWith('linux-')) {
                    foreach ($scriptName in @('start.sh','install-shortcut.sh')) {
                        $template = [IO.File]::ReadAllText((Join-Path $PSScriptRoot "linux/$scriptName.in"))
                        $template = $template.Replace('@@EXECUTABLE@@',$executableName).Replace('@@NAME@@',$app.Name + ' Avalonia').Replace("`r`n","`n")
                        $scriptPath = Join-Path $publish $scriptName
                        [IO.File]::WriteAllText($scriptPath,$template,[Text.UTF8Encoding]::new($false))
                    }
                    if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) {
                        & chmod +x $executable (Join-Path $publish 'start.sh') (Join-Path $publish 'install-shortcut.sh')
                        if ($LASTEXITCODE -ne 0) { throw 'Could not set Linux executable permissions.' }
                    }
                }
            }
            $zip = $null
            $reportDirectory = $publish
            $defaultFormat = !$FormatTargets.Count -or $rid -in $FormatTargets
            $buildZip = $defaultFormat -and $CreateZipArchives
            $buildExecutable = ($defaultFormat -and !$CreateZipArchives) -or "$rid-exe" -in $FormatTargets
            $buildNative = $NativePackage -and (!$FormatTargets.Count -or "$rid-$NativePackage" -in $nativeFormatTargets)
            if ($null -ne $TargetOutputs) {
                $buildZip = $target.Binary -in @('Zip','Both')
                $buildExecutable = $target.Binary -in @('Folder','Both')
                $buildNative = $target.Native -ne 'Off'
            }
            if ($buildZip) {
                $zip = Join-Path $output "$name.zip"
                Write-PackageZip $publish $zip
                if ($Framework -eq 'Avalonia') {
                    if ($rid.StartsWith('linux-')) {
                        # Preserve executable bits even when the Linux ZIP is built on Windows.
                        Add-Type -AssemblyName System.IO.Compression.FileSystem
                        $archive = [IO.Compression.ZipFile]::Open($zip,[IO.Compression.ZipArchiveMode]::Update)
                        try { foreach ($entry in $archive.Entries) { if ($entry.FullName -in @($executableName,'start.sh','install-shortcut.sh')) { $entry.ExternalAttributes = -2115174400 } } }
                        finally { $archive.Dispose() }
                        Set-ZipUnixCreator $zip @($executableName,'start.sh','install-shortcut.sh')
                    }
                    if (!$SkipAvaloniaZipMetadata) { Write-AvaloniaUpdateChecksum $zip }
                }
                Write-Host "ZIP: $zip"
            }
            if ($buildExecutable) {
                $exeDirectory = Join-Path $output $name
                New-Item -ItemType Directory -Path $exeDirectory -Force | Out-Null
                $reportDirectory = $exeDirectory
                if ($null -eq $TargetOutputs) {
                    Copy-Item -LiteralPath $executable -Destination (Join-Path $exeDirectory $executableName) -Force
                    Write-Host "EXE: $(Join-Path $exeDirectory $executableName)"
                } else {
                    Get-ChildItem -LiteralPath $publish -Force | Copy-Item -Destination $exeDirectory -Recurse -Force
                    Write-Host "Folder: $exeDirectory"
                }
            }
            Add-PublishSizeRow $sizes $name $reportDirectory $executable $zip
            if ($buildNative) {
                $nativeName = if ($app.Kind -eq 'Viewer') { "v${appVersion}_$rid-$dotnetVariant" } else {
                    $legacyRuntime = if ($withRuntime) { '_self-contained' } else { '' }
                    "$($app.Name)_Avalonia_v${appVersion}${legacyRuntime}_$rid"
                }
                $nativeOutput = $output
                $nativeZipSelected = $CreateZipArchives -and $app.Project -eq 'NxFileViewer.Avalonia'
                if ($null -ne $TargetOutputs) {
                    $nativeName = "$name-$($NativePackage.ToLowerInvariant())"
                    $nativeZipSelected = $target.Native -in @('Zip','Both')
                    $nativeOutput = if ($target.Native -in @('Folder','Both')) { Join-Path $output $nativeName } else { Join-Path $stage 'native-output' }
                    New-Item -ItemType Directory -Path $nativeOutput -Force | Out-Null
                }
                $nativeAsset = & (Join-Path $PSScriptRoot 'Publish-NativePackage.ps1') -Format $NativePackage -Tool $nativeTool -RuntimeIdentifier $rid -Version $appVersion -AppName $app.Name -AssetName $nativeName -PublishDirectory $publish -ExecutableName $executableName -OutputDirectory $nativeOutput
                if ($null -eq $TargetOutputs -or $target.Native -in @('Folder','Both')) {
                    Write-Host "$NativePackage package: $nativeAsset"
                }
                if ($nativeZipSelected) {
                    $nativeZip = Join-Path $output "$name-$($NativePackage.ToLowerInvariant()).zip"
                    Compress-Archive -LiteralPath $nativeAsset -DestinationPath $nativeZip -Force
                    if ($NativePackage -eq 'AppImage') {
                        Add-Type -AssemblyName System.IO.Compression.FileSystem
                        $archive = [IO.Compression.ZipFile]::Open($nativeZip,[IO.Compression.ZipArchiveMode]::Update)
                        try { $archive.Entries[0].ExternalAttributes = -2115174400 }
                        finally { $archive.Dispose() }
                        Set-ZipUnixCreator $nativeZip @([IO.Path]::GetFileName($nativeAsset))
                    }
                    Write-Host "ZIP: $nativeZip"
                }
            }
            if ($CopyExecutableToTestDirectory -and $rid -eq $CopyRuntimeIdentifier -and $withRuntime -eq $CopyWithDotNet) {
                foreach ($copy in @($CopyExecutables | Where-Object Project -eq $app.Project)) {
                    $copyName = if ([string]::IsNullOrEmpty($copy.Name)) { $executableName } else { $copy.Name }
                    $copies.Add([pscustomobject]@{Source=$executable;Name=$copyName})
                }
            }
        }
    }
    if ($CreateZipArchives -and !$SkipFirmwareHashes) {
        $hashRoot = Join-Path $stage 'firmware-hashes'
        $hashDirectory = (New-Item -ItemType Directory -Path (Join-Path $hashRoot 'Plugins/fwhashes') -Force).FullName
        $hashFiles = @(Get-ChildItem -LiteralPath (Join-Path $RepositoryRoot 'fw/hashes') -Filter '*.json' -File)
        if ($hashFiles.Count -eq 0) { throw 'No firmware hash references found.' }
        Copy-Item -LiteralPath @($hashFiles.FullName) -Destination $hashDirectory
        $hashZip = Join-Path $output "NxFileViewerFork${frameworkSuffix}_v${Version}_firmware-hashes.zip"
        Compress-Archive -LiteralPath (Join-Path $hashRoot 'Plugins') -DestinationPath $hashZip -Force
        Write-Host "ZIP: $hashZip"
        Add-PublishSizeRow $sizes ([IO.Path]::GetFileName($hashZip)) $hashRoot $null $hashZip
    }
    $reportVersion = $publishedVersions -join '_v'
    Save-PublishSizeReport $sizes (Join-Path $output "publish-sizes-$Framework-v$reportVersion.txt")
    if ($CopyExecutableToTestDirectory) { Copy-PublishExecutables $copies $TestDirectory }
}
finally {
    Pop-Location
    $resolvedStage = (Resolve-Path -LiteralPath $stage).ProviderPath
    $resolvedOutput = (Resolve-Path -LiteralPath $output).ProviderPath
    if ([IO.Path]::GetDirectoryName($resolvedStage) -eq $resolvedOutput -and
        [IO.Path]::GetFileName($resolvedStage) -match '^\.publish-applications-[0-9a-f]{32}$') {
        Remove-Item -LiteralPath $resolvedStage -Recurse -Force
    }
}
