$ErrorActionPreference = 'Stop'
$hostArchitecture = if ($env:PROCESSOR_ARCHITECTURE -eq 'ARM64') { 'arm64' } else { 'x64' }
function Find-SdkBin([string]$Root) {
    if (!$Root -or !(Test-Path -LiteralPath $Root)) { return }
    Get-ChildItem -LiteralPath $Root -Directory | Sort-Object Name -Descending | ForEach-Object {
        $candidate = Join-Path $_.FullName $hostArchitecture
        if (Test-Path -LiteralPath (Join-Path $candidate 'makeappx.exe')) { $candidate }
    } | Select-Object -First 1
}
$installed = if (${env:ProgramFiles(x86)}) { Find-SdkBin (Join-Path ${env:ProgramFiles(x86)} 'Windows Kits/10/bin') }
if ($installed) { Write-Output $installed; return }
$cacheReport = & dotnet nuget locals global-packages --list
if ($LASTEXITCODE -ne 0) { throw 'Could not locate the NuGet package cache.' }
$cacheLine = $cacheReport | Select-Object -Last 1
$cache = $cacheLine.Substring($cacheLine.IndexOf(':') + 1).Trim()
$toolRoot = Join-Path $cache 'microsoft.windows.sdk.buildtools/10.0.26100.9169/bin'
$downloaded = Find-SdkBin $toolRoot
if (!$downloaded) {
    Write-Host 'Restoring Microsoft Windows SDK packaging tools (cached for subsequent publishes)...'
    & dotnet restore (Join-Path $PSScriptRoot 'PackagingTools.csproj') --nologo | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'Could not restore Microsoft Windows SDK packaging tools. Install Windows SDK or supply -ExplorerSdkBin.' }
    $downloaded = Find-SdkBin $toolRoot
}
if (!$downloaded) { throw 'Microsoft Windows SDK MakeAppx.exe was not found after restore.' }
Write-Output $downloaded
