# JSON-only local publishing preferences. No configuration content is executed.
function New-PublishConfiguration {
    [pscustomobject][ordered]@{
        SchemaVersion=1
        Applications=@('WpfViewer','WpfLogViewer','AvaloniaViewer','AvaloniaLogViewer')
        RuntimeIdentifiers=@('win-x64','win-x86','win-arm64','linux-x64','linux-arm64','osx-x64','osx-arm64')
        WithDotNet=$true; WithoutDotNet=$true; CreateZipArchives=$true
        TargetOutputs=$null
        ExplorerOutputFormat='Zip'; TransitionOutputFormat='Zip'
        OutputDirectory='Publish'; FirmwareHashes=$true; IncludeReleaseExtras=$false; Extras=@(); Versions=[pscustomobject]@{}
        CopyExecutables=$false; CopyDirectory='C:\tmp\NxFileViewerTest'
        CopyRuntimeIdentifier='win-x64'; CopyWithDotNet=$false
    }
}
function New-PredefinedPublishProfiles {
    $profiles = [ordered]@{}
    # The tagged release workflow builds the same packages for stable and beta tags.
    foreach ($name in @('Release','Prerelease')) {
        $profile = New-PublishConfiguration
        $profile.Applications = @('AvaloniaViewer','AvaloniaLogViewer')
        $profile.OutputDirectory = "Publish/$name"
        $profile.Extras = @('Explorer','WpfMigration')
        $profiles[$name] = $profile
    }
    return [pscustomobject]$profiles
}
function Add-PredefinedPublishProfiles($Store) {
    $presets=New-PredefinedPublishProfiles
    foreach($property in $presets.PSObject.Properties){
        if($null -eq $Store.Profiles.PSObject.Properties[$property.Name] -and @($Store.Profiles.PSObject.Properties).Count -lt 32){
            $Store.Profiles | Add-Member -NotePropertyName $property.Name -NotePropertyValue $property.Value
        }
    }
    return $Store
}
function Get-ViewerProjectPath([string]$Project) {
    switch ($Project) {
        'NxFileViewer' { 'src/NxFileViewer.WPF/NxFileViewer.WPF.csproj' }
        'NxFileViewer.Avalonia' { 'src/NxFileViewer/NxFileViewer.csproj' }
        'NxFileViewer.LogViewer' { 'src/NxFileViewer.LogViewer.WPF/NxFileViewer.LogViewer.WPF.csproj' }
        'NxFileViewer.LogViewer.Avalonia' { 'src/NxFileViewer.LogViewer/NxFileViewer.LogViewer.csproj' }
        default { "src/$Project/$Project.csproj" }
    }
}
function Get-PublishVersionProjects {
    [ordered]@{
        WpfViewer='NxFileViewer'
        WpfLogViewer='NxFileViewer.LogViewer'
        AvaloniaViewer='NxFileViewer.Avalonia'
        AvaloniaLogViewer='NxFileViewer.LogViewer.Avalonia'
        Transition='NxFileViewer.Transition'
        Explorer='NxFileViewer'
    }
}
function Get-PublishProjectVersions([string]$RepositoryRoot) {
    $versions = @{}
    foreach ($entry in (Get-PublishVersionProjects).GetEnumerator()) {
        $project = Join-Path $RepositoryRoot (Get-ViewerProjectPath $entry.Value)
        $property = if ($entry.Key -eq 'Explorer') { 'ExplorerPackageVersion' } else { 'Version' }
        $versions[$entry.Key] = (Select-Xml -LiteralPath $project -XPath "/Project/PropertyGroup/$property").Node.InnerText
    }
    return $versions
}
function Set-PublishProjectVersions([string]$RepositoryRoot, $Versions) {
    $projects = Get-PublishVersionProjects
    $updates = @{}
    foreach ($entry in $Versions.PSObject.Properties) {
        $project = $projects[$entry.Name]
        $path = Join-Path $RepositoryRoot (Get-ViewerProjectPath $project)
        if (!$updates.ContainsKey($path)) {
            $reader = [IO.StreamReader]::new($path, [Text.UTF8Encoding]::new($false), $true)
            try {
                $updates[$path] = @{ Text=$reader.ReadToEnd(); Encoding=$reader.CurrentEncoding }
            } finally { $reader.Dispose() }
        }
        $file = $updates[$path]
        $property = if ($entry.Name -eq 'Explorer') { 'ExplorerPackageVersion' } else { 'Version' }
        $pattern = "(<${property}>\s*)[^<]*?(\s*</${property}>)"
        if ([regex]::Matches($file.Text, $pattern).Count -ne 1) { throw "Expected exactly one $property in $path." }
        $file.Text = [regex]::Replace($file.Text, $pattern, ('${1}' + $entry.Value + '${2}'))
    }
    foreach ($path in $updates.Keys) {
        $file = $updates[$path]
        $temporary = "$path.$([guid]::NewGuid().ToString('N')).tmp"
        try {
            [IO.File]::WriteAllText($temporary, $file.Text, $file.Encoding)
            Move-Item -LiteralPath $temporary -Destination $path -Force
            Write-Host "Versions updated: $path"
        } finally {
            if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Force }
        }
    }
}
function Get-PublishFormatTargets {
    'win-x64-exe','win-x86-exe','win-arm64-exe'
    'linux-x64-appimage','linux-arm64-appimage'
    'osx-x64-dmg','osx-arm64-dmg'
}

