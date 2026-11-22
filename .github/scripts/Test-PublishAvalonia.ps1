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
        $suffix = if ($rid.StartsWith('win-')) { $rid.Substring(4) } else { $rid }
        foreach ($app in @('NxFileViewerFork', 'NxFileViewerFork.LogViewer')) {
            foreach ($variant in @('', '_self-contained')) {
                $zip = Join-Path $root "${app}_Avalonia_v4.1.1-beta9${variant}_${suffix}.zip"
                if (!(Test-Path -LiteralPath $zip)) { throw "Missing package $zip." }
                $archive = [IO.Compression.ZipFile]::OpenRead($zip)
                try { if ($archive.Entries.Count -ne $(if($rid.StartsWith('linux-')){4}else{2})) { throw "Unexpected package contents: $zip" }; if($rid.StartsWith('linux-')) { foreach($entry in $archive.Entries | Where-Object { $_.FullName.EndsWith('.sh') -or $_.FullName -like 'NxFileViewerFork*' }) { if((($entry.ExternalAttributes -shr 16) -band 511) -ne 493) { throw 'Linux executable permissions missing.' } } } }
                finally { $archive.Dispose() }
            }
        }
    }
    if (@(Get-ChildItem -LiteralPath $root -Force).Count -ne 57) { throw 'Unexpected output or uncleaned staging directory.' }
    $report = Join-Path $root 'publish-sizes-Avalonia-v4.1.1-beta9.txt'
    $reportLines = @(Get-Content -LiteralPath $report)
    if ($reportLines.Count -ne 30) { throw 'Size report must list every package.' }
    foreach ($line in $reportLines | Select-Object -Skip 2) {
        $columns = $line -split "`t"
        $zipBytes = (Get-Item -LiteralPath (Join-Path $root ($columns[0].TrimEnd() + '.zip'))).Length
        $expectedMB = ([decimal]$zipBytes / 1000000).ToString('0.00',[Globalization.CultureInfo]::GetCultureInfo('de-DE'))
        if ($columns[3].Trim() -ne $expectedMB) { throw 'Reported ZIP MB size differs from the actual file.' }
    }
    $exeOutput = Join-Path $root 'exe-mode'
    $copyOutput = Join-Path $root 'copies'
    & (Join-Path $PSScriptRoot '../../tools/Publish-Applications.ps1') -Framework Avalonia -Version '4.1.1-beta9' -OutputDirectory $exeOutput -RuntimeIdentifiers win-x64 -ExecutableOnly -CopyTo $copyOutput -SkipFirmwareHashes
    if (@(Get-ChildItem -LiteralPath $exeOutput -Filter '*.zip').Count) { throw 'Executable-only mode created ZIPs.' }
    if (@(Get-ChildItem -LiteralPath $copyOutput -File).Count -ne 2) { throw 'Both selected executables must be copied.' }
    $exeReport = @(Get-Content -LiteralPath (Join-Path $exeOutput 'publish-sizes-Avalonia-v4.1.1-beta9.txt'))
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
