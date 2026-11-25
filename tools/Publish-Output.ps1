# Shared output helpers for the configurable WPF/Avalonia publishers (PowerShell 5.1+).
function Get-AvaloniaUpdateChecksum([string]$Zip) {
    [ordered]@{
        Framework='Avalonia'
        AssetName=[IO.Path]::GetFileName($Zip)
        Sha256=(Get-FileHash -LiteralPath $Zip -Algorithm SHA256).Hash
    }
}
function Write-AvaloniaUpdateChecksum([string]$Zip, [switch]$PassThru) {
    $marker = Get-AvaloniaUpdateChecksum $Zip
    [IO.File]::WriteAllText(($Zip + '.viewer.json'),($marker | ConvertTo-Json),[Text.UTF8Encoding]::new($false))
    if ($PassThru) { $marker }
}
function Add-PublishSizeRow($Rows, [string]$Name, [string]$Directory, [string]$Executable, [string]$Zip) {
    $files = @(Get-ChildItem -LiteralPath $Directory -File -Recurse)
    $total = ($files | Measure-Object -Property Length -Sum).Sum
    $exeBytes = if ($Executable) { (Get-Item -LiteralPath $Executable).Length } else { $null }
    $zipBytes = if ($Zip) { (Get-Item -LiteralPath $Zip).Length } else { $null }
    $Rows.Add([pscustomobject]@{Name=$Name;ExecutableBytes=$exeBytes;UnpackedBytes=[long]$total;ZipBytes=$zipBytes})
}
function Format-PublishMegabytes($Bytes) {
    if ($null -eq $Bytes) { return '-' }
    return ([decimal]$Bytes / 1000000).ToString('0.00', [Globalization.CultureInfo]::GetCultureInfo('de-DE'))
}
function Save-PublishSizeReport($Rows, [string]$Path) {
    $table = @(,@('Datei / Paket','Programmdatei (MB)','Ungepacktes Paket (MB)','ZIP (MB)'))
    foreach ($row in $Rows) {
        $table += ,@([string]$row.Name, (Format-PublishMegabytes $row.ExecutableBytes), (Format-PublishMegabytes $row.UnpackedBytes), (Format-PublishMegabytes $row.ZipBytes))
    }
    $widths = @(0,0,0,0)
    foreach ($entry in $table) {
        for ($column=0; $column -lt 4; $column++) { $widths[$column] = [Math]::Max($widths[$column], $entry[$column].Length) }
    }
    $lines = [Collections.Generic.List[string]]::new()
    $lines.Add('Groessen in MB (1 MB = 1.000.000 Bytes), 2 Nachkommastellen. - = nicht erstellt / nicht anwendbar.')
    foreach ($entry in $table) {
        # Equal field widths keep tab stops aligned, including after the longest filename.
        $fields = for ($column=0; $column -lt 4; $column++) {
            if ($column -lt 3) { $entry[$column].PadRight($widths[$column]) } else { $entry[$column] }
        }
        $lines.Add(($fields -join "`t"))
    }
    [IO.File]::WriteAllLines($Path, $lines, [Text.UTF8Encoding]::new($true))
    Write-Host "Size report: $Path"
}
function Copy-PublishExecutables($Copies, [string]$Directory) {
    New-Item -ItemType Directory -Path $Directory -Force | Out-Null
    foreach ($copy in $Copies) {
        if ([IO.Path]::GetFileName($copy.Name) -cne $copy.Name -or $copy.Name.IndexOfAny([IO.Path]::GetInvalidFileNameChars()) -ge 0) { throw 'Supply a plain destination executable filename.' }
        $target = Join-Path $Directory $copy.Name
        try { Copy-Item -LiteralPath $copy.Source -Destination $target -Force }
        catch { throw "Cannot copy executable to '$target'. Close the running application. $($_.Exception.Message)" }
        Write-Host "Test executable: $target"
    }
}
