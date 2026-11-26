# Shared by the workflow jobs and the Publish.ps1 workflow editor.
function Get-WorkflowPublishPlatforms {
    @(
        @{ platform='Windows'; os='windows-latest'; runtimes=@('win-x64','win-x86','win-arm64'); native='' }
        @{ platform='Linux'; os='ubuntu-latest'; runtimes=@('linux-x64','linux-arm64'); native='AppImage' }
        @{ platform='macOS'; os='macos-latest'; runtimes=@('osx-x64','osx-arm64'); native='App' }
    )
}
function Get-WorkflowPublishTargets {
    foreach ($platform in Get-WorkflowPublishPlatforms) {
        $formats = @('zip')
        if ($platform.native) { $formats += $platform.native.ToLowerInvariant() }
        foreach ($format in $formats) {
            foreach ($variant in @('with-net','without-net')) { "$($platform.platform) $format $variant" }
        }
    }
}
function Get-WorkflowPublishMatrix([string]$Selection) {
    $targets = @($Selection -split '\r?\n' | ForEach-Object { (($_ -replace '#.*$').Trim()) -replace '\s+', ' ' } | Where-Object { $_ })
    $allowed = @(Get-WorkflowPublishTargets)
    $unknown = @($targets | Where-Object { $_ -notin $allowed })
    if ($unknown.Count) { throw "Unknown workflow targets: $($unknown -join ', ')" }
    $platforms = @(foreach ($platform in Get-WorkflowPublishPlatforms) {
        $platform.zipVariants = @('with-net','without-net' | Where-Object { "$($platform.platform) zip $_" -in $targets })
        $platform.nativeVariants = @('with-net','without-net' | Where-Object { "$($platform.platform) $($platform.native) $_" -in $targets })
        if (!$platform.nativeVariants.Count) { $platform.native = '' }
        if ($platform.zipVariants.Count -or $platform.nativeVariants.Count) { $platform }
    })
    if (!$platforms.Count) { throw 'Enable at least one workflow target.' }
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
