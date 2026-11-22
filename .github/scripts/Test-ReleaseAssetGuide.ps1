<# Dependency-free tests for release ZIP descriptions; uses isolated fixtures. #>
$ErrorActionPreference = 'Stop'
$helper = Join-Path $PSScriptRoot 'Add-ReleaseAssetGuide.ps1'
$tempBase = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$testRoot = Join-Path $tempBase ('nxfv-release-guide-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot | Out-Null
$script:checks = 0
function Assert([bool]$condition, [string]$message) {
    if (!$condition) { throw $message }
}
function Fixture([string]$name, [bool]$optional = $true, [int]$runtime = 10) {
    $root = Join-Path $testRoot $name
    $out = Join-Path $root 'packages'
    New-Item -ItemType Directory -Path $out -Force | Out-Null
    foreach ($project in @('NxFileViewer', 'NxFileViewer.LogViewer')) {
        $path = Join-Path $root "src/$project"
        New-Item -ItemType Directory -Path $path -Force | Out-Null
        [IO.File]::WriteAllText((Join-Path $path "$project.csproj"), "<Project><PropertyGroup><TargetFramework>net$runtime.0-windows7.0</TargetFramework></PropertyGroup></Project>")
    }
    $version = '4.1.1-beta1'
    $packages = @()
    $explorer = @()
    foreach ($architecture in @('x64', 'x86', 'arm64')) {
        foreach ($contained in @($false, $true)) {
            $variant = if ($contained) { '_self-contained' } else { '' }
            $file = "NxFileViewerFork_v$($version)$($variant)_$architecture.zip"
            [IO.File]::WriteAllBytes((Join-Path $out $file), [byte[]]@())
            $packages += @{Architecture=$architecture;SelfContained=$contained;AssetName=$file}
            if ($optional) {
                foreach ($extra in @("NxFileViewerFork.LogViewer_v$($version)$($variant)_$architecture.zip", "NxFileViewer_v$($version)$($variant)_$architecture.zip")) {
                    [IO.File]::WriteAllBytes((Join-Path $out $extra), [byte[]]@())
                }
            }
        }
    }
    if ($optional) {
        foreach ($architecture in @('x64', 'arm64')) {
            $file = "NxFileViewerFork.Explorer_1.0.2.0_$($architecture)_setup.zip"
            [IO.File]::WriteAllBytes((Join-Path $out $file), [byte[]]@())
            $explorer += @{Architecture=$architecture;SelfContained=$false;AssetName=$file}
        }
        [IO.File]::WriteAllBytes((Join-Path $out "NxFileViewerFork_v$($version)_firmware-hashes.zip"), [byte[]]@())
    }
    $descriptor = @{SchemaVersion=1;ApplicationId='NxFileViewerFork';Version=$version;Packages=@($packages | Where-Object Architecture -ne 'arm64');Arm64Packages=@($packages | Where-Object Architecture -eq 'arm64');ExplorerPackages=$explorer}
    [IO.File]::WriteAllText((Join-Path $out 'nxfileviewer-update.json'), ($descriptor | ConvertTo-Json -Depth 6))
    $notes = Join-Path $root 'notes.md'
    [IO.File]::WriteAllText($notes, "### Fixed" + [char]10 + "- Preserve this changelog text." + [char]10)
    return [pscustomobject]@{Root=$root;Output=$out;Notes=$notes;Version=$version;Packages=$packages}
}
function Run-Guide($fixture) {
    & $helper -Version $fixture.Version -NotesPath $fixture.Notes -PackageDirectory $fixture.Output -RepositoryRoot $fixture.Root
}
try {
    $full = Fixture 'full'
    Run-Guide $full
    $text = [IO.File]::ReadAllText($full.Notes)
    Assert ($text.StartsWith('### Fixed')) 'Changelog content was not preserved.'
    $contained = $text.IndexOf('Viewer: includes .NET 10')
    $standard = $text.IndexOf('Viewer: requires .NET 10 Desktop Runtime')
    Assert ($contained -ge 0 -and $standard -gt $contained) 'Runtime group headings missing.'
    foreach ($package in $full.Packages) {
        $pattern = $package.AssetName -replace '([_-])(x64|x86|arm64)(?=\.zip$)', '${1}{Arch}'
        $position = $text.IndexOf($pattern)
        if ($package.SelfContained) {
            Assert ($position -gt $contained -and $position -lt $standard) 'Self-contained package in wrong group.'
        } else { Assert ($position -gt $standard) 'Standard package in wrong group.' }
    }
    foreach ($file in (Get-ChildItem -LiteralPath $full.Output -Filter '*.zip')) {
        $pattern = $file.Name -replace '([_-])(x64|x86|arm64)(?=(_setup)?\.zip$)', '${1}{Arch}'
        Assert ([regex]::Matches($text, [regex]::Escape($pattern)).Count -eq 1) "ZIP pattern not explained exactly once: $pattern"
    }
    Assert ($text.Contains('Explorer_1.0.2.0_{Arch}_setup.zip')) 'Independent Explorer version missing.'
    Assert ([regex]::Matches($text, '(?m)^- `').Count -eq 8) 'Architecture-only variants were not grouped.'
    Assert ($text.Contains('{Arch}: x64, x86, arm64.')) 'Architecture legend missing.'
    Assert ($text.Contains('Legacy transition updaters')) 'Legacy explanation missing.'
    Assert ($text.Contains('standalone LogViewer')) 'LogViewer explanation missing.'
    Assert ($text.Contains('offline firmware hash references')) 'Firmware explanation missing.'
    $script:checks++

    Run-Guide $full
    Assert ([IO.File]::ReadAllText($full.Notes) -ceq $text) 'Guide duplicated or changed on repeated invocation.'
    $script:checks++

    $minimal = Fixture 'minimal' $false
    Run-Guide $minimal
    $minimalText = [IO.File]::ReadAllText($minimal.Notes)
    Assert (!$minimalText.Contains('##### Optional:')) 'Nonexistent optional packages advertised.'
    Assert (!$minimalText.Contains('Legacy transition updaters')) 'Nonexistent transition advertised.'
    $script:checks++

    $future = Fixture 'runtime11' $false 11
    Run-Guide $future
    Assert ([IO.File]::ReadAllText($future.Notes).Contains('requires .NET 11 Desktop Runtime')) 'Runtime version hard-coded.'
    $script:checks++

    $beta = Fixture 'avalonia'
    foreach ($app in @('NxFileViewerFork', 'NxFileViewerFork.LogViewer')) {
        foreach ($rid in @('x64', 'x86', 'arm64', 'linux-x64', 'linux-arm64', 'osx-x64', 'osx-arm64')) {
            foreach ($variant in @('', '_self-contained')) {
                [IO.File]::WriteAllBytes((Join-Path $beta.Output "${app}_Avalonia_v$($beta.Version)${variant}_${rid}.zip"), [byte[]]@())
            }
        }
    }
    Run-Guide $beta
    $betaText = [IO.File]::ReadAllText($beta.Notes)
    Assert ($betaText.Contains('Avalonia beta: Windows, Linux and macOS')) 'Avalonia platform guide missing.'
    foreach ($file in (Get-ChildItem -LiteralPath $beta.Output -Filter '*_Avalonia_*.zip')) {
        $pattern = $file.Name -replace '([_-])(x64|x86|arm64)(?=\.zip$)', '${1}{Arch}'
        Assert ([regex]::Matches($betaText, [regex]::Escape($pattern)).Count -eq 1) "Avalonia ZIP pattern not explained exactly once: $pattern"
    }
    Assert ($betaText.Contains('All Avalonia packages (viewer and LogViewer, on every operating system) are early beta versions. Not all features are implemented or tested yet.')) 'Early-beta notice missing.'
    Assert ([regex]::Matches($betaText, '(?m)^- `').Count -eq 20) 'Avalonia architecture variants were not grouped.'
    Assert ($betaText.Contains('_linux-{Arch}.zip') -and $betaText.Contains('_osx-{Arch}.zip')) 'Operating systems were merged.'
    $script:checks++

    $missing = Fixture 'missing'
    $before = [IO.File]::ReadAllBytes($missing.Notes)
    Remove-Item -LiteralPath (Join-Path $missing.Output $missing.Packages[0].AssetName)
    $failed = $false
    try { Run-Guide $missing } catch { $failed = $true }
    Assert $failed 'Missing package was accepted.'
    Assert ([Convert]::ToBase64String([IO.File]::ReadAllBytes($missing.Notes)) -ceq [Convert]::ToBase64String($before)) 'Failed guide changed notes.'
    $script:checks++

    $invalid = Fixture 'wrong-version'
    $failed = $false
    try {
        & $helper -Version '4.2.0' -NotesPath $invalid.Notes -PackageDirectory $invalid.Output -RepositoryRoot $invalid.Root
    } catch { $failed = $true }
    Assert $failed 'Mismatched descriptor version accepted.'
    $script:checks++
    Write-Host "$script:checks release guide scenarios passed."
}
finally {
    $resolved = [IO.Path]::GetFullPath($testRoot)
    if ($resolved.StartsWith($tempBase.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase) -and
        [IO.Path]::GetFileName($resolved).StartsWith('nxfv-release-guide-')) {
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
