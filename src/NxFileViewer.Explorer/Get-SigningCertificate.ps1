param([string]$Publisher = 'CN=NxFileViewer Fork')
$ErrorActionPreference = 'Stop'
# PKI commands run in Windows PowerShell, including when Publish is started from PowerShell 7.
$subjectLiteral = $Publisher.Replace("'", "''")
$script = @'
$ErrorActionPreference = 'Stop'
try {
    [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
    $ProgressPreference = 'SilentlyContinue'
    Import-Module Microsoft.PowerShell.Security -ErrorAction Stop
    Import-Module PKI -ErrorAction Stop
    $subject = '__SUBJECT__'
    $friendlyName = 'NxFileViewer Fork Explorer signing (local)'
    $certificate = Get-ChildItem Cert:\CurrentUser\My | Where-Object {
        $_.Subject -eq $subject -and $_.FriendlyName -eq $friendlyName -and $_.HasPrivateKey -and
        $_.NotBefore -le (Get-Date) -and $_.NotAfter -gt (Get-Date).AddDays(30)
    } | Sort-Object NotAfter -Descending | Select-Object -First 1
    $created = $false
    if (!$certificate) {
        $certificate = New-SelfSignedCertificate -Type CodeSigningCert -Subject $subject `
            -FriendlyName $friendlyName -CertStoreLocation Cert:\CurrentUser\My `
            -KeyAlgorithm RSA -KeyLength 3072 -HashAlgorithm SHA256 -KeyExportPolicy Exportable `
            -NotAfter (Get-Date).AddYears(5)
        $created = $true
    }
    $publicCertificate = [Convert]::ToBase64String($certificate.Export([Security.Cryptography.X509Certificates.X509ContentType]::Cert))
    [pscustomobject]@{ PublicCertificate=$publicCertificate; Subject=$certificate.Subject; Thumbprint=$certificate.Thumbprint; Created=$created; NotAfter=$certificate.NotAfter.ToString('o') } | ConvertTo-Json -Compress
} catch { [Console]::Error.WriteLine($_.Exception.Message); exit 1 }
'@
$script = $script.Replace('__SUBJECT__', $subjectLiteral)
$systemDirectory = if ([Environment]::Is64BitOperatingSystem -and ![Environment]::Is64BitProcess) { 'Sysnative' } else { 'System32' }
$powershell = Join-Path $env:WINDIR "$systemDirectory/WindowsPowerShell/v1.0/powershell.exe"
$start = New-Object Diagnostics.ProcessStartInfo
$start.FileName = $powershell
$start.EnvironmentVariables["PSModulePath"] = (Join-Path $env:WINDIR "System32/WindowsPowerShell/v1.0/Modules") + ";" + (Join-Path $env:ProgramFiles "WindowsPowerShell/Modules")
$start.Arguments = '-NoProfile -NonInteractive -EncodedCommand ' + [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($script))
$start.UseShellExecute = $false; $start.CreateNoWindow = $true
$start.RedirectStandardOutput = $true; $start.RedirectStandardError = $true
$start.StandardOutputEncoding = [Text.Encoding]::UTF8
$process = [Diagnostics.Process]::Start($start)
try {
    $output = $process.StandardOutput.ReadToEndAsync(); $errorOutput = $process.StandardError.ReadToEndAsync()
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) { throw $errorOutput.GetAwaiter().GetResult() }
    $output.GetAwaiter().GetResult() | ConvertFrom-Json
} finally { $process.Dispose() }
