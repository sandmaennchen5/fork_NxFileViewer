param([Parameter(Mandatory=$true)][string]$ZigExe)
$ErrorActionPreference = 'Stop'
$toolchain = & $ZigExe env | ConvertFrom-Json
$source = Join-Path $PSScriptRoot 'launcher.c'
$output = Join-Path $PSScriptRoot 'NxFileViewerFork.Open.exe'
& $ZigExe cc $source -target x86-windows-gnu -Os -nostdlib -fno-stack-protector -isystem (Join-Path $toolchain.lib_dir 'libc/include/any-windows-any') '-Wl,--entry,LauncherEntry@0' '-Wl,--subsystem,windows' -lkernel32 -luser32 -lshell32 -o $output
if ($LASTEXITCODE -ne 0) { throw 'Native association launcher build failed.' }
