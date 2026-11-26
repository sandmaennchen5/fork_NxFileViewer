# Regression test for tools/Publish-Applications.ps1 (Avalonia).
# Exercise packaging, copies and size reports without downloading runtimes or compiling apps.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$root = Join-Path ([IO.Path]::GetTempPath()) ('nxfv-avalonia-publish-' + [guid]::NewGuid().ToString('N'))
$global:avaloniaPublishTestCalls = [Collections.Generic.List[object]]::new()
function dotnet {
    $arguments = @($args)
    if ($arguments[0] -ne 'publish') { throw 'Unexpected dotnet command.' }
    $artifactsIndex = [Array]::IndexOf($arguments, '--artifacts-path')
    if ($artifactsIndex -lt 0) { throw 'Publishing must isolate restore assets from normal builds.' }
    $artifacts = [string]$arguments[$artifactsIndex + 1]
    if (![IO.Path]::IsPathRooted($artifacts) -or $artifacts -notmatch '\.publish-applications-[0-9a-f]{32}[\\/]artifacts$') { throw 'Expected a per-run artifacts directory inside staging.' }

    $rid = $arguments[[Array]::IndexOf($arguments, '-r') + 1]
    $output = $arguments[[Array]::IndexOf($arguments, '-o') + 1]
    $contained = $arguments[[Array]::IndexOf($arguments, '--self-contained') + 1]
    if ('-p:Version=4.1.1-beta9' -notin $arguments) { throw 'Release version was not passed to dotnet.' }
    New-Item -ItemType Directory -Path $output -Force | Out-Null
    $project = $arguments[1]
    $assemblyName = (Select-Xml -LiteralPath $project -XPath '/Project/PropertyGroup/AssemblyName').Node.InnerText
    $exe = $assemblyName + $(if ($rid.StartsWith('win-')) { '.exe' } else { '' })
    [IO.File]::WriteAllText((Join-Path $output $exe), "$rid / $contained")
    $global:avaloniaPublishTestCalls.Add([pscustomobject]@{Rid=$rid;Contained=[bool]$contained})
    $global:LASTEXITCODE = 0
}
try {
    $rids = @('win-x64', 'win-x86', 'win-arm64', 'linux-x64', 'linux-arm64', 'osx-x64', 'osx-arm64')
    & (Join-Path $PSScriptRoot '../../tools/Publish-Applications.ps1') -Framework Avalonia -Version '4.1.1-beta9' -OutputDirectory $root -SkipFirmwareHashes -AllTargets -NoCopy
    if ($global:avaloniaPublishTestCalls.Count -ne 28) { throw 'Expected two applications and two distributions for each runtime.' }
    foreach ($rid in $rids) {
        if (@($global:avaloniaPublishTestCalls | Where-Object Rid -eq $rid).Count -ne 4) { throw "Missing builds for $rid." }
        foreach ($app in @('NxFileViewerFork', 'NxFileViewerFork.LogViewer')) {
            foreach ($variant in @('', '_self-contained')) {
                $runtime = if ($variant) { 'with-net' } else { 'without-net' }
                $name = "${app}_v4.1.1-beta9_$rid-$runtime.zip"
                $zip = Join-Path $root $name
                if (!(Test-Path -LiteralPath $zip)) { throw "Missing package $zip." }
                $archive = [IO.Compression.ZipFile]::OpenRead($zip)
                try { if ($archive.Entries.Count -ne $(if($rid.StartsWith('linux-')){3}elseif($app -eq 'NxFileViewerFork' -and $rid.StartsWith('win-')){3}else{2})) { throw "Unexpected package contents: $zip" }; if($rid.StartsWith('linux-')) { if ($archive.Entries.FullName -like '*.sh') { throw 'Unexpected Linux helper script.' }; if (!$archive.GetEntry('nxfv-icon.png')) { throw 'Linux icon missing.' }; foreach($entry in $archive.Entries | Where-Object { $_.FullName.EndsWith('.sh') -or $_.FullName -in @('NxFileViewerFork','NxFileViewerFork.LogViewer') }) { if((($entry.ExternalAttributes -shr 16) -band 511) -ne 493) { throw 'Linux executable permissions missing.' } } } }
                finally { $archive.Dispose() }
            }
        }
    }
    if (@(Get-ChildItem -LiteralPath $root -Force).Count -ne 57) { throw 'Unexpected output or uncleaned staging directory.' }
    $report = Join-Path $root 'publish-sizes-v4.1.1-beta9.txt'
    $reportLines = @(Get-Content -LiteralPath $report)
    if ($reportLines.Count -ne 30) { throw 'Size report must list every package.' }
    foreach ($line in $reportLines | Select-Object -Skip 2) {
        $columns = $line -split "`t"
        $zipBytes = (Get-Item -LiteralPath (Join-Path $root ($columns[0].TrimEnd() + '.zip'))).Length
        $expectedMB = ([decimal]$zipBytes / 1000000).ToString('0.00',[Globalization.CultureInfo]::GetCultureInfo('de-DE'))
        if ($columns[3].Trim() -ne $expectedMB) { throw 'Reported ZIP MB size differs from the actual file.' }
    }
    & (Join-Path $PSScriptRoot '../../Publish.ps1') -Version '4.1.1-beta9' -OutputDirectory $root -Extras UpdateManifest
    $descriptor = Get-Content -LiteralPath (Join-Path $root 'nxfileviewer-update.json') -Raw | ConvertFrom-Json
    if (@($descriptor.AvaloniaPackages).Count -ne 28 -or $descriptor.Packages.Count -or $descriptor.Arm64Packages.Count) {
        throw 'Expected only the 28 modern Avalonia update packages.'
    }
    & (Join-Path $PSScriptRoot '../../Publish.ps1') -Version '4.1.1-beta9' -OutputDirectory $root -Extras UpdateCompatibility
    $compatibility = Get-Content -LiteralPath (Join-Path $root 'nxfileviewer-update.json') -Raw | ConvertFrom-Json
    if ($compatibility.Packages.Count -ne 4 -or $compatibility.Arm64Packages.Count -ne 2 -or $compatibility.AvaloniaPackages.Count -ne 28) {
        throw 'Compatibility manifest must include Windows variants without duplicating the modern packages.'
    }
    $canonical = Join-Path $root 'NxFileViewerFork_v4.1.1-beta9_win-x64-with-net.zip'
    $forkAlias = Join-Path $root 'NxFileViewerFork_v4.1.1-beta9_self-contained_x64.zip'
    if ((Get-FileHash -LiteralPath $canonical).Hash -ne (Get-FileHash -LiteralPath $forkAlias).Hash) {
        throw 'The old Windows filename must point to the modern package.'
    }
    foreach ($suffix in @('x64','linux-x64','osx-x64')) {
        $alias = Join-Path $root "NxFileViewerFork_Avalonia_v4.1.1-beta9_self-contained_$suffix.zip"
        $archive = [IO.Compression.ZipFile]::OpenRead($alias)
        try {
            if (!$archive.GetEntry('nxfileviewer-avalonia.json') -or $archive.GetEntry('nxfileviewer-viewer.json') -or $archive.GetEntry('nxfileviewer-package.json')) {
                throw "The beta1 update archive for $suffix contains the wrong manifests."
            }
        } finally { $archive.Dispose() }
    }
    if (Test-Path -LiteralPath (Join-Path $root 'NxFileViewer_v4.1.1-beta9_x64.zip')) { throw 'The legacy bridge should only ship in stable releases.' }
    $exeOutput = Join-Path $root 'exe-mode'
    $copyOutput = Join-Path $root 'copies'
    & (Join-Path $PSScriptRoot '../../tools/Publish-Applications.ps1') -Framework Avalonia -Version '4.1.1-beta9' -OutputDirectory $exeOutput -RuntimeIdentifiers win-x64 -ExecutableOnly -CopyTo $copyOutput -SkipFirmwareHashes
    if (@(Get-ChildItem -LiteralPath $exeOutput -Filter '*.zip').Count) { throw 'Executable-only mode created ZIPs.' }
    if (@(Get-ChildItem -LiteralPath $copyOutput -File).Count -ne 2) { throw 'Both selected executables must be copied.' }
    $exeReport = @(Get-Content -LiteralPath (Join-Path $exeOutput 'publish-sizes-v4.1.1-beta9.txt'))
    if ($exeReport.Count -ne 6 -or @($exeReport | Select-Object -Skip 2 | Where-Object { ($_ -split "`t")[3] -ne '-' }).Count) { throw 'Executable-only report should have four variants and no ZIP sizes.' }
    Write-Host 'Avalonia packaging passed: 7 runtimes, 28 packages, release version and staging cleanup.'
}
finally {
    Remove-Variable -Name avaloniaPublishTestCalls -Scope Global -ErrorAction SilentlyContinue
    $resolved = [IO.Path]::GetFullPath($root)
    if ([IO.Path]::GetDirectoryName($resolved) -eq [IO.Path]::GetTempPath().TrimEnd([IO.Path]::DirectorySeparatorChar) -and
        [IO.Path]::GetFileName($resolved) -match '^nxfv-avalonia-publish-[0-9a-f]{32}$') {
        if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse -Force }
    }
}
