[CmdletBinding()]
param([Parameter(Mandatory)][string]$PackageDirectory)
$ErrorActionPreference = 'Stop'
$entries = @(Get-ChildItem -LiteralPath $PackageDirectory -Filter '*.zip' -File | Where-Object Name -NotLike '*LogViewer*' | Sort-Object Name | ForEach-Object {
    $framework = if ($_.Name -like '*_Avalonia_*') { 'Avalonia' } else { 'WPF' }
    [ordered]@{ AssetName=$_.Name; Framework=$framework; Sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
})
if ($entries.Count -eq 0) { throw 'No release ZIPs found.' }
$index = [ordered]@{ SchemaVersion=1; Packages=$entries }
[IO.File]::WriteAllText((Join-Path (Resolve-Path -LiteralPath $PackageDirectory).Path 'nxfileviewer-packages.json'), ($index | ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($false))
