param([Parameter(Mandatory=$true)][string]$ZigExe)
$ErrorActionPreference = 'Stop'
# Explorer's architecture, independent of the viewer's x86/x64 build.
foreach ($architecture in @('x64', 'x86', 'arm64')) {
    $target = switch ($architecture) { 'x64' { 'x86_64-windows-gnu' }; 'x86' { 'x86-windows-gnu' }; 'arm64' { 'aarch64-windows-gnu' } }
    $output = Join-Path $PSScriptRoot "Native/$architecture"
    New-Item -ItemType Directory -Path $output -Force | Out-Null
    & $ZigExe c++ (Join-Path $PSScriptRoot 'ExplorerCommands.cpp') (Join-Path $PSScriptRoot 'ExplorerCommands.def') -std=c++17 -target $target -shared -Os -s -static -lole32 -lshell32 -lshlwapi -ladvapi32 -luuid -o (Join-Path $output 'NxFileViewer.Explorer.dll')
    if ($LASTEXITCODE -ne 0) { throw "Explorer extension build failed for $architecture." }
    & $ZigExe c++ (Join-Path $PSScriptRoot 'ExplorerHost.cpp') -std=c++17 -target $target -municode -mwindows -Os -s -static -ladvapi32 -o (Join-Path $output 'NxFileViewer.ExplorerHost.exe')
    if ($LASTEXITCODE -ne 0) { throw "Explorer package host build failed for $architecture." }
}