function Get-PublishApplicationNames {
    [ordered]@{
        WpfViewer='NxFileViewerFork.WPF'
        WpfLogViewer='NxFileViewerFork.WPF.LogViewer'
        AvaloniaViewer='NxFileViewerFork'
        AvaloniaLogViewer='NxFileViewerFork.LogViewer'
    }
}
function Get-PublishOutputMatrix($Config) {
    if ($null -ne $Config.TargetOutputs) { return $Config.TargetOutputs }
    $matrix = [pscustomobject]@{}
    foreach ($target in $Config.RuntimeIdentifiers) {
        $key = $target -replace '-exe$'
        if ($target -like '*-exe') {
            $mode = if ($Config.CreateZipArchives -and $key -in $Config.RuntimeIdentifiers) { 'Both' } else { 'Folder' }
        } elseif ($target -match '-(appimage|dmg)$') {
            $mode = if ($Config.CreateZipArchives) { 'Both' } else { 'Folder' }
        } elseif ($Config.CreateZipArchives) {
            $mode = if ("$target-exe" -in $Config.RuntimeIdentifiers) { 'Both' } else { 'Zip' }
        } else {
            $mode = 'Folder'
        }
        $matrix | Add-Member -NotePropertyName $key -NotePropertyValue ([pscustomobject]@{
            WithDotNet = if ($Config.WithDotNet) { $mode } else { 'Off' }
            WithoutDotNet = if ($Config.WithoutDotNet) { $mode } else { 'Off' }
        }) -Force
    }
    return $matrix
}
function Assert-PublishConfiguration($Config) {
    $defaults=New-PublishConfiguration
    if ($null -eq $Config -or $Config.SchemaVersion -ne 1) { throw 'Unsupported publish configuration schema.' }
    foreach ($property in $Config.PSObject.Properties) {
        if ($property.Name -notin $defaults.PSObject.Properties.Name) { throw "Unknown publish setting: $($property.Name)" }
    }
    foreach ($name in $defaults.PSObject.Properties.Name) {
        if ($name -notin @('Extras','Versions','TargetOutputs','ExplorerOutputFormat','TransitionOutputFormat') -and $name -notin $Config.PSObject.Properties.Name) { throw "Missing publish setting: $name" }
    }
    foreach ($name in @('WithDotNet','WithoutDotNet','CreateZipArchives','FirmwareHashes','CopyExecutables','CopyWithDotNet','IncludeReleaseExtras')) {
        if ($Config.$name -isnot [bool]) { throw "$name must be true or false." }
    }
    foreach ($name in @('ExplorerOutputFormat','TransitionOutputFormat')) {
        if ($Config.PSObject.Properties[$name] -and ($Config.$name -isnot [string] -or $Config.$name -notin @('Zip','Folder','Both'))) {
            throw "Invalid $name."
        }
    }
    if ($Config.PSObject.Properties['Extras']) {
        if ($Config.Extras -isnot [array] -or $Config.Extras.Count -gt 5) { throw 'Invalid release extras selection.' }
        foreach ($extra in $Config.Extras) {
            if ($extra -notin @('Explorer','Transition','Updater','UpdateManifest','AvaloniaZipMetadata','WpfMigration')) { throw "Unknown release extra: $extra" }
        }
    }
    if ($Config.PSObject.Properties['Versions']) {
        if ($Config.Versions -isnot [pscustomobject]) { throw 'Invalid program version settings.' }
        foreach ($entry in $Config.Versions.PSObject.Properties) {
            if ($entry.Name -notin (Get-PublishVersionProjects).Keys) { throw "Unknown program version: $($entry.Name)" }
            $pattern = if ($entry.Name -eq 'Explorer') { '^[0-9]+(?:\.[0-9]+){3}$' } else { '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?$' }
            if ($entry.Value -isnot [string] -or $entry.Value.Length -gt 100 -or $entry.Value -notmatch $pattern) { throw "Invalid version for $($entry.Name): $($entry.Value)" }
        }
    }
    if ($Config.Applications -isnot [array] -or (!$Config.Applications.Count -and !$Config.Extras.Count) -or $Config.Applications.Count -gt 4) { throw 'Select at least one application.' }
    foreach ($app in $Config.Applications) { if ($app -notin $defaults.Applications) { throw "Unknown application: $app" } }
    $allowedTargets = $defaults.RuntimeIdentifiers + @(Get-PublishFormatTargets)
    if ($Config.RuntimeIdentifiers -isnot [array] -or $Config.RuntimeIdentifiers.Count -gt $allowedTargets.Count) { throw 'Select at least one publish target.' }
    foreach ($target in $Config.RuntimeIdentifiers) { if ($target -notin $allowedTargets) { throw "Unknown publish target: $target" } }

    $matrix = $Config.TargetOutputs
    $selectedTargets = $Config.RuntimeIdentifiers
    if ($null -ne $matrix) {
        if ($matrix -isnot [pscustomobject]) { throw 'Invalid publish output matrix.' }
        $matrixTargets = $allowedTargets | Where-Object { $_ -notlike '*-exe' }
        $selectedTargets = @(foreach ($entry in $matrix.PSObject.Properties) {
            $row = $entry.Value
            if ($entry.Name -notin $matrixTargets -or $row -isnot [pscustomobject] -or
                @($row.PSObject.Properties).Count -ne 2 -or
                $row.WithDotNet -isnot [string] -or $row.WithoutDotNet -isnot [string] -or
                $row.WithDotNet -notin @('Off','Zip','Folder','Both') -or $row.WithoutDotNet -notin @('Off','Zip','Folder','Both')) {
                throw "Invalid output settings for $($entry.Name)."
            }
            if ($row.WithDotNet -ne 'Off' -or $row.WithoutDotNet -ne 'Off') { $entry.Name }
        })
    } elseif ($Config.Applications.Count -and !$Config.WithDotNet -and !$Config.WithoutDotNet) {
        throw 'Select at least one .NET variant.'
    }
    if ($Config.Applications.Count -and !$selectedTargets.Count) { throw 'Select at least one publish target.' }
    if ($Config.Applications.Count -and @($selectedTargets | Where-Object { $_ -match '-(appimage|dmg)$' }).Count -and
        !@($Config.Applications | Where-Object { $_ -like 'Avalonia*' }).Count) { throw 'AppImage and DMG targets require an Avalonia application.' }
    if (@($Config.Applications | Where-Object { $_ -like 'Wpf*' }).Count -and !@($selectedTargets | Where-Object { $_ -like 'win-*' }).Count) { throw 'WPF applications need at least one Windows architecture.' }
    if ($Config.IncludeReleaseExtras) {
        if ('WpfViewer' -notin $Config.Applications -or 'WpfLogViewer' -notin $Config.Applications -or
            @(@('win-x64','win-x86','win-arm64') | Where-Object { $_ -notin $selectedTargets }).Count -or
            ($null -ne $matrix -and @(@('win-x64','win-x86','win-arm64') | Where-Object {
                $matrix.$_.WithDotNet -ne 'Zip' -or $matrix.$_.WithoutDotNet -ne 'Zip'
            }).Count) -or
            ($null -eq $matrix -and (!$Config.WithDotNet -or !$Config.WithoutDotNet -or !$Config.CreateZipArchives)) -or !$Config.FirmwareHashes -or $Config.CopyExecutables) {
            throw 'Release extras require both WPF apps, all Windows architectures, both .NET variants, ZIPs and firmware hashes, with test copying disabled.'
        }
    }
    foreach ($name in @('OutputDirectory','CopyDirectory','CopyRuntimeIdentifier')) {
        if ($Config.$name -isnot [string] -or [string]::IsNullOrWhiteSpace($Config.$name) -or $Config.$name.Length -gt 2048 -or $Config.$name -match '[\x00-\x1f]') { throw "Invalid $name." }
    }
    if ($Config.CopyRuntimeIdentifier -notin $defaults.RuntimeIdentifiers) { throw 'Invalid copy runtime.' }
    if ($Config.CopyExecutables -and $Config.Applications.Count) {
        if (('WpfViewer' -in $Config.Applications -and 'AvaloniaViewer' -in $Config.Applications) -or
            ('WpfLogViewer' -in $Config.Applications -and 'AvaloniaLogViewer' -in $Config.Applications)) {
            throw 'WPF and Avalonia use the same executable names. Copy them using separate profiles with separate test directories.'
        }
        $copyVariant = if ($Config.CopyWithDotNet) { 'WithDotNet' } else { 'WithoutDotNet' }
        $copyTargets = @($selectedTargets | Where-Object { $null -eq $matrix -or $matrix.$_.$copyVariant -ne 'Off' })
        if ($Config.CopyRuntimeIdentifier -notin @($copyTargets -replace '-(exe|appimage|dmg)$') -or
            ($null -eq $matrix -and (($Config.CopyWithDotNet -and !$Config.WithDotNet) -or (!$Config.CopyWithDotNet -and !$Config.WithoutDotNet)))) { throw 'The copied variant must also be selected for building.' }
        if (@($Config.Applications | Where-Object { $_ -like 'Wpf*' }).Count -and $Config.CopyRuntimeIdentifier -notlike 'win-*') { throw 'Copying WPF applications requires a Windows copy target.' }
    }
}
function Assert-PublishProfileName([string]$Name) {
    if ([string]::IsNullOrWhiteSpace($Name) -or $Name.Length -gt 60 -or $Name -cnotmatch '^[\p{L}\p{N}][\p{L}\p{N} ._-]*$' -or $Name -ne $Name.Trim()) { throw 'Use a profile name of 1-60 letters, numbers, spaces, dots, underscores or hyphens.' }
}
function Read-PublishProfileStore([string]$Path) {
    if (!(Test-Path -LiteralPath $Path)) {
        return [pscustomobject]@{SchemaVersion=2;ActiveProfile='Release';Profiles=(New-PredefinedPublishProfiles)}
    }
    if ((Get-Item -LiteralPath $Path).Length -gt 65536) { throw 'Publish configuration exceeds 64 KiB.' }
    $store=Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($store.SchemaVersion -eq 1) {
        Assert-PublishConfiguration $store
        return Add-PredefinedPublishProfiles ([pscustomobject]@{SchemaVersion=2;ActiveProfile='Standard';Profiles=[pscustomobject]@{Standard=$store}})
    }
    if ($store.SchemaVersion -ne 2 -or $store.Profiles -isnot [pscustomobject] -or
        @($store.Profiles.PSObject.Properties).Count -lt 1 -or @($store.Profiles.PSObject.Properties).Count -gt 32) { throw 'Invalid publish profile configuration.' }
    foreach ($property in $store.PSObject.Properties) { if ($property.Name -notin @('SchemaVersion','ActiveProfile','Profiles')) { throw "Unknown profile-store setting: $($property.Name)" } }
    Assert-PublishProfileName $store.ActiveProfile
    foreach ($property in $store.Profiles.PSObject.Properties) {
        Assert-PublishProfileName $property.Name
        Assert-PublishConfiguration $property.Value
    }
    if ($null -eq $store.Profiles.PSObject.Properties[$store.ActiveProfile]) { throw "Active publish profile not found: $($store.ActiveProfile)" }
    return Add-PredefinedPublishProfiles $store
}
function Read-PublishConfiguration([string]$Path, [string]$Profile) {
    $store=Read-PublishProfileStore $Path
    if (!$Profile) { $Profile=$store.ActiveProfile }
    Assert-PublishProfileName $Profile
    $entry=$store.Profiles.PSObject.Properties[$Profile]
    if ($null -eq $entry) { throw "Publish profile not found: $Profile" }
    Write-Host "Publish profile: $Profile ($Path)"
    return ($entry.Value | ConvertTo-Json -Depth 6 | ConvertFrom-Json)
}
function Save-PublishConfiguration($Config,[string]$Path,[string]$Profile,[bool]$MakeActive=$true) {
    Assert-PublishConfiguration $Config
    $store=Read-PublishProfileStore $Path
    if (!$Profile) { $Profile=$store.ActiveProfile }
    Assert-PublishProfileName $Profile
    $store.Profiles | Add-Member -NotePropertyName $Profile -NotePropertyValue $Config -Force
    if($MakeActive){$store.ActiveProfile=$Profile}
    if (@($store.Profiles.PSObject.Properties).Count -gt 32) { throw 'At most 32 publish profiles are supported.' }
    $json=$store | ConvertTo-Json -Depth 8
    if ([Text.Encoding]::UTF8.GetByteCount($json) -gt 65536) { throw 'Publish configuration exceeds 64 KiB.' }
    $full=[IO.Path]::GetFullPath($Path)
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($full)) | Out-Null
    $temporary=$full+'.'+[guid]::NewGuid().ToString('N')+'.tmp'
    try {
        [IO.File]::WriteAllText($temporary,$json,[Text.UTF8Encoding]::new($false))
        Move-Item -LiteralPath $temporary -Destination $full -Force
    } finally { if(Test-Path -LiteralPath $temporary){Remove-Item -LiteralPath $temporary -Force} }
}
function Set-PublishStartProfile([string]$Path,[string]$Profile) {
    Assert-PublishProfileName $Profile
    $store=Read-PublishProfileStore $Path
    $entry=$store.Profiles.PSObject.Properties[$Profile]
    if($null -eq $entry){throw "Publish profile not found: $Profile"}
    Save-PublishConfiguration $entry.Value $Path $Profile $true
}
function Invoke-PublishConfiguration($Config,[string]$RepositoryRoot,[string]$Version) {
    Assert-PublishConfiguration $Config
    $runtimeVariants=@($(if($Config.WithoutDotNet){$false});$(if($Config.WithDotNet){$true}))
    $matrix = $Config.TargetOutputs
    $selectedTargets = if ($null -eq $matrix) { $Config.RuntimeIdentifiers } else {
        foreach ($entry in $matrix.PSObject.Properties) {
            if ($entry.Value.WithDotNet -ne 'Off' -or $entry.Value.WithoutDotNet -ne 'Off') { $entry.Name }
        }
    }
    $runnableTargets = @(foreach ($target in $selectedTargets) {
        if (($target -like '*-appimage' -and !$IsLinux) -or ($target -like '*-dmg' -and !$IsMacOS)) {
            Write-Warning "Skipping $target. AppImage builds require Linux and DMG builds require macOS. Run the saved profile on the target OS."
            continue
        }
        $target
    })
    if (!$runnableTargets.Count -and !$Config.Extras.Count) { throw 'The selected profile has no build targets for this host.' }
    $projects = Get-PublishVersionProjects
    $versions = @{}
    if (Test-Path -LiteralPath (Join-Path $RepositoryRoot 'src/NxFileViewer.WPF/NxFileViewer.WPF.csproj')) {
        $versions = Get-PublishProjectVersions $RepositoryRoot
        foreach ($key in $projects.Keys) {
            $override = if ($null -ne $Config.Versions) { $Config.Versions.PSObject.Properties[$key] }
            if ($Version -and $key -ne 'Explorer') { $versions[$key] = $Version }
            elseif ($override) { $versions[$key] = $override.Value }
        }
    }
    $firmwareBuilt=$false
    foreach($framework in @('Wpf','Avalonia')) {
        $apps=@()
        if(($framework+'Viewer') -in $Config.Applications){$apps+='Viewer'}
        if(($framework+'LogViewer') -in $Config.Applications){$apps+='LogViewer'}
        if(!$apps.Count){continue}
        if($framework -eq 'Wpf' -and $Config.IncludeReleaseExtras) {
            $releaseOptions=@{OutputDirectory=$Config.OutputDirectory;ReleasePackages=$true;SkipAvalonia=$true}
            if($Version){$releaseOptions.Version=$Version}
            & (Join-Path $RepositoryRoot 'Publish.ps1') @releaseOptions
            $firmwareBuilt=$true
            continue
        }
        $targets = @($runnableTargets | Where-Object { $framework -ne 'Wpf' -or $_ -like 'win-*' })
        if (!$targets.Count) { continue }
        $rids = @($targets -replace '-(exe|appimage|dmg)$' | Select-Object -Unique)
        $options=@{OutputDirectory=$Config.OutputDirectory;Applications=$apps;RuntimeIdentifiers=$rids;RuntimeVariants=$runtimeVariants;AllTargets=$true;NoCopy=(!$Config.CopyExecutables);SkipFirmwareHashes=(!$Config.FirmwareHashes -or $firmwareBuilt)}
        if ($versions.Count) {
            $options.Version = $versions[$framework+'Viewer']
            $options.ApplicationVersions = @{}
            foreach ($app in $apps) { $options.ApplicationVersions[$projects[$framework+$app]] = $versions[$framework+$app] }
        } elseif ($Version) { $options.Version=$Version }
        if ($null -ne $matrix) {
            $options.RuntimeVariants = @($false,$true)
            $options.TargetOutputs = $matrix
        } elseif (!$Config.CreateZipArchives) { $options.ExecutableOnly=$true }
        $options.FormatTargets = $targets
        if ($framework -eq 'Avalonia') { $options.SkipAvaloniaZipMetadata = $true }
        if ($Config.CopyExecutables) {
            $copyVariant = if ($Config.CopyWithDotNet) { 'WithDotNet' } else { 'WithoutDotNet' }
            $copyTargets = @($targets | Where-Object { $null -eq $matrix -or $matrix.$_.$copyVariant -ne 'Off' })
            if ($Config.CopyRuntimeIdentifier -in ($copyTargets -replace '-(exe|appimage|dmg)$')) {
                $options.CopyTo=$Config.CopyDirectory
                $options.CopyRuntime=$Config.CopyRuntimeIdentifier
                $options.CopyRuntimeIncludesDotNet=$Config.CopyWithDotNet
            }
        }
        & (Join-Path $RepositoryRoot 'tools/Publish-Applications.ps1') -Framework $framework @options
        if(($null -ne $matrix -or $Config.CreateZipArchives) -and $Config.FirmwareHashes){$firmwareBuilt=$true}
    }
    if ($Config.Extras.Count) {
        $extraOptions = @{ OutputDirectory=$Config.OutputDirectory }
        if (@($selectedTargets).Count) {
            $extraOptions.ExtraWindowsArchitectures = @($selectedTargets |
                Where-Object { $_ -like 'win-*' } | ForEach-Object { ($_ -split '-')[1] } | Select-Object -Unique)
        }
        if ($versions.Explorer) { $extraOptions.ExplorerPackageVersion = $versions.Explorer }
        $metadataExtras = @(foreach ($extra in @('UpdateManifest','AvaloniaZipMetadata','WpfMigration')) {
            if ($extra -in $Config.Extras -or ($extra -eq 'UpdateManifest' -and 'Updater' -in $Config.Extras)) { $extra }
        })
        foreach ($extra in @('Explorer','Transition','Metadata')) {
            $selectedExtras = if ($extra -eq 'Metadata') { $metadataExtras } elseif ($extra -in $Config.Extras) { @($extra) }
            if (!$selectedExtras) { continue }
            $extraOptions.Extras = @($selectedExtras)
            $outputFormat = "${extra}OutputFormat"
            if ($extra -ne 'Metadata' -and $Config.$outputFormat) { $extraOptions[$outputFormat] = $Config.$outputFormat }
            $extraOptions.Version = if ($extra -eq 'Transition') { $versions.Transition } else { $versions.WpfViewer }
            if ($extra -eq 'Metadata') {
                if ('WpfMigration' -in $selectedExtras) { $extraOptions.Version = $versions.AvaloniaViewer }
                $extraOptions.AvaloniaUpdateVersions = @{
                    'NxFileViewerFork'=$versions.AvaloniaViewer
                    'NxFileViewerFork.LogViewer'=$versions.AvaloniaLogViewer
                }
                if ($Config.Applications.Count) {
                    $extraOptions.UpdateFrameworks = @(foreach ($framework in @('Wpf','Avalonia')) {
                        if ($Config.Applications -like "$framework*") { $framework }
                    })
                }
            }
            & (Join-Path $RepositoryRoot 'Publish.ps1') @extraOptions
            $extraOptions.Remove($outputFormat)
        }
    }
}

