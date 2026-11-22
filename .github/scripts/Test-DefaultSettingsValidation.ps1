$ErrorActionPreference = 'Stop'
$validator = Join-Path $PSScriptRoot '../../src/config/Validate-DefaultSettings.ps1'
$defaults = Join-Path $PSScriptRoot '../../src/config/default-settings.json'
$root = Join-Path ([IO.Path]::GetTempPath()) ('nxfv-defaults-validation-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root | Out-Null
try {
    & $validator
    foreach ($case in @('Theme', 'NszCompressionEngine', 'LogFileRetentionCount', 'UnknownSetting')) {
        $config = Get-Content -LiteralPath $defaults -Raw -Encoding UTF8 | ConvertFrom-Json
        switch ($case) {
            Theme { $config.Settings.Theme = 'InvalidTheme' }
            NszCompressionEngine { $config.Settings.NszCompressionEngine = 'InvalidEngine' }
            LogFileRetentionCount { $config.Settings.LogFileRetentionCount = 0 }
            UnknownSetting { $config.Settings | Add-Member -NotePropertyName UnknownSetting -NotePropertyValue $true }
        }
        $path = Join-Path $root ($case + '.json')
        [IO.File]::WriteAllText($path, ($config | ConvertTo-Json -Depth 16), [Text.UTF8Encoding]::new($false))
        $rejected = $false
        try { & $validator -Path $path } catch { $rejected = $true }
        if (!$rejected) { throw "Invalid defaults were accepted: $case" }
    }
    Write-Host 'Default-settings validation passed: valid defaults, theme/engine enums, numeric bounds and unknown settings.'
}
finally {
    $resolved = [IO.Path]::GetFullPath($root)
    if ([IO.Path]::GetDirectoryName($resolved) -eq [IO.Path]::GetTempPath().TrimEnd([IO.Path]::DirectorySeparatorChar) -and
        [IO.Path]::GetFileName($resolved) -match '^nxfv-defaults-validation-[0-9a-f]{32}$') {
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
