# Shared by the workflow jobs and the Publish.ps1 workflow editor.
function Get-WorkflowPublishPlatforms {
    @(
        @{ platform='Windows'; os='windows-latest'; runtimes=@('win-x64','win-x86','win-arm64'); formats=@('Zip') }
        @{ platform='Linux'; os='ubuntu-latest'; runtimes=@('linux-x64','linux-arm64','linux-arm'); formats=@('Zip','AppImage') }
        @{ platform='macOS'; os='macos-latest'; runtimes=@('osx-x64','osx-arm64'); formats=@('Zip','App','Dmg') }
    )
}
function Get-WorkflowCompatibilityTargets {
    'compatibility win40'
    'compatibility wpf41'
    'compatibility avalonia-beta1'
}
function Get-WorkflowPublishTargets {
    foreach ($platform in Get-WorkflowPublishPlatforms) {
        foreach ($rid in $platform.runtimes) { "$($platform.platform) arch $rid" }
        foreach ($format in $platform.formats) {
            foreach ($variant in @('with-net','without-net')) { "$($platform.platform) $($format.ToLowerInvariant()) $variant" }
        }
    }
    Get-WorkflowCompatibilityTargets
}
function Get-WorkflowPublishSelection([string]$Selection) {
    $declared = @($Selection -split '\r?\n' | ForEach-Object { (($_.TrimStart() -replace '^#+\s*' -replace '#.*$').Trim()) -replace '\s+', ' ' })
    $targets = @($Selection -split '\r?\n' | ForEach-Object { (($_ -replace '#.*$').Trim()) -replace '\s+', ' ' } | Where-Object { $_ })
    $allowed = @(Get-WorkflowPublishTargets)
    $unknown = @($targets | Where-Object { $_ -ne 'compatibility' -and $_ -notin $allowed })
    if ($unknown.Count) { throw "Unknown workflow targets: $($unknown -join ', ')" }

    foreach ($platform in Get-WorkflowPublishPlatforms) {
        if (!($declared -like "$($platform.platform) arch *")) {
            $targets += @($platform.runtimes | Where-Object { $_ -ne 'linux-arm' } | ForEach-Object { "$($platform.platform) arch $_" })
        }
    }
    if (!($declared -like 'macOS dmg *')) {
        $targets += @($targets | Where-Object { $_ -like 'macOS app *' } | ForEach-Object { $_ -replace '^macOS app ', 'macOS dmg ' })
    }
    if (!($declared -like 'compatibility *') -and ('compatibility' -notin $declared -or 'compatibility' -in $targets)) {
        $targets += @(Get-WorkflowCompatibilityTargets)
    }
    $targets = @($targets | Where-Object { $_ -ne 'compatibility' })
    $targets
}
function Get-WorkflowPublishMatrix([string]$Selection) {
    $targets = @(Get-WorkflowPublishSelection $Selection)
    $platforms = @(foreach ($platform in Get-WorkflowPublishPlatforms) {
        $runtimes = @($platform.runtimes | Where-Object { "$($platform.platform) arch $_" -in $targets })
        $formats = @{}
        foreach ($format in $platform.formats) {
            $variants = @('with-net','without-net' | Where-Object { "$($platform.platform) $format $_" -in $targets })
            if ($variants.Count) { $formats[$format] = $variants }
        }
        if (!$formats.Count) { continue }
        if (!$runtimes.Count) { throw "Select an architecture for $($platform.platform)." }
        $nativePackages = @(foreach ($format in @('AppImage','App','Dmg')) {
            if (!$formats[$format]) { continue }
            $nativeRuntimes = @($runtimes | Where-Object { $_ -ne 'linux-arm' })
            if (!$nativeRuntimes.Count) { throw "$format requires a 64-bit architecture." }
            @{ format=$format; runtimes=$nativeRuntimes; variants=$formats[$format] }
        })
        @{
            platform=$platform.platform; os=$platform.os; runtimes=$runtimes
            zipVariants=@(if ($formats.Zip) { $formats.Zip })
            nativePackages=$nativePackages
            appImage = $formats.ContainsKey('AppImage')
            compatibilityExtras = @(if ('compatibility win40' -in $targets) { 'CompatibilityWin40' }
                if ('compatibility wpf41' -in $targets) { 'CompatibilityWpf41' }
                if ('compatibility avalonia-beta1' -in $targets) { 'CompatibilityAvaloniaBeta1' })
        }
    })
    if (!$platforms.Count) { throw 'Enable at least one workflow package.' }
    @{ include=$platforms }
}
function Get-WorkflowTargetBlock([string]$Source,[string]$Name) {
    $matches = [regex]::Matches($Source, "(?m)^  ${Name}: \|\r?\n(?<body>(?: {4}[^\r\n]*(?:\r?\n|$))+)")
    if ($matches.Count -ne 1) { throw "Expected one $Name block in the workflow." }
    $matches[0].Groups['body']
}
function Set-WorkflowTargetBlock([string]$Source,[string]$Name,[string[]]$Targets) {
    $block = Get-WorkflowTargetBlock $Source $Name
    $newline = if ($Source.Contains("`r`n")) { "`r`n" } else { "`n" }
    $lines = foreach ($target in Get-WorkflowPublishTargets) {
        if ($target -in $Targets) { "    $target" } else { "    ## $target" }
    }
    $replacement = ($lines -join $newline) + $newline
    $Source.Remove($block.Index,$block.Length).Insert($block.Index,$replacement)
}
