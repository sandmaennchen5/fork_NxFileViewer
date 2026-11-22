<# Append an accurate ZIP guide using the packages built for this release. #>
param(
    [Parameter(Mandatory = $true)][string]$Version,
    [Parameter(Mandatory = $true)][string]$NotesPath,
    [Parameter(Mandatory = $true)][string]$PackageDirectory,
    [string]$RepositoryRoot = (Join-Path $PSScriptRoot '../..')
)
$ErrorActionPreference = 'Stop'
if ($Version -cnotmatch '^(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)(?:-beta[0-9]*)?$') {
    throw 'Expected a validated release version.'
}
$RepositoryRoot = [IO.Path]::GetFullPath($RepositoryRoot)
$PackageDirectory = [IO.Path]::GetFullPath($PackageDirectory)
$descriptorPath = Join-Path $PackageDirectory 'nxfileviewer-update.json'
$descriptor = [IO.File]::ReadAllText($descriptorPath) | ConvertFrom-Json
if ($descriptor.SchemaVersion -ne 1 -or $descriptor.ApplicationId -cne 'NxFileViewerFork' -or $descriptor.Version -cne $Version) {
    throw 'Release descriptor does not match this application/version.'
}
function Get-RuntimeVersion([string]$project) {
    [xml]$xml = [IO.File]::ReadAllText((Join-Path $RepositoryRoot $project))
    $framework = $xml.SelectSingleNode('/Project/PropertyGroup/TargetFramework')
    if ($null -eq $framework -or $framework.InnerText -notmatch '^net(?<major>\d+)\.(?<minor>\d+)(?:-|$)') {
        throw "Cannot determine Desktop Runtime from $project."
    }
    return $Matches['major'] + $(if ($Matches['minor'] -ne '0') { '.' + $Matches['minor'] } else { '' })
}
function Assert-Asset([object]$name) {
    if ($name -isnot [string] -or $name -cnotmatch '^[A-Za-z0-9][A-Za-z0-9._-]*\.zip$' -or
        !(Test-Path -LiteralPath (Join-Path $PackageDirectory $name) -PathType Leaf)) {
        throw "Missing or invalid release asset: $name"
    }
}
function Asset-Lines([string[]]$assetNames) {
    $groups = [ordered]@{}
    foreach ($name in $assetNames) {
        # Keep OS/runtime/application differences; collapse only the architecture.
        $pattern = $name -creplace '([_-])(x64|x86|arm64)(?=(_setup)?\.zip$)', '${1}{Arch}'
        if (!$groups.Contains($pattern)) { $groups[$pattern] = [Collections.Generic.List[string]]::new() }
        if ($name -cmatch '[_-](x64|x86|arm64)(?:_setup)?\.zip$') { $groups[$pattern].Add($Matches[1]) }
    }
    foreach ($pattern in $groups.Keys) {
        $line = '- ' + [char]96 + $pattern + [char]96
        $architectures = @('x64', 'x86', 'arm64' | Where-Object { $_ -in $groups[$pattern] })
        if ($architectures.Count) { $line += ' — {Arch}: ' + ($architectures -join ', ') + '.' }
        $line
    }
}
$runtime = Get-RuntimeVersion 'src/NxFileViewer/NxFileViewer.csproj'
$packages = @($descriptor.Packages) + @($descriptor.Arm64Packages | Where-Object { $null -ne $_ })
if ($packages.Count -eq 0) { throw 'Release descriptor contains no viewer packages.' }
$names = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($package in $packages) {
    Assert-Asset $package.AssetName
    if ($package.SelfContained -isnot [bool] -or $package.Architecture -notin @('x64', 'x86', 'arm64') -or !$names.Add($package.AssetName)) {
        throw 'Invalid or duplicate viewer package metadata.'
    }
}
$explorer = @($descriptor.ExplorerPackages | Where-Object { $null -ne $_ })
foreach ($package in $explorer) {
    Assert-Asset $package.AssetName
    if ($package.Architecture -notin @('x64', 'arm64') -or !$names.Add($package.AssetName)) {
        throw 'Invalid or duplicate Explorer package metadata.'
    }
}
$escaped = [regex]::Escape($Version)
$zipFiles = @(Get-ChildItem -LiteralPath $PackageDirectory -Filter '*.zip' -File)
$transition = @($zipFiles | Where-Object { $_.Name -cmatch ('^NxFileViewer_v' + $escaped + '(?:_self-contained)?_(?:x64|x86|arm64)\.zip$') -and !$names.Contains($_.Name) } | Sort-Object Name)
$firmware = @($zipFiles | Where-Object { $_.Name -cmatch ('^[A-Za-z0-9._-]+_v' + $escaped + '_firmware-hashes\.zip$') } | Sort-Object Name)
$avalonia = @($zipFiles | Where-Object { $_.Name -cmatch ('^NxFileViewerFork_Avalonia_v' + $escaped + '(?:_self-contained)?_(?:x64|x86|arm64|linux-(?:x64|arm64)|osx-(?:x64|arm64))\.zip$') } | Sort-Object Name)
$lines = [Collections.Generic.List[string]]::new()
$lines.Add('<!-- nxfileviewer-release-asset-guide -->')
$lines.Add('#### Which ZIP file do I need?')
$lines.Add('')
$lines.Add('For a normal Windows installation, choose one of the **viewer ZIPs** below. In each filename pattern, replace {Arch} with one of the listed architectures to match your system. Windows ARM64 packages are for Windows on ARM, not macOS.')
$lines.Add('')
$contained = @($packages | Where-Object SelfContained | Sort-Object Architecture)
$standard = @($packages | Where-Object { !$_.SelfContained } | Sort-Object Architecture)
if ($contained.Count) {
    $lines.Add("##### Viewer: includes .NET $runtime (self-contained)")
    $lines.Add('')
    $lines.Add('No separate .NET installation is required.')
    $lines.Add('')
    foreach ($line in (Asset-Lines @($contained.AssetName))) { $lines.Add($line) }
    $lines.Add('')
}
if ($standard.Count) {
    $lines.Add("##### Viewer: requires .NET $runtime Desktop Runtime")
    $lines.Add('')
    $lines.Add('These smaller ZIPs do not include the runtime. Install the Desktop Runtime for the selected architecture.')
    $lines.Add('')
    foreach ($line in (Asset-Lines @($standard.AssetName))) { $lines.Add($line) }
    $lines.Add('')
}
if ($avalonia.Count) {
    $lines.Add('##### Avalonia beta: Windows, Linux and macOS')
    $lines.Add('')
    $lines.Add('**All Avalonia viewer packages (on every operating system) are early beta versions. Not all features are implemented or tested yet.**')
    $lines.Add('')
    $lines.Add('These are the first converted Avalonia applications; the conversion is still in progress. ZIPs with linux- are for Linux, osx- for macOS; bare x64/x86/arm64 suffixes are for Windows. Choose the architecture of your system.')
    $lines.Add('')
    $lines.Add('Self-contained ZIPs include .NET. The other Avalonia ZIPs require the matching .NET runtime (Desktop Runtime on Windows). On Linux and macOS, extract with executable permissions preserved and start the Avalonia executable; these ZIPs are not signed macOS app installers.')
    $lines.Add('')
    foreach ($line in (Asset-Lines @($avalonia.Name))) { $lines.Add($line) }
    $lines.Add('')
}
if ($explorer.Count) {
    $lines.Add('##### Optional: modern Windows 11 Explorer context menu')
    $lines.Add('')
    $lines.Add('The modern context menu can also be installed through the program settings. Use these setup ZIPs for offline installation and choose the Windows architecture (x64 or ARM64). They do not contain the viewer itself.')
    $lines.Add('')
    foreach ($line in (Asset-Lines @($explorer.AssetName))) { $lines.Add($line) }
    $lines.Add('')
}
if ($firmware.Count) {
    $lines.Add('##### Optional: offline firmware hash references')
    $lines.Add('')
    $lines.Add('Extract beside the viewer for offline firmware verification. These ZIPs contain hash references, not firmware files or the viewer.')
    $lines.Add('')
    foreach ($line in (Asset-Lines @($firmware.Name))) { $lines.Add($line) }
    $lines.Add('')
}
if ($transition.Count) {
    $lines.Add('##### Legacy transition updaters: ZIP names without Fork')
    $lines.Add('')
    $lines.Add('These are auxiliary packages for older installations using the legacy built-in updater. They transition the old installation to the fork naming/layout. For a normal download, choose one of the viewer ZIPs above.')
    $lines.Add('')
    foreach ($line in (Asset-Lines @($transition.Name))) { $lines.Add($line) }
    $lines.Add('')
}
$lines.Add('The nxfileviewer-update.json attachment is update metadata, not an installer or an application ZIP.')
$notes = [IO.File]::ReadAllText($NotesPath).TrimEnd()
$marker = '<!-- nxfileviewer-release-asset-guide -->'
$index = $notes.IndexOf($marker, [StringComparison]::Ordinal)
if ($index -ge 0) { $notes = $notes.Substring(0, $index).TrimEnd() }
$newLine = [string][char]10
$body = $notes + $newLine + $newLine + ($lines -join $newLine).TrimEnd() + $newLine
[IO.File]::WriteAllText($NotesPath, $body, [Text.UTF8Encoding]::new($false))
Write-Host "Appended ZIP guide for $Version using .NET $runtime and the generated release descriptor."
