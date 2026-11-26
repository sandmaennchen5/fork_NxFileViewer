# Test real Avalonia dispatch and PowerShell selector binding without compiling apps.
$ErrorActionPreference = 'Stop'
$root = Join-Path ([IO.Path]::GetTempPath()) ('nxfv-publish-selection-' + [guid]::NewGuid().ToString('N'))
$global:publishSelectionCalls = [Collections.Generic.List[object]]::new()
function dotnet {
    if ($global:publishSelectionStartupProbe) { throw 'Publish startup reached dotnet.' }
    $arguments = @($args)
    $rid = $arguments[[Array]::IndexOf($arguments, '-r') + 1]
    $output = $arguments[[Array]::IndexOf($arguments, '-o') + 1]
    $contained = $arguments[[Array]::IndexOf($arguments, '--self-contained') + 1]
    New-Item -ItemType Directory -Path $output -Force | Out-Null
    $assemblyName = (Select-Xml -LiteralPath $arguments[1] -XPath '/Project/PropertyGroup/AssemblyName').Node.InnerText
    $exe = $assemblyName + $(if ($rid.StartsWith('win-')) { '.exe' } else { '' })
    [IO.File]::WriteAllText((Join-Path $output $exe), 'fixture')
    $global:publishSelectionCalls.Add([pscustomobject]@{Project=$arguments[1];Rid=$rid;Contained=[bool]$contained})
    $global:LASTEXITCODE = 0
}
try {
    $script = Join-Path $PSScriptRoot '../../Publish.ps1'
    & $script -AvaloniaViewer -AvaloniaRuntimeIdentifiers linux-x64 -WithDotNet -Version 4.1.1-beta9 -OutputDirectory $root
    & $script --AvaloniaLogViewer -AvaloniaRuntimeIdentifiers osx-arm64 -WithoutDotNet -Version 4.1.1-beta9 -OutputDirectory $root
    $calls = $global:publishSelectionCalls
    if ($calls.Count -ne 2 -or $calls[0].Project -notlike '*NxFileViewer.csproj' -or
        $calls[0].Rid -ne 'linux-x64' -or !$calls[0].Contained -or
        $calls[1].Project -notlike '*NxFileViewer.LogViewer.csproj' -or
        $calls[1].Rid -ne 'osx-arm64' -or $calls[1].Contained) { throw 'Targeted Avalonia dispatch failed.' }
    if (@(Get-ChildItem -LiteralPath $root -Filter '*.zip').Count -ne 2) { throw 'Targeted mode built extra packages.' }
    # Exercise CI and release initialization, including typed switch variables.
    $global:publishSelectionStartupProbe = $true
    try {
        foreach ($mode in @('GitHub', 'ReleasePackages')) {
            $reachedPublish = $false
            $startupOptions = @{ OutputDirectory = (Join-Path $root $mode); Version = '4.1.1-beta9' }
            $startupOptions[$mode] = $true
            try { & $script @startupOptions }
            catch {
                if ($_.Exception.Message -ne 'Publish startup reached dotnet.') { throw }
                $reachedPublish = $true
            }
            if (!$reachedPublish) { throw "$mode did not reach publishing." }
        }
    } finally { $global:publishSelectionStartupProbe = $false }
    $tokens=$null; $errors=$null
    $ast = [System.Management.Automation.Language.Parser]::ParseInput((Get-Content -LiteralPath $script -Raw), [ref]$tokens, [ref]$errors)
    if ($errors.Count) { throw ($errors | Out-String) }
    $bindSource = '[CmdletBinding(PositionalBinding = $false)]' + [Environment]::NewLine + $ast.ParamBlock.Extent.Text + [Environment]::NewLine + '$PSBoundParameters'
    $bind = [scriptblock]::Create($bindSource)
    $single = & $bind -ViewerX64
    $double = & $bind --ViewerX64
    if (!$single.ViewerX64 -or '--ViewerX64' -notin $double.AdditionalSwitches) { throw 'ViewerX64 switch/alias binding failed.' }
    $rejected = $false
    try { & $script --ViewerX64 -AvaloniaViewer } catch { $rejected = $_.Exception.Message -like '*Do not combine Avalonia-only*' }
    if (!$rejected) { throw 'Double-dash Windows selection was not applied before conflict validation.' }
    $rejected = $false
    try { & $script --ViewerX6 } catch { $rejected = $_.Exception.Message -like '*Unknown switch*' }
    if (!$rejected) { throw 'Unknown selection was accepted.' }
    . (Join-Path $PSScriptRoot '../../tools/Publish-Configuration.ps1')
    $config=New-PublishConfiguration
    $config.Applications=@('AvaloniaLogViewer');$config.RuntimeIdentifiers=@('win-x64');$config.WithDotNet=$false;$config.FirmwareHashes=$false
    $config.OutputDirectory=Join-Path $root 'configured'
    $configPath=Join-Path $root 'preferences.conf'
    Save-PublishConfiguration $config $configPath
    $before=$global:publishSelectionCalls.Count
    & $script -NoGui -ConfigFile $configPath -Version 4.1.1-beta9
    if($global:publishSelectionCalls.Count -ne ($before+1) -or $global:publishSelectionCalls[$before].Project -notlike '*NxFileViewer.LogViewer.csproj'){throw 'Bare configured dispatch failed.'}
    $named=New-PublishConfiguration
    $named.Applications=@('AvaloniaViewer');$named.RuntimeIdentifiers=@('linux-arm64');$named.WithDotNet=$true;$named.WithoutDotNet=$false;$named.FirmwareHashes=$false
    $named.OutputDirectory=Join-Path $root 'profile-output'
    Save-PublishConfiguration $named $configPath 'Linux ARM'
    & $script -NoGui -Profile 'Linux ARM' -ConfigFile $configPath -Version 4.1.1-beta9
    $last=$global:publishSelectionCalls[$global:publishSelectionCalls.Count-1]
    if($last.Rid -ne 'linux-arm64' -or !$last.Contained -or $last.Project -notlike '*NxFileViewer.csproj'){throw 'Named profile dispatch failed.'}
    $named.OutputDirectory=Join-Path $root 'matrix-output'
    $named | Add-Member -NotePropertyName TargetOutputs -NotePropertyValue ([pscustomobject]@{
        'win-x64'=[pscustomobject]@{WithDotNet='Both';WithoutDotNet='Folder'}
        'linux-x64'=[pscustomobject]@{WithDotNet='Zip';WithoutDotNet='Off'}
    }) -Force
    Save-PublishConfiguration $named $configPath 'Mixed targets'
    $before=$global:publishSelectionCalls.Count
    & $script -NoGui -Profile 'Mixed targets' -ConfigFile $configPath -Version 4.1.1-beta9
    $matrixCalls=@($global:publishSelectionCalls | Select-Object -Skip $before)
    if ($matrixCalls.Count -ne 3 -or @($matrixCalls | Where-Object { $_.Rid -eq 'linux-x64' -and $_.Contained }).Count -ne 1) {
        throw 'The output matrix built extra variants or skipped Linux with .NET.'
    }
    $prefix='NxFileViewerFork_v4.1.1-beta9'
    $matrixOutput=$named.OutputDirectory
    $expected=@(
        "${prefix}_win-x64-with-net.zip"
        "${prefix}_win-x64-with-net/nxfileviewer-package.json"
        "${prefix}_win-x64-without-net/nxfileviewer-package.json"
        "${prefix}_linux-x64-with-net.zip"
    )
    foreach ($file in $expected) {
        if (!(Test-Path -LiteralPath (Join-Path $matrixOutput $file) -PathType Leaf)) { throw "Missing matrix output: $file" }
    }
    if ((Test-Path -LiteralPath (Join-Path $matrixOutput "${prefix}_win-x64-without-net.zip")) -or
        (Test-Path -LiteralPath (Join-Path $matrixOutput "${prefix}_linux-x64-with-net"))) {
        throw 'The output matrix produced an unselected output format.'
    }
    Write-Host 'Publish selection passed: targeted apps, OS/architecture, .NET variants, -ViewerX64 and --ViewerX64.'
}
finally {
    Remove-Variable -Name publishSelectionStartupProbe -Scope Global -ErrorAction SilentlyContinue
    Remove-Variable -Name publishSelectionCalls -Scope Global -ErrorAction SilentlyContinue
    $resolved = [IO.Path]::GetFullPath($root)
    if ([IO.Path]::GetDirectoryName($resolved) -eq [IO.Path]::GetTempPath().TrimEnd([IO.Path]::DirectorySeparatorChar) -and
        [IO.Path]::GetFileName($resolved) -match '^nxfv-publish-selection-[0-9a-f]{32}$') {
        if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse -Force }
    }
}
