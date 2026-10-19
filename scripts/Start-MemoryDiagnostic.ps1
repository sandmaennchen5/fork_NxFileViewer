param(
    [ValidateSet('baseline', 'no-logos', 'one-preview')]
    [string]$Mode = 'baseline',
    [string]$ExePath = (Join-Path $PSScriptRoot '../src/NxFileViewer/bin/Debug/net8.0-windows7.0/NxFileViewerFork.exe')
)
$ErrorActionPreference = 'Stop'
$resolvedExe = (Resolve-Path -LiteralPath $ExePath).Path
$start = New-Object System.Diagnostics.ProcessStartInfo
$start.FileName = $resolvedExe
$start.WorkingDirectory = Split-Path -Parent $resolvedExe
$start.UseShellExecute = $false
$start.EnvironmentVariables['NXFV_MEMORY_DIAGNOSTICS'] = '1'
$start.EnvironmentVariables['NXFV_MEMORY_MODE'] = $Mode
$process = [System.Diagnostics.Process]::Start($start)
Write-Host "Started diagnostic mode: $Mode (PID $($process.Id))"
Write-Host 'CSV logs: %LOCALAPPDATA%\NxFileViewer\Diagnostics'