function Get-PublishAutoStartRemaining([double]$ElapsedSeconds, [bool]$HasInput) {
    if ($HasInput) { return -1 }
    return [int][Math]::Max(0, [Math]::Ceiling(10 - $ElapsedSeconds))
}
# GetNewClosure runs event handlers in a dynamic module. Expose the helper graph
# through module-bound methods so callbacks never depend on the caller's scope.
function New-PublishGuiApi {
    $definitions=@{}
    foreach($name in @('New-PublishConfiguration','Get-ViewerProjectPath','Get-PublishVersionProjects','Set-PublishProjectVersions','Get-PublishFormatTargets','Get-PublishOutputMatrix','New-PredefinedPublishProfiles','Add-PredefinedPublishProfiles','Assert-PublishConfiguration','Assert-PublishProfileName','Read-PublishProfileStore','Read-PublishConfiguration','Save-PublishConfiguration','Set-PublishStartProfile')) {
        $definitions[$name]=(Get-Command $name -CommandType Function -ErrorAction Stop).Definition
    }
    New-Module -AsCustomObject -ArgumentList $definitions -ScriptBlock {
        param($Definitions)
        foreach($name in $Definitions.Keys) { Set-Item -Path ("Function:"+$name) -Value ([scriptblock]::Create($Definitions[$name])) }
        function GetOutputMatrix($Config) { Get-PublishOutputMatrix $Config }
        function NewConfiguration { New-PublishConfiguration }
        function ValidateConfiguration($Config) { Assert-PublishConfiguration $Config }
        function WriteProjectVersions([string]$RepositoryRoot,$Versions) { Set-PublishProjectVersions $RepositoryRoot $Versions }
        function ReadConfiguration([string]$Path,[string]$Profile) { Read-PublishConfiguration $Path $Profile }
        function SaveConfiguration($Config,[string]$Path,[string]$Profile) { Save-PublishConfiguration $Config $Path $Profile $false }
        function SetStartProfile([string]$Path,[string]$Profile) { Set-PublishStartProfile $Path $Profile }
        Export-ModuleMember -Function GetOutputMatrix,NewConfiguration,ValidateConfiguration,WriteProjectVersions,ReadConfiguration,SaveConfiguration,SetStartProfile
    }
}

