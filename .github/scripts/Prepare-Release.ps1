<# Prepares the checked-out tag for publication; never commits or pushes changes. #>
param(
    [Parameter(Mandatory = $true)][string]$Version,
    [Parameter(Mandatory = $true)][string]$NotesPath,
    [string]$RepositoryRoot = (Join-Path $PSScriptRoot '../..')
)
$ErrorActionPreference = 'Stop'
if ($Version -cnotmatch '^(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)(?:-beta[0-9]*)?$') {
    throw 'Expected the validated release version without a v prefix.'
}
$RepositoryRoot = [IO.Path]::GetFullPath($RepositoryRoot)
$changelogPath = Join-Path $RepositoryRoot 'CHANGELOG.md'
$lines = [IO.File]::ReadAllLines($changelogPath)
$sections = [Collections.Generic.List[object]]::new()
$fence = $null
for ($i = 0; $i -lt $lines.Length; $i++) {
    $line = $lines[$i]
    if ($null -ne $fence) {
        if ($line -match ('^ {0,3}' + [regex]::Escape([string]$fence[0]) + '{' + $fence.Length + ',}\s*$')) { $fence = $null }
        continue
    }
    if ($line -match '^ {0,3}(`{3,}|~{3,})') { $fence = $Matches[1]; continue }
    if ($line -match '^ {0,3}##\s+(?<title>.+?)\s*$') {
        $sections.Add([pscustomobject]@{ Line = $i; Title = $Matches['title'] })
    }
}
$headingPattern = '^\[v?' + [regex]::Escape($Version) + '\](?:\s+-\s+\d{4}-\d{2}-\d{2})?$'
$matchingSections = @($sections | Where-Object { $_.Title -cmatch $headingPattern })
if ($matchingSections.Count -ne 1) {
    throw "CHANGELOG.md must contain exactly one section ## [$Version] (optionally followed by - YYYY-MM-DD)."
}
$start = $matchingSections[0].Line + 1
$following = @($sections | Where-Object { $_.Line -ge $start })
$end = if ($following.Count) { $following[0].Line } else { $lines.Length }
$notes = if ($end -gt $start) { ($lines[$start..($end - 1)] -join "`n").Trim() } else { '' }
if ([string]::IsNullOrWhiteSpace($notes)) { throw "The changelog section for $Version is empty." }

$projectPath = Join-Path $RepositoryRoot 'src/NxFileViewer/NxFileViewer.csproj'
$project = [IO.File]::ReadAllText($projectPath)
[xml]$xml = $project
$versionNodes = @($xml.SelectNodes('/Project/PropertyGroup/Version'))
$versionPattern = '<Version>\s*[^<]*\s*</Version>'
if ($versionNodes.Count -ne 1 -or [regex]::Matches($project, $versionPattern).Count -ne 1) {
    throw 'Expected exactly one application Version property.'
}
$previous = $versionNodes[0].InnerText
if ($previous -cne $Version) {
    $project = [regex]::Replace($project, $versionPattern, "<Version>$Version</Version>")
    [IO.File]::WriteAllText($projectPath, $project, [Text.UTF8Encoding]::new($false))
    Write-Host "Corrected application version for this build: $previous -> $Version"
} else {
    Write-Host "Application version already matches $Version."
}
[IO.File]::WriteAllText($NotesPath, $notes + "`n", [Text.UTF8Encoding]::new($false))
Write-Host "Release notes extracted from CHANGELOG.md [$Version]."
