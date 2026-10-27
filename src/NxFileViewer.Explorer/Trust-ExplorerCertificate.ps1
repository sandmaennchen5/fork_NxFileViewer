# This template is published with a pinned public-certificate thumbprint.
param([switch]$Elevated, [switch]$ValidateOnly, [string]$CertificateBase64)
$ErrorActionPreference = 'Stop'
$expectedThumbprint = '@CERTIFICATE_THUMBPRINT@'
try {
    if ($PSVersionTable.PSEdition -eq 'Desktop') {
        $env:PSModulePath = (Join-Path $env:WINDIR 'System32/WindowsPowerShell/v1.0/Modules') + ';' + (Join-Path $env:ProgramFiles 'WindowsPowerShell/Modules')
    }
    Import-Module Microsoft.PowerShell.Security -ErrorAction Stop
    $certificatePath = if (!$CertificateBase64) { Join-Path $PSScriptRoot 'NxFileViewerFork.Explorer.cer' } else { $null }
    $certificate = if ($CertificateBase64) { [Security.Cryptography.X509Certificates.X509Certificate2]::new([Convert]::FromBase64String($CertificateBase64)) } else { [Security.Cryptography.X509Certificates.X509Certificate2]::new($certificatePath) }
    if ($certificate.Thumbprint -ne $expectedThumbprint) { throw 'The certificate does not match this release helper.' }
    if ($certificate.HasPrivateKey) { throw 'Only a public certificate may be distributed.' }
    if ($certificate.NotBefore -gt (Get-Date)) { throw 'The certificate is not valid yet.' }
    if ($ValidateOnly) { Write-Output ("Validated public certificate: " + $certificate.Thumbprint); exit 0 }
    if (Test-Path -LiteralPath "Cert:\LocalMachine\TrustedPeople\$expectedThumbprint") {
        Write-Host 'This certificate is already trusted. You can add the modern menu in NxFileViewer Fork.'
        exit 0
    }
    $principal = [Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())
    if (!$principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        $scriptLiteral = $PSCommandPath.Replace("'", "''")
        $command = "& '$scriptLiteral' -Elevated"
        $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($command))
        $systemDirectory = if ([Environment]::Is64BitOperatingSystem -and ![Environment]::Is64BitProcess) { 'Sysnative' } else { 'System32' }
        $powershell = Join-Path $env:WINDIR "$systemDirectory/WindowsPowerShell/v1.0/powershell.exe"
        # This is an interactive consent window, not a background helper.
        $child = Start-Process -FilePath $powershell -ArgumentList @('-NoProfile','-ExecutionPolicy','Bypass','-EncodedCommand',$encoded) -Verb RunAs -WindowStyle Normal -Wait -PassThru
        exit $child.ExitCode
    }
    Write-Host 'NxFileViewer Fork - trust the Explorer package certificate'
    Write-Host ("Publisher:   " + $certificate.Subject)
    Write-Host ("Thumbprint:  " + $certificate.Thumbprint)
    Write-Host ("Valid until: " + $certificate.NotAfter.ToString('yyyy-MM-dd'))
    if ($certificate.NotAfter -le (Get-Date)) { Write-Host 'This certificate has expired. Windows must validate the package timestamp before installation.' }
    Write-Host ''
    Write-Host 'This trusts packages signed with this certificate on this PC, for all users.'
    Write-Host 'Only continue if you trust the publisher and obtained these files from its official release.'
    if ((Read-Host 'Type YES to trust this certificate, or press Enter to cancel') -cne 'YES') {
        Write-Host 'Cancelled. No certificate was imported.'
        exit 1
    }
    $store = [Security.Cryptography.X509Certificates.X509Store]::new('TrustedPeople', 'LocalMachine')
    try { $store.Open([Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite); $store.Add($certificate) }
    finally { $store.Dispose() }
    Write-Host 'Certificate trusted. Now use Add / update modern menu in NxFileViewer Fork as your normal user.'
    if ($Elevated) { [void](Read-Host 'Press Enter to close') }
} catch {
    Write-Error $_ -ErrorAction Continue
    if ($Elevated) { [void](Read-Host 'Press Enter to close') }
    exit 1
}
