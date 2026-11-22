# Test real Avalonia dispatch and PowerShell selector binding without compiling apps.
$ErrorActionPreference = 'Stop'
$root = Join-Path ([IO.Path]::GetTempPath()) ('nxfv-publish-selection-' + [guid]::NewGuid().ToString('N'))
$global:publishSelectionCalls = [Collections.Generic.List[object]]::new()
function dotnet {
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
    if ($calls.Count -ne 2 -or $calls[0].Project -notlike '*NxFileViewer.Avalonia.csproj' -or
        $calls[0].Rid -ne 'linux-x64' -or !$calls[0].Contained -or
        $calls[1].Project -notlike '*NxFileViewer.LogViewer.Avalonia.csproj' -or
        $calls[1].Rid -ne 'osx-arm64' -or $calls[1].Contained) { throw 'Targeted Avalonia dispatch failed.' }
    if (@(Get-ChildItem -LiteralPath $root -Filter '*.zip').Count -ne 2) { throw 'Targeted mode built extra packages.' }
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
    if (!$rejected) { throw 'Double-dash WPF selection was not applied before conflict validation.' }
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
    if($global:publishSelectionCalls.Count -ne ($before+1) -or $global:publishSelectionCalls[$before].Project -notlike '*NxFileViewer.LogViewer.Avalonia.csproj'){throw 'Bare configured dispatch failed.'}
    & $script -Wpf -ConfigFile $configPath -Version 4.1.1-beta9
    if($global:publishSelectionCalls.Count -ne ($before+3) -or $global:publishSelectionCalls[$before+1].Project -notlike '*NxFileViewer.csproj'){throw '-Wpf group dispatch failed.'}
    $named=New-PublishConfiguration
    $named.Applications=@('AvaloniaViewer');$named.RuntimeIdentifiers=@('linux-arm64');$named.WithDotNet=$true;$named.WithoutDotNet=$false;$named.FirmwareHashes=$false
    $named.OutputDirectory=Join-Path $root 'profile-output'
    Save-PublishConfiguration $named $configPath 'Linux ARM'
    & $script -NoGui -Profile 'Linux ARM' -ConfigFile $configPath -Version 4.1.1-beta9
    $last=$global:publishSelectionCalls[$global:publishSelectionCalls.Count-1]
    if($last.Rid -ne 'linux-arm64' -or !$last.Contained -or $last.Project -notlike '*NxFileViewer.Avalonia.csproj'){throw 'Named profile dispatch failed.'}
    Write-Host 'Publish selection passed: targeted apps, OS/architecture, .NET variants, -ViewerX64 and --ViewerX64.'
}
finally {
    Remove-Variable -Name publishSelectionCalls -Scope Global -ErrorAction SilentlyContinue
    $resolved = [IO.Path]::GetFullPath($root)
    if ([IO.Path]::GetDirectoryName($resolved) -eq [IO.Path]::GetTempPath().TrimEnd([IO.Path]::DirectorySeparatorChar) -and
        [IO.Path]::GetFileName($resolved) -match '^nxfv-publish-selection-[0-9a-f]{32}$') {
        if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse -Force }
    }
}
