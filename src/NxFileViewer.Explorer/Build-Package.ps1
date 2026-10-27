param(
    [Parameter(Mandatory=$true)][string]$Publisher,
    [ValidateSet('x64','arm64')][string]$Architecture = 'x64',
    [string]$Version = '1.0.0.0',
    [string]$CertificateThumbprint,
    [switch]$Unsigned,
    [string]$SdkBin,
    [string]$OutputDirectory,
    [string]$TimestampServer = 'http://timestamp.digicert.com'
)
$ErrorActionPreference = 'Stop'
if (!$CertificateThumbprint -and !$Unsigned) { throw 'Provide -CertificateThumbprint for signing, or -Unsigned to prepare a package for later signing.' }
if (!$SdkBin) {
    $SdkBin = & (Join-Path $PSScriptRoot 'Get-SdkBin.ps1')
}
if (!$SdkBin -or !(Test-Path -LiteralPath (Join-Path $SdkBin 'makeappx.exe'))) { throw 'Windows SDK MakeAppx.exe is required; supply -SdkBin.' }
$native = Join-Path $PSScriptRoot "Native/$Architecture"
foreach ($file in @('NxFileViewer.Explorer.dll','NxFileViewer.ExplorerHost.exe')) {
    if (!(Test-Path -LiteralPath (Join-Path $native $file))) { throw 'Build the native extension with build.ps1 first.' }
}
$output = if ($OutputDirectory) { [IO.Path]::GetFullPath($OutputDirectory) } else { Join-Path $PSScriptRoot "bin/$Architecture" }
# Keep packaging intermediates in bin, even when the resulting MSIX goes into Publish.
$stageRoot = Join-Path $PSScriptRoot "bin/$Architecture"
$stage = Join-Path $stageRoot ([Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $output -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $stage 'Assets') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $native 'NxFileViewer.Explorer.dll'),(Join-Path $native 'NxFileViewer.ExplorerHost.exe') -Destination $stage
$manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'AppxManifest.xml') -Raw
$manifest = $manifest.Replace('@PUBLISHER@', [Security.SecurityElement]::Escape($Publisher)).Replace('@VERSION@', $Version).Replace('@ARCHITECTURE@', $Architecture)
[IO.File]::WriteAllText((Join-Path $stage 'AppxManifest.xml'), $manifest, [Text.UTF8Encoding]::new($false))
Add-Type -AssemblyName System.Drawing
$icon = [Drawing.Icon]::new((Join-Path $PSScriptRoot '../NxFileViewer/Styling/Icons/Icon.ico'))
$source = $icon.ToBitmap()
try {
    foreach ($asset in @(@('StoreLogo.png',50),@('Logo150.png',150),@('Logo44.png',44))) {
        $bitmap = [Drawing.Bitmap]::new([int]$asset[1], [int]$asset[1])
        $graphics = [Drawing.Graphics]::FromImage($bitmap)
        try { $graphics.DrawImage($source, 0, 0, $bitmap.Width, $bitmap.Height); $bitmap.Save((Join-Path $stage ('Assets/' + $asset[0])), [Drawing.Imaging.ImageFormat]::Png) }
        finally { $graphics.Dispose(); $bitmap.Dispose() }
    }
} finally { $source.Dispose(); $icon.Dispose() }
$suffix = if ($Unsigned) { '.unsigned' } else { '' }
$package = Join-Path $output "NxFileViewerFork.Explorer_${Version}_${Architecture}${suffix}.msix"
& (Join-Path $SdkBin 'makeappx.exe') pack /o /d $stage /p $package
if ($LASTEXITCODE -ne 0) { throw 'MSIX packaging failed.' }
if (!$Unsigned) {
    [string[]]$timestampArguments = @()
    if ($TimestampServer) { $timestampArguments = @('/tr', $TimestampServer, '/td', 'SHA256') }
    & (Join-Path $SdkBin 'signtool.exe') sign /fd SHA256 /sha1 $CertificateThumbprint @timestampArguments $package
    if ($LASTEXITCODE -ne 0) { throw 'MSIX signing failed. Certificate subject must match Publisher.' }
}
Write-Output $package
