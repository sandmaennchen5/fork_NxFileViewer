[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PackagesJson,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [ValidateSet('Viewer','LogViewer')][string[]]$Applications = @('Viewer','LogViewer'),
    [string]$Version
)
$ErrorActionPreference = 'Stop'
foreach ($package in ($PackagesJson | ConvertFrom-Json)) {
    $format = [string]$package.format
    $runtimes = @($package.runtimes)
    $variants = @($package.variants | ForEach-Object { $_ -eq 'with-net' })
    $targets = @($runtimes | ForEach-Object { "$_-$($format.ToLowerInvariant())" })
    & (Join-Path $PSScriptRoot 'Publish-Applications.ps1') -Framework Avalonia -Applications $Applications -Version $Version -OutputDirectory $OutputDirectory -RuntimeIdentifiers $runtimes -RuntimeVariants $variants -NativePackage $format -FormatTargets $targets -ExecutableOnly -SkipFirmwareHashes -NoCopy -IncludeAppImageZip:($format -eq 'AppImage')
    $extensions = switch ($format) {
        AppImage { '*.AppImage'; '*.AppImage.zip' }
        App { '*.app.zip' }
        Dmg { '*.dmg' }
    }
    $expected = $runtimes.Count * $variants.Count * $Applications.Count
    foreach ($extension in $extensions) {
        if (@(Get-ChildItem -LiteralPath $OutputDirectory -File -Filter $extension).Count -ne $expected) {
            throw "Expected $expected packages matching $extension."
        }
    }
}
