# JSON-only local publishing preferences. No configuration content is executed.
function New-PublishConfiguration {
    [pscustomobject][ordered]@{
        SchemaVersion=1
        Applications=@('WpfViewer','WpfLogViewer','AvaloniaViewer','AvaloniaLogViewer')
        RuntimeIdentifiers=@('win-x64','win-x86','win-arm64','linux-x64','linux-arm64','osx-x64','osx-arm64')
        WithDotNet=$true; WithoutDotNet=$true; CreateZipArchives=$true
        OutputDirectory='Publish'; FirmwareHashes=$true; IncludeReleaseExtras=$false
        CopyExecutables=$false; CopyDirectory='C:\tmp\NxFileViewerTest'
        CopyRuntimeIdentifier='win-x64'; CopyWithDotNet=$false
    }
}
function New-PredefinedPublishProfiles {
    $avalonia=New-PublishConfiguration
    $avalonia.Applications=@('AvaloniaViewer','AvaloniaLogViewer')
    $avalonia.OutputDirectory='Publish/Avalonia';$avalonia.FirmwareHashes=$false
    $wpf=New-PublishConfiguration
    $wpf.Applications=@('WpfViewer','WpfLogViewer')
    $wpf.RuntimeIdentifiers=@('win-x64','win-x86','win-arm64')
    $wpf.OutputDirectory='Publish/WPF';$wpf.FirmwareHashes=$false
    $testWin=New-PublishConfiguration
    $testWin.RuntimeIdentifiers=@('win-x64')
    $testWin.WithDotNet=$false;$testWin.WithoutDotNet=$true
    $testWin.FirmwareHashes=$false;$testWin.CopyExecutables=$true;$testWin.CreateZipArchives=$false
    $standard=New-PublishConfiguration
    return [pscustomobject][ordered]@{Avalonia=$avalonia;WPF=$wpf;'Test Win'=$testWin;Standard=$standard}
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
function Assert-PublishConfiguration($Config) {
    $defaults=New-PublishConfiguration
    if ($null -eq $Config -or $Config.SchemaVersion -ne 1) { throw 'Unsupported publish configuration schema.' }
    foreach ($property in $Config.PSObject.Properties) {
        if ($property.Name -notin $defaults.PSObject.Properties.Name) { throw "Unknown publish setting: $($property.Name)" }
    }
    foreach ($name in $defaults.PSObject.Properties.Name) {
        if ($name -notin $Config.PSObject.Properties.Name) { throw "Missing publish setting: $name" }
    }
    foreach ($name in @('WithDotNet','WithoutDotNet','CreateZipArchives','FirmwareHashes','CopyExecutables','CopyWithDotNet','IncludeReleaseExtras')) {
        if ($Config.$name -isnot [bool]) { throw "$name must be true or false." }
    }
    if (!$Config.WithDotNet -and !$Config.WithoutDotNet) { throw 'Select at least one .NET variant.' }
    if ($Config.Applications -isnot [array] -or !$Config.Applications.Count -or $Config.Applications.Count -gt 4) { throw 'Select at least one application.' }
    foreach ($app in $Config.Applications) { if ($app -notin $defaults.Applications) { throw "Unknown application: $app" } }
    if ($Config.RuntimeIdentifiers -isnot [array] -or !$Config.RuntimeIdentifiers.Count -or $Config.RuntimeIdentifiers.Count -gt 7) { throw 'Select at least one target platform/architecture.' }
    foreach ($rid in $Config.RuntimeIdentifiers) { if ($rid -notin $defaults.RuntimeIdentifiers) { throw "Unknown runtime: $rid" } }
    if (@($Config.Applications | Where-Object { $_ -like 'Wpf*' }).Count -and !@($Config.RuntimeIdentifiers | Where-Object { $_ -like 'win-*' }).Count) { throw 'WPF applications need at least one Windows architecture.' }
    if ($Config.IncludeReleaseExtras) {
        if ('WpfViewer' -notin $Config.Applications -or 'WpfLogViewer' -notin $Config.Applications -or
            @(@('win-x64','win-x86','win-arm64') | Where-Object { $_ -notin $Config.RuntimeIdentifiers }).Count -or
            !$Config.WithDotNet -or !$Config.WithoutDotNet -or !$Config.CreateZipArchives -or !$Config.FirmwareHashes -or $Config.CopyExecutables) {
            throw 'Release extras require both WPF apps, all Windows architectures, both .NET variants, ZIPs and firmware hashes, with test copying disabled.'
        }
    }
    foreach ($name in @('OutputDirectory','CopyDirectory','CopyRuntimeIdentifier')) {
        if ($Config.$name -isnot [string] -or [string]::IsNullOrWhiteSpace($Config.$name) -or $Config.$name.Length -gt 2048 -or $Config.$name -match '[\x00-\x1f]') { throw "Invalid $name." }
    }
    if ($Config.CopyRuntimeIdentifier -notin $defaults.RuntimeIdentifiers) { throw 'Invalid copy runtime.' }
    if ($Config.CopyExecutables) {
        if ($Config.CopyRuntimeIdentifier -notin $Config.RuntimeIdentifiers -or
            ($Config.CopyWithDotNet -and !$Config.WithDotNet) -or (!$Config.CopyWithDotNet -and !$Config.WithoutDotNet)) { throw 'The copied variant must also be selected for building.' }
        if (@($Config.Applications | Where-Object { $_ -like 'Wpf*' }).Count -and $Config.CopyRuntimeIdentifier -notlike 'win-*') { throw 'Copying WPF applications requires a Windows copy target.' }
    }
}
function Assert-PublishProfileName([string]$Name) {
    if ([string]::IsNullOrWhiteSpace($Name) -or $Name.Length -gt 60 -or $Name -cnotmatch '^[\p{L}\p{N}][\p{L}\p{N} ._-]*$' -or $Name -ne $Name.Trim()) { throw 'Use a profile name of 1-60 letters, numbers, spaces, dots, underscores or hyphens.' }
}
function Read-PublishProfileStore([string]$Path) {
    if (!(Test-Path -LiteralPath $Path)) {
        return [pscustomobject]@{SchemaVersion=2;ActiveProfile='Standard';Profiles=(New-PredefinedPublishProfiles)}
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
        $rids=@($Config.RuntimeIdentifiers | Where-Object { $framework -ne 'Wpf' -or $_ -like 'win-*' })
        $options=@{OutputDirectory=$Config.OutputDirectory;Applications=$apps;RuntimeIdentifiers=$rids;RuntimeVariants=$runtimeVariants;AllTargets=$true;NoCopy=(!$Config.CopyExecutables);SkipFirmwareHashes=(!$Config.FirmwareHashes -or $firmwareBuilt)}
        if($Version){$options.Version=$Version}
        if(!$Config.CreateZipArchives){$options.ExecutableOnly=$true}
        if($Config.CopyExecutables){$options.CopyTo=$Config.CopyDirectory;$options.CopyRuntime=$Config.CopyRuntimeIdentifier;$options.CopyRuntimeIncludesDotNet=$Config.CopyWithDotNet}
        & (Join-Path $RepositoryRoot 'tools/Publish-Applications.ps1') -Framework $framework @options
        if($Config.CreateZipArchives -and $Config.FirmwareHashes){$firmwareBuilt=$true}
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
    foreach($name in @('New-PublishConfiguration','New-PredefinedPublishProfiles','Add-PredefinedPublishProfiles','Assert-PublishConfiguration','Assert-PublishProfileName','Read-PublishProfileStore','Read-PublishConfiguration','Save-PublishConfiguration','Set-PublishStartProfile')) {
        $definitions[$name]=(Get-Command $name -CommandType Function -ErrorAction Stop).Definition
    }
    New-Module -AsCustomObject -ArgumentList $definitions -ScriptBlock {
        param($Definitions)
        foreach($name in $Definitions.Keys) { Set-Item -Path ("Function:"+$name) -Value ([scriptblock]::Create($Definitions[$name])) }
        function NewConfiguration { New-PublishConfiguration }
        function ValidateConfiguration($Config) { Assert-PublishConfiguration $Config }
        function ReadConfiguration([string]$Path,[string]$Profile) { Read-PublishConfiguration $Path $Profile }
        function SaveConfiguration($Config,[string]$Path,[string]$Profile) { Save-PublishConfiguration $Config $Path $Profile $false }
        function SetStartProfile([string]$Path,[string]$Profile) { Set-PublishStartProfile $Path $Profile }
        Export-ModuleMember -Function NewConfiguration,ValidateConfiguration,ReadConfiguration,SaveConfiguration,SetStartProfile
    }
}
function Show-PublishConfigurationMenu($Config,[string]$ConfigPath,[string]$Profile) {
    if([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT){throw '-Gui requires Windows. Edit publish.ps1.conf or use command-line selectors on other platforms.'}
    $guiApi=New-PublishGuiApi
    Add-Type -AssemblyName System.Windows.Forms
    Add-Type -AssemblyName System.Drawing
    $form=New-Object System.Windows.Forms.Form
    $form.Text='NxFileViewer - Publish';$form.Width=720;$form.Height=880
    $form.StartPosition='CenterScreen';$form.AutoScroll=$true
    $form.MaximumSize=New-Object System.Drawing.Size(1200,([System.Windows.Forms.Screen]::PrimaryScreen.WorkingArea.Height))
    function Add-Label($Text,$X,$Y,$Width=310) {
        $control=New-Object System.Windows.Forms.Label
        $control.Text=$Text;$control.SetBounds($X,$Y,$Width,24);$form.Controls.Add($control)
    }
    function Add-Check($Text,$X,$Y,$Checked) {
        $control=New-Object System.Windows.Forms.CheckBox
        $control.Text=$Text;$control.SetBounds($X,$Y,320,26);$control.Checked=$Checked;$form.Controls.Add($control);return $control
    }
    function Add-Text($Text,$X,$Y,$Width=490) {
        $control=New-Object System.Windows.Forms.TextBox
        $control.Text=$Text;$control.SetBounds($X,$Y,$Width,26);$form.Controls.Add($control);return $control
    }
    Add-Label 'Anwendungen' 16 14
    Add-Label 'Betriebssystem / Architektur' 350 14
    $applications=New-Object System.Windows.Forms.CheckedListBox
    $applications.SetBounds(16,40,315,130);$applications.CheckOnClick=$true
    foreach($item in @('WpfViewer','WpfLogViewer','AvaloniaViewer','AvaloniaLogViewer')){[void]$applications.Items.Add($item,($item -in $Config.Applications))}
    $form.Controls.Add($applications)
    $runtimes=New-Object System.Windows.Forms.CheckedListBox
    $runtimes.SetBounds(350,40,330,180);$runtimes.CheckOnClick=$true
    foreach($item in (New-PublishConfiguration).RuntimeIdentifiers){[void]$runtimes.Items.Add($item,($item -in $Config.RuntimeIdentifiers))}
    $form.Controls.Add($runtimes)
    $withNet=Add-Check 'Mit .NET (self-contained)' 16 180 $Config.WithDotNet
    $withoutNet=Add-Check 'Ohne .NET' 16 210 $Config.WithoutDotNet
    $zip=Add-Check 'ZIPs erstellen (aus: nur Programmdateien)' 16 250 $Config.CreateZipArchives
    $firmware=Add-Check 'Firmware-Hash-ZIP erstellen' 350 250 $Config.FirmwareHashes
    Add-Label 'Ausgabeordner' 16 290
    $output=Add-Text $Config.OutputDirectory 16 316 664
    $copy=Add-Check 'Programmdateien in Testordner kopieren' 16 355 $Config.CopyExecutables
    Add-Label 'Testordner' 16 390
    $copyFolder=Add-Text $Config.CopyDirectory 16 416 664
    Add-Label 'Genau diese gebaute Variante kopieren:' 16 452
    $copyRid=New-Object System.Windows.Forms.ComboBox
    $copyRid.DropDownStyle='DropDownList';$copyRid.SetBounds(16,480,260,28)
    foreach($item in (New-PublishConfiguration).RuntimeIdentifiers){[void]$copyRid.Items.Add($item)}
    $copyRid.SelectedItem=$Config.CopyRuntimeIdentifier;$form.Controls.Add($copyRid)
    $copyNet=Add-Check 'Kopierte Variante mit .NET' 300 480 $Config.CopyWithDotNet
    $extras=Add-Check 'Release-Zusaetze: Explorer / Transition / Updater' 16 526 $Config.IncludeReleaseExtras
    $extras.Width=660
    Add-Label ('Konfiguration: '+$ConfigPath) 16 568 664
    $status=New-Object System.Windows.Forms.Label
    $status.SetBounds(16,596,664,54);$form.Controls.Add($status)
    foreach($control in $form.Controls){$control.Top+=60}
    Add-Label 'Auswahlprofil / neuer Profilname' 16 6 410
    $profileStore=Read-PublishProfileStore $ConfigPath
    if(!$Profile){$Profile=$profileStore.ActiveProfile}
    $profileBox=New-Object System.Windows.Forms.ComboBox
    $profileBox.SetBounds(16,30,410,26);$profileBox.DropDownStyle='DropDown'
    foreach($name in $profileStore.Profiles.PSObject.Properties.Name){[void]$profileBox.Items.Add($name)}
    $profileBox.Text=$Profile;$form.Controls.Add($profileBox);$form.Text="NxFileViewer - Publish - $Profile"
    $loadProfile={
        try{
            $value=$guiApi.ReadConfiguration($ConfigPath,$profileBox.Text)
            for($i=0;$i -lt $applications.Items.Count;$i++){$applications.SetItemChecked($i,([string]$applications.Items[$i] -in $value.Applications))}
            for($i=0;$i -lt $runtimes.Items.Count;$i++){$runtimes.SetItemChecked($i,([string]$runtimes.Items[$i] -in $value.RuntimeIdentifiers))}
            $withNet.Checked=$value.WithDotNet;$withoutNet.Checked=$value.WithoutDotNet
            $zip.Checked=$value.CreateZipArchives;$firmware.Checked=$value.FirmwareHashes;$extras.Checked=$value.IncludeReleaseExtras
            $output.Text=$value.OutputDirectory;$copy.Checked=$value.CopyExecutables;$copyFolder.Text=$value.CopyDirectory
            $copyRid.SelectedItem=$value.CopyRuntimeIdentifier;$copyNet.Checked=$value.CopyWithDotNet
            $form.Text="NxFileViewer - Publish - $($profileBox.Text)"
            $status.Text="Profil '$($profileBox.Text)' geladen. Bauen startet die Auswahl."
        }catch{$status.Text=$_.Exception.GetBaseException().Message}
    }.GetNewClosure()
    $profileBox.Add_SelectedIndexChanged({if($profileBox.SelectedIndex -ge 0){& $loadProfile}}.GetNewClosure())
    Add-Label 'Startprofil (ohne -Profile)' 446 6 234
    $startProfileBox=New-Object System.Windows.Forms.ComboBox
    $startProfileBox.SetBounds(446,30,234,26);$startProfileBox.DropDownStyle='DropDownList'
    foreach($name in $profileStore.Profiles.PSObject.Properties.Name){[void]$startProfileBox.Items.Add($name)}
    $startProfileBox.SelectedItem=$profileStore.Profiles.PSObject.Properties[$profileStore.ActiveProfile].Name;$form.Controls.Add($startProfileBox)
    $startProfileBox.Add_SelectedIndexChanged({
        try{
            $guiApi.SetStartProfile($ConfigPath,[string]$startProfileBox.SelectedItem)
            $status.Text="Startprofil '$($startProfileBox.SelectedItem)' gespeichert. Gilt fuer Aufrufe ohne -Profile."
        }catch{$status.Text=$_.Exception.GetBaseException().Message}
    }.GetNewClosure())
    $readControls={
        $result=$guiApi.NewConfiguration()
        $result.Applications=@($applications.CheckedItems | ForEach-Object {[string]$_})
        $result.RuntimeIdentifiers=@($runtimes.CheckedItems | ForEach-Object {[string]$_})
        $result.WithDotNet=$withNet.Checked;$result.WithoutDotNet=$withoutNet.Checked
        $result.CreateZipArchives=$zip.Checked;$result.FirmwareHashes=$firmware.Checked;$result.IncludeReleaseExtras=$extras.Checked
        $result.OutputDirectory=$output.Text;$result.CopyExecutables=$copy.Checked
        $result.CopyDirectory=$copyFolder.Text;$result.CopyRuntimeIdentifier=[string]$copyRid.SelectedItem;$result.CopyWithDotNet=$copyNet.Checked
        $guiApi.ValidateConfiguration($result)
        return $result
    }.GetNewClosure()
    $save=New-Object System.Windows.Forms.Button;$save.Text='Profil speichern';$save.SetBounds(16,726,140,36)
    $save.Add_Click({try{$value=& $readControls;$guiApi.SaveConfiguration($value,$ConfigPath,$profileBox.Text);if($profileBox.Text -notin $profileBox.Items){[void]$profileBox.Items.Add($profileBox.Text)};if($profileBox.Text -notin $startProfileBox.Items){[void]$startProfileBox.Items.Add($profileBox.Text)};$status.Text="Profil '$($profileBox.Text)' gespeichert. Startprofil bleibt '$($startProfileBox.SelectedItem)'."}catch{$status.Text=$_.Exception.GetBaseException().Message}}.GetNewClosure())
    $form.Controls.Add($save)
    $run=New-Object System.Windows.Forms.Button;$run.Text='Bauen';$run.SetBounds(176,726,140,36)
    $run.Add_Click({try{$value=& $readControls;$form.Tag=$value;$form.DialogResult=[System.Windows.Forms.DialogResult]::OK;$form.Close()}catch{$status.Text=$_.Exception.GetBaseException().Message}}.GetNewClosure())
    $form.Controls.Add($run)
    $cancel=New-Object System.Windows.Forms.Button;$cancel.Text='Abbrechen';$cancel.SetBounds(336,726,140,36)
    $cancel.DialogResult=[System.Windows.Forms.DialogResult]::Cancel;$form.CancelButton=$cancel;$form.Controls.Add($cancel)
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