function Show-PublishVersionDialog($Owner, $GuiApi, $ProjectVersions, $ProfileVersions, $BuildVersions, $VersionLabels, [string]$RepositoryRoot) {
    $dialog = New-Object System.Windows.Forms.Form
    $dialog.Text = 'Programmversionen bearbeiten'
    $dialog.StartPosition = 'CenterParent'
    $dialog.ClientSize = New-Object System.Drawing.Size(755, 342)
    $dialog.FormBorderStyle = 'FixedDialog'
    $dialog.MaximizeBox = $false
    $dialog.MinimizeBox = $false

    $columns = @(
        @{ Title='Projektdateien'; IsProject=$true; Values=$ProjectVersions; Fields=@{}; X=177 }
        @{ Title='Nur dieses Profil'; Values=$ProfileVersions; Fields=@{}; X=365 }
        @{ Title='Nur dieser Build'; Values=$BuildVersions; Fields=@{}; X=553 }
    )
    foreach ($column in $columns) {
        $header = New-Object System.Windows.Forms.Label
        $header.Text = $column.Title
        $header.SetBounds($column.X, 14, 178, 24)
        $dialog.Controls.Add($header)
    }
    $y = 40
    foreach ($key in $VersionLabels.Keys) {
        $label = New-Object System.Windows.Forms.Label
        $label.Text = $VersionLabels[$key]
        $label.SetBounds(16, $y+3, 155, 24)
        $dialog.Controls.Add($label)
        foreach ($column in $columns) {
            $field = New-Object System.Windows.Forms.TextBox
            $field.Text = $column.Values[$key]
            $field.SetBounds($column.X, $y, 178, 26)
            $dialog.Controls.Add($field)
            $column.Fields[$key] = $field
        }
        $y += 31
    }

    $hint = New-Object System.Windows.Forms.Label
    $hint.Text = "Leere Profil- und Build-Felder erben die Version. Build hat Vorrang vor Profil und Projekt.`r`nProjektdateien werden beim Uebernehmen geschrieben. Profilwerte mit 'Profil speichern' sichern."
    $hint.SetBounds(16, 231, 715, 42)
    $dialog.Controls.Add($hint)
    $status = New-Object System.Windows.Forms.Label
    $status.SetBounds(16, 276, 715, 24)
    $dialog.Controls.Add($status)
    $apply = New-Object System.Windows.Forms.Button
    $apply.Text = 'Uebernehmen'
    $apply.SetBounds(491, 302, 110, 30)
    $dialog.Controls.Add($apply)
    $dialog.AcceptButton = $apply
    $cancel = New-Object System.Windows.Forms.Button
    $cancel.Text = 'Abbrechen'
    $cancel.SetBounds(621, 302, 110, 30)
    $cancel.DialogResult = [System.Windows.Forms.DialogResult]::Cancel
    $dialog.Controls.Add($cancel)
    $dialog.CancelButton = $cancel

    $apply.Add_Click({
        try {
            $results = foreach ($column in $columns) {
                $config = $GuiApi.NewConfiguration()
                foreach ($key in $VersionLabels.Keys) {
                    $value = $column.Fields[$key].Text.Trim()
                    $include = if ($column.IsProject) { $value -cne [string]$ProjectVersions[$key] } else { $value -ne '' }
                    if ($include) {
                        $config.Versions | Add-Member -NotePropertyName $key -NotePropertyValue $value
                    }
                }
                $GuiApi.ValidateConfiguration($config)
                $config.Versions
            }
            if (@($results[0].PSObject.Properties).Count) {
                $GuiApi.WriteProjectVersions($RepositoryRoot, $results[0])
                foreach ($property in $results[0].PSObject.Properties) {
                    $ProjectVersions[$property.Name] = $property.Value
                }
            }
            $dialog.Tag = @{ Profile=$results[1]; Build=$results[2] }
            $dialog.DialogResult = [System.Windows.Forms.DialogResult]::OK
        } catch {
            $status.Text = $_.Exception.GetBaseException().Message
        }
    }.GetNewClosure())
    try {
        if ($dialog.ShowDialog($Owner) -eq [System.Windows.Forms.DialogResult]::OK) { return $dialog.Tag }
    } finally {
        $dialog.Dispose()
    }
}
function Show-PublishConfigurationMenu($Config,[string]$ConfigPath,[string]$Profile,[string]$RepositoryRoot,[string]$Version) {
    if([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT){throw '-Gui requires Windows. Edit publish.ps1.conf or use command-line selectors on other platforms.'}
    $guiApi=New-PublishGuiApi
    Add-Type -AssemblyName System.Windows.Forms
    Add-Type -AssemblyName System.Drawing
    $form=New-Object System.Windows.Forms.Form
    $form.Text='NxFileViewer - Publish'
    $form.StartPosition='CenterScreen';$form.AutoScroll=$true
    function Add-Label($Text,$X,$Y,$Width=310) {
        $control=New-Object System.Windows.Forms.Label
        $control.Text=$Text;$control.SetBounds($X,$Y,$Width,24);$form.Controls.Add($control)
    }
    function Add-Check($Text,$X,$Y,$Checked) {
        $control=New-Object System.Windows.Forms.CheckBox
        $control.Text=$Text;$control.SetBounds($X,$Y,268,26);$control.Checked=$Checked;$form.Controls.Add($control);return $control
    }
    function Add-Text($Text,$X,$Y,$Width=490) {
        $control=New-Object System.Windows.Forms.TextBox
        $control.Text=$Text;$control.SetBounds($X,$Y,$Width,26);$form.Controls.Add($control);return $control
    }

    Add-Label 'Anwendungen' 16 70 278
    Add-Label 'Ziel / Dateiformat' 304 70 120
    Add-Label 'Mit .NET' 424 70 102
    Add-Label 'Ohne .NET' 534 70 102
    Add-Label 'Release-Zusaetze' 656 70 332
    $applicationNames = Get-PublishApplicationNames
    $applications=New-Object System.Windows.Forms.CheckedListBox
    $applications.SetBounds(16,94,268,80);$applications.CheckOnClick=$true
    foreach($item in $applicationNames.Keys){[void]$applications.Items.Add($applicationNames[$item],($item -in $Config.Applications))}
    $form.Controls.Add($applications)
    $matrixRows = [ordered]@{}
    $outputModes = [ordered]@{ Off='Aus'; Zip='ZIP'; Folder='Ordner'; Both='ZIP + Ordner' }
    $rowTop = 94
    $matrixTargets = @((New-PublishConfiguration).RuntimeIdentifiers) + @(Get-PublishFormatTargets | Where-Object { $_ -notlike '*-exe' })
    foreach ($target in $matrixTargets) {
        $label = switch -Wildcard ($target) {
            'win-*' { "$target-exe" }
            '*-appimage' { $target }
            '*-dmg' { $target }
            default { "$target-bin" }
        }
        Add-Label $label 304 ($rowTop+2) 120
        $row = @{}
        $x = 424
        foreach ($variant in @('WithDotNet','WithoutDotNet')) {
            $field = New-Object System.Windows.Forms.ComboBox
            $field.SetBounds($x,$rowTop,102,24)
            $field.DropDownStyle = 'DropDownList'
            foreach ($mode in $outputModes.Values) { [void]$field.Items.Add($mode) }
            $field.AccessibleName = "$label $(if ($variant -eq 'WithDotNet') { 'mit .NET' } else { 'ohne .NET' })"
            $form.Controls.Add($field)
            $row[$variant] = $field
            $x += 110
        }
        $matrixRows[$target] = $row
        $rowTop += 23
    }
    $loadMatrix = {
        param($Value)
        $matrix = $guiApi.GetOutputMatrix($Value)
        foreach ($target in $matrixRows.Keys) {
            foreach ($variant in @('WithDotNet','WithoutDotNet')) {
                $mode = $matrix.$target.$variant
                $matrixRows[$target][$variant].SelectedItem = if ($mode) { $outputModes[$mode] } else { $outputModes.Off }
            }
        }
    }.GetNewClosure()
    & $loadMatrix $Config
    $extras=New-Object System.Windows.Forms.CheckedListBox
    $extras.CheckOnClick=$true
    $extras.IntegralHeight=$false
    $extraPackageFields = [ordered]@{}
    $extraPackageTop = 94
    foreach ($extra in @('Explorer','Transition')) {
        Add-Label $extra 656 ($extraPackageTop+2) 155
        $field = New-Object System.Windows.Forms.ComboBox
        $field.SetBounds(812,$extraPackageTop,176,24)
        $field.DropDownStyle = 'DropDownList'
        $field.AccessibleName = "$extra Ausgabe"
        foreach ($mode in $outputModes.Values) { [void]$field.Items.Add($mode) }
        $form.Controls.Add($field)
        $extraPackageFields[$extra] = $field
        $extraPackageTop += 26
    }
    $loadExtraPackageFormats = {
        param($Value)
        foreach ($extra in $extraPackageFields.Keys) {
            $mode = 'Off'
            if ($Value.IncludeReleaseExtras -or $extra -in $Value.Extras) {
                $format = $Value."${extra}OutputFormat"
                $mode = if ($format) { $format } else { 'Zip' }
            }
            $extraPackageFields[$extra].SelectedItem = $outputModes[$mode]
        }
    }.GetNewClosure()
    & $loadExtraPackageFormats $Config
    $extraItems = [ordered]@{
        'Updatemanifest' = 'Updatemanifest'
        'WPF-Migrations-ZIPs + Updatemanifest' = 'WpfMigration'
        'Avalonia-JSON je ZIP' = 'AvaloniaZipMetadata'
        'Firmware-Hash-ZIP' = $null
    }
    $extraChecks = {
        param($Value)
        foreach ($item in $extraItems.Keys) {
            if ($null -eq $extraItems[$item]) { [bool]$Value.FirmwareHashes }
            elseif ($extraItems[$item] -in @('AvaloniaZipMetadata','WpfMigration')) { $extraItems[$item] -in $Value.Extras }
            else { $Value.IncludeReleaseExtras -or $extraItems[$item] -in $Value.Extras -or ($item -eq 'Updatemanifest' -and 'Updater' -in $Value.Extras) }
        }
    }.GetNewClosure()
    $checkedExtras = @(& $extraChecks $Config)
    $index = 0
    foreach ($item in $extraItems.Keys) {
        [void]$extras.Items.Add($item, $checkedExtras[$index])
        $index++
    }
    $extras.SetBounds(656,($extraPackageTop+4),332,($extras.Items.Count * $extras.ItemHeight + 6))
    $form.Controls.Add($extras)
    $extraNotesTop = $extras.Bottom + 5
    Add-Label 'Explorer: Windows x64/ARM64, WPF und Avalonia' 656 $extraNotesTop 332
    Add-Label 'Transition: Windows x64/x86' 656 ($extraNotesTop+20) 332
    Add-Label 'Update-Dateien verwenden vorhandene ZIPs.' 656 ($extraNotesTop+40) 332
    Add-Label 'AppImage auf Linux, DMG auf macOS bauen' 304 ($rowTop+4) 332

    $foldersTop = [Math]::Max($rowTop+32, $extraNotesTop+68)
    Add-Label 'Ausgabeordner' 16 $foldersTop 460
    $output=Add-Text $Config.OutputDirectory 16 ($foldersTop+22) 460
    $copy=Add-Check 'Programmdateien in Testordner kopieren' 496 $foldersTop $Config.CopyExecutables
    $copyFolder=Add-Text $Config.CopyDirectory 496 ($foldersTop+22) 492
    Add-Label 'Variante fuer den Testordner' 496 ($foldersTop+54) 180
    $copyRid=New-Object System.Windows.Forms.ComboBox
    $copyRid.DropDownStyle='DropDownList';$copyRid.SetBounds(680,($foldersTop+50),132,26)
    foreach($item in (New-PublishConfiguration).RuntimeIdentifiers){[void]$copyRid.Items.Add($item)}
    $copyRid.SelectedItem=$Config.CopyRuntimeIdentifier;$form.Controls.Add($copyRid)
    $copyNet=Add-Check 'Mit .NET' 828 ($foldersTop+50) $Config.CopyWithDotNet
    $copyNet.Width = 160

    $versionsTop = $foldersTop + 83
    Add-Label 'Versionsuebersicht' 16 $versionsTop 200
    $editVersions = New-Object System.Windows.Forms.Button
    $editVersions.Text = 'Versionen bearbeiten...'
    $editVersions.SetBounds(788, ($versionsTop-4), 200, 26)
    $form.Controls.Add($editVersions)
    $projectVersions = Get-PublishProjectVersions $RepositoryRoot
    $versionLabels = [ordered]@{
        WpfViewer=$applicationNames.WpfViewer
        WpfLogViewer=$applicationNames.WpfLogViewer
        AvaloniaViewer=$applicationNames.AvaloniaViewer
        AvaloniaLogViewer=$applicationNames.AvaloniaLogViewer
        Transition='Transition'
        Explorer='Explorer'
    }
    $versionColumns = [ordered]@{ Projekt=192; Profil=460; Build=728 }
    $commandLineVersions = [ordered]@{}
    $versionFields = @{}
    foreach ($column in $versionColumns.Keys) {
        Add-Label $column $versionColumns[$column] ($versionsTop+24) 260
        $versionFields[$column] = @{}
    }
    $y = $versionsTop + 46
    foreach ($key in $versionLabels.Keys) {
        Add-Label $versionLabels[$key] 16 ($y+2) 170
        foreach ($column in $versionColumns.Keys) {
            $field = Add-Text '' $versionColumns[$column] $y 260
            $field.ReadOnly = $true
            $field.TabStop = $false
            $versionFields[$column][$key] = $field
        }
        if ($Version -and $key -ne 'Explorer') { $commandLineVersions[$key] = $Version }
        $y += 24
    }
    $versionState = @{ Profile=$Config.Versions; Build=[pscustomobject]$commandLineVersions }
    $refreshVersions = {
        foreach ($key in $versionLabels.Keys) {
            $versionFields.Projekt[$key].Text = $projectVersions[$key]
            $versionFields.Profil[$key].Text = if ($versionState.Profile.$key) { $versionState.Profile.$key } else { '-' }
            $versionFields.Build[$key].Text = if ($versionState.Build.$key) { $versionState.Build.$key } else { '-' }
        }
    }.GetNewClosure()
    & $refreshVersions
    $versionDialog = ${function:Show-PublishVersionDialog}
    Add-Label ('Konfiguration: '+$ConfigPath) 16 ($y+8) 972
    $status=New-Object System.Windows.Forms.Label
    $status.SetBounds(16,($y+30),972,30);$form.Controls.Add($status)
    $buttonsTop = $status.Bottom
    Add-Label 'Auswahlprofil / neuer Profilname' 16 6 620
    $profileStore=Read-PublishProfileStore $ConfigPath
    if(!$Profile){$Profile=$profileStore.ActiveProfile}
    $profileBox=New-Object System.Windows.Forms.ComboBox
    $profileBox.SetBounds(16,30,620,26);$profileBox.DropDownStyle='DropDown'
    foreach($name in $profileStore.Profiles.PSObject.Properties.Name){[void]$profileBox.Items.Add($name)}
    $profileBox.Text=$Profile;$form.Controls.Add($profileBox);$form.Text="NxFileViewer - Publish - $Profile"
    $loadProfile={
        try{
            $value=$guiApi.ReadConfiguration($ConfigPath,$profileBox.Text)
            $applicationKeys = @($applicationNames.Keys)
            for ($i=0; $i -lt $applications.Items.Count; $i++) {
                $applications.SetItemChecked($i, ($applicationKeys[$i] -in $value.Applications))
            }
            & $loadMatrix $value
            & $loadExtraPackageFormats $value
            $checkedExtras = @(& $extraChecks $value)
            for ($i=0; $i -lt $extras.Items.Count; $i++) {
                $extras.SetItemChecked($i, $checkedExtras[$i])
            }
            $versionState.Profile = $value.Versions
            $versionState.Build = [pscustomobject]$commandLineVersions
            & $refreshVersions
            $output.Text=$value.OutputDirectory;$copy.Checked=$value.CopyExecutables;$copyFolder.Text=$value.CopyDirectory
            $copyRid.SelectedItem=$value.CopyRuntimeIdentifier;$copyNet.Checked=$value.CopyWithDotNet
            $form.Text="NxFileViewer - Publish - $($profileBox.Text)"
            $status.Text="Profil '$($profileBox.Text)' geladen. Bauen startet die Auswahl."
        }catch{$status.Text=$_.Exception.GetBaseException().Message}
    }.GetNewClosure()
    $profileBox.Add_SelectedIndexChanged({if($profileBox.SelectedIndex -ge 0){& $loadProfile}}.GetNewClosure())
    Add-Label 'Startprofil (ohne -Profile)' 656 6 332
    $startProfileBox=New-Object System.Windows.Forms.ComboBox
    $startProfileBox.SetBounds(656,30,332,26);$startProfileBox.DropDownStyle='DropDownList'
    foreach($name in $profileStore.Profiles.PSObject.Properties.Name){[void]$startProfileBox.Items.Add($name)}
    $startProfileBox.SelectedItem=$profileStore.Profiles.PSObject.Properties[$profileStore.ActiveProfile].Name;$form.Controls.Add($startProfileBox)
    $startProfileBox.Add_SelectedIndexChanged({
        try{
            $guiApi.SetStartProfile($ConfigPath,[string]$startProfileBox.SelectedItem)
            $status.Text="Startprofil '$($startProfileBox.SelectedItem)' gespeichert. Gilt fuer Aufrufe ohne -Profile."
        }catch{$status.Text=$_.Exception.GetBaseException().Message}
    }.GetNewClosure())
    $readControls={
        param([bool]$ForBuild=$true)
        $result=$guiApi.NewConfiguration()

        $result.Applications = @(foreach ($key in $applicationNames.Keys) {
            if ($applications.CheckedItems.Contains($applicationNames[$key])) { $key }
        })
        $selected = [ordered]@{}
        $modeKeys = @($outputModes.Keys)
        foreach ($target in $matrixRows.Keys) {
            $row = $matrixRows[$target]
            $with = $modeKeys[$row.WithDotNet.SelectedIndex]
            $without = $modeKeys[$row.WithoutDotNet.SelectedIndex]
            if ($with -ne 'Off' -or $without -ne 'Off') {
                $selected[$target] = [pscustomobject]@{ WithDotNet=$with; WithoutDotNet=$without }
            }
        }
        $result.TargetOutputs = [pscustomobject]$selected
        $result.RuntimeIdentifiers = @($selected.Keys)
        $result.WithDotNet = @($selected.Values | Where-Object { $_.WithDotNet -ne 'Off' }).Count -gt 0
        $result.WithoutDotNet = @($selected.Values | Where-Object { $_.WithoutDotNet -ne 'Off' }).Count -gt 0
        $result.CreateZipArchives = @($selected.Values | Where-Object { $_.WithDotNet -in @('Zip','Both') -or $_.WithoutDotNet -in @('Zip','Both') }).Count -gt 0
        $result.FirmwareHashes = $extras.CheckedItems.Contains('Firmware-Hash-ZIP')
        $result.Extras = @(
            foreach ($extra in $extraPackageFields.Keys) {
                $mode = $modeKeys[$extraPackageFields[$extra].SelectedIndex]
                if ($mode -ne 'Off') {
                    $result."${extra}OutputFormat" = $mode
                    $extra
                }
            }
            foreach ($item in $extras.CheckedItems) {
                if ($extraItems[$item]) { $extraItems[$item] }
            }
        )
        foreach ($entry in $versionState.Profile.PSObject.Properties) {
            $result.Versions | Add-Member -NotePropertyName $entry.Name -NotePropertyValue $entry.Value
        }
        if ($ForBuild) {
            foreach ($entry in $versionState.Build.PSObject.Properties) {
                $result.Versions | Add-Member -NotePropertyName $entry.Name -NotePropertyValue $entry.Value -Force
            }
        }
        $result.OutputDirectory=$output.Text;$result.CopyExecutables=$copy.Checked
        $result.CopyDirectory=$copyFolder.Text;$result.CopyRuntimeIdentifier=[string]$copyRid.SelectedItem;$result.CopyWithDotNet=$copyNet.Checked
        $guiApi.ValidateConfiguration($result)
        return $result
    }.GetNewClosure()
    $save=New-Object System.Windows.Forms.Button;$save.Text='Profil speichern';$save.SetBounds(16,$buttonsTop,140,36)
    $save.Add_Click({try{$value=& $readControls $false;$guiApi.SaveConfiguration($value,$ConfigPath,$profileBox.Text);if($profileBox.Text -notin $profileBox.Items){[void]$profileBox.Items.Add($profileBox.Text)};if($profileBox.Text -notin $startProfileBox.Items){[void]$startProfileBox.Items.Add($profileBox.Text)};$status.Text="Profil '$($profileBox.Text)' gespeichert. Startprofil bleibt '$($startProfileBox.SelectedItem)'."}catch{$status.Text=$_.Exception.GetBaseException().Message}}.GetNewClosure())
    $form.Controls.Add($save)
    $run=New-Object System.Windows.Forms.Button;$run.Text='Bauen';$run.SetBounds(176,$buttonsTop,140,36)
    $run.Add_Click({try{$value=& $readControls;$form.Tag=$value;$form.DialogResult=[System.Windows.Forms.DialogResult]::OK;$form.Close()}catch{$status.Text=$_.Exception.GetBaseException().Message}}.GetNewClosure())
    $form.Controls.Add($run)
    $cancel=New-Object System.Windows.Forms.Button;$cancel.Text='Abbrechen';$cancel.SetBounds(336,$buttonsTop,140,36)
    $cancel.DialogResult=[System.Windows.Forms.DialogResult]::Cancel;$form.CancelButton=$cancel;$form.Controls.Add($cancel)
    $workingArea = [System.Windows.Forms.Screen]::FromPoint([System.Windows.Forms.Cursor]::Position).WorkingArea
    $clientSize = New-Object System.Drawing.Size(1004, ($cancel.Bottom + 12))
    $form.ClientSize = $clientSize
    $windowSize = $form.Size
    $form.Size = New-Object System.Drawing.Size(
        [Math]::Min($windowSize.Width, $workingArea.Width),
        [Math]::Min($windowSize.Height, $workingArea.Height)
    )
    $form.AutoScrollMinSize = $clientSize
    $autoState=[pscustomobject]@{HasInput=$false}
    $clock=[Diagnostics.Stopwatch]::new()
    $timer=New-Object System.Windows.Forms.Timer
    $timer.Interval=200
    $form.KeyPreview=$true
    $status.Text='Automatischer Build in 10 Sekunden. Tastatur, Klick oder Aenderung beendet den Autostart.'
    $stopAuto={
        if(!$autoState.HasInput){
            $autoState.HasInput=$true;$timer.Stop()
            $status.Text='Autostart beendet. Einstellungen bearbeiten oder Bauen waehlen.'
        }
    }.GetNewClosure()
    $editVersions.Add_Click($stopAuto)
    $editVersions.Add_Click({
        $profileValues = @{}
        $buildValues = @{}
        foreach ($entry in $versionState.Profile.PSObject.Properties) { $profileValues[$entry.Name] = $entry.Value }
        foreach ($entry in $versionState.Build.PSObject.Properties) { $buildValues[$entry.Name] = $entry.Value }
        $changes = & $versionDialog $form $guiApi $projectVersions $profileValues $buildValues $versionLabels $RepositoryRoot
        if ($changes) {
            $versionState.Profile = $changes.Profile
            $versionState.Build = $changes.Build
            & $refreshVersions
        }
    }.GetNewClosure())
    $form.Add_KeyDown($stopAuto)
    $form.Add_MouseDown($stopAuto)
    $form.Add_MouseWheel($stopAuto)
    foreach($control in $form.Controls){
        $control.Add_PreviewKeyDown($stopAuto)
        $control.Add_MouseDown($stopAuto)
        $control.Add_MouseWheel($stopAuto)
        if($control -is [System.Windows.Forms.TextBox]){$control.Add_TextChanged($stopAuto)}
        elseif($control -is [System.Windows.Forms.CheckBox]){$control.Add_CheckedChanged($stopAuto)}
        elseif($control -is [System.Windows.Forms.CheckedListBox]){$control.Add_ItemCheck($stopAuto)}
        elseif($control -is [System.Windows.Forms.ComboBox]){$control.Add_SelectionChangeCommitted($stopAuto);$control.Add_TextChanged($stopAuto)}
    }
    $countdownFunction=${function:Get-PublishAutoStartRemaining}
    $timer.Add_Tick({
        $remaining=& $countdownFunction $clock.Elapsed.TotalSeconds $autoState.HasInput
        if($remaining -lt 0){$timer.Stop();return}
        $status.Text="Automatischer Build in $remaining Sekunden. Eingabe beendet den Autostart."
        if($remaining -eq 0){
            $timer.Stop()
            try{
                $value=& $readControls
                $form.Tag=$value;$form.DialogResult=[System.Windows.Forms.DialogResult]::OK;$form.Close()
            }catch{$autoState.HasInput=$true;$status.Text=$_.Exception.GetBaseException().Message}
        }
    }.GetNewClosure())
    $form.Add_Shown({$clock.Start();$timer.Start()}.GetNewClosure())
    $form.Add_FormClosing({$timer.Stop()}.GetNewClosure())
    try{if($form.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK){return $form.Tag};return $null}
    finally{$timer.Stop();$timer.Dispose();$clock.Stop();$form.Dispose()}
}
