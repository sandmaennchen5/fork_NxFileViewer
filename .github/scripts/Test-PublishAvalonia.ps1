# Exercise the real packaging loop without downloading runtimes or compiling applications.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$root = Join-Path ([IO.Path]::GetTempPath()) ('nxfv-avalonia-publish-' + [guid]::NewGuid().ToString('N'))
$global:avaloniaPublishTestCalls = [Collections.Generic.List[object]]::new()
function dotnet {
    $arguments = @($args)
    if ($arguments[0] -ne 'publish') { throw 'Unexpected dotnet command.' }
    $rid = $arguments[[Array]::IndexOf($arguments, '-r') + 1]
    $output = $arguments[[Array]::IndexOf($arguments, '-o') + 1]
    $contained = $arguments[[Array]::IndexOf($arguments, '--self-contained') + 1]
    if ('-p:Version=4.1.1-beta9' -notin $arguments) { throw 'Release version was not passed to dotnet.' }
    New-Item -ItemType Directory -Path $output -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $output 'application.txt'), "$rid / $contained")
    $global:avaloniaPublishTestCalls.Add([pscustomobject]@{Rid=$rid;Contained=[bool]$contained})
    $global:LASTEXITCODE = 0
}
try {
    $rids = @('win-x64', 'win-x86', 'win-arm64', 'linux-x64', 'linux-arm64', 'osx-x64', 'osx-arm64')
    & (Join-Path $PSScriptRoot '../../PublishAvalonia.ps1') -Version '4.1.1-beta9' -OutputDirectory $root -RuntimeIdentifiers $rids -SkipFirmwareHashes
    if ($global:avaloniaPublishTestCalls.Count -ne 28) { throw 'Expected two applications and two distributions for each runtime.' }
    foreach ($rid in $rids) {
        if (@($global:avaloniaPublishTestCalls | Where-Object Rid -eq $rid).Count -ne 4) { throw "Missing builds for $rid." }
        $suffix = if ($rid.StartsWith('win-')) { $rid.Substring(4) } else { $rid }
        foreach ($app in @('NxFileViewerFork', 'NxFileViewerFork.LogViewer')) {
            foreach ($variant in @('', '_self-contained')) {
                $zip = Join-Path $root "${app}_Avalonia_v4.1.1-beta9${variant}_${suffix}.zip"
                if (!(Test-Path -LiteralPath $zip)) { throw "Missing package $zip." }
                $archive = [IO.Compression.ZipFile]::OpenRead($zip)
                try { if ($archive.Entries.Count -ne 1) { throw "Unexpected package contents: $zip" } }
                finally { $archive.Dispose() }
            }
        }
    }
    if (@(Get-ChildItem -LiteralPath $root -Force).Count -ne 28) { throw 'Unexpected output or uncleaned staging directory.' }
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
