param([string]$Path = (Join-Path $PSScriptRoot 'master-keys.json'))
$ErrorActionPreference = 'Stop'
$raw = Get-Content -LiteralPath $Path -Raw -Encoding UTF8
$c = $raw | ConvertFrom-Json
if (($c.SchemaVersion -isnot [int] -and $c.SchemaVersion -isnot [long]) -or $c.SchemaVersion -ne 1) { throw 'Unsupported master key catalog schema.' }
if ($null -eq $c.MasterKeys -or $c.MasterKeys -isnot [pscustomobject]) { throw 'MasterKeys must be an object.' }
$entries = @($c.MasterKeys.PSObject.Properties)
if ($entries.Count -eq 0) { throw 'Master key catalog must not be empty.' }
foreach ($entry in $entries) {
    if ($entry.Name -cnotmatch '^[0-9a-f]{2}$') { throw "Invalid revision: $($entry.Name)" }
    if ($entry.Value.Firmware -isnot [string] -or [string]::IsNullOrWhiteSpace($entry.Value.Firmware)) { throw "Missing firmware for revision $($entry.Name)" }
    $crc = $entry.Value.Crc32
    if (($crc -isnot [int] -and $crc -isnot [long]) -or $crc -lt 0 -or $crc -gt [uint32]::MaxValue) { throw "Invalid CRC32 for revision $($entry.Name)" }
}
# ConvertFrom-Json collapses duplicate properties. Tokenize the raw document to
# detect repeated revision names, consuming whole strings so quoted braces do not affect depth.
$depth = 0
$seen = New-Object 'System.Collections.Generic.HashSet[string]'
foreach ($token in [regex]::Matches($raw, '"(?:\\.|[^"\\])*"|[{}\[\]]')) {
    switch ($token.Value) {
        '{' { $depth++; break }
        '[' { $depth++; break }
        '}' { $depth--; break }
        ']' { $depth--; break }
        default {
            if ($depth -eq 2) {
                $name = ((('{"Value":' + $token.Value + '}') | ConvertFrom-Json).Value)
                if (!$seen.Add($name)) { throw "Duplicate master key revision: $name" }
            }
        }
    }
}
Write-Host 'Master key catalog validated.'