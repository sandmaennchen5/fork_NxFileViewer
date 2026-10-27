param([Parameter(Mandatory=$true)][string]$ZigExe)
$ErrorActionPreference = 'Stop'
# Explorer's architecture, independent of the viewer's x86/x64 build.
foreach ($architecture in @('x64', 'x86', 'arm64')) {
    $target = switch ($architecture) { 'x64' { 'x86_64-windows-gnu' }; 'x86' { 'x86-windows-gnu' }; 'arm64' { 'aarch64-windows-gnu' } }
    $output = Join-Path $PSScriptRoot "Native/$architecture"
    New-Item -ItemType Directory -Path $output -Force | Out-Null
    & $ZigExe c++ (Join-Path $PSScriptRoot 'ExplorerCommands.cpp') (Join-Path $PSScriptRoot 'ExplorerCommands.def') -std=c++17 -target $target -shared -Os -s -lole32 -lshell32 -lshlwapi -ladvapi32 -luuid -o (Join-Path $output 'NxFileViewer.Explorer.dll')
    if ($LASTEXITCODE -ne 0) { throw "Explorer extension build failed for $architecture." }
    # -static together with -shared produces an archive with newer Zig versions.
    $payload = [IO.File]::ReadAllBytes((Join-Path $output 'NxFileViewer.Explorer.dll'))
    if ($payload.Length -lt 64 -or $payload[0] -ne 0x4d -or $payload[1] -ne 0x5a) { throw "Explorer extension is not a Windows PE image: $architecture." }
    $peOffset = [BitConverter]::ToInt32($payload, 60)
    $expectedMachine = switch ($architecture) { 'x64' { 0x8664 } 'x86' { 0x14c } 'arm64' { 0xaa64 } }
    if ($peOffset -lt 0 -or $peOffset -gt $payload.Length - 24 -or
        [BitConverter]::ToUInt32($payload, $peOffset) -ne 0x4550 -or
        [BitConverter]::ToUInt16($payload, $peOffset + 4) -ne $expectedMachine -or
        ([BitConverter]::ToUInt16($payload, $peOffset + 22) -band 0x2000) -eq 0) {
        throw "Explorer extension must be a DLL for $architecture."
    }
    & $ZigExe c++ (Join-Path $PSScriptRoot 'ExplorerHost.cpp') -std=c++17 -target $target -municode -mwindows -Os -s -static -ladvapi32 -o (Join-Path $output 'NxFileViewer.ExplorerHost.exe')
    if ($LASTEXITCODE -ne 0) { throw "Explorer package host build failed for $architecture." }
}
