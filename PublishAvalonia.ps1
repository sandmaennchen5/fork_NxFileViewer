[CmdletBinding()]
param(
    [string]$OutputDirectory = 'Publish/Avalonia',
    [string]$Version,
    [ValidateSet('win-x64', 'win-x86', 'win-arm64', 'linux-x64', 'linux-arm64', 'osx-x64', 'osx-arm64')]
    [string[]]$RuntimeIdentifiers = @('win-x64', 'win-x86'),
    [switch]$SkipFirmwareHashes
)

$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'src/config/Validate-ExternalSources.ps1')
$projects = @(
    @{ Project = 'NxFileViewer.Avalonia'; Name = 'NxFileViewerFork' }
    @{ Project = 'NxFileViewer.LogViewer.Avalonia'; Name = 'NxFileViewerFork.LogViewer' }
)
if (!$Version) {
    $viewer = Join-Path $PSScriptRoot 'src/NxFileViewer/NxFileViewer.csproj'
    $Version = (Select-Xml -LiteralPath $viewer -XPath '/Project/PropertyGroup/Version').Node.InnerText
}
if ($Version -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?$') {
    throw 'Supply a version such as 4.2.0 or 4.2.0-beta1.'
}

$outputPath = if ([IO.Path]::IsPathRooted($OutputDirectory)) { $OutputDirectory } else { Join-Path $PSScriptRoot $OutputDirectory }
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
$stage = (New-Item -ItemType Directory -Path (Join-Path $output ('.publish-avalonia-' + [guid]::NewGuid().ToString('N')))).FullName
Push-Location -LiteralPath (Join-Path $PSScriptRoot 'src')
try {
    foreach ($app in $projects) {
        $project = Join-Path $PSScriptRoot "src/$($app.Project)/$($app.Project).csproj"
        foreach ($rid in @($RuntimeIdentifiers | Select-Object -Unique)) {
            $architecture = if ($rid.StartsWith('win-')) { $rid.Substring(4) } else { $rid }
            foreach ($withRuntime in @($false, $true)) {
                $suffix = if ($withRuntime) { '_self-contained' } else { '' }
                $name = "$($app.Name)_Avalonia_v${Version}${suffix}_$architecture"
                $publish = Join-Path $stage $name
                dotnet publish $project -c Release -r $rid --self-contained $withRuntime "-p:Version=$Version" -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true "-p:EnableCompressionInSingleFile=$withRuntime" -o $publish
                if ($LASTEXITCODE -ne 0) { throw "Publishing $name failed ($LASTEXITCODE)." }
                $zip = Join-Path $output "$name.zip"
                Write-PackageZip $publish $zip
                Write-Host "ZIP: $zip"
            }
        }
    }
    if (!$SkipFirmwareHashes) {
        $hashRoot = Join-Path $stage 'firmware-hashes'
        $hashDirectory = (New-Item -ItemType Directory -Path (Join-Path $hashRoot 'Plugins/fwhashes') -Force).FullName
        $hashFiles = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'fw/hashes') -Filter '*.json' -File)
        if ($hashFiles.Count -eq 0) { throw 'No firmware hash references found.' }
        Copy-Item -LiteralPath @($hashFiles.FullName) -Destination $hashDirectory
        $hashZip = Join-Path $output "NxFileViewerFork_Avalonia_v${Version}_firmware-hashes.zip"
        Compress-Archive -LiteralPath (Join-Path $hashRoot 'Plugins') -DestinationPath $hashZip -Force
        Write-Host "ZIP: $hashZip"
    }
}
finally {
    Pop-Location
    $resolvedStage = (Resolve-Path -LiteralPath $stage).ProviderPath
    $resolvedOutput = (Resolve-Path -LiteralPath $output).ProviderPath
    if ([IO.Path]::GetDirectoryName($resolvedStage) -eq $resolvedOutput -and
        [IO.Path]::GetFileName($resolvedStage) -match '^\.publish-avalonia-[0-9a-f]{32}$') {
        Remove-Item -LiteralPath $resolvedStage -Recurse -Force
    }
}
