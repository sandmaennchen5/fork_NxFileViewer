param([string]$Path = (Join-Path $PSScriptRoot 'external-sources.json'))
$ErrorActionPreference = 'Stop'
if ((Get-Item -LiteralPath $Path).Length -gt 65536) { throw 'External sources file exceeds 64 KiB.' }
$config = [IO.File]::ReadAllText($Path) | ConvertFrom-Json
if ($config.SchemaVersion -ne 1) { throw 'Expected external source schema 1.' }
function Assert-Repository([object]$value) {
 if ($value -isnot [string] -or $value.Length -gt 201 -or $value -notmatch '^[A-Za-z0-9_-][A-Za-z0-9_.-]*/[A-Za-z0-9_.-]+$' -or @($value.Split('/') | Where-Object { $_ -in @('.','..') -or $_.EndsWith('.') }).Count) { throw 'Expected a GitHub owner/repository.' }
}
foreach ($location in @($config.FirmwareHashes, $config.TitleDb)) {
 if ($null -eq $location -or $location.Ref -isnot [string] -or [string]::IsNullOrWhiteSpace($location.Ref) -or $location.Ref.Length -gt 256 -or $location.Ref -match '[\x00-\x1f\\]' -or $location.Path -isnot [string] -or $location.Path.Length -gt 1024) { throw 'Invalid content location.' }
 Assert-Repository $location.Repository
 if ($location.Path.Length -gt 0) {
  foreach ($part in $location.Path.Split('/')) { if ([string]::IsNullOrWhiteSpace($part) -or $part -in @('.','..') -or $part -match '[\x00-\x1f\\?#]') { throw 'Invalid content path.' } }
 }
}
foreach ($repository in @($config.NszRepository,$config.NandRepository,$config.ExplorerRepository,$config.RustPackageRepository,$config.KronosPackageRepository)) { Assert-Repository $repository }
foreach ($template in @($config.TitlePageUrl,$config.TitleInfoApiUrl,$config.NLibApiUrl,$config.NxContentPageUrl)) {
 if ($template -isnot [string] -or !$template.Contains('{TitleId}') -or $template.Length -gt 4096 -or $template -match '[\x00-\x1f]') { throw 'Expected URL template with {TitleId}.' }
 $url = $template.Replace('{TitleId}','0100000000000000').Replace('{Language}','en')
 $uri = $null
 if ($url -match '[{}]' -or ![Uri]::TryCreate($url,[UriKind]::Absolute,[ref]$uri) -or $uri.Scheme -ne 'https' -or $uri.UserInfo -or $uri.Fragment) { throw 'Expected public HTTPS URL template without credentials or fragments.' }
}


function Assert-SourceList([object]$values) {
 if ($null -eq $values -or $values -is [string] -or $values -isnot [array] -or $values.Count -gt 8) { throw 'Expected repository source array with at most 8 entries.' }
 foreach ($source in $values) {
  if ($source -isnot [string] -or $source.Length -gt 2048 -or $source -match '\s') { throw 'Invalid repository source.' }
  if ($source -match '^github:(.+)$') { Assert-Repository $Matches[1]; continue }
  if ($source -notmatch '^(gitlab|forgejo):(https://.+)$') { throw 'Expected github:, gitlab: or forgejo: source.' }
  $uri=$null
  if (![Uri]::TryCreate($Matches[2],[UriKind]::Absolute,[ref]$uri) -or $uri.Scheme -ne 'https' -or $uri.UserInfo -or $uri.Fragment -or $uri.Query -or $uri.AbsolutePath.Trim('/').Split('/').Count -lt 2) { throw 'Expected public HTTPS repository URL.' }
 }
}
if ($config.PSObject.Properties.Name -contains 'ExplorerSources') { Assert-SourceList $config.ExplorerSources }
foreach ($location in @($config.FirmwareHashes,$config.TitleDb)) {
 if ($location.PSObject.Properties.Name -contains 'Sources') { Assert-SourceList $location.Sources }
}

if ($null -ne $config.TitleDb.Sources -and @($config.TitleDb.Sources).Count -gt 0) { throw 'TitleDB supports its GitHub repository setting only.' }
Write-Host 'External service sources validated.'
