# Packages an already published Avalonia application on its target OS.
[CmdletBinding()]
param(
    [ValidateSet('AppImage','Dmg','App')][string]$Format,
    [string]$Tool,
    [string]$RuntimeIdentifier,
    [string]$Version,
    [string]$AppName,
    [string]$AssetName,
    [string]$PublishDirectory,
    [string]$ExecutableName,
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
$utf8 = [Text.UTF8Encoding]::new($false)
$stage = Split-Path -Parent $PublishDirectory
$displayName = "$AppName Avalonia"

if ($Format -eq 'AppImage') {
    $asset = Join-Path $stage "$AssetName.AppImage"
    $appDir = (New-Item -ItemType Directory -Path (Join-Path $stage "$AssetName.AppDir")).FullName
    Get-ChildItem -LiteralPath $PublishDirectory -Force |
        Where-Object Name -notin @('start.sh','install-shortcut.sh') |
        Copy-Item -Destination $appDir -Recurse
    $appRun = Join-Path $appDir 'AppRun'
    Copy-Item -LiteralPath (Join-Path $PublishDirectory 'start.sh') -Destination $appRun
    & chmod +x $appRun (Join-Path $appDir $ExecutableName)
    if ($LASTEXITCODE -ne 0) { throw 'Could not set AppImage executable permissions.' }

    $id = $AppName.ToLowerInvariant()
    $desktop = @"
[Desktop Entry]
Type=Application
Name=$displayName
Exec=$ExecutableName %F
Icon=$id
Categories=Utility;
Terminal=false
"@
    [IO.File]::WriteAllText((Join-Path $appDir "$id.desktop"), $desktop.Replace("`r`n", "`n") + "`n", $utf8)
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot '../src/Resources/nxfv-xci.png') -Destination (Join-Path $appDir "$id.png")
    Copy-Item -LiteralPath (Join-Path $appDir "$id.png") -Destination (Join-Path $appDir '.DirIcon')

    $previousArch = $env:ARCH
    $previousExtractAndRun = $env:APPIMAGE_EXTRACT_AND_RUN
    try {
        $env:ARCH = if ($RuntimeIdentifier -eq 'linux-arm64') { 'aarch64' } else { 'x86_64' }
        $env:APPIMAGE_EXTRACT_AND_RUN = '1'
        & $Tool $appDir $asset | Out-Host
        if ($LASTEXITCODE -ne 0) { throw "appimagetool failed ($LASTEXITCODE)." }
    } finally {
        $env:ARCH = $previousArch
        $env:APPIMAGE_EXTRACT_AND_RUN = $previousExtractAndRun
    }
} else {
    $imageRoot = Join-Path $stage "$AssetName-bundle"
    $bundle = Join-Path $imageRoot "$displayName.app"
    $contents = Join-Path $bundle 'Contents'
    $bin = (New-Item -ItemType Directory -Path (Join-Path $contents 'MacOS') -Force).FullName
    Get-ChildItem -LiteralPath $PublishDirectory -Force | Copy-Item -Destination $bin -Recurse
    & /bin/chmod +x (Join-Path $bin $ExecutableName)
    if ($LASTEXITCODE -ne 0) { throw 'Could not set macOS executable permissions.' }

    $bundleVersion = ($Version -split '-', 2)[0]
    $plist = @"
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleExecutable</key><string>$ExecutableName</string>
    <key>CFBundleIdentifier</key><string>io.github.nxfileviewer.$($AppName.ToLowerInvariant()).avalonia</string>
    <key>CFBundleName</key><string>$displayName</string>
    <key>CFBundlePackageType</key><string>APPL</string>
    <key>CFBundleShortVersionString</key><string>$bundleVersion</string>
    <key>CFBundleVersion</key><string>$bundleVersion</string>
</dict>
</plist>
"@
    [IO.File]::WriteAllText((Join-Path $contents 'Info.plist'), $plist, $utf8)
    if ($Format -eq 'App') {
        $asset = Join-Path $stage "$AssetName.app.zip"
        & /usr/bin/ditto -c -k --keepParent $bundle $asset
        if ($LASTEXITCODE -ne 0) { throw "Archiving the macOS app failed ($LASTEXITCODE)." }
    } else {
        $asset = Join-Path $stage "$AssetName.dmg"
        & /bin/ln -s /Applications (Join-Path $imageRoot 'Applications')
        if ($LASTEXITCODE -ne 0) { throw 'Could not create the Applications link.' }
        & $Tool create -srcfolder $imageRoot -volname $displayName -format UDZO $asset | Out-Host
        if ($LASTEXITCODE -ne 0) { throw "hdiutil failed ($LASTEXITCODE)." }
    }
}
if (!(Test-Path -LiteralPath $asset -PathType Leaf)) { throw "Native package missing: $asset" }
$destination = Join-Path $OutputDirectory ([IO.Path]::GetFileName($asset))
Move-Item -LiteralPath $asset -Destination $destination -Force
$destination
