param([string]$Path=(Join-Path $PSScriptRoot 'naming-presets.json'))
$ErrorActionPreference='Stop'
if((Get-Item -LiteralPath $Path).Length -gt 131072){throw 'Naming preset file exceeds 128 KiB.'}
$catalog=[IO.File]::ReadAllText($Path)|ConvertFrom-Json
if(($catalog.SchemaVersion -isnot [int] -and $catalog.SchemaVersion -isnot [long]) -or $catalog.SchemaVersion -ne 2){throw 'Unsupported naming preset schema; expected 2.'}
if($catalog.Defaults -isnot [pscustomobject] -or $catalog.Defaults.ReplaceWhiteSpaceChars -isnot [bool]){throw 'Defaults.ReplaceWhiteSpaceChars must be true or false.'}
foreach($name in @('InvalidFileNameCharsReplacement','WhiteSpaceCharsReplacement')){
 $value=$catalog.Defaults.$name
 if($value -isnot [string] -or $value.IndexOfAny([IO.Path]::GetInvalidFileNameChars()) -ge 0){throw "Defaults.$name must be a string without invalid Windows filename characters."}
}
$keywordFile=Join-Path $PSScriptRoot '../NxFileViewer/Services/FileRenaming/Models/PatternParts/PatternKeyword.cs'
$keywords=@([regex]::Matches([IO.File]::ReadAllText($keywordFile),'\[SerializedValue\("([^"\r\n]+)"\)\]')|ForEach-Object{$_.Groups[1].Value})
if($keywords.Count -eq 0){throw 'Naming keyword definitions missing.'}
function Assert-Pattern([object]$pattern,[bool]$addon){
 if($pattern -isnot [string] -or [string]::IsNullOrWhiteSpace($pattern) -or $pattern.Length -gt 2048){throw ("Preset '{0}', field '{1}': patterns must contain 1 to 2048 characters." -f $preset.Id,$field)}
 $inside=$false;$start=0
 for($i=0;$i -lt $pattern.Length;$i++){
  $c=$pattern[$i]
  if($i -gt 0 -and $pattern[$i-1] -eq '\' -and ($c -eq '{' -or $c -eq '}')){continue}
  if($c -eq '{'){
   if($inside){throw 'Unexpected opening brace.'};$inside=$true;$start=$i+1
  }elseif($c -eq '}'){
   if(!$inside){throw 'Unexpected closing brace.'}
   $parts=$pattern.Substring($start,$i-$start).Split(':',2)
   $keyword=$parts[0].Trim()
   if(($keyword -notin $keywords) -or ($addon -and $keyword -eq 'Title')){throw ('Unsupported pattern variable: '+$keyword)}
   if($parts.Count -gt 1 -and $parts[1] -notin @('U','L')){throw ('Unsupported pattern modifier: '+$parts[1])}
   $inside=$false
  }
 }
 if($inside){throw 'Missing closing brace.'}
}
foreach($sectionName in @('Application','Patch','Addon','Combined','MultiPackage')){
 $section=$catalog.$sectionName
 if($null -eq $section -or $section.Presets -isnot [array] -or $section.Presets.Count -lt 1 -or $section.Presets.Count -gt 50){throw "Invalid naming preset section: $sectionName."}
 $ids=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
 foreach($preset in $section.Presets){
  if($null -eq $preset -or $preset.Id -isnot [string] -or $preset.Id -notmatch '^[A-Za-z0-9_-]{1,64}$' -or $preset.Id -eq 'manual' -or !$ids.Add($preset.Id)){throw "Invalid, reserved or duplicate preset ID in $sectionName."}
  if($preset.Names -isnot [pscustomobject] -or $preset.Names.PSObject.Properties.Name -cnotcontains 'en' -or [string]::IsNullOrWhiteSpace($preset.Names.en)){throw "Each preset requires an English name in $sectionName."}
  foreach($name in $preset.Names.PSObject.Properties.Value){if($name -isnot [string] -or [string]::IsNullOrWhiteSpace($name) -or $name.Length -gt 120){throw "Invalid preset label in $sectionName."}}
  try{
   $field="$sectionName.Pattern"
   Assert-Pattern $preset.Pattern ($sectionName -eq 'Addon')
   if($sectionName -eq 'MultiPackage'){
    foreach($match in [regex]::Matches($preset.Pattern,'(?<!\\)\{([^}:]+)(?::[^}]+)?\}')){
     if($match.Groups[1].Value.Trim() -notin @('GameCount','UpdateCount','DlcCount','Ext')){throw 'MultiPackage permits only content counts and Ext.'}
    }
   }
  }catch{throw ("Section '{0}', preset '{1}': {2}" -f $sectionName,$preset.Id,$_.Exception.Message)}
 }
 if(!@($section.Presets|Where-Object{$_.Id -ceq $section.DefaultPreset}).Count){throw "Default naming preset does not exist in $sectionName."}
}
Write-Host 'Naming presets validated.'