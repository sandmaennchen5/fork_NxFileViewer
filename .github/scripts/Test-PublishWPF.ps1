# Regression test for tools/Publish-Applications.ps1 (WPF).
# Exercise packaging, copies and size reports without downloading runtimes or compiling apps.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$root = Join-Path ([IO.Path]::GetTempPath()) ('nxfv-wpf-publish-' + [guid]::NewGuid().ToString('N'))
$global:wpfPublishTestCalls = [Collections.Generic.List[object]]::new()
function dotnet {
    $arguments = @($args)
    if ($arguments[0] -ne 'publish') { throw 'Unexpected dotnet command.' }
    $rid = $arguments[[Array]::IndexOf($arguments, '-r') + 1]
    $output = $arguments[[Array]::IndexOf($arguments, '-o') + 1]
    $contained = $arguments[[Array]::IndexOf($arguments, '--self-contained') + 1]
    if ('-p:Version=4.1.1-beta9' -notin $arguments) { throw 'Release version was not passed to dotnet.' }
    New-Item -ItemType Directory -Path $output -Force | Out-Null
    $project = $arguments[1]
    $exe = if ($project -like '*NxFileViewer.LogViewer.csproj') { 'NxFileViewerFork.LogViewer.exe' } else { 'NxFileViewerFork.exe' }
    [IO.File]::WriteAllText((Join-Path $output $exe), "$rid / $contained")
    [IO.File]::WriteAllText((Join-Path $output 'LICENSE.txt'), 'fixture license')
    $global:wpfPublishTestCalls.Add([pscustomobject]@{Rid=$rid;Contained=[bool]$contained})
    $global:LASTEXITCODE = 0
}
try {
    $rids = @('win-x64', 'win-x86', 'win-arm64')
    & (Join-Path $PSScriptRoot '../../tools/Publish-Applications.ps1') -Framework WPF -Version '4.1.1-beta9' -OutputDirectory $root -SkipFirmwareHashes -AllTargets -NoCopy
    if ($global:wpfPublishTestCalls.Count -ne 12) { throw 'Expected two applications and two distributions for each runtime.' }
    foreach ($rid in $rids) {
        if (@($global:wpfPublishTestCalls | Where-Object Rid -eq $rid).Count -ne 4) { throw "Missing builds for $rid." }
        $suffix = if ($rid.StartsWith('win-')) { $rid.Substring(4) } else { $rid }
        foreach ($app in @('NxFileViewerFork', 'NxFileViewerFork.LogViewer')) {
            foreach ($variant in @('', '_self-contained')) {
                $zip = Join-Path $root "${app}_v4.1.1-beta9${variant}_${suffix}.zip"
                if (!(Test-Path -LiteralPath $zip)) { throw "Missing package $zip." }
                $archive = [IO.Compression.ZipFile]::OpenRead($zip)
                try { if ($archive.Entries.Count -ne $(if ($app -eq 'NxFileViewerFork') { 3 } else { 2 })) { throw "Unexpected package contents: $zip" } }
                finally { $archive.Dispose() }
            }
        }
    }
    if (@(Get-ChildItem -LiteralPath $root -Force).Count -ne 13) { throw 'Unexpected output or uncleaned staging directory.' }
    $report = Join-Path $root 'publish-sizes-WPF-v4.1.1-beta9.txt'
    $reportLines = @(Get-Content -LiteralPath $report)
    if ($reportLines.Count -ne 14) { throw 'Size report must list every package.' }
    foreach ($line in $reportLines | Select-Object -Skip 2) {
        $columns = $line -split "`t"
        $zipBytes = (Get-Item -LiteralPath (Join-Path $root ($columns[0].TrimEnd() + '.zip'))).Length
        $expectedMB = ([decimal]$zipBytes / 1000000).ToString('0.00',[Globalization.CultureInfo]::GetCultureInfo('de-DE'))
        if ($columns[3].Trim() -ne $expectedMB) { throw 'Reported ZIP MB size differs from the actual file.' }
    }
    $exeOutput = Join-Path $root 'exe-mode'
    $copyOutput = Join-Path $root 'copies'
    & (Join-Path $PSScriptRoot '../../tools/Publish-Applications.ps1') -Framework WPF -Version '4.1.1-beta9' -OutputDirectory $exeOutput -RuntimeIdentifiers win-x64 -ExecutableOnly -CopyTo $copyOutput -SkipFirmwareHashes
    if (@(Get-ChildItem -LiteralPath $exeOutput -Filter '*.zip').Count) { throw 'Executable-only mode created ZIPs.' }
    if (@(Get-ChildItem -LiteralPath $copyOutput -File).Count -ne 2) { throw 'Both selected executables must be copied.' }
    $exeReport = @(Get-Content -LiteralPath (Join-Path $exeOutput 'publish-sizes-WPF-v4.1.1-beta9.txt'))
    if ($exeReport.Count -ne 6 -or @($exeReport | Select-Object -Skip 2 | Where-Object { ($_ -split "`t")[3] -ne '-' }).Count) { throw 'Executable-only report should have four variants and no ZIP sizes.' }
    Write-Host 'WPF packaging passed: 3 Windows runtimes, 12 packages, release version and staging cleanup.'
}
finally {
    Remove-Variable -Name wpfPublishTestCalls -Scope Global -ErrorAction SilentlyContinue
    $resolved = [IO.Path]::GetFullPath($root)
    if ([IO.Path]::GetDirectoryName($resolved) -eq [IO.Path]::GetTempPath().TrimEnd([IO.Path]::DirectorySeparatorChar) -and
        [IO.Path]::GetFileName($resolved) -match '^nxfv-wpf-publish-[0-9a-f]{32}$') {
        if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse -Force }
    }
}
