$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '../../tools/Publish-Configuration.ps1')
$root=Join-Path ([IO.Path]::GetTempPath()) ('nxfv-publish-config-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root | Out-Null
try {
    if((Get-PublishAutoStartRemaining 0 $false) -ne 10 -or
       (Get-PublishAutoStartRemaining 9.9 $false) -ne 1 -or
       (Get-PublishAutoStartRemaining 10 $false) -ne 0 -or
       (Get-PublishAutoStartRemaining 20 $false) -ne 0 -or
       (Get-PublishAutoStartRemaining 0 $true) -ne -1 -or
       (Get-PublishAutoStartRemaining 20 $true) -ne -1){throw 'GUI inactivity countdown failed.'}
    $path=Join-Path $root 'publish.ps1.conf'
    $config=New-PublishConfiguration
    Assert-PublishConfiguration $config
    $config.Applications=@('AvaloniaViewer','AvaloniaLogViewer');$config.RuntimeIdentifiers=@('win-arm64','linux-x64')
    $config.WithDotNet=$false;$config.WithoutDotNet=$true;$config.CopyExecutables=$false
    Save-PublishConfiguration $config $path
    $loaded=Read-PublishConfiguration $path
    if($loaded.Applications.Count -ne 2 -or $loaded.WithDotNet -or !$loaded.WithoutDotNet){throw 'Configuration round trip failed.'}
    $named=New-PublishConfiguration
    $named.Applications=@('AvaloniaViewer');$named.RuntimeIdentifiers=@('linux-arm64')
    Save-PublishConfiguration $named $path 'Linux ARM'
    $active=Read-PublishConfiguration $path
    $original=Read-PublishConfiguration $path 'Release'
    if($active.RuntimeIdentifiers[0] -ne 'linux-arm64' -or $original.Applications.Count -ne 2){throw 'Named profiles did not preserve the other selections.'}
    $missing=$false;try{Read-PublishConfiguration $path 'Does not exist' | Out-Null}catch{$missing=$true}
    if(!$missing){throw 'Missing profile was accepted.'}
    $legacy=Join-Path $root 'legacy.conf'
    [IO.File]::WriteAllText($legacy,(($config | ConvertTo-Json -Depth 6).Replace('"AvaloniaViewer"','"WpfViewer"')))
    $legacyConfig=Read-PublishConfiguration $legacy
    if($legacyConfig.Applications.Count -ne 2){throw 'Legacy flat configuration did not load.'}
    Save-PublishConfiguration $legacyConfig $legacy 'Windows'
    $upgraded=Read-PublishProfileStore $legacy
    if($upgraded.SchemaVersion -ne 2 -or @($upgraded.Profiles.PSObject.Properties).Count -ne 4){throw 'Legacy configuration was not preserved when adding a profile.'}
    # Exercise the same dynamic-module boundary as WinForms GetNewClosure events.
    # Construct the API in a temporary function scope, then invoke it after return.
    function Create-CallbackApi {
        . (Join-Path $PSScriptRoot '../../tools/Publish-Configuration.ps1')
        New-PublishGuiApi
    }
    $api=Create-CallbackApi
    $callbackPath=Join-Path $root 'callback-profile.conf'
    $callback={
        $value=$api.NewConfiguration()
        $value.OutputDirectory='callback-output'
        $api.ValidateConfiguration($value)
        $api.SaveConfiguration($value,$callbackPath,'77')
        $api.ReadConfiguration($callbackPath,'77')
    }.GetNewClosure()
    $callbackResult=& $callback
    if($callbackResult.OutputDirectory -ne 'callback-output'){throw 'GUI callback scope lost its configuration helpers.'}
    if((Read-PublishProfileStore $callbackPath).ActiveProfile -ne 'Release'){throw 'Saving a GUI profile unexpectedly changed the start profile.'}
    $startCallback={
        $api.SetStartProfile($callbackPath,'77')
        $draft=$api.NewConfiguration()
        $draft.OutputDirectory='another-profile-output'
        $api.SaveConfiguration($draft,$callbackPath,'Another profile')
        $api.ReadConfiguration($callbackPath,'')
    }.GetNewClosure()
    $defaultResult=& $startCallback
    if($defaultResult.OutputDirectory -ne 'callback-output' -or (Read-PublishProfileStore $callbackPath).ActiveProfile -ne '77'){throw 'Explicit start profile was not retained after saving another profile.'}
    if((Read-PublishConfiguration $callbackPath 'Another profile').OutputDirectory -ne 'another-profile-output'){throw 'Explicit profile selection no longer overrides the start profile.'}
    foreach($change in @('UnknownApp','NoRuntimeVariant','RetiredApp','CopyMissingVariant','UnknownProperty')) {
        $bad=New-PublishConfiguration
        switch($change){
            UnknownApp {$bad.Applications=@('not-an-app')}
            NoRuntimeVariant {$bad.WithDotNet=$false;$bad.WithoutDotNet=$false}
            RetiredApp {$bad.Applications=@('WpfViewer')}
            CopyMissingVariant {$bad.CopyExecutables=$true;$bad.RuntimeIdentifiers=@('win-arm64')}
            UnknownProperty {$bad|Add-Member -NotePropertyName Command -NotePropertyValue 'not allowed'}
        }
        $rejected=$false;try{Assert-PublishConfiguration $bad}catch{$rejected=$true}
        if(!$rejected){throw "Invalid configuration accepted: $change"}
    }
    $newStore=Read-PublishProfileStore (Join-Path $root 'new-install.conf')
    if ($newStore.ActiveProfile -ne 'Release' -or ($newStore.Profiles.PSObject.Properties.Name -join ',') -ne 'Release,Prerelease') {
        throw 'Expected only Release and Prerelease presets, with Release active.'
    }
    foreach ($name in @('Release','Prerelease')) {
        $preset = Read-PublishConfiguration (Join-Path $root 'new-install.conf') $name
        if (($preset.Applications -join ',') -ne 'AvaloniaViewer,AvaloniaLogViewer' -or
            ($preset.RuntimeIdentifiers -join ',') -ne 'win-x64,win-x86,win-arm64,linux-x64,linux-arm64,osx-x64,osx-arm64' -or
            ($preset.Extras -join ',') -ne 'Explorer,UpdateCompatibility' -or $preset.OutputDirectory -ne "Publish/$name" -or
            !$preset.WithDotNet -or !$preset.WithoutDotNet -or !$preset.CreateZipArchives -or !$preset.FirmwareHashes -or
            $preset.ExplorerOutputFormat -ne 'Zip' -or $preset.CopyExecutables) {
            throw "$name preset does not match the GitHub release packages."
        }
    }
    $preset.OutputDirectory='custom-prerelease-output'
    Save-PublishConfiguration $preset $path 'Prerelease'
    if((Read-PublishConfiguration $path 'Prerelease').OutputDirectory -ne 'custom-prerelease-output'){throw 'Predefined profile overwrote a user customization.'}
    $stub=@'
param($Framework,$OutputDirectory,$Applications,$RuntimeIdentifiers,$RuntimeVariants,[switch]$AllTargets,[switch]$NoCopy,[switch]$SkipFirmwareHashes,$Version,[switch]$ExecutableOnly,$CopyTo,$CopyRuntime,$CopyRuntimeIncludesDotNet)
$PSBoundParameters | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $PSScriptRoot ('backend-'+$Framework+'.json')) -Encoding UTF8
'@
    New-Item -ItemType Directory -Path (Join-Path $root 'tools') | Out-Null
    [IO.File]::WriteAllText((Join-Path $root 'tools/Publish-Applications.ps1'),$stub)
    Invoke-PublishConfiguration $loaded $root '4.1.1-beta5'
    $avalonia=Get-Content (Join-Path $root 'tools/backend-Avalonia.json') -Raw | ConvertFrom-Json
    if(@($avalonia.RuntimeIdentifiers).Count -ne 2 -or ($avalonia.Applications -join ',') -ne 'Viewer,LogViewer'){throw 'Avalonia target selection failed.'}
    if(!$avalonia.NoCopy.IsPresent -or !$avalonia.AllTargets.IsPresent -or $avalonia.RuntimeVariants[0] -ne $false){throw 'Explicit config overrides were lost.'}
    Write-Host 'Publish configuration passed: defaults, JSON round trip, invalid settings, application/OS/runtime dispatch, no-copy override.'
}
finally {
    $resolved=[IO.Path]::GetFullPath($root)
    if([IO.Path]::GetDirectoryName($resolved) -eq [IO.Path]::GetTempPath().TrimEnd([IO.Path]::DirectorySeparatorChar) -and [IO.Path]::GetFileName($resolved) -match '^nxfv-publish-config-[0-9a-f]{32}$'){Remove-Item -LiteralPath $resolved -Recurse -Force}
}
