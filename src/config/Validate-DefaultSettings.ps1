param([string]$Path = (Join-Path $PSScriptRoot 'default-settings.json'))
$ErrorActionPreference = 'Stop'
$c = Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json
if (($c.SchemaVersion -isnot [int] -and $c.SchemaVersion -isnot [long]) -or $c.SchemaVersion -ne 1) { throw 'Unsupported default settings schema.' }
$source = Get-Content (Join-Path $PSScriptRoot '../NxFileViewer/Settings/AppSettings.cs') -Raw
$matches = [regex]::Matches($source, 'DefaultSettings.Get<([^>]+)>\("([^"]+)", "([^"]+)"\)')
foreach ($section in @('Settings','RenamingOptions')) {
    $expected = @($matches | Where-Object { $_.Groups[2].Value -eq $section } | ForEach-Object { $_.Groups[3].Value })
    if ($null -eq $c.$section -or $c.$section -isnot [pscustomobject]) { throw "Missing section $section" }
    foreach ($property in $c.$section.PSObject.Properties) {
        if ($property.Name -cnotin $expected) { throw "Unknown default: $section.$($property.Name)" }
    }
}
foreach ($m in $matches) {
    $type=$m.Groups[1].Value; $section=$m.Groups[2].Value; $name=$m.Groups[3].Value
    if ($name -cnotin $c.$section.PSObject.Properties.Name) { throw "Missing default: $section.$name" }
    $value=$c.$section.$name
    if ($null -eq $value) { throw "Null default: $section.$name" }
    switch ($type) {
        'bool' { if ($value -isnot [bool]) { throw "$name must be true or false." } }
        'int' {
            if ($value -isnot [int] -and $value -isnot [long]) { throw "$name must be an integer." }
            $min=1;$max=100
            if ($name -eq 'NszCompressionLevel') { $max=22 }
            if ($name -eq 'NszBlockSizeExponent') { $min=14;$max=32 }
            if ($value -lt $min -or $value -gt $max) { throw "$name must be between $min and $max." }
        }
        { $_ -in @('string','string?') } { if ($value -isnot [string]) { throw "$name must be a string." } }
        default {
            $allowed = switch ($type) {
                'LogLevel' { @('Trace','Debug','Information','Warning','Error','Critical','None') }
                default {
                    $file = Get-ChildItem (Join-Path $PSScriptRoot '../NxFileViewer') -Recurse -Filter "$type.cs" | Select-Object -First 1
                    if (!$file) { throw "Enum definition missing: $type" }
                    $enum = Get-Content $file.FullName -Raw
                    $enum=[regex]::Replace($enum, '(?m)//[^\r\n]*', '')
                    $body=[regex]::Match($enum, '(?s)enum\s+'+[regex]::Escape($type)+'\s*\{(.*?)\}').Groups[1].Value
                    @([regex]::Matches($body, '(?:^|,)\s*(\w+)') | ForEach-Object { $_.Groups[1].Value })
                }
            }
            if ($value -isnot [string] -or $value -cnotin $allowed) { throw "Invalid value for $name. Expected: $($allowed -join ', ')" }
        }
    }
}
Write-Host 'Default settings validated.'